# Exact vector counting in the validator selector

The validity-only cost selector still counts E0/ED/F0+ bytes in the first256
bytes and selects BCL validation at eight leads. Checked Vector256 reads,
comparisons and popcount now count complete32-byte blocks; the original scalar
tail/fallback remains. Validation algorithms, invalid offsets, invocation work,
interruption, admission and allocations are unchanged. No new mode/helper/API.
This differs from the rejected span-search skip in ValidationProbe-20261101.

The actual Python `unicode-literal/search-miss` uses validity-only validation.
Its837-byte subject hash05D2E466 matches the frozen catalog. A delegate probe
confirms unchanged choices for this case, common/constrained three-byte and
four-byte subjects. Separate no-tiering Release listings show vector comparisons,
mask extraction/popcount and a bounded scalar tail, with no hot per-load bounds
check. Code size145 ->177bytes is diagnostic, not a throughput claim.

ONE [public comparison](cost-summary.json), **81.929043374s**, separate
Default-process ABBA,16workloads/320samples/minimum66.2929ms. Exact frozen
source27faafbb plus patch, CoreB7A109B5 ->592B3935, identical PythonF49889DF,
PCRE2B31FE792 and caller. Full hashes/settings were [bound before timing](comparison-declaration.json).
750ms/min32 warmup, independent75ms targets, five samples and one calibration
confirmation before sampling. Complete result batches are checked after timing.
No repeat comparison, timed comparator lane or package/catalog refresh.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual Python Unicode miss |1.00|0.31|-69.35%|0/0|
| Same finite/live |1.29|1.32|+2.56%|112/112|
| Same deadline/live |1.38|1.42|+3.33%|160/160|
| Long Python Unicode miss |4.51|4.03|-10.60%|0/0|
| Previously adverse common-three-byte IsMatch |0.56|0.41|-26.36%|0/0|
| Two-byte IsMatch |0.84|0.36|-57.07%|0/0|
| Constrained three-byte IsMatch |1.60|1.59|-0.90%|0/0|
| Four-byte IsMatch |0.67|0.65|-3.61%|0/0|
| Mixed-width IsMatch |1.92|1.89|-1.75%|0/0|
| Core Unicode boolean |0.70|0.77|+9.51%|0/0|

**Promote with the uncertainty retained.** Actual-miss baseline process medians
are1.3660/0.6302us versus candidate0.3058/0.3022us: baseline spreads are large,
but both candidate processes are faster with disjoint sample ranges. The pooled
69.35% is this caller's observation, not a noise-free general estimate. Long
miss, common-three-byte, two-byte, four-byte and mixed-width gains also persist
with disjoint ranges. No repeat/confirmation is needed to decide those gains.

Finite/deadline+2.56%/+3.33%, Core Unicode boolean+9.51%, PCRE boolean+1.57%,
Chinese metadata Count+2.05% and other control ranges interleave. Core boolean
processes are before0.7693/0.6277us, after0.7096/0.8184us; preserve the adverse
median without claiming a stable causal regression. Short ASCII, hit and native
controls are not normalized or credited. Allocations are unchanged everywhere.
The Full48 catalog's0.53us Python miss remains separate; no new .NET/CPython ratio
or qualification Status is produced from this custom caller.

Focused Release checks pass per runtime on both Windows/Linux: Core384,
PythonRe137, PCRE274. Seven new cases cover every byte class, seven/eight leads,
slices, vector/tail/window boundaries, mixed tags and malformed original offsets.
Existing tests retain resource/cancellation/deadline authority across profiles.
Matching Windows binaries with DOTNET_EnableHWIntrinsic=0 add7/runtime to check
the original scalar fallback. No complete solution/package matrix repeated.

Canonical off-VM raw:
`artifacts/experiments/vector-validation-selector-20261107/evidence.tar.gz`,
SHA-256 `5A7F46F89FADE742AD05002818F0A04B964065A554F42869969826A53B4015FB`,4611000B.
It includes exact graphs/symbols, source/test patch, caller, all logs/samples and
codegen. Immutable dev.48 and earlier staged/published assets remain untouched.
The broader parity/delivery plan stays active.
