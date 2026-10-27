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
    # Only test hosts whose executable is under this checkout are eligible.
    # Command-line mentions and sibling path prefixes do not establish ownership.
    # Unknown executable paths fail closed. VBCSCompiler is deliberately
    # excluded on Windows: it is a shared SDK-wide compiler server without
    # repository identity. Ordinary builds reuse the SDK's build/compiler servers.
    # Recovery is attempted only after an observed file-lock failure.
    if ($IsWindows) {
        $repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        Get-CimInstance Win32_Process -Filter "Name = 'testhost.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.ExecutablePath -and
                ([IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase) } |
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

# A stale filter must not report successful validation without running tests.
$testArguments += @("--", "RunConfiguration.TreatNoTestsAsError=true")
$testExitCode = Invoke-DotNetWithRetry -Arguments $testArguments -MaxAttempts $RetryCount
exit $testExitCode
