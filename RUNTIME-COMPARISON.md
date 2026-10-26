# .NET 10 / .NET 11 Count comparison

The staged common dependency is [0.4.0-rc.1](bench/Experiments/ReleaseCandidate-20261020/release.json)
for all three packages. Development is 0.4.0-rc.2. The runtime experiment
below uses **frozen Core114**; it does not qualify either product's current
performance or complete runtime/profile behavior. Its comparative-lane gate is
closed. [Performance evidence and package costs](CURRENT-PERFORMANCE.md) have
separate identities. No old runtime batch is rerun for documentation cleanup.

## Preserved implementation decisions

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

The actual qualified **Core/PythonRe114**
Core DLL, producer01968d0f, is identical in every cell. [Numeric source](bench/Experiments/Runtime11-Scalar114-20261020/measurement.json),
[frozen fixture and protocol](bench/Experiments/Runtime11-Scalar114-20261020/README.md).
The detailed report `artifacts/runtime11-scalar114-comparison-20261020/RESULT.md`
is local-only and is not included in a source checkout.
Older110/59 observations below remain historical and are not reclassified.

SDK10.0.401/net10 runs on10.0.12 and11.0.0-rc.1.26425.128 with identical harness
IL. SDK11RC1/net11 on that runtime adds fresh TFM/compiler/generator effects.
Four real generated runners per SDK, with ordinary/compiled controls. ASCII
Count/range digests agree; dot/emoji scalar/code-unit disagreement stays excluded.
Installed preview-runtime evidence is not a runtime-upgrade recommendation.

CPU2/FullOpts/R2R-off, one fixed15-process batch267.51s. All840samples exceed65.453ms
against a50ms floor. No confirmation, retarget, package qualification, Status or
old-data rerun. These results are separate from default-tier actual114 costs
(local-only `artifacts/core-python-scalar114-20261020/vm-cost/RESULT.md`)
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

## Earlier runtime history

Core110, Core59 and earlier observations retain their original identities in
[repository history](https://github.com/Lokad/Utf8Regex/blob/d0fd579bc4eb3005a017ea8682b6a1e96f30a08a/RUNTIME-COMPARISON.md)
and the local gitignored PLAN.HISTORY.md. Original versioned driver/measurement
receipts remain unchanged. These are historical comparisons, not a current
runtime-upgrade recommendation, package qualification or benchmark Status.
