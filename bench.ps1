param(
    [string]$Configuration = "Release",
    [ValidateSet("Dry", "Short", "Medium", "Long")]
    [string]$Job = "Dry",
    [string]$Filter = "*",
    [ValidateSet("None", "EP", "CV", "NativeMemory")]
    [string]$Profiler = "None",
    [switch]$Memory = $true,
    [switch]$Join = $true,
    [switch]$SkipBuild,
    [string[]]$ExtraArgs,
    [string[]]$CommandArgs,
    [ValidateRange(-1, 86400)]
    [int]$MaxWallTimeSeconds = -1
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
Set-Location $repoRoot
$benchmarkArtifactsRoot = Join-Path $repoRoot "bench" "Lokad.Utf8Regex.Benchmarks" "bin" $Configuration "net10.0"

if (-not $CommandArgs) {
    Get-ChildItem -LiteralPath $benchmarkArtifactsRoot -Directory -Filter 'Lokad.Utf8Regex.Benchmarks-*' -ErrorAction SilentlyContinue |
        ForEach-Object {
            try {
                $target = [IO.Path]::GetFullPath($_.FullName)
                $prefix = [IO.Path]::GetFullPath($benchmarkArtifactsRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
                if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Benchmark artifact escaped its output directory: $target"
                }
                Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
            }
            catch {
                Write-Warning "Unable to remove stale benchmark artifact $($_.FullName): $($_.Exception.Message)"
            }
        }
}

if (-not $SkipBuild) {
    dotnet build ./bench/Lokad.Utf8Regex.Benchmarks/Lokad.Utf8Regex.Benchmarks.csproj --configuration $Configuration --tl:off --nologo -v minimal --no-restore -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$benchmarkArgs = @()
if ($CommandArgs) {
    $benchmarkArgs += $CommandArgs
}
else {
    $benchmarkArgs += @(
        "--filter", $Filter,
        "--job", $Job
    )

    if ($Join) {
        $benchmarkArgs += "--join"
    }

    if ($Memory) {
        $benchmarkArgs += "--memory"
    }

    if ($Profiler -ne "None") {
        $benchmarkArgs += @("--profiler", $Profiler)
    }
    if ($ExtraArgs) {
        $benchmarkArgs += $ExtraArgs
    }
}

$effectiveMaxWallTimeSeconds = $MaxWallTimeSeconds
if ($effectiveMaxWallTimeSeconds -lt 0) {
    $effectiveMaxWallTimeSeconds = if ($CommandArgs) { 120 } else { 0 }
}

$processInfo = [System.Diagnostics.ProcessStartInfo]::new()
$processInfo.FileName = "dotnet"
$processInfo.UseShellExecute = $false
$processInfo.RedirectStandardOutput = $true
$processInfo.RedirectStandardError = $true
# The DLL resolves repository files (CPython runner, corpus data) from its working
# directory. Set it explicitly: PowerShell location and process CWD can diverge on
# non-Windows hosts, and .NET child processes inherit the latter, not the former.
$processInfo.WorkingDirectory = $repoRoot

$benchmarkDll = Join-Path $benchmarkArtifactsRoot "Lokad.Utf8Regex.Benchmarks.dll"
$processInfo.ArgumentList.Add($benchmarkDll)

foreach ($argument in $benchmarkArgs) {
    $processInfo.ArgumentList.Add($argument)
}

$benchmarkProcess = [System.Diagnostics.Process]::Start($processInfo)
if ($null -eq $benchmarkProcess) {
    throw "Unable to start the benchmark process."
}

try {
    # Drain both pipes concurrently: a full stderr pipe must not block stdout.
    # Forward both as PowerShell output so ordinary redirection retains the full
    # measurement receipt. The child's exit code remains the success authority.
    $readers = @{ Out = $benchmarkProcess.StandardOutput; Error = $benchmarkProcess.StandardError }
    $pendingReads = @{ Out = $readers.Out.ReadLineAsync(); Error = $readers.Error.ReadLineAsync() }
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    $timedOut = $false
    while ($pendingReads.Count -gt 0 -or -not $benchmarkProcess.HasExited) {
        foreach ($name in @($pendingReads.Keys)) {
            if ($pendingReads[$name].IsCompleted) {
                $line = $pendingReads[$name].GetAwaiter().GetResult()
                if ($null -eq $line) { $pendingReads.Remove($name) }
                else {
                    Write-Output $line
                    $pendingReads[$name] = $readers[$name].ReadLineAsync()
                }
            }
        }
        if (-not $timedOut -and -not $benchmarkProcess.HasExited -and $effectiveMaxWallTimeSeconds -gt 0 -and
            $elapsed.Elapsed.TotalSeconds -ge $effectiveMaxWallTimeSeconds) {
            $timedOut = $true
            if (-not $benchmarkProcess.HasExited) { $benchmarkProcess.Kill($true) }
        }
        if ($pendingReads.Count -gt 0) {
            [void][Threading.Tasks.Task]::WaitAny([Threading.Tasks.Task[]]@($pendingReads.Values), 100)
        }
        elseif (-not $benchmarkProcess.HasExited) { [void]$benchmarkProcess.WaitForExit(100) }
    }
    $benchmarkProcess.WaitForExit()
    if ($timedOut) {
        Write-Error "Benchmark exceeded the $effectiveMaxWallTimeSeconds second wall-time cap. Pass -MaxWallTimeSeconds 0 only for a preflighted milestone refresh." -ErrorAction Continue
        exit 124
    }
    exit $benchmarkProcess.ExitCode
}
finally {
    if (-not $benchmarkProcess.HasExited) { $benchmarkProcess.Kill($true) }
    $benchmarkProcess.Dispose()
}
