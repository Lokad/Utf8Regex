# Bounded cell/range value Match

**Accepted, source ea44ad1e.** Existing complete cell/range-reference proofs join
the <=2048-byte fully validated ASCII value-Match envelope, reusing the existing
spreadsheet helper, invocation checks and projection. The classifier already
qualifies the construction-culture ASCII alphabet; Turkic variants that differ
decline. No culture switch, matcher, state, API, grammar or unchecked access.
Unicode subjects, long inputs, nonzero origins, detailed histories and NB/RTL
retain their existing routes. Public resource/lifetime contracts are unchanged.

[One public A/B](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt): exact accepted 819ab078
and ea44ad1e DLLs, quiet VM CPU2, .NET 11 RC3, symmetric load contexts, 500ms
warmup, independent calibration and three alternating samples. **49.01s** under
the 120s cap, minimum timed batch **29.27ms**. Equal per-sample public ranges
and aggregate lengths pass producer/predecoded/decode-then-.NET checks. The
actual catalog loops contain **15 cell calls / 37 bytes**, **9 range calls /
44 bytes**. Compilation/startup are excluded. No confirmation.

| Public Match loop | Before us | After us | Before / after B/loop |
| --- | ---: | ---: | ---: |
| Cell, ordinary | 15.428 | 1.979 | 86400 / 0 |
| Cell, compiled | 14.041 | 1.991 | 86400 / 0 |
| Range, ordinary | 11.742 | 1.233 | 52128 / 0 |
| Range, compiled | 11.288 | 1.243 | 52128 / 0 |
| Cell, finite/live token | 15.787 | 2.862 | 88080 / 1680 |
| Range, finite/live token | 12.105 | 1.645 | 53136 / 1008 |
| Cell misses | 7.617 | 1.121 | 39248 / 0 |
| Range misses | 9.005 | 1.041 | 39360 / 0 |

Cell is about **7-8x faster**, range **9-10x**, finite loops **5.5x/7.4x**.
Controls retain allocations: Unicode +0.1%/+2.8%, incomplete-proof +1.4%, NB
+8.2%, RTL +5.8%, Count +8.7%. Detailed -3.6%, long -0.9% and literal -1.1%
variation is not credited as an isolated gain. Keep these costs in the raw record.

[Cell refresh](refresh-cell.txt) and [range refresh](refresh-range.txt), five V3
samples/lane, all >=40.68ms: compiled **8.01x -> 1.07x (Gap)** and **7.14x ->
0.78x (Win)**. Matching-flag ordinary is **0.68x/0.43x (Win)**. Old ordinary
7.64x/7.36x used compiled .NET; their denominator correction is not a producer
gain. [Scope check](snapshot-scope.txt) confirms only these four rows changed.
The existing rewrite/verify preserves README's decreasing-ratio order. No
Python/PCRE2 data, complete catalog or package qualification was refreshed.

[Both-runtime Release checks](verification.txt) cover missing/invalid prefixes,
optional colon, NUL/CR/LF, 2047/2048/2049-byte bounds, malformed suffixes, offset
spans/origins, en-US/Turkic construction culture retained after ambient changes,
Kelvin/Unicode digits, independent supplementary-scalar Nd expectations, digit
capture histories/retained aliases, NB/RTL, finite denial and original synchronous
token/deadline publication. Adjacent Core work/depth/working/inventory, PythonRe/
PCRE2 guards and API/snapshot checks pass. [Initial route](initial-route.txt).

Immutable files under `artifacts/experiments/reference-value-match-20261029/`:

- `parity-reference-value-ea44ad1e.tar.gz`: exact DLL/PDBs, host, source bundle,
  driver/binaries, raw costs and before/after snapshots.
  SHA256 `92872008D979C7AC9F3DB910212B7F674772D2A0D5C7D82963448A35C5BDAF08`.
- `correctness-and-refresh.zip`: fixture, focused checks, route/setup/refresh logs.
  SHA256 `E150F86796C85258891F90B09269E01C8EEE3AF5EF3525007870B581A559A90A`.
