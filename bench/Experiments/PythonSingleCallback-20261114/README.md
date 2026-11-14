# One-replacement callback projection

Source `462bd67d` reuses existing final-result projection when PythonRe callback
substitution has `count: 1`. Project through every participating capture end,
including lookarounds; do not map the unused suffix. No new API, owner, execution
mode or unchecked access. Other counts keep their shared map. Complete tail
output and assembled raw-byte callback UTF-8 validation remain required.

All5159 PythonRe Release checks pass pertarget on Windows;121 focused checks
pertarget on Linux.19 new checks cover all three APIs, count0/1/2, misses,
lookahead coordinates/LastIndex, retained values, original token/deadline and
invalid raw-byte output. The original262159-byte input failed all three callback
APIs at their1MiB working limit, requesting1066048 bytes. The same policies now
admit identical complete outputs and retained captures. Pattern/template checks
and useful admission remain; numerical work/working demands can decrease.

ONE public before/after comparison on the quiet VM, .NET11.0.0: valid unchanged
UTF8, same pattern/options/counts/limits and requested output. Prepared constant
callbacks remain inside timing; full rich capture replay and result consumption
are outside timing. Separate ordinary producers reuse matching Core and caller
DLL/PDB pairs, with clean changing Python/source identities verified.9 samples,
250ms target, every sample>=20ms; no confirmation or further trials.

| Public operation | Before → after us | Before → after allocated bytes/call | Template control movement |
|---|---:|---:|---:|
| text/early-unicode | 1151.890 → 505.250 | 1066811 → 525943 | +1.09% |
| bytes/early-unicode | 1310.813 → 715.368 | 1066789 → 525885 | +6.59% |
| text/finite-live | 1126.382 → 515.236 | 1066899 → 526012 | +3.38% |
| text/late-unicode | 134.179 → 145.374 | 200160 → 200160 | +7.54% |
| text/bulk-control | 213.790 → 213.758 | 81432 → 81432 | -1.51% |

Early/finite callback ranges separate with large allocation savings. Retain the
late-match+8.34% observation and its+7.54% template-control movement; their
allocations are unchanged. The unlimited callback neighbor is effectively stable.
Controls span−1.51..+7.54%; selected processors differ (CPU3/CPU2). These limit
exact causal percentages; do not normalize controls or claim blanket equivalence.
Allocation observations include managed runtime noise. This is a producer
comparison, not a fresh CPython ratio or a composed cumulative gain.

All532 staged package/symbol assets preserve hashes. No package/platform/Full
refresh or publication. This source change is NOT in frozen dev84 packages or
their catalog. [Acceptance](acceptance.json). Raw verified offVM, keeper terminal0;
SHA256 `31F40DF64152D543D4978B40A35FF18EA1210E3996580BEC1D4B28429D5B7EF7`.
Raw: `artifacts/experiments/python-single-callback-20261114/`.
