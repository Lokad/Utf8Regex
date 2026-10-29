# Execute Python Unicode boundaries with the pinned scalar predicate

d458c0ec replaces parser-generated Unicode boundary lookarounds with the existing
owned boundary instruction. Its optional fixed scalar predicate uses the existing
category-table pool; builtin Unicode/ECMA/ASCII encodings remain unchanged. Python
word membership stays pinned to CPython 3.13.2 / Unicode 15.1, including numeric,
mark, joiner and newer-assignment distinctions. Python `\B` retains its
nonempty-subject guard. No public API, profile enum, execution mode or extra
program/cache was introduced.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; exact 715488c2 baseline
versus d458c0ec. One **61.32s** public A/B under a 120s cap, 14 workloads,
one-second warmup, independent calibration and three alternating samples. The
shortest timed sample was 46.92ms. Integers/Boolean results are consumed and checked;
successful Match/FullMatch results consume Success. .NET lanes are diagnostics,
excluded for pinned-category differences and unsupported scoped ASCII syntax.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Date IsMatch miss | 11.380 | 3.399 | 3568 / 3568 |
| Date IsMatch hit | 12.427 | 3.829 | 3568 / 3568 |
| Finite limits/live token | 14.541 | 3.770 | 21936 / 3680 |
| Dense ASCII candidates | 1946.838 | 411.993 | 3568 / 1696 |
| Mixed Unicode date hit | 19.080 | 4.679 | 3568 / 3568 |
| Post-pinned Unicode assignment edge | 2.006 | 1.149 | 3568 / 3568 |
| Python numeric word-member edge | 6.946 | 1.780 | 3568 / 1696 |
| ASCII boundary Count control | 220.396 | 230.300 | 0 / 0 |
| Class/capture control | 0.796 | 0.850 | 3872 / 3872 |
| Exact literal control | 0.212 | 0.211 | 0 / 0 |
| Successful date Match | 1.461 | 1.030 | 3568 / 3568 |
| Successful date FullMatch | 1.467 | 1.039 | 3568 / 3568 |
| Finite Core boundary Count control | 187.389 | 196.323 | 3664 / 3664 |
| Finite Core NonBacktracking Count control | 267.564 | 274.848 | 26336 / 26336 |

Controls vary -0.3% to +6.9%, with unchanged allocations; no equivalence claim.
The finite Core controls force VM boundary execution. No confirmation was needed.
a40bda03 remains the fixed cumulative release baseline.

The selected [raw refresh](date-refresh.txt) and [paired qualification](date-qualification.txt)
give **3.909x CPython [3.871,3.933]**, previously 13.14x. Qualified managed median
is 3.395us, CPython 0.870us, allocation 3568 B, CPU7. Verdict remains **CPython
faster**. Order/spread/harness gates pass. The independent raw managed median is
5.008us and stays separate; diagnostic .NET decode samples are noisy. No rerun
was used to reconcile those independent observations. Only common/date-miss
changed; catalog/corpus/lifecycle/scaling and all other case data remain intact.

[Release checks](verification.txt) passed on .NET 10 and 11: complete PythonRe,
focused Core boundary/NB/construction/dependency checks and PCRE2 boundary/
resource/cancellation checks, plus API/inventory/snapshot guards. New tests compare
the primitive with independently parsed expanded assertions at every scalar
offset, including scoped flags, supplementary scalars, pinned categories, captures,
cursor aliases, empty progression, output limits, malformed tails and original
interruption after validation. All three Core boundary consumers are exercised.
Initial fixture compile errors were corrected; logs remain in the archive below.
[Generated-page verification](markdown.txt) passed. No package/full catalog matrix
was repeated.

Immutable local archives under `artifacts/experiments/python-unicode-boundary-20261029/`:

- `parity-python-unicode-boundary-d458c0ec.tar.gz`: exact baseline/candidate
  DLL/PDBs, benchmark host, source bundle, driver/build/preflight/comparison and
  final snapshots/reports. SHA256
  `34BCA4AF99EE743DC27C5FAE51CCE893B08B2BFDB9BEF800CA4EFB738CCD739D`.
- `correctness-logs.zip`: initial/corrected focused and complete validation logs.
  SHA256 `1B0826C3AC4A87528BC9C6A10E69A9D2CF078A6F16D6F8DD4ED58F3546FC991E`.
