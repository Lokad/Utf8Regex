# Use the dedicated checked boolean ASCII primitive

**Accepted.** `Utf8InputAnalyzer.IsAscii` delegates to `System.Text.Ascii.IsValid`
instead of producing a non-ASCII index through a generic range search. This is
the only production change. Index-producing validation, UTF-8 strategy selection,
scalar execution, allocation behavior and every admission/interruption checkpoint
remain unchanged. Both target frameworks provide this checked span API.

One quiet-VM .NET 11 RC3 comparison completed **12 public cases in 56.445s**,
under a 120-second cap; minimum timed sample **43.816ms**. Fixed original
`a40bda03`, current `05c6fac7` and current plus the one-line patch load in
separate contexts. PythonRe DLL/PDB bytes are identical before/after. Normal
runtime startup precedes CPU2 affinity. Existing protocol: 500ms/minimum 128-call
warmup per lane, independently calibrated 60ms batches, five alternating samples,
complete Count/range outputs consumed after timing, allocation medians and
equal-output predecoded/decode-then compiled Regex diagnostics.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Core Count, 4 KiB literal hit |0.289|0.262|-9.41%|
| Core Count, 4 KiB literal miss |0.285|0.259|-9.11%|
| Core Count, 128 KiB literal hit |6.104|5.593|-8.38%|
| Core Count, 128 KiB literal miss |8.166|7.054|-13.62%|
| Core Count, 128 KiB late Unicode |13.427|12.231|-8.90%|
| Core Count, live token, 4 KiB |0.254|0.226|-11.12%|
| Core Count, finite limits/live token, 4 KiB |0.249|0.221|-11.21%|
| Core Count, deadline/live token, 4 KiB |0.357|0.339|-5.03%|
| Unchanged PythonRe scalar Search |1.080|1.057|-2.08%|

All allocations match. Short hit/miss change -1.63/-0.53%; early-Unicode control
is the largest adverse current median, **+0.38%**. The 128 KiB hit candidate has
a 1.189 max/min spread; other primary 4 KiB/miss samples have tight spreads and
independently meet the useful-gain criterion. No noise confirmation or variant
was needed. Original ratios remain in the complete summary: unchanged PythonRe
scalar Search is still **+13.65%**. This is not cumulative release acceptance.

Captured public Count code generation reaches the actual predicate: Tier0 calls
the range API before and ASCII validity API after, with subsequent tiers retained
in the raw disassembly. Probe call counts are warmup diagnostics, not performance
evidence. Separate predicate checks passed **53,324 cases per assembly**, including
all 256 byte values, empty/vector/block boundaries, offsets, and poisoned bytes
outside the admitted span. Initial preflight accidentally applied FullMatch
anchors to the Count comparator; it failed before timing, was corrected to
unanchored Count, and its failure log is preserved. Windows-to-Linux patch line
endings also required normalization before application; no timed run was repeated.

Focused Release checks pass on both .NET 10 and 11: Core **499/runtime**,
PythonRe **103/runtime**, PCRE2 **105/runtime**. They cover strict validation and
malformed tails, original cancellation/deadlines, finite resource authority and
publication. Existing tests were reused; no test-only executor or framework added.
No public contract changed and no package/catalog/full-platform matrix was run.

Raw driver/protocol, three exact DLL/PDB sets and hashes, patch, all samples,
boundary probe, code generation, setup caveats and correctness logs:
`artifacts/experiments/ascii-bool-20261102/evidence.tar.gz`, SHA256
`7F04D1542FD46BBC4E12A8639711C652A72B7FE4F5C14DBBBB1A1E815D83BC76`.
