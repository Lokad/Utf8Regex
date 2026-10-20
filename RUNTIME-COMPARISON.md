# .NET 10 / .NET 11 Count comparison

The latest qualified Core/PythonRe dependency is [scalar.119](artifacts/core-python-scalar119-20261020/HANDOFF.md).
The newest versioned runtime experiment below uses frozen Core114. Its original
comparative-lane gate is complete; it does not qualify current119 performance
or complete runtime/profile behavior. [Current product/source costs](CURRENT-PERFORMANCE.md)
remain separate. No new comparator framework or repeated closed batch is needed.

## Current source-applicability decisions

The historical decision to inherit upstream backreference span equality through
BCL fallback is superseded by engine independence. The owned VM has separate
ordinal RTL/LTR byte loops; folded references walk complete scalars. A bounded
prototype reused the existing checked span comparer, retaining right-to-left
chunk order, original interruption and capture ranges without capture copying.

That prototype is **rejected and reverted**. Long public hits improved about
10–14% and supplementary references about 28–31%, but an unchanged unlimited
literal Count control repeatedly slowed 37% then 41% in the sole reversed-order
confirmation. The cause is unproved. This fails the declared public-cost gate;
no port or per-byte-to-chunk accounting change is accepted, and no further timing
follows. New boundary/capture/original-authority tests remain and pass against the
byte-identical restored baseline. [Both observations and rejected patch](bench/Experiments/OrdinalBackreference-20261020/README.md).

A separate owned-engine change now filters impossible candidate starts after a
mandatory initial RTL strict-end opcode. Focused scalar/capture/resource/original-
authority checks and one public FullOpts pair support85–87% lower long-miss Count
costs, with no measured Core regression above2.3%. [Evidence and scope](bench/Experiments/RtlStrictEndCount-20261020/README.md).
This implements an owned candidate proof; it does not transfer UTF16 length rules
or reopen the rejected ordinal-span comparison. Existing versioned comparator lanes need no replacement.
Frozen observations below retain their original product/runtime scope.

## Frozen immutable Core114, measured 2026-10-20

The actual qualified [Core/PythonRe114](artifacts/core-python-scalar114-20261020/HANDOFF.md)
Core DLL, producer01968d0f, is identical in every cell. [Numeric source](bench/Experiments/Runtime11-Scalar114-20261020/measurement.json),
[frozen fixture](bench/Experiments/Runtime11-Scalar114-20261020/README.md),
[full receipt](artifacts/runtime11-scalar114-comparison-20261020/RESULT.md).
Older110/59 observations below remain historical and are not reclassified.

SDK10.0.401/net10 runs on10.0.12 and11.0.0-rc.1.26425.128 with identical harness
IL. SDK11RC1/net11 on that runtime adds fresh TFM/compiler/generator effects.
Four real generated runners per SDK, with ordinary/compiled controls. ASCII
Count/range digests agree; dot/emoji scalar/code-unit disagreement stays excluded.
Installed preview-runtime evidence is not a runtime-upgrade recommendation.

CPU2/FullOpts/R2R-off, one fixed15-process batch267.51s. All840samples exceed65.453ms
against a50ms floor. No confirmation, retarget, package qualification, Status or
old-data rerun. These results are separate from [default-tier actual114 costs](artifacts/core-python-scalar114-20261020/vm-cost/RESULT.md)
and do not qualify complete runtime/profile semantic/resource behavior.

Median public Count us/op, ordinary Core, unlimited / finite / finite+live-token.
Work50M/depth1024/working64MiB/result128MiB are experiment allowances, not defaults.

| Case | Runtime10/SDK10 | Runtime11/SDK10 | Runtime11/SDK11 |
| --- | ---: | ---: | ---: |
| literal-late |4.921 /3.814 /3.841|3.948 /2.787 /2.801|3.792 /2.755 /2.804|
| fixed-width |7.830 /19.724 /19.330|8.530 /21.039 /20.729|7.365 /19.703 /19.800|
| optional-prefix |138.814 /539.587 /535.357|134.942 /1143.013 /1138.786|134.062 /1139.508 /1136.508|
| shadowed-family |114.704 /123.407 /123.021|116.167 /128.449 /125.534|116.934 /130.441 /126.094|

