# Compare short literals at their required start

PythonRe value/text Match and FullMatch used complete candidate discovery for an
exact literal even when its start was already required. Eligible 1-16-byte
unfolded literals now borrow the prepared needle and perform one charged checked
span comparison after whole-subject validation. FullMatch also checks the suffix
length. Boundary/trailing-literal requirements, folding, families, longer literals
and Search retain their existing routes. No buffer, helper, state, engine, mode
or unchecked access is added. Final publication, coordinates, captures, funded
owners and retained results keep their contracts. Avoiding discovery deliberately
lowers work requirements; depth, major-capacity and result admission are unchanged.

One predeclared quiet-VM .NET 11 RC3/CPU2 comparison completed **23 public cases
in 107.422s**, under a 120-second cap. Separate contexts load fixed original
`a40bda03`, parent `0a26b586`, and parent plus the archived runtime patch. Current
Core DLL bytes are identical. Equal-result compiled Regex diagnostics use both
predecoded and decode-then inputs. Warmup is 500ms/minimum 128 calls per lane,
independently calibrated 60ms batches, five alternating samples, all at least
**44.535ms**. Full range/count digests are consumed after timing; runtime startup
precedes in-process affinity. No confirmation, variant, catalog or package run.

| Public value operation | Before us | After us | Change | After vs original |
| --- | ---: | ---: | ---: | ---: |
| Short FullMatch hit |0.083|0.064|-22.29%|-16.40%|
| Short FullMatch miss |0.081|0.056|-30.54%|-25.71%|
| Short Match hit |0.082|0.062|-23.66%|-19.21%|
| Short Match miss |0.081|0.060|-25.45%|-20.26%|
| Unicode literal FullMatch |0.098|0.080|-17.58%|-14.97%|
| FullMatch after BMP/astral prefix |0.125|0.109|-13.07%|-8.83%|
| 16-byte FullMatch |0.083|0.064|-22.61%|-15.57%|
| 17-byte FullMatch control |0.086|0.090|+5.23%|+17.00%|
| 4,096-byte FullMatch control |0.200|0.207|+3.92%|+6.29%|
| Unchanged native Count |0.134|0.134|-0.24%|+19.19%|

**Promote.** The largest adverse parent median is +5.23%, within the allowance.
All allocation medians are unchanged, including finite/live/deadline controls.
`summary.json` preserves every control and simultaneous fixed-original ratio.
Search +12.21% versus original, suffixed FullMatch +11.05%, 17-byte FullMatch
+17.00% and unchanged Count +19.19% remain visible. The latter controls already
cost more in the parent; this experiment does not establish cumulative acceptance
or explain all context-dependent movements. Preserve these representative new
cases alongside the existing complete gate at the next coherent boundary.

Complete PythonRe Release tests pass **4,592 per runtime** on .NET 10 and 11.
Six further tests add direct production work-cutoff coverage for the new literal
route and final-publication original-token/deadline injection for value/text.
The affected 81 tests pass on both runtimes after that test-only addition; the
product implementation is unchanged from the full-suite run. Coverage includes
1/16/17/4,096-byte needles, scalar offsets, prefix/tail malformed UTF-8, strict
validation before immediate mismatch, assertions/folding exclusions, captures,
funded admission/close/overlap and retained aliases. Positive lookahead must not
degrade to a plain literal. The specification records lower work requirements.

Release public caller code remains 2,894 bytes with the same 2,056-byte main frame.
The discovery helper's Tier0 code grows from 956 to 1,511 bytes; public timing
governs acceptance. This is a reduction in unnecessary discovery work, not an
inlining annotation or a removal of basic metering.

Raw driver/protocol, three DLL/PDB sets, runtime patch, preflights, codegen and all
samples/logs: `artifacts/experiments/python-anchored-literal-20261102/evidence.tar.gz`,
SHA256 `E3D129F360554C0D2CC3CD08D4FC8775072523E1C98774C74286A2ED9072C3C3`.
`correctness-source.zip`: SHA256 `383187C07DEB52B1CEF66DA4BC27C28724988D18FCD7F7A4BCCA777590D9DB2C`.
Published/staged bytes remain immutable. The failed dev.11 gate remains evidence;
no new package or CPython Status acceptance is claimed here.
