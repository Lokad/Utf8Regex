# Bounded anchored-email Boolean matching

7da785da reuses the existing complete-language proof and checked whole-email
kernel after strict whole-subject validation and required-literal rejection.
The Boolean route accepts ASCII subjects up to 2048 bytes and charges/checks the
original invocation before and after the bounded scan. Wider class aliases and
longer inputs retain scalar execution; Count, Match, captures, offsets, RTL,
folding and NonBacktracking keep their existing implementations. No grammar,
public API, engine, unchecked access or accounting mechanism was added.

[Initial route](initial-route.txt), [comparison](comparison.json),
[console](comparison.txt), [driver](driver.cs.txt), [project](driver.csproj.txt).
Quiet Ubuntu VM, Xeon 8573C, .NET 11 RC3, CPU2. Exact f6c823f8 baseline versus
7da785da candidate. One **53.65s** comparison under a 120s cap: eleven public
workloads, four equal-result lanes, one-second warmup, independent calibration
and three alternating samples. Public results match .NET before and during
timing. No confirmation or complete package/catalog qualification was needed.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Catalog hit, ordinary IsMatch | 1.817 | 0.179 | 5856 / 0 |
| Catalog hit, compiled IsMatch | 1.752 | 0.180 | 5856 / 0 |
| Genuine catalog miss, compiled IsMatch | 1.993 | 0.184 | 5856 / 0 |
| Finite limits/live token, hit | 1.859 | 0.158 | 5968 / 112 |
| Finite limits/live token, miss | 2.137 | 0.167 | 5968 / 112 |
| No-at negative, 2000 bytes | 7.590 | 0.726 | 3984 / 0 |
| Adversarial repeated domain labels | 73.873 | 2.919 | 31032 / 0 |
| Unicode alias control | 1.659 | 1.595 | 5856 / 5856 |
| Long-input control | 15.120 | 15.871 | 5856 / 5856 |
| Count control | 1.339 | 1.325 | 3616 / 3616 |
| Literal control | 0.214 | 0.213 | 0 / 0 |

The historical driver name `no-at-control` is an affected negative path, not an
unchanged control. Unicode/long/Count/literal controls vary -3.9% to +5.0%, with
unchanged allocations; no exact cost-equivalence claim. Match/capture preservation
is checked for correctness rather than separately timed here. a40bda03 remains
the fixed cumulative release baseline.

Selective [hit](hit-refresh.txt) and [miss](miss-refresh.txt) refreshes qualify
compiled ratios **14.86x -> 2.17x** and **21.80x -> 2.05x**, respectively, still
Status=Gap. Ordinary ratios are **0.67x/0.53x**, Status=Win. Allocations are zero
and every V3 sample exceeds 20ms. [Parity verification](parity.txt),
[Markdown generation](markdown.txt), [snapshot/API/managed-dependency checks](verification.txt)
and [unselected-row comparison](snapshot-diff.txt) passed. Only two identities /
four ordinary/compiled rows changed. A mistyped Markdown executable path was
corrected without repeating any measurement.

Focused Release checks pass on both runtimes: 324 email/final-family/work-policy,
187 public runtime/admission/budget, 19 PythonRe boundary/resource, 236 PCRE2
boundary/resource and 16 snapshot/API/dependency checks per runtime. New coverage
includes padded spans, 2047/2048/2049-byte boundaries, Unicode digit aliases,
independent scalar verification, final-LF/strict anchors, excluded modes, retained
captures, offsets, malformed tails, work/working denial and original-token
cancellation/deadline publication. [Runtime guards](runtime-guards.txt).

Immutable archives under `artifacts/experiments/email-boolean-20261029/`:

- `parity-email-7da785da.tar.gz`: exact baseline/candidate DLL/PDB/driver/bundle,
  build, comparison, final snapshots/reports and next-route inspection. SHA256
  `58EF9A227901F2492289BFFF5CAF01CD2AB85414AAE8B2544708DAA490EEF818`.
- `correctness.zip`: focused Core/adapter checks and snapshot comparison. SHA256
  `072836AF3299AA58C8607900E870C22870772CE23C9662474078A820E86323A2`.
