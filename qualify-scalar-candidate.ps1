[CmdletBinding()]
param()

# A successful candidate is designated once. Failed attempts retain their evidence.
# This flow never publishes packages or changes a consumer's global NuGet cache.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "test-packaged-common.ps1")

$repositoryRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$version = Read-CoherentPackageVersion $repositoryRoot
if ($version -notmatch '^\d+\.\d+\.\d+-scalar\.\d+$') {
    throw "Scalar candidates require a distinct version such as 0.4.0-scalar.1."
}
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$candidateRoot = Join-Path $artifactsRoot "scalar-candidates/$version"
if (Test-Path -LiteralPath $candidateRoot) {
    throw "The immutable scalar candidate already exists: $candidateRoot. Use a new version for new bytes."
}
$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve the candidate source revision." }
[void] (Read-PackageQualificationSource $repositoryRoot $sourceRevision $true)

$attemptName = [Guid]::NewGuid().ToString("N").Substring(0, 12)
$attemptRoot = Join-Path $artifactsRoot "scalar-candidate-attempts/$attemptName"
[void] (New-Item -ItemType Directory -Path $attemptRoot)
$feed = (New-Item -ItemType Directory -Path (Join-Path $attemptRoot "feed")).FullName
$steps = [Collections.Generic.List[object]]::new()
$packages = @("Lokad.Utf8Regex", "Lokad.Utf8Regex.Pcre2", "Lokad.Utf8Regex.PythonRe")
$startedAt = [DateTime]::UtcNow

function Invoke-CandidateStep([string] $Name, [scriptblock] $Action) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $success = $false
    try { & $Action; $success = $true }
    finally {
        $timer.Stop()
        $steps.Add([ordered]@{ Name = $Name; Succeeded = $success; WallSeconds = $timer.Elapsed.TotalSeconds })
        $steps.ToArray() | ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath (Join-Path $attemptRoot "steps.json") -Encoding utf8
    }
}

