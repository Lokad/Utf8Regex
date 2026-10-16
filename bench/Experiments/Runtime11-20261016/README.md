# Frozen .NET 11 comparator experiment

This is the source fixture for the four-case experiment in
[RUNTIME-COMPARISON.md](../../../RUNTIME-COMPARISON.md), not a new product or
qualification framework. `measurement.json` is the numeric source of truth;
it contains the identities, raw samples, process order and exclusions.

Both harnesses used these exact `Program.cs` bytes. The SDK11 project differs
only in TargetFramework=net11.0. SDK10.0.401 used the net10.0 reference generator;
SDK11.0.100-rc.1.26425.128 used its own generator. The exact hashes/generated-source
hashes and four expected range digests are retained in the measurement.

To reproduce, stage this project outside the repository on a non-benchmark build
host. Put the exact private scalar.59 Core DLL in `../product/Lokad.Utf8Regex.dll`;
the program rejects a different hash. Its archive/producer identity is recorded
in the measurement. A current source build is a different input. Keep separate
build directories for the two SDK/TFM projects so their restore assets stay separate.

Build Release with each explicit SDK DLL, `--tl:off --nologo -v minimal`,
`-p:MSBuildEnableWorkloadResolver=false` and `-p:GeneratorSha256=<recorded hash>`.
Use `dotnet /path/to/sdk/<version>/dotnet.dll build driver.csproj -c Release ...`.
Do not retarget shipped projects or build on the quiet VM. Inspect freshly emitted
`obj/Release/<TFM>/generated` source: all four methods must have runner factories,
with no runtime `new Regex` fallback.

Freeze each DLL/deps/runtimeconfig plus identical Core bytes before timing.
Launch via explicit `dotnet --fx-version <runtime> driver.dll --verify <new.jsonl>`
in all three cells: SDK10/net10 on runtime10, the same binary on runtime11,
and SDK11/net11 on runtime11. Use `--sample <ordinal> <new.jsonl>` for each
of five recorded three-cell orders, sequentially on CPU2 with
`DOTNET_TieredCompilation=0` and `DOTNET_ReadyToRun=0`. Each child is capped at60s,
the fixed15-process batch at480s. Constructors/startup are untimed; every sample
reports its actual floor, count and allocation. Outputs use CreateNew.

The original build scripts, generated source/diff, all journals/logs and the
immutable payload remain locally at artifacts/runtime11-comparison-20261016/.
No rerun is needed for unchanged inputs. The original SDK11 build attempted a
TargetFramework override that restored net10 assets; separate projects corrected
that before any timing. This fixture is intentionally outside the default solution
and does not require SDK11 for ordinary repository builds.
