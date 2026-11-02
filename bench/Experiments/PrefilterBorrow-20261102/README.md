# Borrow immutable required-literal descriptors

**Accepted.** Two existing Core rejection helpers borrow the primary searcher
from the owned immutable plan. Two internal readonly accessors expose its existing
storage; the search/rejection algorithm, argument checks, exact work/checkpoints,
captures and funding remain unchanged. No descriptor, owner, mode or unchecked
operation was added. References stay within the synchronous helper call. Core's
readonly program field retains the backing plan; managed interior references and
existing active-operation funding preserve its lifetime through callbacks/GC.

Before the edit, saved CPU-run code and fresh public-route code both showed an
824-byte prefilter copy followed by a 408-byte searcher copy before the kind check.
Both delivery controls actually have ExactLiteral screens, so merely returning
earlier for None would not remove their copies. Actual SizeOf values and normalized
field type/order/readonly sequences match before/after: searcher408, substring40,
prefilter824, searchplan2720. No manual size estimates or new metadata allocation.

Fresh .NET 11 RC3 optimized code from the public nullable Search route removes
those copies: helper local frame **1288 -> 56 bytes**, code **295 -> 166 bytes**.
The C# compiler checks ref lifetimes; readonly backing fields keep their declaration
positions. This is unrelated to the closed 20-byte VM instruction-reference trial.
The prior offline audit is preserved separately as raw `7BDF0725` under
`artifacts/experiments/prefilter-copy-audit-20261102/`; it resampled no process.

One quiet-VM **18-case comparison took 84.415s**, minimum timed sample **47.792ms**,
under its 120-second cap. Original `a40bda03`, parent `0e24989f` and parent plus the
three-file patch load separately. Existing protocol: normal runtime startup then
CPU2, 500ms/minimum128-call warmup, independent60ms calibration, five alternating
samples, complete public outputs consumed after timing, allocations, equal-output
compiled Regex predecoded/decode-then diagnostics. The previous15 PythonRe cases
are retained, plus finite Core required-screen hit/miss and native Count controls.

| Public operation, first matrix | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| PythonRe nullable atomic Search |1.085|1.052|-3.03%|
| PythonRe suffixed FullMatch |36.193|36.653|+1.27%|
| PythonRe folded FullMatch |80.598|84.173|+4.44%|
| PythonRe lazy-short FullMatch control |0.627|0.582|-7.16%|
| Core finite required-screen hit |19.716|19.491|-1.14%|
| Core finite required-screen miss |0.199|0.145|-27.38%|
| Core unchanged native Count |0.104|0.105|+0.96%|

The first nullable result's before/after max/min spreads1.041/1.055 overlap the
predeclared5% useful-gain threshold. The **sole selected noise confirmation** kept
that operation and an unchanged Core Count control, using the same exact binaries
and unchanged driver/protocol, with a20-second cap: **9.809s**, minimum **40.402ms**.
It measured nullable-6.28% and control-0.45%. Before running it, the decision was
fixed to true medians of all10 initial+confirmation observations per lane, averaging
the middle two. Pooled nullable gain **5.65%** meets the primary criterion; control
is-0.15%. No faster-run substitution, variant or further confirmation. The selected
run has a different preceding workload context; separate raw results remain visible.

Every allocation median matches; the largest adverse current median is **4.44%**,
within the focused allowance. Core's miss gain alone was not the acceptance target.
This is **not cumulative release acceptance**: first-matrix original folded+12.22%
and suffix+12.86% remain adverse; pooled nullable original+10.047% still narrowly
exceeds10%. Do not round that into a pass or replay this pilot for a better result.

Release checks pass both runtimes: complete Core **41229/runtime**, complete
PythonRe **4600/runtime**, affected PCRE2 **276/runtime**. All benchmark consumers
build for both targets in18.39s. Existing tests cover scalar/UTF-8 boundaries,
capture/result/cursor aliases, original cancellation/deadlines, exact finite
cutoffs, numeric-before-host admission, refused funding and final refunds.
No new executor/test framework or package/catalog/platform matrix was introduced.

Raw driver/protocol, three DLL/PDB sets and hashes, both comparisons, codegen,
actual-size/field-order proof, complete patch and validation logs:
`artifacts/experiments/prefilter-borrow-20261102/evidence.tar.gz`, SHA256
`E57C922370C6FB190F64A9DFB68E09E040E9C55A287F7BCA46637240AF9FAD42`.
`correctness-source.zip`: SHA256
`46C9384F4040642CC087D46FA31F48428756137C771DFD5222564EDB46B00C1B`.
