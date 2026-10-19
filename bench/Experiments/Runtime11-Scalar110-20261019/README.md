# Frozen current110 .NET11 comparison

Numeric source: measurement.json. This reuses the original four-case/three-cell
fixture at ../Runtime11-20261016, with only its product-hash guard updated for the
actual immutable Core110 DLL. Original59 fixture/data remain unchanged.

Build these exact Program.cs/driver.csproj bytes off the quiet VM in separate
net10/net11 directories, with the recorded SDK10.0.401/SDK11RC1 explicit SDK DLLs,
--tl:off and actual GeneratorSha256 metadata. Only net11 project TargetFramework
changes. Place the actual Core110 DLL at ../product/Lokad.Utf8Regex.dll; its guard
rejects other bytes. Verify all four generated methods have real runner factories.

Freeze payloads and launch --verify in all cells, then --sample ordinal output
for the five recorded rotations: SDK10/net10 on10.0.12; same DLL on11RC1;
SDK11/net11 on11RC1. Explicit --fx-version, CPU2, TieredCompilation=0 and
ReadyToRun=0. Original120ms warmup/80ms calibration/50ms settled floor;60s child /
480s batch cap. Output paths use CreateNew. No retargeting shipped projects,
new framework or rerun for unchanged inputs. All raw samples/order/identities,
limits and exclusions are retained in measurement.json.

Full local scripts/payloads/generation sources/journals and verified archive live
at artifacts/runtime11-scalar110-comparison-20261019. This is Count-only curated
ASCII overlap, not default-tier cost, full semantics/runtime qualification or
current CPython Status. Source-generator changes are not isolated from fresh
TFM/compiler/harness effects; ordinary/compiled lanes are the controls.
