# Shared machinery for isolated package-consumer qualification scripts (PLAN.md D08).
# Each per-package script dot-sources this file; the per-package flow (packs,
# shape asserts, evidence manifest) stays in the calling script.

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Read-PackageProvenance {
    param([string] $PackagePath, [string] $ExpectedAssembly)

    # Inspect metadata without loading or executing a packaged assembly.
    $package = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $nuspecEntries = @($package.Entries | Where-Object FullName -Like "*.nuspec")
        if ($nuspecEntries.Count -ne 1) {
            throw "$PackagePath must contain exactly one nuspec."
        }
        $textReader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try { [xml] $nuspec = $textReader.ReadToEnd() }
        finally { $textReader.Dispose() }
        $repository = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='repository']")

        $assemblyEntry = $package.GetEntry("lib/net10.0/$ExpectedAssembly")
        if ($null -eq $assemblyEntry) { throw "$PackagePath does not contain $ExpectedAssembly." }
        $assemblyStream = [System.IO.MemoryStream]::new()
        try {
            $entryStream = $assemblyEntry.Open()
            try { $entryStream.CopyTo($assemblyStream) }
            finally { $entryStream.Dispose() }
            $assemblyHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($assemblyStream.ToArray()))
            $assemblyStream.Position = 0
            $pe = [System.Reflection.PortableExecutable.PEReader]::new($assemblyStream)
            try {
                $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                $versions = @(foreach ($handle in $metadata.GetAssemblyDefinition().GetCustomAttributes()) {
                    $attribute = $metadata.GetCustomAttribute($handle)
                    if ($attribute.Constructor.Kind -ne [System.Reflection.Metadata.HandleKind]::MemberReference) { continue }
                    $row = [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($attribute.Constructor)
                    $member = $metadata.GetMemberReference([System.Reflection.Metadata.Ecma335.MetadataTokens]::MemberReferenceHandle($row))
                    if ($member.Parent.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) { continue }
                    $typeRow = [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($member.Parent)
                    $type = $metadata.GetTypeReference([System.Reflection.Metadata.Ecma335.MetadataTokens]::TypeReferenceHandle($typeRow))
                    if ($metadata.GetString($type.Namespace) -eq "System.Reflection" -and
                        $metadata.GetString($type.Name) -eq "AssemblyInformationalVersionAttribute") {
                        $blob = $metadata.GetBlobReader($attribute.Value)
                        if ($blob.ReadUInt16() -ne 1) { throw "$PackagePath has an invalid version attribute." }
                        $blob.ReadSerializedString()
                    }
                })
                if ($versions.Count -ne 1) { throw "$PackagePath must have exactly one informational version." }
                $informationalVersion = $versions[0]
                $assemblyRevision = $null
                if ($informationalVersion -match '\+(?<Revision>[0-9a-fA-F]{40})$') {
                    $assemblyRevision = $Matches.Revision.ToLowerInvariant()
                }
                $codeViewEntries = @(foreach ($debugEntry in $pe.ReadDebugDirectory()) {
                    if ($debugEntry.Type -eq [System.Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView) {
                        $codeView = $pe.ReadCodeViewDebugDirectoryData($debugEntry)
                        [ordered]@{ Guid = $codeView.Guid.ToString(); Stamp = $debugEntry.Stamp; Age = $codeView.Age }
                    }
                })
            }
            finally { $pe.Dispose() }
        }
        finally { $assemblyStream.Dispose() }
    }
    finally { $package.Dispose() }

    $symbolPackagePath = [System.IO.Path]::ChangeExtension($PackagePath, ".snupkg")
    $symbolPackagePresent = Test-Path -LiteralPath $symbolPackagePath -PathType Leaf
    $symbolPackageHash = $null
    $pdbPresent = $false
    $pdbHash = $null
    $pdbGuid = $null
    $pdbStamp = $null
    $sourceLinkRecords = @()
    if ($symbolPackagePresent) {
        $symbolPackageHash = (Get-FileHash -LiteralPath $symbolPackagePath -Algorithm SHA256).Hash
        $symbols = [System.IO.Compression.ZipFile]::OpenRead($symbolPackagePath)
        try {
            $pdbEntry = $symbols.GetEntry("lib/net10.0/$($ExpectedAssembly -replace '\.dll$', '.pdb')")
            $pdbPresent = $null -ne $pdbEntry
            if ($pdbPresent) {
                $pdbStream = [System.IO.MemoryStream]::new()
                try {
                    $entryStream = $pdbEntry.Open()
                    try { $entryStream.CopyTo($pdbStream) }
                    finally { $entryStream.Dispose() }
                    $pdbHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($pdbStream.ToArray()))
                    $pdbStream.Position = 0
                    $provider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream(
                        $pdbStream, [System.Reflection.Metadata.MetadataStreamOptions]::LeaveOpen, 0)
                    try {
                        $pdb = $provider.GetMetadataReader()
                        [byte[]] $pdbId = @($pdb.DebugMetadataHeader.Id)
                        if ($pdbId.Length -ne 20) { throw "$symbolPackagePath has an invalid portable PDB identity." }
                        $pdbGuid = [Guid]::new([byte[]] $pdbId[0..15]).ToString()
                        $pdbStamp = [BitConverter]::ToUInt32($pdbId, 16)
                        $sourceLinkRecords = @(foreach ($handle in $pdb.CustomDebugInformation) {
                            $information = $pdb.GetCustomDebugInformation($handle)
                            if ($pdb.GetGuid($information.Kind).ToString() -eq "cc110556-a091-4d38-9fec-25ab9a351a6a") {
                                $sourceLink = [Text.Encoding]::UTF8.GetString($pdb.GetBlobBytes($information.Value)) | ConvertFrom-Json -AsHashtable
                                $mappings = @(foreach ($mapping in $sourceLink.documents.GetEnumerator()) {
                                    $revision = $null
                                    if ($mapping.Value -match '^https://raw\.githubusercontent\.com/Lokad/Utf8Regex/(?<Revision>[0-9a-fA-F]{40})/\*$') {
                                        $revision = $Matches.Revision.ToLowerInvariant()
                                    }
                                    [ordered]@{ Document = $mapping.Key; Url = $mapping.Value; Revision = $revision }
                                })
                                [ordered]@{
                                    IsModuleRecord = $information.Parent.Kind -eq [System.Reflection.Metadata.HandleKind]::ModuleDefinition
                                    Mappings = $mappings
                                }
                            }
                        })
                    }
                    finally { $provider.Dispose() }
                }
                finally { $pdbStream.Dispose() }
            }
        }
        finally { $symbols.Dispose() }
    }

    return [pscustomobject][ordered]@{
        Package = [System.IO.Path]::GetFileName($PackagePath)
        PackageSha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
        NuSpecRevision = if ($null -ne $repository) { $repository.GetAttribute("commit").ToLowerInvariant() } else { $null }
        Assembly = $ExpectedAssembly
        AssemblySha256 = $assemblyHash
        InformationalVersion = $informationalVersion
        AssemblyRevision = $assemblyRevision
        CodeViewEntries = $codeViewEntries
        SymbolPackage = [System.IO.Path]::GetFileName($symbolPackagePath)
        SymbolPackagePresent = $symbolPackagePresent
        SymbolPackageSha256 = $symbolPackageHash
        PdbPresent = $pdbPresent
        PdbSha256 = $pdbHash
        PdbGuid = $pdbGuid
        PdbStamp = $pdbStamp
        SourceLinkRecords = $sourceLinkRecords
    }
}

