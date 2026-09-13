# Shared machinery for isolated package-consumer qualification scripts (PLAN.md D08).
# Each per-package script dot-sources this file; the per-package flow (packs,
# shape asserts, evidence manifest) stays in the calling script.

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
function Read-CoherentPackageVersion {
    param([string]$RepositoryRoot)

    $coreProjectPath = Join-Path $RepositoryRoot "src\Lokad.Utf8Regex\Lokad.Utf8Regex.csproj"
    [xml] $coreProject = Get-Content -LiteralPath $coreProjectPath
    $versionNode = $coreProject.SelectSingleNode("/Project/PropertyGroup/Version")
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        # Shared release metadata centralizes the coherent package version.
        $sharedPropsPath = Join-Path $RepositoryRoot "src\Directory.Build.props"
        [xml] $sharedProps = Get-Content -LiteralPath $sharedPropsPath
        $versionNode = $sharedProps.SelectSingleNode("/Project/PropertyGroup/Version")
    }

    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Could not read the package version from $coreProjectPath."
    }

    return $versionNode.InnerText
}

function New-PackageQualificationRoot {
    param(
        [string]$RepositoryRoot,
        [string]$SourceRevision,
        [bool]$Archive
    )

    $artifactsParent = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot "artifacts"))
    $qualificationParent = [System.IO.Path]::GetFullPath((Join-Path $artifactsParent "package-qualification"))

    if ($Archive) {
        $trackedStatus = @(& git -C $RepositoryRoot status --porcelain --untracked-files=no)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect tracked worktree state before archival qualification."
        }

        if ($trackedStatus.Count -ne 0) {
            throw "Archival qualification requires a clean tracked worktree."
        }

        $qualificationRoot = [System.IO.Path]::GetFullPath(
            (Join-Path $qualificationParent (Join-Path "archive" $SourceRevision)))
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

    return [pscustomobject]@{
        QualificationRoot = $qualificationRoot
        PackageDirectory = $packageDirectory.FullName
        TestDirectory = $testDirectory.FullName
        PackageCache = $packageCache.FullName
    }
}

function Invoke-IsolatedConsumerTests {
    param(
        [string]$RepositoryRoot,
        [string]$QualificationRoot,
        [string]$PackageDirectory,
        [string]$TestDirectory,
        [string]$PackageCache,
        [string]$Configuration,
        [string]$SourceTestProjectName,
        [string[]]$PackageIds,
        [string]$Version
    )

    $sourceTests = Join-Path (Join-Path $RepositoryRoot "tests") $SourceTestProjectName
    Copy-Item -Path (Join-Path $sourceTests "*") -Destination $TestDirectory -Recurse -Force
    $testPrefix = $TestDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    Get-ChildItem -LiteralPath $TestDirectory -Directory -Recurse |
        Where-Object Name -In @("bin", "obj") |
        Sort-Object { $_.FullName.Length } -Descending |
        ForEach-Object {
            $resolved = [System.IO.Path]::GetFullPath($_.FullName)
            if (!$resolved.StartsWith($testPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove a copied build directory outside the qualification test directory."
            }

            Remove-Item -LiteralPath $resolved -Recurse -Force
        }

    $testProjectPath = Join-Path $TestDirectory ($SourceTestProjectName + ".csproj")
    [xml] $testProject = Get-Content -LiteralPath $testProjectPath
    @($testProject.SelectNodes("//ProjectReference")) | ForEach-Object {
        [void] $_.ParentNode.RemoveChild($_)
    }

    $packageGroup = $testProject.CreateElement("ItemGroup")
    foreach ($packageId in $PackageIds) {
        $reference = $testProject.CreateElement("PackageReference")
        $reference.SetAttribute("Include", $packageId)
        $reference.SetAttribute("Version", $Version)
        [void] $packageGroup.AppendChild($reference)
    }
    [void] $testProject.Project.AppendChild($packageGroup)
    $testProject.Save($testProjectPath)

    if ($testProject.SelectNodes("//ProjectReference").Count -ne 0) {
        throw "The isolated test project still contains a sibling project reference."
    }

    $nugetConfigPath = Join-Path $QualificationRoot "NuGet.Config"
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="qualified-packages" value="$PackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
    $nugetConfig | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8

    $previousPackageCache = $env:NUGET_PACKAGES
    try {
        $env:NUGET_PACKAGES = $PackageCache
        Invoke-DotNet @("restore", $testProjectPath, "--configfile", $nugetConfigPath, "--tl:off", "-v", "minimal")
        Invoke-DotNet @("test", $testProjectPath, "-c", $Configuration, "--no-restore", "--tl:off", "--nologo", "-v", "minimal")
    }
    finally {
        $env:NUGET_PACKAGES = $previousPackageCache
    }
}
