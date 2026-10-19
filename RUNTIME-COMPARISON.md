# .NET 10 / .NET 11 Count comparison

The timed results below remain frozen Core59 evidence. Current qualified
Core/PythonRe110 has a separate [untimed three-cell preflight](artifacts/runtime11-scalar110-preflight-20261019/RESULT.md):
four case/range digests and finite/token lanes agree on actual .NET10/.NET11
execution, with real SDK-specific generated runners. This does not refresh the
timings below or establish current-runtime performance or full compatibility.

Measured 2026-10-16 on the quiet Xeon Platinum 8573C VM, CPU2, Release FullOpts,
ReadyToRun off. This four-case experiment adds actual SDK-specific generated
comparators to the runtime comparison. It is not whole-engine parity, full
.NET11 runtime qualification, consumer adoption or a new package.

The [measurement](bench/Experiments/Runtime11-20261016/measurement.json) is the
numeric source of truth, including all raw samples, exact identities, order,
subject/range digests and exclusions. The [source fixture and reproduction
steps](bench/Experiments/Runtime11-20261016/README.md) retain the measured driver.
Existing README/adapter benchmark snapshots retain their original evidence.

## Inputs and controls

All three cells load identical Core0.4.0-scalar.59 bytes, producer b67b9233,
SHA2569453ACFFFC0E1C18A30FE8B1096481E48503A8089134A2EE5B1BBFC2CC99FBE9.
Core source was unchanged at0be8367f. This did not designate standalone Core59
for consumers; Core/PythonRe55 was the qualified pair at measurement time.
The current corrected dependency is [Core/PythonRe110](artifacts/core-python-scalar110-20261019/HANDOFF.md).

- R10/G10: SDK10.0.401/net10 driver on runtime10.0.12.
- R11/G10: that exact driver on runtime11.0.0-rc.1.26425.128. Same IL isolates
  runtime/JIT changes from fresh generator/TFM/compiler changes.
- R11/G11: SDK11.0.100-rc.1.26425.128/net11 driver on the same runtime11. Ordinary
  and compiled lanes control fresh-harness effects; do not attribute every change
  between the two R11 cells to the generator.

Generator hashes and generated-source hashes are recorded. All four generated
methods have runner factories with no runtime Regex fallback. Actual SDK/runtime11
build3551975 and historically assessed runtime tagab194157 remain separately
identified; matching version stamps do not prove source equivalence.

Five three-cell rotations run sequentially with alternating case/lane order;
constructors/startup are excluded. Each warm public Count returns a fixed integer.
All ordinary/compiled/generated/NonBacktracking count and whole-match range digests
agree across the cells on ASCII subjects. Dot over an emoji remains one owned
match/four bytes versus two BCL code-unit matches; that sentinel is untimed and
excluded from parity. Strict malformed-input and synchronous cancellation
preflights pass. These do not establish complete runtime semantic/resource coverage.

Finite policy is work50M/depth1024/working64MiB/result128MiB; live-token lanes use
an uncanceled token. These are experiment allowances, not safe host defaults.
Core includes strict UTF-8 validation. BCL predecoded lanes omit decoding; decode
lanes include strict UTF-8 decode/string allocation. NonBacktracking is compatible
with these capture-free regular patterns and is not a general replacement oracle.

## Core public Count costs

Median microseconds per operation, ordinary engine: unlimited / finite / finite
plus live token. The fixture also retains every compiled-Core lane.

| Case | R10/G10 | R11/G10 | R11/G11 |
| --- | ---: | ---: | ---: |
| literal-late | 7.55 / 4.08 / 4.07 | 5.17 / 3.98 / 3.98 | 4.25 / 3.70 / 3.69 |
| fixed-width | 8.20 / 932.87 / 910.70 | 8.96 / 909.31 / 903.98 | 8.99 / 907.02 / 904.32 |
| optional-prefix | 3170.30 / 1404.92 / 1391.70 | 3088.53 / 1396.15 / 1389.68 | 3118.05 / 1405.94 / 1396.29 |
| shadowed-family | 126.15 / 187.25 / 182.17 | 119.85 / 179.66 / 176.09 | 119.95 / 180.03 / 175.45 |

Literal late hit has65,542 UTF-8 bytes/one match. Other subjects repeat a frozen
fragment1,000 times and return2,000 matches: fixed-width `ab[0-9]{2}` is14,000B,
optional-prefix `a?b[0-9]{2}`13,000B, shadowed-family `foo|foobar`12,000B.

Finite allocations are112B for literal/family,1,792B for fixed-width and3,664B for
optional-prefix. Unlimited optional-prefix allocates about3.59MB per call; other
unlimited rows allocate0B. Thus a finite policy is not a uniform surcharge: route
selection matters. The large fixed-width finite/unlimited gap is a concrete next
operating-cost target. Basic work/depth/storage/result admission must remain.

## BCL comparator costs

Median microseconds, predecoded ordinary / compiled / SDK-generated /
NonBacktracking. All strict-decode lanes are in the measurement.

| Case | R10/G10 | R11/G11 |
| --- | ---: | ---: |
| literal-late | 5.70 / 4.26 / 4.26 / 5.68 | 5.71 / 4.20 / 4.21 / 5.70 |
| fixed-width | 106.65 / 57.22 / 53.38 / 115.60 | 106.26 / 57.51 / 53.26 / 117.21 |
| optional-prefix | 126.19 / 55.87 / 50.14 / 137.72 | 115.07 / 57.04 / 50.05 / 137.69 |
| shadowed-family | 102.32 / 58.11 / 49.55 / 152.44 | 97.57 / 52.43 / 49.55 / 152.95 |

Fresh generated predecoded costs, controlling runtime11:

| Case | R11/G10 us | R11/G11 us | Median paired change |
| --- | ---: | ---: | ---: |
| literal-late | 4.230 | 4.215 | -0.25% |
| fixed-width | 53.215 | 53.259 | -0.20% |
| optional-prefix | 50.007 | 50.050 | +0.12% |
| shadowed-family | 49.775 | 49.547 | -0.89% |

SDK11's emitted `foo|foobar` runner removes an empty-alternative verification
loop and captures the `foo` found by search directly. That smaller source does
not demonstrate a useful public Count gain here. The other three generated
bodies differ only in generator-version annotations. No owned-engine rewrite or
upstream port is justified by these results.

## Limits and decision

All840 samples exceed50ms; minimum55.865ms. Several rows have substantial
outliers: worst spread66.9% in R11/G10 generated shadowed-family,37.5% in its R10
counterpart, and up to62.8% in unchanged Core controls. Raw samples remain intact.
Near-zero generator changes are not an equivalence or win claim. No repeat or
confirmation follows this closed, bounded comparison.

Literal Core also changes materially between the two R11 harnesses despite
identical product bytes, including about-16.6% paired unlimited cost. This warns
against assigning every shift to runtime/JIT or generator changes. TFM/compiler,
calling context and process/layout sensitivity are not isolated further here.
No universal .NET11 speedup is claimed.

The comparison supports four scoped ordinary/compiled/generated comparator lanes
with reproducible identities and results. It does not refresh the full catalog,
qualify other operations/profiles, establish host containment, or replace any
immutable package/platform handoff. Next investigate controlled fixed-width Count
using its existing prepared SIMD program under the original invocation; preserve
all controls and prove eligibility before choosing an implementation.

The subsequent bounded source pilot reuses the admitted checked AVX2 counter
under the original finite invocation; its separate before/after receipt is
artifacts/finite-fixedwidth-count-20261016/RESULT.md. The rows above remain the
original frozen Core59 measurements and do not qualify that later source.