function Get-Sha512Base64([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    try { return [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData($stream)) }
    finally { $stream.Dispose() }
}

$transcriptStarted = $false
try {
    [void] (Start-Transcript -LiteralPath (Join-Path $attemptRoot "qualification.log"))
    $transcriptStarted = $true
    Write-Host "Scalar candidate attempt: $attemptRoot"
    $toolchain = & dotnet --info
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect the candidate toolchain." }
    $toolchain | Set-Content -LiteralPath (Join-Path $attemptRoot "dotnet-info.txt") -Encoding utf8
    $stableBuildArguments = @("--disable-build-servers", "/m:1", "/nodeReuse:false", "/p:UseSharedCompilation=false")
    Invoke-CandidateStep "build-once" {
        Invoke-DotNet (@(
            "build", (Join-Path $repositoryRoot "Lokad.Utf8Regex.slnx"), "-t:Rebuild",
            "-c", "Release", "--tl:off", "--nologo", "-v", "minimal",
            "-p:GeneratePackageOnBuild=false"
        ) + $stableBuildArguments)
    }
    foreach ($packageId in $packages) {
        Invoke-CandidateStep "pack-once/$packageId" {
            Invoke-DotNet (@(
                "pack", (Join-Path $repositoryRoot "src/$packageId/$packageId.csproj"),
                "-c", "Release", "--no-build", "--tl:off", "--nologo", "-v", "minimal",
                "-p:GeneratePackageOnBuild=false", "-p:PackageOutputPath=$feed"
            ) + $stableBuildArguments)
        }
        $dependency = if ($packageId -eq "Lokad.Utf8Regex") { "" } else { "Lokad.Utf8Regex" }
        Assert-PackageShape (Join-Path $feed "$packageId.$version.nupkg") "$packageId.dll" $dependency $version
    }
    $packagePaths = @($packages | ForEach-Object { Join-Path $feed "$_.$version.nupkg" })
    $assemblyNames = @($packages | ForEach-Object { "$_.dll" })
    [void] (Write-PackageProvenanceEvidence -RepositoryRoot $repositoryRoot -QualificationRoot $attemptRoot `
        -SourceRevision $sourceRevision -Configuration "Release" -PackagePaths $packagePaths `
        -ExpectedAssemblies $assemblyNames -RequireCleanSource $true)
    $provenance = Get-Content -LiteralPath (Join-Path $attemptRoot "package-provenance.json") -Raw | ConvertFrom-Json
    $packageRecords = @(foreach ($record in $provenance.Packages) {
        $packageId = [IO.Path]::GetFileNameWithoutExtension($record.Assembly)
        if ($record.InformationalVersion -ne "$version+$sourceRevision") {
            throw "Unexpected informational version for $packageId."
        }
        $packagePath = Join-Path $feed $record.Package
        $buildAssembly = Join-Path $repositoryRoot "src/$packageId/bin/Release/net10.0/$($record.Assembly)"
        if ((Get-FileHash -LiteralPath $buildAssembly -Algorithm SHA256).Hash -ne $record.AssemblySha256) {
            throw "Packed and built assembly identities differ for $packageId."
        }
        [ordered]@{
            Id = $packageId; Version = $version; RelativePath = "feed/$($record.Package)"
            PackageSha256 = $record.PackageSha256; PackageSha512Base64 = Get-Sha512Base64 $packagePath
            AssemblySha256 = $record.AssemblySha256; SymbolRelativePath = "feed/$($record.SymbolPackage)"
            SymbolSha256 = $record.SymbolPackageSha256; PdbSha256 = $record.PdbSha256
        }
    })
    if (@(Get-ChildItem -LiteralPath $feed -File).Count -ne 6) {
        throw "The candidate feed must contain exactly three packages and three symbol packages."
    }

    $consumerRecords = [Collections.Generic.List[object]]::new()
    foreach ($testProjectName in @(
        "Lokad.Utf8Regex.Tests", "Lokad.Utf8Regex.DiffTests",
        "Lokad.Utf8Regex.Pcre2.Tests", "Lokad.Utf8Regex.PythonRe.Tests"
    )) {
        # Short storage names keep dependency paths below legacy Windows limits.
        $consumerName = switch ($testProjectName) {
            "Lokad.Utf8Regex.Tests" { "core" }
            "Lokad.Utf8Regex.DiffTests" { "diff" }
            "Lokad.Utf8Regex.Pcre2.Tests" { "pcre2" }
            "Lokad.Utf8Regex.PythonRe.Tests" { "pythonre" }
        }
        $consumerRoot = (New-Item -ItemType Directory -Path (Join-Path $attemptRoot "consumers/$consumerName")).FullName
        $testDirectory = (New-Item -ItemType Directory -Path (Join-Path $consumerRoot "tests")).FullName
        $cache = (New-Item -ItemType Directory -Path (Join-Path $consumerRoot "cache")).FullName
        $packageIds = @("Lokad.Utf8Regex")
        if ($testProjectName -match '^Lokad\.Utf8Regex\.(Pcre2|PythonRe)\.Tests$') {
            $packageIds += "Lokad.Utf8Regex.$($Matches[1])"
        }
        $resultsDirectory = Join-Path $consumerRoot "results"
        Invoke-CandidateStep "consumer/$testProjectName" {
            Invoke-IsolatedConsumerTests -RepositoryRoot $repositoryRoot -QualificationRoot $consumerRoot `
                -PackageDirectory $feed -TestDirectory $testDirectory -PackageCache $cache -Configuration "Release" `
                -SourceTestProjectName $testProjectName -PackageIds $packageIds -Version $version `
                -PinQualifiedPackageSources -TestResultsDirectory $resultsDirectory
        }
        [xml] $trx = Get-Content -LiteralPath (Join-Path $resultsDirectory "qualified.trx")
        $counts = $trx.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
        if ($null -eq $counts -or [int]$counts.total -le 0 -or
            [int]$counts.passed -ne [int]$counts.total -or [int]$counts.failed -ne 0) {
            throw "The $testProjectName consumer did not pass every discovered test."
        }
        [xml] $project = Get-Content -LiteralPath (Join-Path $testDirectory "$testProjectName.csproj")
        if ($project.SelectNodes("//ProjectReference").Count -ne 0) { throw "Project reference remained in $testProjectName." }
        $lockPath = Join-Path $testDirectory "packages.lock.json"
        $packageLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json -AsHashtable
        $identities = @(foreach ($packageId in $packageIds) {
            $record = @($packageRecords | Where-Object Id -EQ $packageId)[0]
            $lowerId = $packageId.ToLowerInvariant()
            $cachedRoot = Join-Path $cache "$lowerId/$version"
            $cachedPackage = Join-Path $cachedRoot "$lowerId.$version.nupkg"
            $cachedAssembly = Join-Path $cachedRoot "lib/net10.0/$packageId.dll"
            $consumerAssembly = Join-Path $testDirectory "bin/Release/net10.0/$packageId.dll"
            $locked = $packageLock.dependencies["net10.0"][$packageId]
            $cachedMetadata = Get-Content -LiteralPath (Join-Path $cachedRoot ".nupkg.metadata") -Raw | ConvertFrom-Json
            if ((Get-FileHash -LiteralPath $cachedPackage -Algorithm SHA256).Hash -ne $record.PackageSha256 -or
                (Get-FileHash -LiteralPath $cachedAssembly -Algorithm SHA256).Hash -ne $record.AssemblySha256 -or
                (Get-FileHash -LiteralPath $consumerAssembly -Algorithm SHA256).Hash -ne $record.AssemblySha256 -or
                $locked.resolved -ne $version -or $locked.contentHash -ne $record.PackageSha512Base64 -or
                $cachedMetadata.source -ne $feed -or $cachedMetadata.contentHash -ne $record.PackageSha512Base64) {
                throw "Feed/cache/lock/consumer identity disagreement for $testProjectName / $packageId."
            }
            [ordered]@{ Id = $packageId; Version = $version; PackageSha256 = $record.PackageSha256
                PackageSha512Base64 = $record.PackageSha512Base64; AssemblySha256 = $record.AssemblySha256 }
        })
        $consumerRecords.Add([ordered]@{
            TestProject = $testProjectName; Total = [int]$counts.total; Passed = [int]$counts.passed; Failed = [int]$counts.failed
            ProjectReferences = 0; Packages = $identities
            ResultsRelativePath = "consumers/$consumerName/results/qualified.trx"
            ResultsSha256 = (Get-FileHash -LiteralPath (Join-Path $resultsDirectory "qualified.trx") -Algorithm SHA256).Hash
            LockRelativePath = "consumers/$consumerName/tests/packages.lock.json"
            LockSha256 = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
        })
    }
    [void] (Read-PackageQualificationSource $repositoryRoot $sourceRevision $true)
    # Relative feed paths survive the atomic move. Consumer configs retain the
    # original observation paths; this root config is the reusable restore input.
    $patterns = ($packages | ForEach-Object { '      <package pattern="' + $_ + '" />' }) -join [Environment]::NewLine
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="scalar-candidate" value="feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="scalar-candidate">
$patterns
    </packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $attemptRoot "NuGet.Config") -Encoding utf8
    [void] (Stop-Transcript)
    $transcriptStarted = $false
    $manifest = [ordered]@{
        SchemaVersion = 1; PackageVersion = $version; SourceRevision = $sourceRevision; CleanSource = $true
        Configuration = "Release"; TargetFramework = "net10.0"
        StartedAtUtc = $startedAt.ToString("O"); CompletedAtUtc = [DateTime]::UtcNow.ToString("O")
        QualificationScope = "Experimental scalar integration candidate; complete detached fixture suites, exact provenance and restore identities. Whole producer resource/cancellation/adapter/performance acceptance remains open."
        Published = $false; BuildCount = 1; PackCountPerPackage = 1
        Packages = $packageRecords; Consumers = $consumerRecords.ToArray(); Steps = $steps.ToArray()
        ProvenanceRelativePath = "package-provenance.json"
        ProvenanceSha256 = (Get-FileHash -LiteralPath (Join-Path $attemptRoot "package-provenance.json") -Algorithm SHA256).Hash
        LogSha256 = (Get-FileHash -LiteralPath (Join-Path $attemptRoot "qualification.log") -Algorithm SHA256).Hash
        NuGetConfigSha256 = (Get-FileHash -LiteralPath (Join-Path $attemptRoot "NuGet.Config") -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath (Join-Path $attemptRoot "candidate.json") -Encoding utf8
    $requiredPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($path in @($attemptRoot, $candidateRoot)) {
        if (![IO.Path]::GetFullPath($path).StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Candidate move escaped the repository artifacts directory."
        }
    }
    [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $candidateRoot) -Force)
    if (Test-Path -LiteralPath $candidateRoot) { throw "Candidate appeared during qualification; refusing to overwrite it." }
    [IO.Directory]::Move($attemptRoot, $candidateRoot)
    Write-Host "Qualified immutable scalar candidate: $candidateRoot"
}
finally {
    if ($transcriptStarted) { [void] (Stop-Transcript) }
}
