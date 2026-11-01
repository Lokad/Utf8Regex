# Read final captures directly during template replacement

Core template expansion previously materialized complete capture histories, then
read only each history's final element. It now reads the existing final capture
slots, normalized for RTL. The existing `RequiresContext` distinction requests
histories for evaluator callbacks. No new framework, mode, result class or engine.

The runner still maintains/restores captures for backreferences, balancing and
assertions. Both backtracking and NFA paths fill final slots independently of
history projection. Templates preserve group-zero, sparse/duplicate numbers,
empty/nonparticipating captures and existing `$+` behavior. Callbacks retain their
full profile history, coordinate projection and lifetimes.

One91.794-second public A/B,16cases/four equal-result lanes, dedicated VM/CPU2/
.NET11RC3. Frozen `4dd30757` versus that source plus the archived patch; identical
typed lane IL in isolated contexts. Actual-loop1s/minimum1000call warmup,
independent60ms calibration, five alternating samples of at least56.178ms. Complete
outputs are consumed after timing. BCL predecoded/decode-then byte-output lanes
include required UTF-8 encoding; text results remain text.

| Public replacement | Before us | After us | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Grouped Unicode template |161.440|124.148|−23.10%|73928/24776|
| Finite limits/live token |205.946|150.327|−27.01%|74048/24896|
| Deadline/live token |264.948|187.620|−29.19%|74096/24944|
| RTL |168.823|132.093|−21.76%|78104/28952|
| NonBacktracking |67.173|51.698|−23.04%|70920/38152|
| Evaluator callback control |326.959|325.630|−0.41%|156448/156448|
| NB evaluator control |176.956|182.407|+3.08%|152416/152416|

[All costs, allocations, spreads and paired observations](summary.json).
Other template shapes improve18–29%, with fewer allocations. Maximum adverse
control median3.42%, pair5.21%, candidate spread1.038x. No confirmation or variants.
Parent gains do not replace the fixed-original cumulative gate or refresh catalog
Status. Immutable packages and snapshots remain unchanged.

Full Core41182 and PythonRe4530 Release checks pass per runtime on.NET10/11.
New public cases cover final restored values, sparse/duplicate names, balancing,
backreferences, assertions, empty captures, RTL/NB and unchanged evaluator
histories. Existing cutoffs, original-token/deadline, shared/funded admission,
retention/alias, malformed-tail and output-buffer checks remain.

The [resource contract](../../../docs/SPEC.md#cancellation-deadlines-and-resource-limits)
records intentionally reduced work and working/result capacity charges from
avoiding unused snapshots. Execution/output metering and admission stay active;
this does not remove history required by matching or callbacks. The initial
temporary driver compile failure was corrected before timing; its original log
and frozen final cases remain in the archive.

Immutable artifacts under `artifacts/experiments/core-template-final-captures-20261101/`:

- `evidence.tar.gz`: SHA256 `4D0EE66423DB2C4DF9BE2B7EA328316883886ACB544E96C775D1747AF45A98E9`.
- `correctness.zip`: SHA256 `8BAEF1932ABF8086B23CFEA2ABF027BE8FF6079AAB83A08484664165CB184908`.

They retain DLLs/PDBs, identities, source/applied patch, driver, preflight,
predeclaration, raw samples, source tests and documentation/correctness logs.
