# Shared ASCII-range repeat trial

Baseline `3fd56b21`, .NET 11 RC3 quiet VM. The existing no-rewind repeat runner
can consume exact positive ASCII sets through checked span
`IndexOfAnyExceptInRange`, bounded to 2,048 bytes and the remaining finite maximum.
Reuse its range proof; two checked mask-word reads, population and endpoint width
replace 128 bit probes. Holes, negation, folding and Unicode tables decline.
Capture/trail instructions, min/max, original interruption and memory/result
admission are unchanged. Numeric work can fall to bounded scan units. No metadata,
state, mode, unchecked read or separate executor is added.

One **75.809-second** ordinary-process ABBA and the sole **75.690-second** noise
confirmation, each capped at 180 seconds, identical worker/Python adapter and
deliberately changed Core. Independently calibrated lanes, five samples/process
of at least 20 ms after 500 ms warmup. Full Python outputs and zero loans checked
outside timing. Core probes time `MatchDetailed` plus returned byte-coordinate
fields; full capture text is verified before sampling. These are public combined
workloads, not isolated kernels or bare-Match timings.

| Public workload | Before (µs) | After (µs) | First / confirmation change |
|---|---:|---:|---:|
| Long contiguous capture plus coordinates | 13.68 | 6.99 | −48.9% / −48.7% |
| Same, finite/live | 16.28 | 7.28 | −55.3% / −55.0% |
| Holey capture plus coordinates | 13.80 | 16.09 | +16.6% / +17.1% |
| Folded capture plus coordinates | 24.48 | 26.10 | +6.6% / +6.5% |
| Python many-capture strings, unlimited | 796.28 | 818.30 | +2.8% / +1.0% |
| Python many-capture strings, finite/live | 1,182.36 | 1,161.92 | −1.7% / −1.5% |
| Unchanged Core literal Count | 0.86 | 0.86 | 0.0% / +0.2% |

**Accept the long-range gain with explicit local regressions; the Python capture
gap is not closed.** This uses the approved major-gain/local-regression trade-off,
not a universal 10% gate. No new native/CPython ratio or composed gain is inferred.
Allocations are unchanged across all 19 lanes, including Core 9,264/9,376 B for
unlimited/finite positive captures and Python 242,008/246,208 B for many captures.

One-capture unlimited is inconclusive: baseline process medians are 1,233/477 µs
in the first comparison and 1,233/482 in confirmation; candidate observations
range 564–726 µs. Pooled medians reverse +18.8% to −21.7% and cannot establish a
stable gain/regression. No third timing. Unicode tuples move +4.0%/+3.4%; other
small/unmodified lane movement and process ranges remain in
[first summary](summary.json) and [confirmation](confirmation-summary.json), without
normalization or attributing unaffected operations to the range branch.

Focused Release checks pass on both targets and platforms: Core 514, PythonRe
5,009 per target. New independent public cases cover mask word/ASCII endpoints,
holes, 2,047/2,048/2,049-byte blocks, finite maxima and supplementary prefixes.
Direct production scan tests also cancel/expire inside a long ASCII run while
preserving a parent loan and releasing scratch. Existing captures, histories,
RTL, original authority and finite refusal checks pass. Initial test-property and
worker-source-glob build errors were corrected before timing; actual resume
scripts/failed logs are retained. Passed suites were not repeated for driver fixes.

Raw evidence is copied off the VM under
`artifacts/experiments/ascii-range-repeat-20261109/`:

- `evidence.tar.gz`: `0C79B168ACC9C79F69A8EFC68A514627C55437F25C181222B295CD25F5991420`.
- `confirmation-evidence.tar.gz`: `143D001EB4DE60EB3049D3A838C32CCB18FC5958D1C5650531064BE2EFA636C3`.

No package qualification or catalog refresh. Existing staged bytes stay immutable.
Next, investigate the extra selector inside the fallback scalar loop as a distinct
bounded correction, retaining this evidence and the fixed original baseline.
