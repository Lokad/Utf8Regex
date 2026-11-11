# dev.71: shared capture-state and POSIX history improvements

Frozen producer `2c5e341619f878874c45d12002c3f69f76dfb27c`; all four profiles at
0.5.0-dev.71, .NET10/11, exact Core pins. Windows and Linux each pass111960
detached checks across ten fixtures. Linux consumes the Windows-produced bytes
without rebuilding or repacking. Original copied Awk/Lython checks pass19/118 per
target/OS, including public API checks and final shared-root loans of zero. This
establishes compatibility, not downstream adoption or POSIX consumer acceptance.
Packages and symbols in artifacts/nuget are immutable; nothing is published.

One complete quiet-VM refresh passed **46m45s** (2804.520s/3600s), all85 existing
steps: one Release build, both-runtime correctness/disabled dynamic code, four
catalogs, paired comparators, lifecycle/scaling, resource checks and verification.
The raw archive is verified off the VM. Frozen-source CI43063326325 is green on
Windows/Linux. Nine matching-source snapshot checks per target/OS pass. The
[Full receipt](full-refresh.json) and [acceptance](acceptance.json) preserve source,
phase, package/restore/TRX and offVM evidence. All420 earlier canonical packages
and symbols remain unchanged. A completed Full does not qualify every timing or
scaling fit.

This batch directly lowers parser-owned ASCII bracket operands, skips verified
shared POSIX history prefixes and proves fixed parent bounds, retains cheap
capture-opening repeat fusion, and lets eligible Python Count omit unobserved
capture state. No new engine, retained eligibility metadata or public policy was
added. Scalar semantics, input policy, captures, original interruption, admission
and result/cursor lifetimes remain. Causal evidence and costs are in the
[bracket lowering](../PythonAsciiBracketUnions-20261111/README.md),
[shared prefix](../PosixSharedPrefix-20261111/README.md),
[parent bound](../PosixParentBound-20261111/README.md),
[bounded capture proof](../BoundedCaptureProof-20261111/README.md) and
[Count capture state](../PythonCountCaptureState-20261111/README.md) receipts.

Ordinary catalogs use **Validate**. Ratios are managed/reference time; lower is
better. These separate Full observations include comparator movement and are not
causal A/B results. The fixed dev.66 imported baseline remains `7b0cc1c3`.

| Public operation | dev.70 → dev.71 ratio | Current managed cost |
|---|---:|---:|
| Core bounded English repeat, compiled reference | 0.602 → 0.606 | 1124.124µs |
| Core name-family Count, compiled reference | 0.559 → 0.539 | 1626.783µs |
| Core river-window Count, compiled reference | 1.416 → 1.405 | 2219.959µs |
| Python many-capture strings | 2.242 → 2.275 | 800.508µs |
| Python one-capture strings | 2.499 → 2.452 | 525.872µs |
| Python captured split | 1.636 → 1.667 | 426.227µs |
| POSIX SKU,4096 calls | 4.021 → 4.064 | 2791.631µs |

Core has122 wins/52 gaps/4 tie candidates. Python has48 managed-faster,31 CPython-
faster,5 inconclusive,1 equivalent and5 unqualified rows. PCRE2 has81 managed-
faster,8 native-faster,11 inconclusive,1 equivalent and25 excluded rows. The eight
comparable POSIX observations retain6 native-faster/1 equivalent/1 managed-faster;
eight history rows remain excluded from native capture equivalence.

Warm Python output remains over2×. Strict Core email2.584×, Chinese literal3.445×,
river1.405× and AWSquick1.038× remain gaps. AssumeValid has a distinct input contract
and stays separately labeled. SKU remains NativeFaster, about0.682µs per call;
general unresolved-parent histories still need architectural work. Preserve
exclusions, lifecycle uncertainty and rejected scaling fits.

Keep the causal costs too: Count's one-capture literal-prefix case is12.67/14.42%
slower despite seven settled14.1–28.5% gains and reduced allocation. Bounded capture
proof confirmation retains overlap17–18% and folding8–10% higher medians with
overlapping ranges. POSIX parent bounds retain strict variable-tail7.34% and
trusted previous-history3.86% disjoint costs. Older category/ceiling/PCRE controls
remain in their receipts; a new Full does not cancel those observations.

Raw outputs are in artifacts/benchmark-refresh/current. The immutable Full archive
is under artifacts/experiments/delivery-dev71-20261111, SHA-256
`CA426C342F532CD28ED90EE3A4CF359838383289061F4744C951F882E98CE0B8`.
Linux raw archive SHA-256:
`9D01625A1A791522B8695BF4A83AA721C2EB929B60AEB7B45ABEE735F15DBBCE`.
Both evidence keepers and all qualification/snapshot jobs completed successfully.
Performance work remains active. Assess bounded synchronous POSIX state spans
from the current immutable-package allocation audit, keeping durable cursor
storage, conservative admission and the same interpreter. General P3/P4 gaps
remain; no parity-completion claim or repeated qualification follows this delivery.
