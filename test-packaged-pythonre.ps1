[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [switch] $Archive
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "test-packaged-common.ps1")

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceRevision -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Could not resolve the source revision for package qualification."
}

$locations = New-PackageQualificationRoot -RepositoryRoot $repositoryRoot -SourceRevision $sourceRevision -Archive $Archive
$version = Read-CoherentPackageVersion -RepositoryRoot $repositoryRoot

$coreProjectPath = Join-Path $repositoryRoot "src\Lokad.Utf8Regex\Lokad.Utf8Regex.csproj"
$pythonreProjectPath = Join-Path $repositoryRoot "src\Lokad.Utf8Regex.PythonRe\Lokad.Utf8Regex.PythonRe.csproj"
Invoke-DotNet @(
    "pack", $coreProjectPath,
    "-c", $Configuration,
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($locations.PackageDirectory)")
Invoke-DotNet @(
    "pack", $pythonreProjectPath,
    "-c", $Configuration,
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($locations.PackageDirectory)")

$corePackage = Join-Path $locations.PackageDirectory "Lokad.Utf8Regex.$version.nupkg"
$pythonrePackage = Join-Path $locations.PackageDirectory "Lokad.Utf8Regex.PythonRe.$version.nupkg"
Assert-PackageShape $corePackage "Lokad.Utf8Regex.dll" "" ""
Assert-PackageShape $pythonrePackage "Lokad.Utf8Regex.PythonRe.dll" "Lokad.Utf8Regex" $version

# The PythonRe test project links benchmark sources by relative path. Mirror
# them beside the qualification root so the isolated copy resolves identically.
$benchMirrorRoot = [System.IO.Path]::GetFullPath((Join-Path $locations.QualificationRoot "..\bench\Lokad.Utf8Regex.Benchmarks"))
$artifactsPrefix = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts")).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (!$benchMirrorRoot.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Benchmark mirror escaped the repository artifacts directory."
}

if (Test-Path -LiteralPath $benchMirrorRoot) {
    Remove-Item -LiteralPath $benchMirrorRoot -Recurse -Force
}

$benchMirror = New-Item -ItemType Directory -Path $benchMirrorRoot
foreach ($benchSource in @("PythonReBenchmarkCatalog.cs", "BenchmarkDataFiles.cs", "BenchmarkSubjectDefinitions.cs", "PythonReQualificationFreshness.cs")) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "bench\Lokad.Utf8Regex.Benchmarks\$benchSource") -Destination $benchMirror.FullName
}

Invoke-IsolatedConsumerTests -RepositoryRoot $repositoryRoot -QualificationRoot $locations.QualificationRoot -PackageDirectory $locations.PackageDirectory -TestDirectory $locations.TestDirectory -PackageCache $locations.PackageCache -Configuration $Configuration -SourceTestProjectName "Lokad.Utf8Regex.PythonRe.Tests" -PackageIds @("Lokad.Utf8Regex", "Lokad.Utf8Regex.PythonRe") -Version $version

if ($Archive) {
    $evidenceDirectory = New-Item -ItemType Directory -Path (Join-Path $locations.QualificationRoot "evidence")
    $snapshotSource = Join-Path $repositoryRoot "PythonRe.Benchmarks.json"
    $snapshotArchive = Join-Path $evidenceDirectory.FullName "PythonRe.Benchmarks.json"
    Copy-Item -LiteralPath $snapshotSource -Destination $snapshotArchive

    $manifestArtifacts = @(
        [ordered]@{
            Name = "core-package"
            RelativePath = "packages/Lokad.Utf8Regex.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $corePackage -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pythonre-package"
            RelativePath = "packages/Lokad.Utf8Regex.PythonRe.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $pythonrePackage -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pythonre-benchmark-snapshot"
            RelativePath = "evidence/PythonRe.Benchmarks.json"
            Sha256 = (Get-FileHash -LiteralPath $snapshotArchive -Algorithm SHA256).Hash
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
        Artifacts = $manifestArtifacts
    }
    $manifestPath = Join-Path $locations.QualificationRoot "manifest.json"
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-Host "Archived immutable qualification evidence at $($locations.QualificationRoot)."
}

Write-Host "Qualified Lokad.Utf8Regex and Lokad.Utf8Regex.PythonRe $version from packed binaries."
