[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $SourceRevision,
    [string] $ExpectedAssembly = "Lokad.Utf8Regex.dll",
    [string] $OutputDirectory = (Join-Path $PSScriptRoot "artifacts/package-provenance-tests/$([Guid]::NewGuid())")
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "test-packaged-common.ps1")

# Exercise real PE/PDB/NuSpec payloads. Mutation is confined to new fixture copies.
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$symbolPackagePath = [IO.Path]::ChangeExtension($PackagePath, ".snupkg")
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$artifactsPrefix = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "artifacts")).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (!$outputRoot.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $outputRoot)) {
    throw "Provenance fixtures require a new directory inside repository artifacts."
}
[void] (New-Item -ItemType Directory -Path $outputRoot)
$good = Read-PackageProvenance $PackagePath $ExpectedAssembly
Assert-PackageProvenance $good $SourceRevision

function Replace-FixtureBytes {
    param([byte[]] $Bytes, [byte[]] $Old, [byte[]] $New)
    if ($Old.Length -ne $New.Length) { throw "Fixture mutation must preserve metadata layout." }
    $content = [Text.Encoding]::Latin1.GetString($Bytes)
    $oldText = [Text.Encoding]::Latin1.GetString($Old)
    if ($content.IndexOf($oldText, [StringComparison]::Ordinal) -lt 0 -or
        $content.IndexOf($oldText, [StringComparison]::Ordinal) -ne $content.LastIndexOf($oldText, [StringComparison]::Ordinal)) {
        throw "Fixture mutation requires exactly one matching payload."
    }
    return ,([Text.Encoding]::Latin1.GetBytes($content.Replace($oldText, [Text.Encoding]::Latin1.GetString($New), [StringComparison]::Ordinal)))
}

function Edit-FixtureEntry {
    param([string] $ArchivePath, [string] $EntryName, [scriptblock] $Transform)
    $zip = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $zip.GetEntry($EntryName)
        $buffer = [IO.MemoryStream]::new()
        try {
            $stream = $entry.Open()
            try { $stream.CopyTo($buffer) }
            finally { $stream.Dispose() }
            [byte[]] $changed = & $Transform $buffer.ToArray()
        }
        finally { $buffer.Dispose() }
        $entry.Delete()
        $replacement = $zip.CreateEntry($EntryName)
        $stream = $replacement.Open()
        try { $stream.Write($changed, 0, $changed.Length) }
        finally { $stream.Dispose() }
    }
    finally { $zip.Dispose() }
}

