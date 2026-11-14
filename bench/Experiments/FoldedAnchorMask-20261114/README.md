# Exact punctuation anchors in folded vector search

Accept Core `25fade98`, before `8b9b0a69`. Keep existing selected correlated
anchors, SIMD reads/loops and complete verification. Each ASCII letter anchor
uses0x20 case folding; other ASCII anchors use a zero mask for exact equality.
Wider anchors retain the prior fallback. This admits the real space-anchor
cases identified by the [current audit](../FoldedSearchCpu-20261114/README.md).
Delete the two obsolete private letter-only helpers. No new fields, allocation,
mode, ranking/threshold policy, unchecked reads or accounting mechanism.

Windows/Linux725 focused Release checks pertarget (.NET10/11) pass. Raw-byte
oracle covers borrowed starts/tails, both anchors, all256 byte values, exact
NUL/space and @/backtick distinctions, short spans and nonoverlap. Public calls
preserve scalar/byte/UTF16 results, strict malformed suffix, both input policies,
finite limits and original interruption/lifetimes. Existing suites remain.
Additional131 checks each pass with AVX2 disabled and all intrinsics disabled;
same public caller observes128-bit-only and scalar runtime capabilities and
checks all eight outputs. No alternate production mode or rebuild is added.

ONE public .NET11GA VM A/B, no confirmation. Exact staged distinct DLL/PDBs,
matching source documents and unchanged caller; separate ordinary processes,
internal affinity,1s warmup/lane,9 samples/250ms target/235ms minimum observed.
Both policies receive the same valid unchanged text and full Count outputs,
consumed after timing. Exact literal Count is the unchanged public control;
finite/live measures its original work10M/depth4096/working16MiB/result16MiB.

| Public Count | Strict before → after us | Trusted before → after us |
|---|---:|---:|
| letter-corpus | 153.864 → 153.164 (-0.45%) | 120.675 → 119.466 (-1.00%) |
| space-corpus | 527.222 → 140.418 (-73.37%) | 492.147 → 104.728 (-78.72%) |
| dense-space | 70.345 → 48.660 (-30.83%) | 70.004 → 47.970 (-31.47%) |
| family-corpus | 174.057 → 176.565 (+1.44%) | 136.241 → 136.830 (+0.43%) |
| john-corpus | 601.525 → 96.611 (-83.94%) | 565.103 → 63.819 (-88.71%) |
| finite-live | 1052.110 → 1058.284 (+0.59%) | 1023.749 → 1025.015 (+0.12%) |
| unchanged-exact | 65.248 → 65.310 (+0.09%) | 32.077 → 31.878 (-0.62%) |
| dense-letters | 52.456 → 53.161 (+1.34%) | 51.335 → 52.369 (+2.01%) |

Accept the large punctuation gain. Retain dense-letter +1.34/+2.01%, family
+1.44/+0.43% and finite/live +0.59/+0.12%. Exact control +0.09/-0.62%.
Allocations unchanged:0B except112B finite/live. No normalization, composed
speedup or new .NET parity ratio. This does not erase the prior dispatch's
strict-family regression or claim parity on all patterns.

Raw SHA256 `FEFD8F945F0CE6388BC44942763C05CE595EDE2A0998809D0EAB7CCAF0660FB2`, verified offVM before keeper49967 terminal0.
Local raw: `artifacts/experiments/core-folded-anchor-mask-20261114/`.
[Acceptance](acceptance.json) retains medians, ranges, source/images and width
observations. Comparison closed; no more timing/tuning for this candidate.
No package or Full refresh here; all540 immutable assets remain unchanged.
Batch these accepted Core changes at the next common dependency checkpoint.
