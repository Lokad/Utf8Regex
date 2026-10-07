[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $QualificationId
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "test-packaged-common.ps1")

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$archiveParent = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts\package-qualification\archive"))
$archiveRoot = [System.IO.Path]::GetFullPath((Join-Path $archiveParent $QualificationId))
$requiredPrefix = $archiveParent.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (!$archiveRoot.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Qualification archive escaped the expected artifacts directory."
}

$manifestPath = Join-Path $archiveRoot "manifest.json"
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Qualification manifest does not exist: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.SchemaVersion -ne 1) {
    throw "Unsupported qualification manifest schema $($manifest.SchemaVersion)."
}

if ($manifest.QualificationId -cne $QualificationId -or
    $manifest.SourceRevision -cne $QualificationId) {
    throw "Qualification manifest identity does not match $QualificationId."
}

$archivePrefix = $archiveRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
foreach ($artifact in $manifest.Artifacts) {
    if ([string]::IsNullOrWhiteSpace($artifact.RelativePath) -or
        $artifact.Sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Qualification manifest contains an invalid artifact entry."
    }

    $artifactPath = [System.IO.Path]::GetFullPath((Join-Path $archiveRoot $artifact.RelativePath))
    if (!$artifactPath.StartsWith($archivePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Qualification artifact escaped its archive: $($artifact.RelativePath)"
    }

    if (!(Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "Qualification artifact is missing: $artifactPath"
    }

    $actualHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash
    if ($actualHash -cne $artifact.Sha256) {
        throw "Qualification artifact hash mismatch for $($artifact.Name): expected $($artifact.Sha256), got $actualHash."
    }
}

$provenanceArtifacts = @($manifest.Artifacts | Where-Object Name -EQ "package-provenance")
if ($provenanceArtifacts.Count -ne 1 -or $provenanceArtifacts[0].RelativePath -ne "package-provenance.json") {
    throw "Qualification lacks the rebuilt-package provenance report; historical functional evidence is insufficient for clean-commit qualification."
}
$provenance = Get-Content -LiteralPath (Join-Path $archiveRoot "package-provenance.json") -Raw | ConvertFrom-Json
if ($provenance.SchemaVersion -ne 1 -or $provenance.SourceRevision -ne $QualificationId -or
    $provenance.CleanSource -ne $true -or $provenance.Configuration -ne $manifest.Configuration -or
    $provenance.Packages.Count -ne 2) {
    throw "Qualification provenance does not identify a clean coherent PCRE2 package pair."
}
foreach ($packageId in @("Lokad.Utf8Regex", "Lokad.Utf8Regex.Pcre2")) {
    $packageName = "$packageId.$($manifest.PackageVersion).nupkg"
    $packagePath = Join-Path $archiveRoot "packages/$packageName"
    $record = Read-PackageProvenance $packagePath "$packageId.dll"
    Assert-PackageProvenance $record $QualificationId
    foreach ($expected in @(
        @{ Path = "packages/$($record.Package)"; Hash = $record.PackageSha256 },
        @{ Path = "packages/$($record.SymbolPackage)"; Hash = $record.SymbolPackageSha256 }
    )) {
        $entries = @($manifest.Artifacts | Where-Object RelativePath -EQ $expected.Path)
        if ($entries.Count -ne 1 -or $entries[0].Sha256 -ne $expected.Hash) {
            throw "Qualification manifest does not cover $($expected.Path)."
        }
    }
    $reportedPackages = @($provenance.Packages | Where-Object Package -EQ $packageName)
    if ($reportedPackages.Count -ne 1 -or
        $reportedPackages[0].PackageSha256 -ne $record.PackageSha256 -or
        $reportedPackages[0].SymbolPackageSha256 -ne $record.SymbolPackageSha256 -or
        $reportedPackages[0].AssemblySha256 -ne $record.AssemblySha256 -or
        $reportedPackages[0].PdbSha256 -ne $record.PdbSha256) {
        throw "Qualification provenance does not cover the inspected $packageName payloads."
    }
    Assert-PackageProvenance $reportedPackages[0] $QualificationId
}

Write-Host "Verified immutable PCRE2 qualification $QualificationId ($($manifest.Artifacts.Count) artifacts)."
