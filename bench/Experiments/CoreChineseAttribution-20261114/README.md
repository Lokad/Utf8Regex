# Current Chinese Count attribution

One bounded read-only diagnosis on frozen dev88 `b2ade220`, .NET11 GA, after
Windows/same-byte Linux consumer checks and offVM collection finished. Existing
`--measure-count-attribution-case` protocol, two existing cases,1000 calls/five
samples per lane,250ms/64-call minimum warmup,120-second outer cap. All returned
counts agree; all ten benchmark/producer DLL/PDB hashes remain unchanged.
[Observations and identities](attribution.json).

| Phase / public Count | Chinese literal us | Chinese family us |
|---|---:|---:|
| Validity only |47.160|47.299|
| Selected compiled search kernel |22.288|43.425|
| Validity then selected kernel |69.335|90.315|
| Public compiled Count |69.865|91.532|
| Predecoded compiled.NET |24.056|36.261|

Strict Core validity remains a substantial required cost. Public Count is close
to the separately measured validation-plus-search path; this supplies no unused
UTF16 coordinate or ASCII-classification removal. The literal uses existing span
sequence search; the family uses the existing packed-nibble SIMD strategy. Its
diagnostic replay reports212 verifications/207 matches; this is not a census of
executed production branches or evidence that excessive false candidates dominate.

These phase observations replace the old.NET11 RC3 diagnosis for prioritization,
not the canonical tables or their Status. Independently timed sums are attribution
ceilings, not achievable speedups. The shortest reference batches are below20ms;
no qualification, exact phase fraction, allocation delta or version gain is claimed.
Do not weaken Core's default validation, add a policy matrix or reopen rejected
validation/search fusion and mixed-width SIMD merely from this result.

Next inspect the higher absolute operating cost of Core email Count or Python
multi-capture progression, using a demonstrated live cause before another change.
No producer edits, rebuild, package/Full rerun, confirmation or table refresh.
Raw copied and verified offVM at
`artifacts/experiments/core-chinese-attribution-20261114/evidence.tar.gz`,
SHA256 `DD2BB19B32D4D0F9A97F1C3B28A27C100E710F524EEC8F8921B93DA612F975EF`.
Diagnostic SSH worker terminal0.
