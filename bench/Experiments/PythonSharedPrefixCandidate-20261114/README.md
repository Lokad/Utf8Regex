# Prepared shared-prefix candidates

Source `c430228e` reuses the existing single-bucket shared prefix/discriminator
for checked complete-prefix span search in at most2048-byte windows. Verify the
discriminator and only the remaining unproved bytes. Prefixes longer than1024
bytes retain bounded confirmation beyond the searched part. The same complete
candidate set/earliest start escapes to Python's scalar verifier. No new cache,
descriptor, owner, mode, buffer or capture compiler.

[Current CPU evidence](../PythonOneCaptureCpu-20261114/README.md) prompted the
change; it is not a speedup forecast. Core303 focused and PythonRe5162 Release
checks pass pertarget on Windows/Linux, including bounded work cutoffs, original
token/deadline, prefix/window/Unicode-byte cuts, false prefixes and tails.
One new public finite-policy regression passes pertarget on Windows and is
included in ordinary source CI. Controlled frozen-pair.NET11 evidence: the old
65542-byte false-head input denies work2000 at requested2001; the same policy
now returns the complete retained capture. Admission stays; work units can fall.

ONE public VM A/B plus the sole permitted noise confirmation, unchanged frozen
Core producer pairs/Python/caller. No confirmation rebuild or source change.
PythonRe consumes valid unchanged UTF8; FindAllToStrings materializes all captures.
9 samples/250ms target, every sample>=20ms; independent complete output checking
outside measured batches. An unchanged public literal/string control accompanies
each operation. No kernel timing or new CPython ratio.

| Public operation | First before → after us | Confirmation before → after us | Confirmation control movement |
|---|---:|---:|---:|
| dense | 296.468 → 311.223 | 295.664 → 303.898 | +52.92% |
| dense-finite-live | 404.288 → 414.734 | 409.232 → 402.411 | -1.86% |
| false-heads | 622.409 → 3.709 | 653.357 → 3.619 | -1.46% |
| false-columns | 39.582 → 52.828 | 41.355 → 52.711 | -1.42% |
| long-prefix-columns | 131909.650 → 128383.900 | 137459.400 → 131210.400 | -1.25% |

Accept the large false-head gain while RETAINING dense+4.98%/+2.79%, finite
+2.58%/−1.67% and false-column+33.46%/+27.46% observations. Confirmation's
false-column control moves−1.42%, so that cost is a demonstrated tradeoff.
Allocations remain identical. This is not a dense-capture parity improvement.
The user's allowance for local regressions in exchange for major relative gains
applies; the checked-access10% gate is not a universal veto.

First late controls move+47%; confirmation's dense control also shifts within
samples (median+52.92%), while remaining confirmation controls move−1.25..−1.86%.
Keep all contexts/selected processor differences rather than normalize them or
claim exact causal percentages. No further timing, threshold tuning or package
qualification for this edit. The ~130ms long-prefix case remains unresolved;
its changed-path coverage is not established and it is not a claimed gain.

All532 staged package/symbol assets preserve hashes; no publication or adoption.
This source is NOT in frozen dev84 packages/catalogs. [Acceptance](acceptance.json).
Raw verified offVM before both keepers were released. Initial SHA256
`FBC3869BA4F98D1061157A5C6AB015517DA0603DD6433EEE4051011F6C3DFFB2`, sole confirmation `00EDABAFF8A5F1D195B2331E5759BC1EBD0A81BAAE6FD95EC76F6A566F836A47`.
Raw: `artifacts/experiments/python-shared-prefix-candidate-20261114/`.
