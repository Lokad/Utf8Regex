param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [string]$Filter,
    [int]$RetryCount = 3
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
Set-Location $repoRoot

function Stop-StaleProcesses {
    $staleProcesses = @("testhost", "VBCSCompiler")
    foreach ($processName in $staleProcesses) {
        Get-Process -Name $processName -ErrorAction SilentlyContinue | Stop-Process -Force
    }
}

function Invoke-DotNetWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [int]$MaxAttempts
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $output = & dotnet @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | Write-Host

        if ($exitCode -eq 0) {
            return 0
        }

        $isLastAttempt = $attempt -ge $MaxAttempts
        $isTransientLock = ($output | Out-String) -match 'error CS2012|error MSB3021|error MSB3026|being used by another process|file may be locked'
        if (-not $isTransientLock -or $isLastAttempt) {
            return $exitCode
        }

        Stop-StaleProcesses
        Start-Sleep -Seconds 2
    }

    return 1
}

Stop-StaleProcesses
dotnet build-server shutdown | Out-Null

$stableDotNetArgs = @(
    "--disable-build-servers",
    "/nodeReuse:false",
    "/p:UseSharedCompilation=false"
)

if (-not $SkipBuild) {
    $buildArguments = @(
        "build",
        "Lokad.Utf8Regex.slnx",
        "--configuration", $Configuration,
        "--tl:off",
        "--nologo",
        "-v", "minimal",
        "--no-restore"
    ) + $stableDotNetArgs

    $buildExitCode = Invoke-DotNetWithRetry -Arguments $buildArguments -MaxAttempts $RetryCount
    if ($buildExitCode -ne 0) {
        exit $buildExitCode
    }
}

$testArguments = @(
    "test",
    "Lokad.Utf8Regex.slnx",
    "--configuration", $Configuration,
    "--tl:off",
    "--nologo",
    "-v", "minimal",
    "--no-build",
    "--no-restore"
) + $stableDotNetArgs

if ($Filter) {
    $testArguments += @("--filter", $Filter)
}

$testExitCode = Invoke-DotNetWithRetry -Arguments $testArguments -MaxAttempts $RetryCount
exit $testExitCode
