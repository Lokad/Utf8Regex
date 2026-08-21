[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $QualificationId
)

$ErrorActionPreference = "Stop"

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

Write-Host "Verified immutable PCRE2 qualification $QualificationId ($($manifest.Artifacts.Count) artifacts)."