function Assert-PackageProvenance {
    param([object] $Provenance, [string] $SourceRevision)

    if ($SourceRevision -notmatch '^[0-9a-fA-F]{40}$') { throw "Invalid qualification source revision." }
    if ($Provenance.NuSpecRevision -ne $SourceRevision) {
        throw "$($Provenance.Package) NuSpec revision does not match $SourceRevision."
    }
    if ($Provenance.AssemblyRevision -ne $SourceRevision) {
        throw "$($Provenance.Package) assembly revision $($Provenance.AssemblyRevision) does not match $SourceRevision; rebuild before packing."
    }
    if (!$Provenance.SymbolPackagePresent -or !$Provenance.PdbPresent) {
        throw "$($Provenance.Package) is missing its symbol package or portable PDB."
    }
    if ($Provenance.CodeViewEntries.Count -ne 1 -or $Provenance.CodeViewEntries[0].Age -ne 1 -or
        $Provenance.CodeViewEntries[0].Guid -ne $Provenance.PdbGuid -or
        $Provenance.CodeViewEntries[0].Stamp -ne $Provenance.PdbStamp) {
        throw "$($Provenance.Package) portable PDB identity does not match its assembly."
    }
    if ($Provenance.SourceLinkRecords.Count -ne 1 -or !$Provenance.SourceLinkRecords[0].IsModuleRecord -or
        $Provenance.SourceLinkRecords[0].Mappings.Count -eq 0) {
        throw "$($Provenance.Package) is missing a unique module SourceLink document map."
    }
    foreach ($mapping in $Provenance.SourceLinkRecords[0].Mappings) {
        if ($mapping.Revision -ne $SourceRevision) {
            throw "$($Provenance.Package) SourceLink revision does not match ${SourceRevision}: $($mapping.Url)"
        }
    }
}

