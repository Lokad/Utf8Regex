param(
    [string]$ReportPath,
    [int]$Top = 10
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
Set-Location $repoRoot

if (-not $ReportPath) {
    $latest = Get-ChildItem .\BenchmarkDotNet.Artifacts\results\*report.csv -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $latest) {
        throw "No BenchmarkDotNet CSV report found under .\BenchmarkDotNet.Artifacts\results\."
    }

    $ReportPath = $latest.FullName
}

$rows = Import-Csv -Path $ReportPath

if (-not $rows) {
    throw "Benchmark report '$ReportPath' is empty."
}

function Parse-MeanNs {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    $normalized = $Text.Replace([char]0x03BC, 'u').Replace([char]0x00B5, 'u')

    if ($normalized -match '^\s*([0-9,\.]+)\s*(ns|us|ms|s)\s*$') {
        $value = [double](($matches[1] -replace ',', ''))
        switch ($matches[2]) {
            'ns' { return $value }
            'us' { return $value * 1000.0 }
            'ms' { return $value * 1000000.0 }
            's'  { return $value * 1000000000.0 }
        }
    }

    return $null
}

$grouped = $rows | Group-Object CaseId

$summaries = foreach ($group in $grouped) {
    $caseRows = $group.Group
    $utf8 = $caseRows | Where-Object Method -eq 'Utf8Regex' | Select-Object -First 1
    $decode = $caseRows | Where-Object Method -eq 'DecodeThenRegex' | Select-Object -First 1
    $predecoded = $caseRows | Where-Object Method -eq 'PredecodedRegex' | Select-Object -First 1

    if (-not $utf8 -or -not $decode -or -not $predecoded) {
        continue
    }

    [pscustomobject]@{
        CaseId = $group.Name
        Utf8RegexMean = $utf8.Mean
        DecodeThenRegexMean = $decode.Mean
        PredecodedRegexMean = $predecoded.Mean
        Utf8Allocated = $utf8.Allocated
        DecodeAllocated = $decode.Allocated
        PredecodedAllocated = $predecoded.Allocated
    }
}

function Get-RatioText {
    param(
        [string]$Numerator,
        [string]$Denominator
    )

    $left = Parse-MeanNs $Numerator
    $right = Parse-MeanNs $Denominator
    if ($left -and $right) {
        return "{0:N2}x" -f ($left / $right)
    }

    return "n/a"
}

Write-Host "Report: $ReportPath"
Write-Host ""
Write-Host "Worst Utf8Regex vs DecodeThenRegex cases"
foreach ($row in ($summaries | Sort-Object { [double]((Get-RatioText $_.Utf8RegexMean $_.DecodeThenRegexMean) -replace 'x','' -replace 'n/a','0') } -Descending | Select-Object -First $Top)) {
    Write-Host ("{0} | Utf8Regex={1} | DecodeThenRegex={2} | Ratio={3} | Alloc={4} vs {5}" -f $row.CaseId, $row.Utf8RegexMean, $row.DecodeThenRegexMean, (Get-RatioText $row.Utf8RegexMean $row.DecodeThenRegexMean), $row.Utf8Allocated, $row.DecodeAllocated)
}

Write-Host ""
Write-Host "Worst Utf8Regex vs PredecodedRegex cases"
foreach ($row in ($summaries | Sort-Object { [double]((Get-RatioText $_.Utf8RegexMean $_.PredecodedRegexMean) -replace 'x','' -replace 'n/a','0') } -Descending | Select-Object -First $Top)) {
    Write-Host ("{0} | Utf8Regex={1} | PredecodedRegex={2} | Ratio={3} | Alloc={4} vs {5}" -f $row.CaseId, $row.Utf8RegexMean, $row.PredecodedRegexMean, (Get-RatioText $row.Utf8RegexMean $row.PredecodedRegexMean), $row.Utf8Allocated, $row.PredecodedAllocated)
}

Write-Host ""
Write-Host "Full per-case summary"
foreach ($row in ($summaries | Sort-Object CaseId)) {
    Write-Host ("{0} | Utf8Regex={1} | DecodeThenRegex={2} | PredecodedRegex={3} | Utf8/Decode={4} | Utf8/Predecoded={5}" -f $row.CaseId, $row.Utf8RegexMean, $row.DecodeThenRegexMean, $row.PredecodedRegexMean, (Get-RatioText $row.Utf8RegexMean $row.DecodeThenRegexMean), (Get-RatioText $row.Utf8RegexMean $row.PredecodedRegexMean))
}
