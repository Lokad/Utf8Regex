[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [switch] $Archive,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "test-packaged-common.ps1")

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceRevision -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Could not resolve the source revision for package qualification."
}

if ($SkipBuild) { [void] (Read-PackageQualificationSource $repositoryRoot $sourceRevision $true) }
$locations = New-PackageQualificationRoot -RepositoryRoot $repositoryRoot -SourceRevision $sourceRevision -Archive $Archive
$version = Read-CoherentPackageVersion -RepositoryRoot $repositoryRoot

$coreProjectPath = Join-Path $repositoryRoot "src\Lokad.Utf8Regex\Lokad.Utf8Regex.csproj"
if (-not $SkipBuild) {
    Invoke-DotNet @(
        "build", $coreProjectPath,
        "-c", $Configuration,
        "--tl:off", "--nologo", "-v", "minimal",
        "-p:GeneratePackageOnBuild=false",
        "-p:PackageOutputPath=$($locations.PackageDirectory)")
}
Invoke-DotNet @(
    "pack", $coreProjectPath,
    "-c", $Configuration, "--no-build",
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($locations.PackageDirectory)")

$corePackage = Join-Path $locations.PackageDirectory "Lokad.Utf8Regex.$version.nupkg"
Assert-PackageShape $corePackage "Lokad.Utf8Regex.dll" "" ""
$provenance = Write-PackageProvenanceEvidence -RepositoryRoot $repositoryRoot -QualificationRoot $locations.QualificationRoot -SourceRevision $sourceRevision -Configuration $Configuration -PackagePaths @($corePackage) -ExpectedAssemblies @("Lokad.Utf8Regex.dll") -RequireCleanSource ($Archive -or $SkipBuild)

foreach ($testProjectName in @("Lokad.Utf8Regex.Tests", "Lokad.Utf8Regex.DiffTests")) {
    $projectTestDirectory = New-Item -ItemType Directory -Path (Join-Path $locations.TestDirectory $testProjectName)
    Invoke-IsolatedConsumerTests -RepositoryRoot $repositoryRoot -QualificationRoot $locations.QualificationRoot -PackageDirectory $locations.PackageDirectory -TestDirectory $projectTestDirectory.FullName -PackageCache $locations.PackageCache -Configuration $Configuration -SourceTestProjectName $testProjectName -PackageIds @("Lokad.Utf8Regex") -Version $version
}

if ($Archive) {
    [void] (Read-PackageQualificationSource $repositoryRoot $sourceRevision $true)
    $evidenceDirectory = New-Item -ItemType Directory -Path (Join-Path $locations.QualificationRoot "evidence")
    $snapshotSource = Join-Path $repositoryRoot "README.Benchmarks.json"
    $paritySource = Join-Path $repositoryRoot "README.Parity.json"
    $snapshotArchive = Join-Path $evidenceDirectory.FullName "README.Benchmarks.json"
    $parityArchive = Join-Path $evidenceDirectory.FullName "README.Parity.json"
    Copy-Item -LiteralPath $snapshotSource -Destination $snapshotArchive
    Copy-Item -LiteralPath $paritySource -Destination $parityArchive

    $manifestArtifacts = @(
        [ordered]@{
            Name = "core-package"
            RelativePath = "packages/Lokad.Utf8Regex.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $corePackage -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "core-benchmark-snapshot"
            RelativePath = "evidence/README.Benchmarks.json"
            Sha256 = (Get-FileHash -LiteralPath $snapshotArchive -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "core-parity-report"
            RelativePath = "evidence/README.Parity.json"
            Sha256 = (Get-FileHash -LiteralPath $parityArchive -Algorithm SHA256).Hash
        }
    )
    $manifest = [ordered]@{
        SchemaVersion = 1
        QualificationId = $sourceRevision
        SourceRevision = $sourceRevision
        CreatedAtUtc = [DateTime]::UtcNow.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
        Configuration = $Configuration
        TargetFramework = "net10.0"
        PackageVersion = $version
        Artifacts = $manifestArtifacts + $provenance.Artifacts
    }
    $manifestPath = Join-Path $locations.QualificationRoot "manifest.json"
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-Host "Archived immutable qualification evidence at $($locations.QualificationRoot)."
}

Write-Host "Qualified Lokad.Utf8Regex $version from packed binaries."
