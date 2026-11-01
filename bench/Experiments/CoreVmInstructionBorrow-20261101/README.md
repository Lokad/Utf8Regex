# Core VM instruction borrow: closed and reverted

Reading the readonly instruction by checked managed reference removes its eager
20-byte copy, but the one bounded public comparison shows no useful imports gain.
The production line is restored. No matcher, mode, API, package, snapshot or
resource contract changes remain; no confirmation or variants ran.

Baseline is the exact frozen `825ed1f7` Core DLL/PDB. Parent `95eb66f8` changes
only documentation/generated evidence; candidate is that parent plus the archived
one-line `ref readonly` read in `Utf8SliceRunner.RunSegment`. Original packages
and benchmark-host binaries remain unchanged.

The [before](probe-before.json) and [after](probe-after.json) diagnostics confirm
identical 45-instruction imports programs, masks and primitive program facts for
ordinary/compiled options and across both binaries. Both public Count routes use
the same scalar runner; Compiled does not select a different verifier here.
Actual `Unsafe.SizeOf` is20 bytes. Tier1 removes the dispatch copy, but the complete
method grows19014→19480 bytes. Each diagnostic checks760000 accumulated matches;
codegen and short earlier phase measurements are not throughput qualifications.

[One comparison](comparison.json) completed in88.035s under120s,12public cases,
four equal-result lanes, minimum44.281ms sample. Existing typed-delegate driver,
isolated assembly contexts, CPU2, .NET11RC3, warm1.5s/min1000calls, independent50ms
calibration and five alternating samples. Every result batch is consumed after
timing. Allocations are unchanged in every row.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual imports Count, ordinary |30.991|31.100|+0.35%|
| Actual imports Count, compiled |30.875|31.711|+2.71%|
| Actual imports, finite/live |36.388|36.016|−1.02%|
| Finite/live miss |47.339|46.190|−2.43%|
| Dense ASCII |55.641|56.497|+1.54%|
| Dense mixed Unicode |60.655|60.910|+0.42%|
| Boundary hit |1.636|1.597|−2.43%|
| Detailed Match control |156.259|153.646|−1.67%|
| Unchanged literal control |67.682|68.172|+0.72%|

[All rows, allocations and spreads](summary.json) retain positive-set, folded and
false-tail controls too. Correctness/finite/deadline checks passed on both targets:
Core8951, PythonRe4493, PCRE2focused211 per runtime; restored Core89 pass again.
Affected outputs were rebuilt after restoration. No new implementation-mirroring
test or package/platform matrix. Deadline throughput and a fixed-original
cumulative comparison are outside this rejected pilot's scope.

Earlier short phase results differed between ordinary/compiled execution, but
their matching program shapes and this warmed comparison do not establish an
algorithmic Compiled regression. The full catalog's remaining gap is unchanged;
do not replace its ratios or Status with these measurements. Investigate catalog
measurement/warmup only if source inspection demonstrates a concrete issue.

Immutable raw source/DLL/PDB/driver/codegen evidence lives under
`artifacts/experiments/core-vm-instruction-borrow-20261101-deferred/`:

- `evidence.tar.gz`, SHA256
  `83B8A1C31479F4D3F0CF30E58C4469AD4EEA0B6D1539F937F5F68B455EE84C59`.
- `correctness.zip`, SHA256
  `8ED5573898AA5454B3AEB21434F43CAD71ADADAA9077E757E5B1C7450F6058BB`.

Preparation failures are retained: the initial probe lacked AppContext-relative
corpus data; preparation then referenced a nonexistent Data folder; archive
comparison initially included platform-dependent diff index hashes. Each stopped
before timing/archive output. Corrections changed probe layout/guard only; exactly
one public comparison completed. [Verification summary](verification.json).
