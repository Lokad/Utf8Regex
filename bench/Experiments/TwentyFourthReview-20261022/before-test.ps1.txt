param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [string]$Filter,
    [string]$Project = "Lokad.Utf8Regex.slnx",
    [int]$RetryCount = 3
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
Set-Location $repoRoot

function Stop-StaleProcess([System.Diagnostics.Process]$process, [string]$description) {
    if ($null -eq $process) {
        return
    }

    try {
        Stop-Process -InputObject $process -Force -ErrorAction Stop
    }
    catch {
        Write-Warning "Unable to stop stale $description $($process.Id): $($_.Exception.Message)"
    }
}

function Stop-StaleProcesses {
    # Only test hosts launched from this repository are eligible: their command
    # line references this repository's test output. VBCSCompiler is deliberately
    # excluded on Windows: it is a shared SDK-wide compiler server without
    # repository identity. Ordinary builds reuse the SDK's build/compiler servers.
    # Recovery is attempted only after an observed file-lock failure.
    if ($IsWindows) {
        Get-CimInstance Win32_Process -Filter "Name = 'testhost.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -and $_.CommandLine -like ("*" + [WildcardPattern]::Escape($repoRoot) + "*") } |
            ForEach-Object { Stop-StaleProcess -process (Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue) -description "test host" }
        return
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

if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) {
    throw "Test project or solution does not exist: $Project"
}

if (-not $SkipBuild) {
    $buildArguments = @(
        "build",
        $Project,
        "--configuration", $Configuration,
        "--tl:off",
        "--nologo",
        "-v", "minimal",
        "--no-restore",
        "-p:GeneratePackageOnBuild=false"
    )

    $buildExitCode = Invoke-DotNetWithRetry -Arguments $buildArguments -MaxAttempts $RetryCount
    if ($buildExitCode -ne 0) {
        exit $buildExitCode
    }
}

$testArguments = @(
    "test",
    $Project,
    "--configuration", $Configuration,
    "--tl:off",
    "--nologo",
    "-v", "minimal",
    "--no-build",
    "--no-restore"
)

if ($Filter) {
    $testArguments += @("--filter", $Filter)
}

$testExitCode = Invoke-DotNetWithRetry -Arguments $testArguments -MaxAttempts $RetryCount
exit $testExitCode
