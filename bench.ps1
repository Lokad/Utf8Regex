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
    [string[]]$CommandArgs
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

dotnet run --project .\bench\Lokad.Utf8Regex.Benchmarks\Lokad.Utf8Regex.Benchmarks.csproj -c $Configuration -- @benchmarkArgs
exit $LASTEXITCODE
