[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [switch] $Archive
)

$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-PackageShape {
    param(
        [string] $PackagePath,
        [string] $ExpectedAssembly,
        [string] $RequiredDependency,
        [string] $RequiredVersion
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object FullName)
        $expectedLibrary = "lib/net10.0/$ExpectedAssembly"
        if ($expectedLibrary -notin $entryNames) {
            throw "$PackagePath does not contain $expectedLibrary."
        }

        $expectedDocumentation = $expectedLibrary -replace '\.dll$', '.xml'
        if ($expectedDocumentation -notin $entryNames) {
            throw "$PackagePath does not contain $expectedDocumentation."
        }

        $forbiddenEntries = @($entryNames | Where-Object {
            $_ -match '(^|/)runtimes/' -or
            $_ -match '(^|/)native/' -or
            $_ -match '\.(so|dylib|exe)$'
        })
        if ($forbiddenEntries.Count -ne 0) {
            throw "$PackagePath contains forbidden native or RID assets: $($forbiddenEntries -join ', ')."
        }

        $implementationAssemblies = @($entryNames | Where-Object { $_ -match '^lib/[^/]+/.*\.dll$' })
        if ($implementationAssemblies.Count -ne 1 -or $implementationAssemblies[0] -ne $expectedLibrary) {
            throw "$PackagePath contains an unexpected implementation assembly set: $($implementationAssemblies -join ', ')."
        }

        if ($RequiredDependency.Length -ne 0) {
            $nuspecEntry = $archive.Entries | Where-Object FullName -Like "*.nuspec" | Select-Object -First 1
            if ($null -eq $nuspecEntry) {
                throw "$PackagePath does not contain a nuspec."
            }
            $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
            try {
                $nuspec = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            $requiredDeclaration = "id=`"$RequiredDependency`" version=`"$RequiredVersion`""
            if (!$nuspec.Contains($requiredDeclaration, [StringComparison]::Ordinal)) {
                throw "$PackagePath does not declare the required $RequiredDependency $RequiredVersion dependency."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$artifactsParent = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$qualificationParent = [System.IO.Path]::GetFullPath((Join-Path $artifactsParent "package-qualification"))
$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceRevision -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Could not resolve the source revision for package qualification."
}

if ($Archive) {
    $trackedStatus = @(& git -C $repositoryRoot status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect tracked worktree state before archival qualification."
    }

    if ($trackedStatus.Count -ne 0) {
        throw "Archival qualification requires a clean tracked worktree."
    }

    $qualificationRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $qualificationParent (Join-Path "archive" $sourceRevision)))
}
else {
    $qualificationRoot = [System.IO.Path]::GetFullPath((Join-Path $qualificationParent "current"))
}

$requiredPrefix = $artifactsParent.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (!$qualificationRoot.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Qualification output escaped the repository artifacts directory."
}

if (Test-Path -LiteralPath $qualificationRoot) {
    if ($Archive) {
        throw "The immutable qualification archive already exists: $qualificationRoot"
    }

    Remove-Item -LiteralPath $qualificationRoot -Recurse -Force
}

$packageDirectory = New-Item -ItemType Directory -Path (Join-Path $qualificationRoot "packages")
$testDirectory = New-Item -ItemType Directory -Path (Join-Path $qualificationRoot "tests")
$packageCache = New-Item -ItemType Directory -Path (Join-Path $qualificationRoot "package-cache")

$coreProjectPath = Join-Path $repositoryRoot "src\Lokad.Utf8Regex\Lokad.Utf8Regex.csproj"
$pcre2ProjectPath = Join-Path $repositoryRoot "src\Lokad.Utf8Regex.Pcre2\Lokad.Utf8Regex.Pcre2.csproj"
[xml] $coreProject = Get-Content -LiteralPath $coreProjectPath
$versionNode = $coreProject.SelectSingleNode("/Project/PropertyGroup/Version")
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Could not read the package version from $coreProjectPath."
}