function Read-PackageQualificationSource {
    param([string] $RepositoryRoot, [string] $SourceRevision, [bool] $RequireCleanSource)

    $currentRevision = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $currentRevision -ne $SourceRevision) {
        throw "Source revision changed during package qualification."
    }
    $trackedChanges = @(& git -C $RepositoryRoot status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect package source state." }
    $untrackedSources = @(& git -C $RepositoryRoot ls-files --others --exclude-standard -- src)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect untracked package sources." }
    $cleanSource = $trackedChanges.Count -eq 0 -and $untrackedSources.Count -eq 0
    if ($RequireCleanSource -and !$cleanSource) { throw "Archival provenance requires clean tracked files and no untracked package sources." }
    return [pscustomobject]@{ CleanSource = $cleanSource; TrackedChanges = $trackedChanges; UntrackedSources = $untrackedSources }
}

function Write-PackageProvenanceEvidence {
    param(
        [string] $RepositoryRoot,
        [string] $QualificationRoot,
        [string] $SourceRevision,
        [string] $Configuration,
        [string[]] $PackagePaths,
        [string[]] $ExpectedAssemblies,
        [bool] $RequireCleanSource
    )

    $source = Read-PackageQualificationSource $RepositoryRoot $SourceRevision $RequireCleanSource
    if ($PackagePaths.Count -ne $ExpectedAssemblies.Count -or $PackagePaths.Count -eq 0) {
        throw "Package provenance requires one assembly identity per package."
    }
    $records = @(for ($index = 0; $index -lt $PackagePaths.Count; $index++) {
        $record = Read-PackageProvenance $PackagePaths[$index] $ExpectedAssemblies[$index]
        Assert-PackageProvenance $record $SourceRevision
        $record
    })
    $report = [ordered]@{
        SchemaVersion = 1
        SourceRevision = $SourceRevision
        Configuration = $Configuration
        CleanSource = $source.CleanSource
        TrackedChanges = $source.TrackedChanges
        UntrackedSources = $source.UntrackedSources
        Packages = $records
    }
    $path = Join-Path $QualificationRoot "package-provenance.json"
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
    # Added artifacts retain manifest schema 1, consumed by existing verifiers.
    $artifacts = @(foreach ($record in $records) {
        [ordered]@{ Name = "$($record.Assembly)-symbols"; RelativePath = "packages/$($record.SymbolPackage)"; Sha256 = $record.SymbolPackageSha256 }
    })
    $artifacts += [ordered]@{ Name = "package-provenance"; RelativePath = "package-provenance.json"; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    return [pscustomobject]@{ Path = $path; Artifacts = $artifacts }
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

            $requiredDeclaration = "id=`"$RequiredDependency`" version=`"[$RequiredVersion]`""
            if (!$nuspec.Contains($requiredDeclaration, [StringComparison]::Ordinal)) {
                throw "$PackagePath does not declare the exact $RequiredDependency [$RequiredVersion] dependency."
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
        [void] (Read-PackageQualificationSource $RepositoryRoot $SourceRevision $true)

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
        [string]$Version,
        [switch]$PinQualifiedPackageSources,
        [string]$TestResultsDirectory
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
    # Keep linked fixture helpers inside the detached qualification tree.
    # PythonRe links four benchmark catalog/provenance sources this way.
    $linkedRoot = [IO.Path]::GetFullPath((Join-Path $QualificationRoot "linked-source"))
    $linkedPrefix = $linkedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $repositoryPrefix = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($compile in @($testProject.SelectNodes("//Compile[@Include and @Link]"))) {
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $sourceTests $compile.GetAttribute("Include")))
        $destinationPath = [IO.Path]::GetFullPath((Join-Path $linkedRoot $compile.GetAttribute("Link")))
        if (!$sourcePath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            !$destinationPath.StartsWith($linkedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Linked fixture source escaped its repository or qualification directory."
        }
        [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $destinationPath) -Force)
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
        $compile.SetAttribute("Include", [IO.Path]::GetRelativePath($TestDirectory, $destinationPath))
    }
    @($testProject.SelectNodes("//ProjectReference")) | ForEach-Object {
        [void] $_.ParentNode.RemoveChild($_)
    }

    $packageGroup = $testProject.CreateElement("ItemGroup")
    foreach ($packageId in $PackageIds) {
        $reference = $testProject.CreateElement("PackageReference")
        $reference.SetAttribute("Include", $packageId)
        $reference.SetAttribute("Version", $(if ($PinQualifiedPackageSources) { "[$Version]" } else { $Version }))
        [void] $packageGroup.AppendChild($reference)
    }
    [void] $testProject.Project.AppendChild($packageGroup)
    $testProject.Save($testProjectPath)

    if ($testProject.SelectNodes("//ProjectReference").Count -ne 0) {
        throw "The isolated test project still contains a sibling project reference."
    }

    $nugetConfigPath = Join-Path $QualificationRoot "NuGet.Config"
    $sourceMapping = ""
    if ($PinQualifiedPackageSources) {
        $patterns = @($PackageIds | ForEach-Object {
            '      <package pattern="' + [Security.SecurityElement]::Escape($_) + '" />'
        }) -join [Environment]::NewLine
        $sourceMapping = @"
  <packageSourceMapping>
    <packageSource key="qualified-packages">
$patterns
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
"@
    }
    $escapedPackageDirectory = [Security.SecurityElement]::Escape($PackageDirectory)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="qualified-packages" value="$escapedPackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
$sourceMapping
</configuration>
"@
    $nugetConfig | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8

    $previousPackageCache = $env:NUGET_PACKAGES
    try {
        $env:NUGET_PACKAGES = $PackageCache
        $restoreArguments = @("restore", $testProjectPath, "--configfile", $nugetConfigPath, "--tl:off", "-v", "minimal")
        if ($PinQualifiedPackageSources) { $restoreArguments += "--use-lock-file" }
        Invoke-DotNet $restoreArguments
        $testArguments = @("test", $testProjectPath, "-c", $Configuration, "--no-restore", "--tl:off", "--nologo", "-v", "minimal")
        if ($TestResultsDirectory) {
            $testArguments += @("--logger", "trx;LogFileName=qualified.trx", "--results-directory", $TestResultsDirectory)
        }
        if ($PinQualifiedPackageSources) {
            $testArguments += @("--disable-build-servers", "/m:1", "/nodeReuse:false", "/p:UseSharedCompilation=false")
        }
        Invoke-DotNet $testArguments
    }
    finally {
        $env:NUGET_PACKAGES = $previousPackageCache
    }
}
