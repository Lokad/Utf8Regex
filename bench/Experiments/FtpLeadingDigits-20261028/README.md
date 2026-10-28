# Reuse the anchored leading-digits/tail proof for Boolean matching

14ef506b uses the existing complete-language classifier before renting scalar VM
capture slots. After whole-subject UTF-8 validation, leading ASCII digits and
the non-LF tail are searched in at most 2,048-byte windows under the original
invocation. Unicode digits/separators defer to scalar verification. Match,
Count, captures, nonzero starts, RTL, folding and NonBacktracking keep their
existing executors. No grammar, public API, matcher or execution mode was added.

[Initial route](initial-route.txt), [comparison](comparison.json),
[console](comparison.txt), [driver](driver.cs.txt), [project](driver.csproj.txt).
Quiet VM, .NET 11 RC3, CPU2; exact 51605746 baseline versus 14ef506b. Core source
at the baseline matches 38573dfe. One **28.92s** comparison, capped at 120s,
four equal-result public lanes, 500ms warmup and three alternating samples.
No A/B confirmation or full package/catalog qualification was needed.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| FTP header IsMatch, ordinary | 1.514 | 0.178 | 5760 / 0 |
| FTP header IsMatch, compiled | 1.412 | 0.179 | 5760 / 0 |
| Finite limits and live token | 1.655 | 0.145 | 5872 / 112 |
| 8 KiB leading digits | 288.458 | 0.887 | 340440 / 0 |
| Mixed-Unicode/control tail | 11.485 | 2.303 | 5760 / 0 |
| Interior-newline miss | 34.325 | 0.830 | 5760 / 0 |
| Unchanged Count control | 1.187 | 1.214 | 3552 / 3552 |
| Unchanged Match control | 1.182 | 1.200 | 5760 / 5760 |
| Unchanged literal control | 0.235 | 0.219 | 0 / 0 |

Count/Match controls vary +2.2%/+1.5%, literal -6.8%; these are not claimed
improvements. The wider-prefix fallback records 437.626 -> 246.617us with
unchanged 344328 B/op. Its extra prefix scan cannot explain that large reduction;
retain it as unexplained timing variation, not a scalar-VM improvement.

The [selective refresh](refresh.txt) qualifies the two affected public rows:
compiled **29.97x -> 3.67x**, ordinary **1.64x** predecoded .NET 11, zero
allocation. Both retain Status=Gap. All sample durations exceed 20ms.
[Parity verification](parity.txt) and [Markdown generation](markdown.txt) passed.
Every other snapshot/parity row was checked unchanged; the README was generated.
[Snapshot and managed-dependency checks](verification.txt) passed on both runtimes.
a40bda03 remains the fixed cumulative release baseline.

Focused Release checks passed on both runtimes: Core grammar/scalar, construction,
route and work-policy checks; PCRE2 ownership/anchor checks; PythonRe prefix and
finite-policy checks. New tests cover padded spans, window boundaries, Unicode
digit/separator aliases, scalar tails, LF/CR/NUL, captures and retained results,
malformed tails, excluded modes, original-token cancellation/deadlines after
validation, final publication polls and every kernel work cutoff with parent loans.
An initial RTL comparison oracle used byte zero instead of the input end; it was
corrected without changing production semantics. Both attempts remain archived.

Immutable local archives under `artifacts/experiments/ftp-leading-digits-20261028/`:

- `parity-ftp-line-14ef506b.tar.gz`: exact DLL/PDB/driver/bundle, build output,
  comparison and final snapshots/reports. SHA256
  `CBB8C6A1EE4C969908905AF2BF82242C8B86C90CB98926BA3A9B8B82C289E38A`.
- `correctness-attempts.zip`: initial and corrected tests, final cross-profile
  checks and local build. SHA256
  `984C42BD6B5BEA5022FEC341BD37027C9167952F3A30A6647BD89ED60043634D`.
