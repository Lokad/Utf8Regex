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
$pcre2ProjectPath = Join-Path $repositoryRoot "src\Lokad.Utf8Regex.Pcre2\Lokad.Utf8Regex.Pcre2.csproj"
Invoke-DotNet @(
    "pack", $coreProjectPath,
    "-c", $Configuration,
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($locations.PackageDirectory)")
Invoke-DotNet @(
    "pack", $pcre2ProjectPath,
    "-c", $Configuration,
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($locations.PackageDirectory)")

$corePackage = Join-Path $locations.PackageDirectory "Lokad.Utf8Regex.$version.nupkg"
$pcre2Package = Join-Path $locations.PackageDirectory "Lokad.Utf8Regex.Pcre2.$version.nupkg"
Assert-PackageShape $corePackage "Lokad.Utf8Regex.dll" "" ""
Assert-PackageShape $pcre2Package "Lokad.Utf8Regex.Pcre2.dll" "Lokad.Utf8Regex" $version

Invoke-IsolatedConsumerTests -RepositoryRoot $repositoryRoot -QualificationRoot $locations.QualificationRoot -PackageDirectory $locations.PackageDirectory -TestDirectory $locations.TestDirectory -PackageCache $locations.PackageCache -Configuration $Configuration -SourceTestProjectName "Lokad.Utf8Regex.Pcre2.Tests" -PackageIds @("Lokad.Utf8Regex", "Lokad.Utf8Regex.Pcre2") -Version $version

if ($Archive) {
    $evidenceDirectory = New-Item -ItemType Directory -Path (Join-Path $locations.QualificationRoot "evidence")
    $snapshotSource = Join-Path $repositoryRoot "PCRE2.Benchmarks.json"
    $apiSource = Join-Path $repositoryRoot "tests\Lokad.Utf8Regex.Pcre2.Tests\PublicApi.Shipped.txt"
    $snapshotArchive = Join-Path $evidenceDirectory.FullName "PCRE2.Benchmarks.json"
    $apiArchive = Join-Path $evidenceDirectory.FullName "PublicApi.Shipped.txt"
    Copy-Item -LiteralPath $snapshotSource -Destination $snapshotArchive
    Copy-Item -LiteralPath $apiSource -Destination $apiArchive

    $manifestArtifacts = @(
        [ordered]@{
            Name = "core-package"
            RelativePath = "packages/Lokad.Utf8Regex.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $corePackage -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pcre2-package"
            RelativePath = "packages/Lokad.Utf8Regex.Pcre2.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $pcre2Package -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pcre2-benchmark-snapshot"
            RelativePath = "evidence/PCRE2.Benchmarks.json"
            Sha256 = (Get-FileHash -LiteralPath $snapshotArchive -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pcre2-public-api"
            RelativePath = "evidence/PublicApi.Shipped.txt"
            Sha256 = (Get-FileHash -LiteralPath $apiArchive -Algorithm SHA256).Hash
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

Write-Host "Qualified Lokad.Utf8Regex and Lokad.Utf8Regex.Pcre2 $version from packed binaries."
