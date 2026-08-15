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
$benchmarkArtifactsRoot = Join-Path $repoRoot "bench\\Lokad.Utf8Regex.Benchmarks\\bin\\Release\\net10.0"

Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object { $_.CommandLine -like '*Lokad.Utf8Regex.Benchmarks-*.dll*' } |
    ForEach-Object {
        try {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
        }
        catch {
        }
    }

Get-ChildItem -LiteralPath $benchmarkArtifactsRoot -Directory -Filter 'Lokad.Utf8Regex.Benchmarks-*' -ErrorAction SilentlyContinue |
    ForEach-Object {
        try {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
        }
        catch {
        }
    }

dotnet build-server shutdown | Out-Null

if (-not $SkipBuild) {
    dotnet build .\bench\Lokad.Utf8Regex.Benchmarks\Lokad.Utf8Regex.Benchmarks.csproj --configuration $Configuration --tl:off --nologo -v minimal --no-restore
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

if ($CommandArgs) {
    $benchmarkDll = Join-Path $repoRoot "bench\Lokad.Utf8Regex.Benchmarks\bin\$Configuration\net10.0\Lokad.Utf8Regex.Benchmarks.dll"
    $processInfo.ArgumentList.Add($benchmarkDll)
}
else {
    $processInfo.ArgumentList.Add("run")
    $processInfo.ArgumentList.Add("--project")
    $processInfo.ArgumentList.Add(".\bench\Lokad.Utf8Regex.Benchmarks\Lokad.Utf8Regex.Benchmarks.csproj")
    $processInfo.ArgumentList.Add("-c")
    $processInfo.ArgumentList.Add($Configuration)
    $processInfo.ArgumentList.Add("--")
}

foreach ($argument in $benchmarkArgs) {
    $processInfo.ArgumentList.Add($argument)
}

$benchmarkProcess = [System.Diagnostics.Process]::Start($processInfo)
if ($null -eq $benchmarkProcess) {
    throw "Unable to start the benchmark process."
}

if ($effectiveMaxWallTimeSeconds -gt 0 -and -not $benchmarkProcess.WaitForExit($effectiveMaxWallTimeSeconds * 1000)) {
    $benchmarkProcess.Kill($true)
    $benchmarkProcess.WaitForExit()
    Write-Error "Benchmark exceeded the $effectiveMaxWallTimeSeconds second wall-time cap. Pass -MaxWallTimeSeconds 0 only for a preflighted milestone refresh."
    exit 124
}

$benchmarkProcess.WaitForExit()
exit $benchmarkProcess.ExitCode
