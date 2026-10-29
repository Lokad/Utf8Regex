# Lower Python ASCII boundaries to the existing owned primitive

f6c823f8 replaces generated ASCII boundary lookarounds/alternations with the
existing boundary instruction. Its former ECMA Boolean operand now names
Unicode, ECMA or strict ASCII membership. Existing encodings and Core semantics
remain; strict ASCII excludes ECMA's U+0130 member. Both backtracking and linear
interpreters use the same predicate. Python `\B` retains its nonempty-subject
guard. No parser grammar, matcher, cursor, public option or execution mode was added.

[Initial diagnostic](phases.txt) found 768 splits/769 strings over 2176 bytes:
public Split about 1444us versus 7.5us prepared string projection. These cumulative
controls are not additive and do not establish a public speedup.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; exact d020df64 baseline
versus f6c823f8. One **49.12s** public A/B under a 120s cap, ten workloads, four
equal-result lanes, one-second warmup and three alternating samples. Result
batches are retained and every digest is checked after timing; roots are allocated
before timing and retained payload is capped at 64MiB per lane. Count results are
checked as integers. .NET diagnostic lanes use explicit ASCII boundary formulas
where required; UTF-16 boundary matching is not a Python scalar oracle.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| ASCII boundary Split | 1434.886 | 242.096 | 41888 / 40016 |
| Finite limits/live token | 2025.147 | 307.273 | 3886368 / 44216 |
| Mixed-Unicode subject, ASCII Split | 778.295 | 129.636 | 17320 / 15448 |
| ASCII non-boundary Split | 1770.652 | 429.099 | 78808 / 78808 |
| Scoped ASCII boundary Split | 1438.483 | 243.756 | 41888 / 40016 |
| ASCII boundary Count, 2176 bytes | 1395.450 | 214.298 | 1872 / 0 |
| Unicode-boundary Split control | 1519.197 | 1579.388 | 41888 / 41888 |
| Literal Split control | 42.370 | 43.771 | 19512 / 19512 |
| Core word-boundary Count control | 152.731 | 154.294 | 0 / 0 |
| Core NonBacktracking Count control | 75.184 | 81.635 | 26224 / 26224 |

Controls vary +1.0% to +8.6%, with unchanged allocations. Their costs are not
claimed equivalent. No confirmation was needed; no further tuning campaign ran.
a40bda03 remains the fixed cumulative release baseline.

The selected [Split refresh](split-refresh.txt) and [paired qualification](split-qualification.txt)
give **3.638x CPython [3.603,3.646]**, previously 22.19x. Managed median is
243.238us, CPython 66.934us, allocation 40016 B, CPU3. The shared
[Count refresh](count-refresh.txt)/[qualification](count-qualification.txt) gives
**1.483x [1.479,1.490]**, previously 9.45x, with zero allocation, CPU7. That
catalog Count subject is larger than the A/B Count subject: managed 1669.923us,
CPython 1124.008us. Both verdicts remain **CPython faster**. Paired order effects
and IQR spreads pass existing gates; raw and paired medians remain distinct.

[Generated-page verification](markdown.txt) and [snapshot/API checks](verification.txt)
passed. Only split/zero-width and zero-width/count changed; every other case and
the catalog/corpus/lifecycle/scaling fields were checked unchanged.

The complete PythonRe suite passed on .NET 10 and 11, plus focused Core Unicode,
NonBacktracking, construction/dependency and PCRE2 boundary/resource checks.
New tests compare direct primitives to independently parsed expanded controls at
every scalar offset, including dotted/dotless I, supplementary scalars, combining
marks, scoped flags, empty/adjacent matches, split limits, captures and retained
cursor snapshots. Finite result/working/work denial, malformed tails and original
interruption after validation are preserved. Initial fixture errors (substitution
API spelling and class-cursor shared-position expectations) were corrected; both
attempts remain archived. No package or complete catalog matrix was repeated.

Immutable local archives under `artifacts/experiments/python-ascii-boundary-20261029/`:

- `parity-python-boundary-f6c823f8.tar.gz`: exact DLL/PDB/driver/bundle, build,
  diagnostic, comparison and final snapshots/reports. SHA256
  `87CB08126610CFE7FEAE6B144E862D36BC891268B369EE75120686ABBBD213B2`.
- `correctness-attempts.zip`: initial/corrected fixtures, complete PythonRe and
  focused Core/PCRE2 checks. SHA256
  `BB3014C0D48F4894F195964DA01B348CCD72988B033FBB1605093FF5F56616D1`.
