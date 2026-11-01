# Native template enumeration: remove duplicate overload dispatch

Frozen baseline `c6b716ee`. The native-template worker already rejects finite
policies, finite timeouts and cancellable tokens. Its admitted simple-pattern
calls reach the no-token enumerator through the token overload's extra dispatch.
Call that same public no-token enumerator directly. Initial scalar selection,
strict validation, discovery, capture maps, cursor lifetime and output admission
are unchanged. No new execution mode or metering exemption; controlled calls
retain their existing routes. No public contract or documentation change.

One [public comparison](comparison.json), **87.153 seconds**, quiet Ubuntu VM,
.NET 11 RC3, CPU2. Ten equal-result cases, producer before/after and predecoded/
decode-then-.NET lanes; warm 1.5s/min64, independent60ms calibration, five
alternating samples, minimum **48.061ms**. Result batches consumed after timing.
[Driver](driver.cs.txt), [summary](summary.json), [route coverage](preflight.txt).

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Warm text template | 5.451 | 4.658 | -14.54% | 400 |
| Warm native template | 5.508 | 4.712 | -14.45% | 400 |
| String result | 5.550 | 4.758 | -14.27% | 416 |
| Native destination | 5.602 | 4.682 | -16.42% | 344 |
| Compiled grouped | 5.466 | 4.664 | -14.68% | 400 |
| Cold32 native templates | 7.175 | 6.412 | -10.64% | 2520 |
| No match | 4.972 | 4.171 | -16.11% | 40 |
| Finite/live control | 2.436 | 2.563 | +5.21% | 3160 |
| Scalar Unicode control | 2.545 | 2.504 | -1.59%, noisy | 6744 |
| Unchanged Count | 0.425 | 0.425 | +0.04% | 0 |

Every warm grouped/no-match pair improves; finite/live paired changes are
+2.10..6.35%, within the allowance. Unicode lane spreads1.09/1.12 leave its
point change uncertain. No confirmation or selective catalog refresh: the
existing word-replacement catalog uses a different, groupless literal route.
The .NET byte-output comparators include encoding; destination uses Replace,
encoding and copy. Native templates are already strings for the BCL. These
comparators establish operating cost, not equivalence of resource policies.

[Focused Release checks](verification.txt) pass on both runtimes: Core6767,
PythonRe284 and PCRE2 1123. Existing tests cover grouped projection, all output
forms, aliases, strict malformed subjects, captures, controls and finite resource
policies. The earlier whole-wrapper prototype is fully reverted and unrelated.

The ~0.8us gain addresses the genuine historical warm-template debt on its exact
grouped workload. Separate historical/current point measurements cannot prove
the complete cumulative allowance; include this workload in the final fixed
`a40bda03` comparison. Do not claim all replacement gaps are closed or reuse this
as package/current-catalog qualification. No published/staged bytes changed.

Immutable `artifacts/experiments/template-enumerator-dispatch-20261101/` archives:

- `evidence.tar.gz`: exact DLL/PDBs, driver DLL, patch and raw comparison,
  SHA256 `6567294E80B1CD13FAE299676709B6C10F182F521370027D62434836ED9EC739`.
- `correctness-source.zip`: driver source, patch and complete focused logs,
  SHA256 `6B6A81A9E4A3EDD03B865D52B3D0CC5CCE16089585DA916095CDF87FD6F7ABFC`.
