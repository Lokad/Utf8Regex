# Bound redundant second-needle scanning

Source `0674459f`, frozen baseline `589d89df`. The existing raw exact-pair
candidate helper now limits its second checked span search to starts earlier
than the first needle's known hit. The span includes the complete longer needle;
miss windows retain their original 2048-byte envelope. Ordered verification,
scalar execution, every work checkpoint, cancellation/deadlines and allocation
behavior are unchanged. No new matcher, payload, switch or accounting contract.

[Route diagnosis](diagnosis.txt) confirms the actual 594930-byte Holmes/Watson
corpus uses scalar Count and returns seven matches. Its 542-candidate replay
and phase timings are diagnostic, not additive costs or a public speedup.

One [public Count comparison](comparison.json), **63.978 seconds**, Ubuntu quiet
VM/.NET 11 RC3/CPU2. Ten cases, equal-result producer before/after, matching
predecoded/decode .NET lanes; warm 1s/min64, independent 60ms calibration,
five alternating samples, minimum **37.427ms**. Result batches consumed after
timing. [Driver](driver.cs.txt), [project](driver.csproj.txt), [summary](summary.json).

| Public operation | Before us | After us | Median change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual corpus, ordinary | 425.951 | 408.192 | -4.17% | 3552 |
| Actual corpus, compiled | 258.917 | 242.628 | -6.29% | 3552 |
| Finite limits/live token | 504.256 | 492.141 | -2.40% | 3672 |
| Deadline/live token | 334.406 | 317.762 | -4.98% | 3720 |
| First literal only | 57.144 | 48.725 | -14.73% | 3552 |
| Second literal only | 58.931 | 58.547 | -0.65% | 3552 |
| Dense mixed-scalar hits | 423.615 | 428.967 | +1.26% | 3552 |
| Unequal needles | 24.662 | 23.059 | -6.50% | 3552 |
| Larger-family control | 66.001 | 66.290 | +0.44% | 3568 |
| Literal control | 0.411 | 0.412 | +0.16% | 0 |

Absolute corpus timings have substantial bimodality (spreads up to 1.73).
Every ordinary paired sample improves 3.97–5.71%, compiled 4.09–6.42%, finite
1.87–4.41%. This supports the bounded change; independent medians do not prove
a precise effect size. Dense-hit paired cost rises 1.09–1.41%. No confirmation
was needed; unchanged controls do not establish gains. No global cost/parity claim.

One [clean-source selective refresh](selective-refresh.txt) qualifies compiled
**4.634x -> 4.455x**, still Gap, and ordinary **0.980x -> 0.906x**, Win;
263.745/246.070us, 3552B. Comparator movement is separate from the A/B evidence.
All unselected [rows and identities](snapshot-diff.txt) are unchanged; generated
README ordering and parity verification pass. No full catalog/package matrix.

[Release checks](verification.txt) pass on both runtimes: complete Core41013 and
PythonRe4387/runtime; focused PCRE2 97/runtime; snapshot/API4/runtime. New
boundary witnesses require an earlier long needle extending past a later short
one, including sliced storage, truncated tails and window edges. Existing
work-cutoff, sticky-denial, caller-loan, original-token and deadline tests pass.
No public contract/documentation changes are needed.

The initial refresh launch accidentally requested 200000 Core iterations (the
PCRE2 command uses different units). It was stopped through its verified owned
timeout process before publishing a snapshot. [Failure receipt](refresh-mistyped-iterations.txt)
is retained. The corrected five-iteration adaptive refresh ran once; this setup
error is neither timing evidence nor a confirmation of the A/B.

Immutable archives under `artifacts/experiments/exact-pair-prefix-20261101/`:

- `evidence.tar.gz`: before/after DLL/PDBs, source bundle/patch, driver, raw
  samples, clean Linux build, selective refresh and preserved prior generated
  PCRE2 files. SHA256 `D71F54075DB567139C64E2DBFE336FB6BCD6857AC64F2E204CB882EDC46509A6`.
- `correctness-source.zip`: full/focused tests, snapshot guards, source patch,
  identities and initial route diagnosis. SHA256
  `E8073F63EA1A5EBEBB4F39CB575007513FECFC04121E50155C33665836D80296`.

The fixed cumulative baseline remains `a40bda03`. Larger explicit deferrals and
final current-source full-refresh/package/consumer gates remain open. Core's
next qualified case is common/backtracking4.536x, then this residual window4.455x;
further window work needs a distinct cause, not another timing campaign.
