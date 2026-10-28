[CmdletBinding()]
param(
    [ValidateSet('Build', 'Tests', 'Core', 'PythonRe', 'Pcre2', 'Resources', 'Verify')]
    [string[]]$Phase = @('Build', 'Tests', 'Core', 'PythonRe', 'Pcre2', 'Resources', 'Verify'),
    [ValidateRange(1, 86400)]
    [int]$MaxWallTimeSeconds = 3600,
    [string]$OutputDirectory
)

# Orchestration only: existing scripts own builds, tests, measurements and their
# qualification rules. Select phases for experiments; the default is the complete
# delivery cycle. On the VM, wrap this command in timeout for a hard process-tree
# cap that also covers a stuck build/test, not just benchmark children.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$clock = [Diagnostics.Stopwatch]::StartNew()
$started = [DateTimeOffset]::UtcNow
$revision = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the source revision.' }
$unexpected = & git status --porcelain=v1 --untracked-files=normal -- . `
    ':(exclude)README.md' ':(exclude)README.Benchmarks.json' ':(exclude)README.Parity.json' `
    ':(exclude)PythonRe.Benchmarks.json' ':(exclude)src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md' `
    ':(exclude)PCRE2.Benchmarks.json' ':(exclude)src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md'
if ($LASTEXITCODE -ne 0 -or $unexpected) { throw "Refresh requires clean source inputs: $unexpected" }
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot ('artifacts/benchmark-refresh/' + $started.ToString('yyyyMMdd-HHmmss'))
}
if (![IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot $OutputDirectory }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Output already exists: $OutputDirectory" }
[void](New-Item -ItemType Directory -Path $OutputDirectory)
$steps = [Collections.Generic.List[object]]::new()
$complete = $false
$full = @('Build', 'Tests', 'Core', 'PythonRe', 'Pcre2', 'Resources', 'Verify').Where({ $_ -notin $Phase }).Count -eq 0

function Save-Receipt {
    [ordered]@{
        Source = $revision; StartedAtUtc = $started.ToString('O')
        UpdatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        ElapsedSeconds = $clock.Elapsed.TotalSeconds; BudgetSeconds = $MaxWallTimeSeconds
        Scope = $(if ($full) { 'Full' } else { 'Selected phases' })
        Phases = $Phase; Complete = $complete; Steps = $steps.ToArray()
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'refresh.json')
}

function Invoke-RefreshStep([string]$name, [scriptblock]$action) {
    if ($clock.Elapsed.TotalSeconds -ge $MaxWallTimeSeconds) { throw 'Refresh wall-time budget exhausted.' }
    if ((& git rev-parse HEAD).Trim() -ne $revision) { throw 'Source revision changed during refresh.' }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $record = [ordered]@{ Name = $name; Status = 'Running'; Seconds = 0; ExitCode = $null; Error = $null }
    $steps.Add($record)
    Save-Receipt
    Write-Output "Starting $name; elapsed $([int]$clock.Elapsed.TotalSeconds)s"
    try {
        & $action *> (Join-Path $OutputDirectory "$name.txt")
        $record.ExitCode = $LASTEXITCODE
        if ($LASTEXITCODE -ne 0) { throw "$name failed with exit code $LASTEXITCODE. See $OutputDirectory/$name.txt" }
        $record.Status = 'Passed'
    }
    catch { $record.Status = 'Failed'; $record.Error = $_.Exception.Message; throw }
    finally {
        $record.Seconds = $timer.Elapsed.TotalSeconds
        Save-Receipt
        Write-Output "$name $($record.Status): $([Math]::Round($record.Seconds, 2))s; total $([Math]::Round($clock.Elapsed.TotalSeconds, 2))s"
    }
}

function Invoke-RefreshBenchmark([string]$name, [string[]]$arguments) {
    Invoke-RefreshStep $name {
        $remaining = [int][Math]::Floor($MaxWallTimeSeconds - $clock.Elapsed.TotalSeconds)
        if ($remaining -lt 1) { throw 'Refresh wall-time budget exhausted.' }
        & ./bench.ps1 -Configuration Release -Framework net11.0 -SkipBuild -CommandArgs $arguments -MaxWallTimeSeconds $remaining
    }
}

try {
    if ('Build' -in $Phase) {
        Invoke-RefreshStep build {
            dotnet build Lokad.Utf8Regex.slnx -c Release --tl:off --nologo -v minimal -p:GeneratePackageOnBuild=false
        }
    }
    if ('Tests' -in $Phase) {
        Invoke-RefreshStep tests { & ./test.ps1 -Configuration Release -SkipBuild }
        foreach ($framework in @('net10.0', 'net11.0')) {
            Invoke-RefreshStep "disabled-dynamic-$framework" {
                $path = Join-Path $PSScriptRoot "tests/Lokad.Utf8Regex.Tests/bin/Release/$framework/Lokad.Utf8Regex.Tests.runtimeconfig.json"
                $original = [IO.File]::ReadAllBytes($path)
                try {
                    $runtime = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
                    $runtime.runtimeOptions.configProperties['System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported'] = $false
                    $runtime.runtimeOptions.configProperties['System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled'] = $false
                    $runtime | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path
                    & ./test.ps1 -Configuration Release -Framework $framework -SkipBuild `
                        -Project tests/Lokad.Utf8Regex.Tests/Lokad.Utf8Regex.Tests.csproj -Filter 'FullyQualifiedName~Utf8DynamicCodeCapabilityTests'
                }
                finally { [IO.File]::WriteAllBytes($path, $original) }
            }
        }
    }
    if ('Core' -in $Phase) {
        Invoke-RefreshBenchmark core @('--refresh-readme-benchmarks', 'dotnet-performance,dotnet-performance-compiled,lokad,lokad-compiled', '1', '5')
    }
    if ('PythonRe' -in $Phase) {
        Invoke-RefreshBenchmark python-semantic @('--verify-pythonre-semantic-digests')
        Invoke-RefreshBenchmark python-raw @('--refresh-pythonre-benchmarks', '1', '5')
        Invoke-RefreshBenchmark python-paired @('--resume-pythonre-qualifications', '9', '17', '90')
        Invoke-RefreshBenchmark python-lifecycle @('--refresh-pythonre-lifecycle', '32', '5')
        Invoke-RefreshBenchmark python-scaling @('--refresh-pythonre-scaling', '5')
    }
    if ('Pcre2' -in $Phase) {
        Invoke-RefreshBenchmark pcre2-raw @('--refresh-pcre2-benchmarks', '', '1', '5')
        $snapshot = Get-Content PCRE2.Benchmarks.json -Raw | ConvertFrom-Json -AsHashtable
        $ids = @($snapshot.Sections.Values | ForEach-Object { $_.Cases.Keys } | Sort-Object -Unique)
        foreach ($id in $ids) {
            Invoke-RefreshBenchmark ('pcre2-paired-' + $id.Replace('/', '_')) @('--qualify-pcre2-comparator-case', $id, '9')
        }
        Invoke-RefreshBenchmark pcre2-scaling @('--refresh-pcre2-scaling-families', '1', '5')
    }
    if ('Resources' -in $Phase) {
        Invoke-RefreshBenchmark resources @('--measure-slice-policy-lanes', '20000', '5')
    }
    if ('Verify' -in $Phase) {
        Invoke-RefreshBenchmark verify-core @('--verify-readme-parity-report')
        Invoke-RefreshBenchmark verify-python @('--verify-pythonre-benchmark-markdown')
        Invoke-RefreshBenchmark verify-python-pairs @('--verify-pythonre-qualifications')
        Invoke-RefreshBenchmark verify-pcre2 @('--verify-pcre2-benchmark-markdown')
        Invoke-RefreshBenchmark verify-pcre2-pairs @('--verify-pcre2-qualification-consistency')
    }
    if ($full) {
        # Reject a successful command sequence that retained rows from a different
        # revision/runtime. Existing fixtures own catalog/result/statistical rules.
        $core = Get-Content README.Benchmarks.json -Raw | ConvertFrom-Json -AsHashtable
        $python = Get-Content PythonRe.Benchmarks.json -Raw | ConvertFrom-Json -AsHashtable
        $pcre2 = Get-Content PCRE2.Benchmarks.json -Raw | ConvertFrom-Json -AsHashtable
        $environments = @($core.Sections.Values | ForEach-Object { $_.Cases.Values.Environment }) +
            @($python.Cases.Values.Environment) +
            @($pcre2.Sections.Values | ForEach-Object { $_.Cases.Values.Environment }) +
            @($python.Lifecycle.Values.ManagedEnvironment) +
            @($python.ScalingFamilies.Values.ManagedEnvironment) +
            @($pcre2.ScalingFamilies.Values | ForEach-Object { $_.Points.Environment })
        foreach ($environment in $environments) {
            if ($environment.Runtime -notlike '.NET 11*' -or
                $environment.SourceCommit -ne $revision.Substring(0, 12)) {
                throw 'Full refresh retained a different source/runtime. Consult snapshot row identities.'
            }
        }
        foreach ($row in $python.Cases.Values) {
            if (!$row.Qualification.PairedEvidence) { throw 'Full refresh lacks Python paired evidence.' }
        }
        foreach ($row in @($pcre2.Sections.Values | ForEach-Object { $_.Cases.Values })) {
            if ($row.PcreNetNativeStatus -ne 'Excluded' -and !$row.PcreNetNativePair) {
                throw 'Full refresh lacks native-PCRE2 paired evidence or an exclusion.'
            }
        }
    }
    if ($clock.Elapsed.TotalSeconds -gt $MaxWallTimeSeconds) { throw 'Refresh exceeded its wall-time budget.' }
    $complete = $true
    Save-Receipt
    Write-Output "Refresh complete ($($Phase -join ', ')): $([Math]::Round($clock.Elapsed.TotalSeconds, 2))s. Receipt: $OutputDirectory"
}
catch { Save-Receipt; Write-Error $_; exit 1 }
