[CmdletBinding()]
param(
    [ValidateSet("Core", "PythonRe", "Pcre2")]
    [string[]] $Profiles = @("Core", "PythonRe"),
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",
    [switch] $SkipBuild
)

# One immutable candidate, one Core pack, selected adapter packages/tests.
# Existing shared provenance, package-shape and isolated-consumer checks remain.
# Publication and broader profile qualification are separate actions.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "test-packaged-common.ps1")

$repositoryRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$version = Read-CoherentPackageVersion $repositoryRoot
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw "Package candidates require a valid common NuGet version."
}
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$candidateRoot = Join-Path $artifactsRoot "package-candidates/$version"
foreach ($existing in @($candidateRoot, (Join-Path $artifactsRoot "scalar-candidates/$version"),
    (Join-Path $artifactsRoot "release-candidates/$version"))) {
    if (Test-Path -LiteralPath $existing) {
        throw "An immutable candidate already exists: $existing. Use a new version for new bytes."
    }
}
$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve the candidate source revision." }
[void] (Read-PackageQualificationSource $repositoryRoot $sourceRevision $true)

$attemptName = [Guid]::NewGuid().ToString("N").Substring(0, 12)
$attemptRoot = Join-Path $artifactsRoot "package-candidate-attempts/$attemptName"
[void] (New-Item -ItemType Directory -Path $attemptRoot)
$feed = (New-Item -ItemType Directory -Path (Join-Path $attemptRoot "feed")).FullName
$steps = [Collections.Generic.List[object]]::new()
# Core is always required by the selected adapter graph and the shared fixture suite.
$packages = @("Lokad.Utf8Regex")
$testProjects = @("Lokad.Utf8Regex.Tests", "Lokad.Utf8Regex.DiffTests")
foreach ($adapter in @("PythonRe", "Pcre2")) {
    if ($adapter -in $Profiles) {
        $packages += "Lokad.Utf8Regex.$adapter"
        $testProjects += "Lokad.Utf8Regex.$adapter.Tests"
    }
}
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
    Write-Host "Package candidate attempt: $attemptRoot"
    $toolchain = & dotnet --info
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect the candidate toolchain." }
    $toolchain | Set-Content -LiteralPath (Join-Path $attemptRoot "dotnet-info.txt") -Encoding utf8
    # Build only selected library roots. Adapter builds include Core; no benchmark
    # host or unselected adapter is needed by the detached fixtures.
    $buildProjects = @($packages | Where-Object { $_ -ne "Lokad.Utf8Regex" })
    if ($buildProjects.Count -eq 0) { $buildProjects = @("Lokad.Utf8Regex") }
    if (-not $SkipBuild) {
        foreach ($buildProject in $buildProjects) {
            Invoke-CandidateStep "build/$buildProject" {
                Invoke-DotNet @(
                    "build", (Join-Path $repositoryRoot "src/$buildProject/$buildProject.csproj"),
                    "-c", $Configuration, "--tl:off", "--nologo", "-v", "minimal",
                    "-p:GeneratePackageOnBuild=false"
                )
            }
        }
    }
    foreach ($packageId in $packages) {
        Invoke-CandidateStep "pack-once/$packageId" {
            Invoke-DotNet @(
                "pack", (Join-Path $repositoryRoot "src/$packageId/$packageId.csproj"),
                "-c", $Configuration, "--no-build", "--tl:off", "--nologo", "-v", "minimal",
                "-p:GeneratePackageOnBuild=false", "-p:PackageOutputPath=$feed"
            )
        }
        $dependency = if ($packageId -eq "Lokad.Utf8Regex") { "" } else { "Lokad.Utf8Regex" }
        Assert-PackageShape (Join-Path $feed "$packageId.$version.nupkg") "$packageId.dll" $dependency $version
    }
    $packagePaths = @($packages | ForEach-Object { Join-Path $feed "$_.$version.nupkg" })
    $assemblyNames = @($packages | ForEach-Object { "$_.dll" })
    [void] (Write-PackageProvenanceEvidence -RepositoryRoot $repositoryRoot -QualificationRoot $attemptRoot `
        -SourceRevision $sourceRevision -Configuration $Configuration -PackagePaths $packagePaths `
        -ExpectedAssemblies $assemblyNames -RequireCleanSource $true)
    $provenance = Get-Content -LiteralPath (Join-Path $attemptRoot "package-provenance.json") -Raw | ConvertFrom-Json
    $packageRecords = @(foreach ($record in $provenance.Packages) {
        $packageId = [IO.Path]::GetFileNameWithoutExtension($record.Assembly)
        if ($record.InformationalVersion -ne "$version+$sourceRevision") {
            throw "Unexpected informational version for $packageId."
        }
        $packagePath = Join-Path $feed $record.Package
        $buildAssembly = Join-Path $repositoryRoot "src/$packageId/bin/$Configuration/net10.0/$($record.Assembly)"
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
    if (@(Get-ChildItem -LiteralPath $feed -File).Count -ne (2 * $packages.Count)) {
        throw "The candidate feed must contain exactly the selected packages and their symbols."
    }

    $consumerRecords = [Collections.Generic.List[object]]::new()
    foreach ($testProjectName in $testProjects) {
        # Short storage names keep dependency paths below legacy Windows limits.
        $consumerName = switch ($testProjectName) {
            "Lokad.Utf8Regex.Tests" { "core" }
            "Lokad.Utf8Regex.DiffTests" { "diff" }
            "Lokad.Utf8Regex.PythonRe.Tests" { "pythonre" }
            "Lokad.Utf8Regex.Pcre2.Tests" { "pcre2" }
        }
        $consumerRoot = (New-Item -ItemType Directory -Path (Join-Path $attemptRoot "consumers/$consumerName")).FullName
        $testDirectory = (New-Item -ItemType Directory -Path (Join-Path $consumerRoot "tests")).FullName
        $cache = (New-Item -ItemType Directory -Path (Join-Path $consumerRoot "cache")).FullName
        $packageIds = @("Lokad.Utf8Regex")
        if ($testProjectName -eq "Lokad.Utf8Regex.PythonRe.Tests") {
            $packageIds += "Lokad.Utf8Regex.PythonRe"
        }
        if ($testProjectName -eq "Lokad.Utf8Regex.Pcre2.Tests") { $packageIds += "Lokad.Utf8Regex.Pcre2" }
        $resultsDirectory = Join-Path $consumerRoot "results"
        Invoke-CandidateStep "consumer/$testProjectName" {
            Invoke-IsolatedConsumerTests -RepositoryRoot $repositoryRoot -QualificationRoot $consumerRoot `
                -PackageDirectory $feed -TestDirectory $testDirectory -PackageCache $cache -Configuration $Configuration `
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
        [xml] $sourceProject = Get-Content -LiteralPath (Join-Path $repositoryRoot "tests/$testProjectName/$testProjectName.csproj")
        $linkedSources = @(foreach ($compile in @($sourceProject.SelectNodes("//Compile[@Include and @Link]"))) {
            $sourcePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "tests/$testProjectName/$($compile.GetAttribute('Include'))"))
            $detachedPath = Join-Path $consumerRoot "linked-source/$($compile.GetAttribute('Link'))"
            $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
            if ((Get-FileHash -LiteralPath $detachedPath -Algorithm SHA256).Hash -ne $sourceHash) {
                throw "Linked fixture source changed for $testProjectName."
            }
            [ordered]@{ Source = [IO.Path]::GetRelativePath($repositoryRoot, $sourcePath)
                RelativePath = "consumers/$consumerName/linked-source/$($compile.GetAttribute('Link'))"; Sha256 = $sourceHash }
        })
        $lockPath = Join-Path $testDirectory "packages.lock.json"
        $packageLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json -AsHashtable
        $identities = @(foreach ($packageId in $packageIds) {
            $record = @($packageRecords | Where-Object Id -EQ $packageId)[0]
            $lowerId = $packageId.ToLowerInvariant()
            $cachedRoot = Join-Path $cache "$lowerId/$version"
            $cachedPackage = Join-Path $cachedRoot "$lowerId.$version.nupkg"
            $cachedAssembly = Join-Path $cachedRoot "lib/net10.0/$packageId.dll"
            $consumerAssembly = Join-Path $testDirectory "bin/$Configuration/net10.0/$packageId.dll"
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
            LinkedSources = $linkedSources
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
    <add key="package-candidate" value="feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="package-candidate">
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
        Configuration = $Configuration; TargetFramework = "net10.0"
        StartedAtUtc = $startedAt.ToString("O"); CompletedAtUtc = [DateTime]::UtcNow.ToString("O")
        QualificationScope = "Selected Core/adapter candidate; complete detached fixture suites, exact provenance and restore identities. Other platforms, broader profiles, downstream adoption, performance and publication are separate acceptance steps."
        Published = $false; BuildCount = $(if ($SkipBuild) { 0 } else { $buildProjects.Count }); PackCountPerPackage = 1
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
    Write-Host "Qualified immutable package candidate: $candidateRoot"
}
finally {
    if ($transcriptStarted) { [void] (Stop-Transcript) }
}