Same-harness runtime11 literal medians improve roughly20-27%, while finite
optional-prefix costs about2.1x runtime10 (paired median2.116x); the fresh SDK11
cell retains that gap. This identifies a runtime/JIT-path investigation, without
attributing a specific defect or removing metering. Generated optional-prefix
stays49us. At114, Core unlimited134-139us and finite0.54-1.14ms superseded
the110 observation for that product. Later source changes make these historical
costs unsuitable as current118/119 measurements or evidence of a current defect.

Some fixed-width/family controls vary (overall maxspread1.541x); small differences
and universal runtime/generator wins remain unqualified. Fresh SDK11 effects mix
TFM/compiler/harness/generator changes. All compiled-Core/BCL/generated /
NonBacktracking/decode/predecoded/allocation lanes and ratios stay in the numeric
source. Keep source FullOpts and actual-package default-PGO evidence separate.

## Historical Core110, measured 2026-10-19

All three cells load the same actual Core bytes. SDK10/net10 on runtime10.0.12
and the identical harness on11.0.0-rc.1.26425.128 isolate runtime/JIT effects.
SDK11.0.100-rc.1.26425.128/net11 on that runtime adds fresh TFM/compiler/generator
effects; ordinary/compiled controls prevent generator-only attribution. Four
real generated runners per SDK, no runtime Regex fallback. ASCII Count/range
contracts agree; dot/emoji scalar/code-unit disagreement is explicitly excluded.

CPU2/FullOpts/ReadyToRun-off; one fixed15-process comparison,271.89s process time.
All840 samples meet50ms floor (minimum64.7455ms). No confirmation, product retarget,
old59 remeasurement, snapshot/Status refresh or package qualification. These are
not the default-tier [current package costs](artifacts/core-python-scalar110-20261019/vm-cost/RESULT.md)
or full-runtime semantic/resource qualification. [All evidence/limitations](artifacts/runtime11-scalar110-comparison-20261019/RESULT.md).

Median public Count us/op, ordinary Core, unlimited / finite / finite+live-token.
Finite allowances are work50M/depth1024/working64MiB/result128MiB, not safe defaults.

| Case | Runtime10/SDK10 | Runtime11/SDK10 | Runtime11/SDK11 |
| --- | ---: | ---: | ---: |
| literal-late | 7.216 / 3.929 / 3.934 | 5.231 / 2.823 / 2.841 | 5.247 / 2.871 / 2.852 |
| fixed-width | 8.000 / 21.031 / 20.287 | 8.666 / 22.052 / 21.297 | 8.610 / 21.911 / 21.099 |
| optional-prefix | 1784.989 / 1377.266 / 1376.342 | 1786.446 / 1354.162 / 1338.471 | 1793.818 / 1356.086 / 1344.152 |
| shadowed-family | 117.541 / 135.219 / 133.182 | 118.147 / 130.984 / 128.062 | 120.179 / 132.865 / 130.421 |

Literal Count improves roughly27% on runtime11 with the same SDK10 IL; fixed-width
Core medians are higher with up to1.262x sample spread. In Core110, the optional-prefix
case cost1.35-1.79ms across cells against49-53us
generated predecoded .NET. This identified an engine-path issue at that time;
later plan-borrow work changed the path. Do not use this historical cost as114's
current cost or as a reason to remove metering. Fresh SDK11 generated rows show no
universal advantage;
no upstream optimizer port is justified by these timings alone. Every compiled
Core and BCL ordinary/compiled/generated/NonBacktracking decode/predecoded lane,
allocation and paired ratio remains in the numeric source.

## Historical Core59 comparison

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
Core/PythonRe110 was the corrected dependency when this page was last refreshed;
the current qualified dependency is118, linked above;114 remains the newest
frozen product in this versioned runtime comparison.

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
