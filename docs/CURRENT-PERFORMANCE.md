# Performance evidence

**Development: 0.4.0-rc.3-dev.** No current package or catalog-wide timing claim
is made. Snapshots record the versions they measured; later source correctness
does not refresh their Status.

## Release and current-source decisions

| Evidence | What it establishes | Remaining limit |
| --- | --- | --- |
| [Frozen RC2](../bench/Experiments/ReleaseCandidate2-20261026/README.md), [cost result](../bench/Experiments/ReleaseCandidate2-20261026/RESULT.md), [erratum](../bench/Experiments/ReleaseCandidate2-20261026/ERRATA.md) | Exact three-package identities and Windows/Linux correctness/consumer checks; unchanged measured allocations | Cumulative <=10% cost acceptance is **inconclusive**. The reduced-lane confirmation changed warm context. Candidate is unpublished and development fixes are not in its bytes. |
| [PythonRe defaults](../bench/Experiments/FortiethReview-20261027/README.md) | Current public default/miss correction; allocations unchanged on nine measured lanes | Tiny unlimited miss was +11.00%, then -14.87% in the sole confirmation, with noise/processor differences. No settled complete cost acceptance or further timing is claimed. |
| [Rejected FullMatch sharing](../bench/Experiments/ThirtyNinthReview-20261027/README.md) | Public costs rejected the prototype; production preparation was restored | Claimed prototype storage savings are not delivered. No new timing campaign is open. |

RC2 compared baseline 844dcbad with its actual DLLs on .NET 10.0.12:
one 109.138-second comparison and one 23.568-second confirmation.
Fifteen public rows initially ranged -18.79% to +4.99%; finite Python Count
was +10.38%, then +0.06%. Both observations remain; the confirmation does not
discharge the uncertainty rule.

## Historical measurements

These receipts preserve useful scoped observations without qualifying today's
engine or replacing a fresh release decision.

| Receipt | Scope |
| --- | --- |
| [Scalar.119 delivery](../bench/Experiments/Scalar119Delivery-20261020/README.md) | Actual Core/PythonRe package costs, including all four finite limits, token/deadline lanes and retained materialized output |
| [Python literal Search](../bench/Experiments/PythonReLiteralFour119-20261020/README.md) | Four qualified public CPython rows; Cyrillic miss is a lead, not an attributed removable-validation cost |
| [PCRE2 preparation](../bench/Experiments/ThirtySixthReview-20261026/README.md) | Smaller constructor allocation; noisy replacement costs do not establish cumulative acceptance |
| [Runtime comparison](RUNTIME-COMPARISON.md) | Frozen Core114 on the recorded .NET 10/11 toolchains |
| [Capture assessment](CAPTURE-COST.md) | Frozen scalar.33 ordered-choice call, not a POSIX consumer measurement |
| [Other receipts](../bench/Experiments) | Exact inputs, raw samples, decisions and uncertainty; rejected prototypes remain rejected |

Finite controls can select different algorithms and carry substantial public
costs. Unlimited/finite ratios are not isolated accounting overhead.
The source-of-truth snapshots are
[README.Benchmarks.json](../README.Benchmarks.json),
[PythonRe.Benchmarks.json](../PythonRe.Benchmarks.json) and
[PCRE2.Benchmarks.json](../PCRE2.Benchmarks.json).

Use warm, result-compatible public operations with unchanged controls and explicit
binary/runtime identities. Separate isolated-kernel attribution from operation
cost. Keep bounded experiments and batch package/platform qualification at a
delivery boundary. The checked-access campaign stays closed; documentation work
does not justify new timing or rewritten historical evidence.