$badRevision = if ($SourceRevision -ne ('0' * 40)) { '0' * 40 } else { '1' * 40 }
$assemblyEntryName = "lib/net10.0/$ExpectedAssembly"
$pdbEntryName = $assemblyEntryName -replace '\.dll$', '.pdb'
$inputZip = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try { $nuspecName = @($inputZip.Entries | Where-Object FullName -Like "*.nuspec")[0].FullName }
finally { $inputZip.Dispose() }
$sourceLinkUrl = $good.SourceLinkRecords[0].Mappings[0].Url
$ascii = [Text.Encoding]::ASCII
[byte[]] $pdbId = ([Guid]::Parse($good.PdbGuid)).ToByteArray() + [BitConverter]::GetBytes([uint32] $good.PdbStamp)
[byte[]] $wrongPdbId = $pdbId.Clone()
$wrongPdbId[0] = $wrongPdbId[0] -bxor 1
[byte[]] $sourceLinkKind = ([Guid]::Parse("cc110556-a091-4d38-9fec-25ab9a351a6a")).ToByteArray()
[byte[]] $unknownKind = $sourceLinkKind.Clone()
$unknownKind[0] = $unknownKind[0] -bxor 1
$cases = @(
    @{ Name = "valid"; Error = $null; Mutate = {} },
    @{ Name = "wrong-nuspec"; Error = "NuSpec revision"; Mutate = {
        Edit-FixtureEntry $fixturePackage $nuspecName { param($bytes)
            [xml] $xml = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char] 0xfeff)
            $xml.SelectSingleNode("//*[local-name()='repository']").SetAttribute("commit", $badRevision)
            ,([Text.Encoding]::UTF8.GetBytes($xml.OuterXml))
        }
    } },
    @{ Name = "stale-assembly"; Error = "assembly revision"; Mutate = {
        Edit-FixtureEntry $fixturePackage $assemblyEntryName { param($bytes)
            Replace-FixtureBytes $bytes ($ascii.GetBytes($good.InformationalVersion)) ($ascii.GetBytes($good.InformationalVersion.Replace($SourceRevision, $badRevision)))
        }
    } },
    @{ Name = "missing-symbol-package"; Error = "missing its symbol package"; NoSymbols = $true; Mutate = {} },
    @{ Name = "missing-pdb"; Error = "missing its symbol package or portable PDB"; Mutate = {
        $zip = [IO.Compression.ZipFile]::Open($fixtureSymbols, [IO.Compression.ZipArchiveMode]::Update)
        try { $zip.GetEntry($pdbEntryName).Delete() }
        finally { $zip.Dispose() }
    } },
    @{ Name = "wrong-pdb-identity"; Error = "PDB identity"; Mutate = {
        Edit-FixtureEntry $fixtureSymbols $pdbEntryName { param($bytes) Replace-FixtureBytes $bytes $pdbId $wrongPdbId }
    } },
    @{ Name = "missing-sourcelink"; Error = "SourceLink document map"; Mutate = {
        Edit-FixtureEntry $fixtureSymbols $pdbEntryName { param($bytes) Replace-FixtureBytes $bytes $sourceLinkKind $unknownKind }
    } },
    @{ Name = "stale-sourcelink"; Error = "SourceLink revision"; Mutate = {
        Edit-FixtureEntry $fixtureSymbols $pdbEntryName { param($bytes)
            Replace-FixtureBytes $bytes ($ascii.GetBytes($sourceLinkUrl)) ($ascii.GetBytes($sourceLinkUrl.Replace($SourceRevision, $badRevision)))
        }
    } },
    @{ Name = "wrong-sourcelink-repository"; Error = "SourceLink revision"; Mutate = {
        Edit-FixtureEntry $fixtureSymbols $pdbEntryName { param($bytes)
            Replace-FixtureBytes $bytes ($ascii.GetBytes($sourceLinkUrl)) ($ascii.GetBytes($sourceLinkUrl.Replace('Lokad/Utf8Regex', 'Other/Utf8RegeX')))
        }
    } }
)
$results = @(foreach ($case in $cases) {
    $directory = New-Item -ItemType Directory -Path (Join-Path $outputRoot $case.Name)
    $fixturePackage = Join-Path $directory.FullName ([IO.Path]::GetFileName($PackagePath))
    $fixtureSymbols = [IO.Path]::ChangeExtension($fixturePackage, ".snupkg")
    Copy-Item -LiteralPath $PackagePath -Destination $fixturePackage
    if (!$case.NoSymbols) { Copy-Item -LiteralPath $symbolPackagePath -Destination $fixtureSymbols }
    & $case.Mutate
    $observedError = $null
    try { Assert-PackageProvenance (Read-PackageProvenance $fixturePackage $ExpectedAssembly) $SourceRevision }
    catch { $observedError = $_.Exception.Message }
    if (($null -eq $case.Error -and $null -ne $observedError) -or
        ($null -ne $case.Error -and ($null -eq $observedError -or !$observedError.Contains($case.Error, [StringComparison]::Ordinal)))) {
        throw "Fixture $($case.Name) expected '$($case.Error)', observed '$observedError'."
    }
    [ordered]@{ Name = $case.Name; Passed = $true; Rejection = $observedError }
})
if ($good.PackageSha256 -ne (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash -or
    $good.SymbolPackageSha256 -ne (Get-FileHash -LiteralPath $symbolPackagePath -Algorithm SHA256).Hash) {
    throw "Input qualification packages changed during fixture checks."
}
[ordered]@{ SourceRevision = $SourceRevision; Input = $good; Cases = $results } |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputRoot "results.json") -Encoding utf8
Write-Host "Passed $($results.Count) real-payload provenance checks; evidence: $outputRoot"
