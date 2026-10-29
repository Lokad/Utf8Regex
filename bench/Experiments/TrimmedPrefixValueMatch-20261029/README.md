# Bounded trimmed-prefix value Match

eeddbe67 reuses the existing complete anchored optional-prefix/tail proof and
checked range helper for value Match on fully validated ASCII subjects up to
2048 bytes. It checks the original invocation around the helper, then uses the
existing coordinate projection. Detailed captures, Unicode/long inputs, offsets,
RTL, folding and NonBacktracking retain their existing executors. No grammar,
public API, matcher, unchecked access or accounting mechanism was added.

[Initial route](initial-route.txt), [comparison](comparison.json),
[console](comparison.txt), [driver](driver.cs.txt), [project](driver.csproj.txt).
Quiet Ubuntu VM, Xeon 8573C, .NET 11 RC3, CPU2. Exact 16b85ecb baseline versus
eeddbe67 candidate. One **44.42s** comparison under a 120s cap: nine public
workloads, four equal-result lanes, one-second warmup, independent calibration
and three alternating samples. Per-sample public range lengths are checked
against .NET before timing. Unicode lanes consume derived UTF-16 lengths, so
producer/comparator sinks use equal units. Timed loops use foreach in all lanes.
No confirmation or complete package/catalog matrix.

The catalog aggregates **31 public Match calls / 1375 input bytes** per operation;
its PrefixMatchLoop label is not a single Count call or an empty input. The same
31 calls are used here, with compilation/startup excluded.

| Public operation loop | Before us | After us | Before / after B/loop |
| --- | ---: | ---: | ---: |
| Catalog value Match, ordinary | 40.356 | 4.301 | 179056 / 0 |
| Catalog value Match, compiled | 38.108 | 4.290 | 179056 / 0 |
| Catalog value Match, finite limits/live token | 48.451 | 7.140 | 182528 / 3472 |
| Five ASCII misses | 5.901 | 0.660 | 28880 / 0 |
| Two samples at 2047/2048 bytes | 18.524 | 0.374 | 11552 / 0 |
| Unicode value-Match control | 4.278 | 4.647 | 23104 / 23104 |
| Long-input value-Match control | 34.614 | 35.973 | 11552 / 11552 |
| Catalog Count control | 38.611 | 39.822 | 111104 / 111104 |
| Literal value-Match control | 0.260 | 0.250 | 0 / 0 |

Controls vary -4.0% to +8.6%, with unchanged allocations. Preserve those costs;
no equivalence claim or favorable rerun. a40bda03 remains the fixed cumulative
release baseline.

Before the selected refresh, the source audit found a comparator-options defect:
Lokad script recipes retained Compiled in DotNetOptions while Core ordinary lanes
cleared it. aa021223 now constructs ordinary .NET with Utf8Options and checks
actual flags in both pairs before timing. Other benchmark contexts already pair
the same flags. [Setup checks](fairness-setup.txt) exercise ordinary, folded and
NonBacktracking recipes. No shipped source changed after eeddbe67's A/B.
Historical ordinary script numbers remain intact with a README caveat; their
comparator change is not credited as a producer speedup. The final current full
release refresh must replace those rows under the corrected setup.

The five-sample [selected refresh](refresh.txt) at aa021223 qualifies compiled
**13.61x -> 1.47x**, Status=Gap, and new matching-flag ordinary **0.89x**,
Status=Win, both zero B/loop. The ordinary historical 13.66x used a compiled
comparator and is not a like-for-like ratio baseline. The paired A/B above
independently demonstrates the ~9x producer gain. Every V3 sample exceeds 20ms.
[Parity](parity.txt), [generated Markdown](markdown.txt),
[snapshot/API/managed-dependency checks](verification.txt) and
[unselected-row comparison](snapshot-diff.txt) passed. Only this case's two
snapshot/parity rows changed.

Focused Release checks pass on both runtimes: 321 value-Match/line-family/work-
policy, 187 public runtime/admission/budget, 19 PythonRe, 236 PCRE2 boundary/
resource and 16 snapshot/API/dependency checks. Coverage includes padded spans,
final-LF greediness, lexical/strict anchors, 2047/2048/2049-byte bounds, wider
subjects and excluded modes, retained detailed captures, offsets, work/working
denial, malformed tails and original cancellation/deadline publication.

Immutable archives under `artifacts/experiments/trimmed-prefix-value-match-20261029/`:

- `parity-doc-prefix-eeddbe67-ab.tar.gz`: exact measured baseline/candidate DLL/
  PDB/driver/bundle, catalog data, build and comparison. SHA256
  `0617424EAD4F0F11CBAE9FEF02FE1DC56023A9851EF54845F4D6F7B9C19DB0A3`.
- `parity-doc-prefix-aa021223-qualified.tar.gz`: exact corrected benchmark DLL/
  PDB/source bundle, final selected snapshots/reports and next-route inspection.
  SHA256 `A6E390C1704EEDBA0FA7BD5BA603B9DC6CA2CB5065988B7C3B80FF21AF5B1A0E`.
- `correctness.zip`: focused Core/adapter, setup, build and snapshot checks.
  SHA256 `05ADC80281DEB9E493AB21D89750F1ED90DC48C04DCA0C4B1709137C77EC8B6E`.
