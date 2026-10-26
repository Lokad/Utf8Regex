# Consuming the common RC2 candidate

These are unpublished prerelease packages. Use all selected packages at
0.4.0-rc.2. Both adapters require exactly Core [0.4.0-rc.2]; do not override pins.

The immutable feed is ../package-candidates/0.4.0-rc.2/feed. Its adjacent
NuGet.Config maps the three package IDs to that feed, with nuget.org for unrelated
dependencies. The release archive places the same feed/config at its root.
Restore with that config and a fresh, task-local --packages cache, then retain
packages.lock.json. An existing global cache is not acceptance evidence.

For example, a net10.0 project can reference:

    <PackageReference Include="Lokad.Utf8Regex" Version="[0.4.0-rc.2]" />
    <PackageReference Include="Lokad.Utf8Regex.PythonRe" Version="[0.4.0-rc.2]" />
    <PackageReference Include="Lokad.Utf8Regex.Pcre2" Version="[0.4.0-rc.2]" />

    dotnet restore consumer.csproj --configfile <candidate>/NuGet.Config --packages <fresh-cache> --use-lock-file --tl:off

Compare the lock SHA512, nupkg SHA256 and loaded DLL SHA256/informational version
with candidate.json and the release receipt. Source/project references or a
version string alone do not establish the candidate's identity.

Read the packaged MIGRATION.md, HOST-INTEGRATION.md, CHANGELOG.md and profile
README before upgrading. The contract matches Unicode scalars over strictly
validated UTF-8, with primary byte coordinates. Finite policies cover modeled
engine capacities; defaults/static calls are unlimited. Host retention and
callbacks require separate admission. Complete .NET/Python/PCRE2 equivalence,
NativeAOT/trimming and whole-process memory containment are not promised.

The producer's unchanged consumer assertions establish their tested subset;
they do not change external consumer pins or prove deployment/adoption.
NuGet publication and release tags are separate actions.