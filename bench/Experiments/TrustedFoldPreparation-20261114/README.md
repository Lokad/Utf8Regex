# Trusted folded Count preparation — corrected attribution

Historical patch70eaf3d2 is superseded by [mixed-text dispatch](../FoldedMixedDispatch-20261114/README.md), which removes the remaining special preparation branch. Measurements below retain their original scope.

Keep source `70eaf3d2` as a small preparation cleanup. Remove DescribeKelvinInput;
trusted nonempty unconstrained folded-literal Count needs no coordinate/ASCII
description after the Kelvin fence. Family/boundary/trailing consumers retain
their metadata. Strict validation, Unicode/Kelvin behavior and controlled routes
remain. No new framework, engine, retained state or API.

**No mixed-text public speedup is established.** The initial source attribution
missed an earlier selector: single invariant folded literals on mixed subjects
go to the owned VM before reaching this preparation. The family case is already
admitted. The diagnostic confirms the selected kinds and earlier diversion;
it is not a throughput measurement or global unreachability proof. Dense ASCII
does reach this lane, where omitted description is another ASCII scan.

ONE public .NET11 VM A/B, no confirmation. Separate exact staged Core DLL/PDBs,
matching sources and unchanged caller, equal valid unchanged text/options/full
Count outputs.9 samples/250ms target,1s warmup/lane; strict and trusted calls
compared with their own baselines. Unchanged route controls retain movement:

| Public Count | Strict movement | Trusted movement |
|---|---:|---:|
| english-corpus | +2.68% | +2.64% |
| mixed-text | +0.43% | +0.72% |
| dense-ascii | -0.98% | -2.04% |
| unaffected-family | -1.24% | -3.63% |
| unaffected-no-k | -1.13% | -1.41% |
| unaffected-finite-live | +4.57% | +5.04% |
| unaffected-kelvin | +0.93% | +0.21% |

Allocations unchanged (0B except112B finite/live). Retain English +2.68/+2.64%,
dense -0.98/-2.04%, finite/live +4.57/+5.04%. No normalization, causal attribution,
new parity ratio or claim that the strict English gap closes. Keep the cleanup
for explicit requested preparation; the next material investigation is the
earlier dispatch exclusion. This comparison is closed, with no more timing.

512 focused Release checks/target on Windows/Linux pass: aliases, culture,
borrowed ranges, strict malformed suffix, valid-text policies and original work/
cancellation/deadline authority. Python's direct Core Count delegation admits
only exact literals, so no profile benchmark matrix was repeated.

Raw verified offVM before keeper54593 terminal0. SHA256
`D37169B77C53B0B33B622DA6422DAFA92EC6EA017F7F9ECAA74DF120C07AF42B`. Local raw under
`artifacts/experiments/core-trusted-fold-count-20261114/`;
[acceptance](acceptance.json) retains medians/ranges and route observations.
No package or Full refresh; immutable dev85 remains the qualified dependency.