$version = $versionNode.InnerText
Invoke-DotNet @(
    "pack", $coreProjectPath,
    "-c", $Configuration,
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($packageDirectory.FullName)")
Invoke-DotNet @(
    "pack", $pcre2ProjectPath,
    "-c", $Configuration,
    "--tl:off", "--nologo", "-v", "minimal",
    "-p:PackageOutputPath=$($packageDirectory.FullName)")

$corePackage = Join-Path $packageDirectory.FullName "Lokad.Utf8Regex.$version.nupkg"
$pcre2Package = Join-Path $packageDirectory.FullName "Lokad.Utf8Regex.Pcre2.$version.nupkg"
Assert-PackageShape $corePackage "Lokad.Utf8Regex.dll" "" ""
Assert-PackageShape $pcre2Package "Lokad.Utf8Regex.Pcre2.dll" "Lokad.Utf8Regex" $version

$sourceTests = Join-Path $repositoryRoot "tests\Lokad.Utf8Regex.Pcre2.Tests"
Copy-Item -Path (Join-Path $sourceTests "*") -Destination $testDirectory.FullName -Recurse -Force
$testPrefix = $testDirectory.FullName.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
Get-ChildItem -LiteralPath $testDirectory.FullName -Directory -Recurse |
    Where-Object Name -In @("bin", "obj") |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $resolved = [System.IO.Path]::GetFullPath($_.FullName)
        if (!$resolved.StartsWith($testPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a copied build directory outside the qualification test directory."
        }

        Remove-Item -LiteralPath $resolved -Recurse -Force
    }

$testProjectPath = Join-Path $testDirectory.FullName "Lokad.Utf8Regex.Pcre2.Tests.csproj"
[xml] $testProject = Get-Content -LiteralPath $testProjectPath
@($testProject.SelectNodes("//ProjectReference")) | ForEach-Object {
    [void] $_.ParentNode.RemoveChild($_)
}

$packageGroup = $testProject.CreateElement("ItemGroup")
foreach ($packageId in @("Lokad.Utf8Regex", "Lokad.Utf8Regex.Pcre2")) {
    $reference = $testProject.CreateElement("PackageReference")
    $reference.SetAttribute("Include", $packageId)
    $reference.SetAttribute("Version", $version)
    [void] $packageGroup.AppendChild($reference)
}
[void] $testProject.Project.AppendChild($packageGroup)
$testProject.Save($testProjectPath)

if ($testProject.SelectNodes("//ProjectReference").Count -ne 0) {
    throw "The isolated test project still contains a sibling project reference."
}

$nugetConfigPath = Join-Path $qualificationRoot "NuGet.Config"
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="qualified-packages" value="$($packageDirectory.FullName)" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8

$previousPackageCache = $env:NUGET_PACKAGES
try {
    $env:NUGET_PACKAGES = $packageCache.FullName
    Invoke-DotNet @("restore", $testProjectPath, "--configfile", $nugetConfigPath, "--tl:off", "-v", "minimal")
    Invoke-DotNet @("test", $testProjectPath, "-c", $Configuration, "--no-restore", "--tl:off", "--nologo", "-v", "minimal")
}
finally {
    $env:NUGET_PACKAGES = $previousPackageCache
}

if ($Archive) {
    $evidenceDirectory = New-Item -ItemType Directory -Path (Join-Path $qualificationRoot "evidence")
    $snapshotSource = Join-Path $repositoryRoot "PCRE2.Benchmarks.json"
    $apiSource = Join-Path $repositoryRoot "tests\Lokad.Utf8Regex.Pcre2.Tests\PublicApi.Shipped.txt"
    $snapshotArchive = Join-Path $evidenceDirectory.FullName "PCRE2.Benchmarks.json"
    $apiArchive = Join-Path $evidenceDirectory.FullName "PublicApi.Shipped.txt"
    Copy-Item -LiteralPath $snapshotSource -Destination $snapshotArchive
    Copy-Item -LiteralPath $apiSource -Destination $apiArchive

    $manifestArtifacts = @(
        [ordered]@{
            Name = "core-package"
            RelativePath = "packages/Lokad.Utf8Regex.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $corePackage -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pcre2-package"
            RelativePath = "packages/Lokad.Utf8Regex.Pcre2.$version.nupkg"
            Sha256 = (Get-FileHash -LiteralPath $pcre2Package -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pcre2-benchmark-snapshot"
            RelativePath = "evidence/PCRE2.Benchmarks.json"
            Sha256 = (Get-FileHash -LiteralPath $snapshotArchive -Algorithm SHA256).Hash
        },
        [ordered]@{
            Name = "pcre2-public-api"
            RelativePath = "evidence/PublicApi.Shipped.txt"
            Sha256 = (Get-FileHash -LiteralPath $apiArchive -Algorithm SHA256).Hash
        }
    )
    $manifest = [ordered]@{
        SchemaVersion = 1
        QualificationId = $sourceRevision
        SourceRevision = $sourceRevision
        CreatedAtUtc = [DateTime]::UtcNow.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
        Configuration = $Configuration
        TargetFramework = "net10.0"
        PackageVersion = $version
        Artifacts = $manifestArtifacts
    }
    $manifestPath = Join-Path $qualificationRoot "manifest.json"
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-Host "Archived immutable qualification evidence at $qualificationRoot."
}

Write-Host "Qualified Lokad.Utf8Regex and Lokad.Utf8Regex.Pcre2 $version from packed binaries."
