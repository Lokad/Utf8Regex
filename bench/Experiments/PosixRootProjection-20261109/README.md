# POSIX root enclosure during projection

Accepted against frozen dev59 producer `d6120197`. Every winning trace has its
root opening first and closing last, including when that root is itself a public
capture. `GetCapture` therefore needs enclosure checks only for non-root parents:
their last repeated occurrence can exclude a child's earlier capture. Remove the
redundant root load/check; retain those inner-parent checks and all ranking,
histories, allocation, scalar validation and projection algorithms.

One **67.437-second** ordinary-process ABBA on the .NET11 RC3 quiet VM, within the
declared180s cap. The existing17-lane public worker is unchanged; Core and caller
DLLs are identical between versions, with one producer per process. Five samples
per process/row follow500ms warmup and independent calibration; all340 samples
exceed64ms. Full values and zero outstanding working loans are checked after
timing. No confirmation, catalog refresh or package qualification.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Empty captures | 1.51 | 1.49 | −1.4% |
| Empty captures, finite/live | 1.96 | 1.88 | −3.8% |
| Nullable captures, finite/live | 1.49 | 1.43 | −4.4% |
| Empty captures, funded/live | 2.11 | 2.07 | −1.9% |
| Nullable captures, funded/live | 1.63 | 1.59 | −2.5% |
| Empty captures, deadline/live | 2.27 | 2.20 | −3.0% |
| Absent capture | 0.67 | 0.65 | −3.6% |
| History length12 | 103.13 | 103.18 | +0.04% |
| SKU control,4096 calls | 5165.96 | 5178.87 | +0.25% |

Allocations are unchanged in all17 lanes; funded empty/nullable peaks remain
976/960B. Unchanged whole Count moves−1.8%, Core Count~0%; controls are not
normalizers. Small differences overlap sample ranges. This is a modest reduction
of redundant work, not a claim to close POSIX ambiguity or establish fresh native
ratios. [Summary](summary.json) retains individual process medians and ranges.

All174 POSIX Release checks pass per target on Windows/Linux, .NET10/11. Existing
independent public-root, nested last-iteration absence, empty/absent, supplementary
offset and capture-window cases exercise the changed projection. Existing original
cancellation/deadline, finite refusal, shared admission, cursor aliases and refunds
pass. Original invocation checks remain in every projected-slot loop, scalar scan
and final publication. Numerical work can decrease by one redundant root check per
participating descendant, consistent with the existing version-dependent contract.
No API/storage/accounting mechanism is added.

Raw protocol, source patch, matching graphs, Linux checks and public observations
are copied off the VM at
`artifacts/experiments/posix-root-projection-20261109/evidence.tar.gz`, SHA-256
`E7D2E6B6BDA0265F4861FA9D734D3585468430AC05301DC52BEE2D6E38EF8E98`.
Windows checks are alongside it. Dev59 packages and fresh snapshots remain
immutable; this is subsequent source work. Source CI is pending.
