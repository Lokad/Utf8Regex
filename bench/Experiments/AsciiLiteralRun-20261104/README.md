# Adjacent literals in scalar dispatch

**Accepted**, source `bde712e7`. Adjacent exact LTR ASCII Literal instructions
share a checked byte loop inside the existing scalar VM. Every literal retains
its original instruction charge and indexes. Capture writes, assertions, rollback,
folding, wider scalars and RTL return to normal dispatch. No opcode, retained
metadata, engine, mode, unchecked access or public accounting change is added.
This avoids repeated full dispatch; it does not bypass capture verification.

The actual Python single-capture FindAll program has five adjacent literals for
`item-`, followed by capture/repeat/end instructions. It already retains necessary
literal-family candidate guidance and the class-repeat no-rewind proof. Those
mechanisms and capture/result ownership are unchanged by this experiment.

One **86.9955535-second** public comparison uses accepted Core `49b8f2b1` versus
`bde712e7`. Only Core DLL/PDB differs; six adapter/benchmark companions remain
byte-identical dev.24 Full files. Normal eight-processor startup precedes CPU2
affinity. Managed warmup is 1.5s/minimum 128 calls, diagnostic comparators
250ms/minimum 64, independent 60ms calibration and five alternating samples.
All 160 owned samples qualify, minimum **58.1586ms**. The typed bridge introduces
no boxing or additional result allocation. Complete scalar/tuple string arrays
and numeric results are consumed and compared after timing. BCL projected FindAll
lanes are composed diagnostics, not CPython parity or benchmark Status.

| Public operation | Before us | After us | Change | Allocated bytes, unchanged |
| --- | ---: | ---: | ---: | ---: |
| Native ASCII control | 2.508 | 2.520 | +0.47% | 0 |
| Actual one-capture FindAll | 449.714 | 431.619 | -4.02% | 112944 |
| One-capture FindAll, finite/live | 656.222 | 647.335 | -1.35% | 117144 |
| One-capture FindAll, deadline/live | 878.493 | 860.377 | -2.06% | 113104 |
| Actual many-capture FindAll | 724.218 | 686.316 | -5.23% | 260400 |
| Actual imports Count | 20.692 | 20.452 | -1.16% | 3568 |
| Dense captured prefixes | 213.222 | 197.806 | -7.23% | 63704 |
| Long prefix FindAll | 59.103 | 45.242 | -23.45% | 4840 |
| Failed-prefix branch FindAll | 252.128 | 228.697 | -9.29% | 53264 |
| Single-literal FindAll | 173.331 | 171.197 | -1.23% | 47320 |
| Unicode-prefix control | 208.420 | 207.847 | -0.27% | 63744 |
| Folded-prefix control | 118.723 | 118.699 | -0.02% | 26880 |
| RTL Count control | 353.901 | 353.223 | -0.19% | 3552 |
| NonBacktracking control | 323.533 | 313.270 | -3.17% | 143536 |
| PCRE2 prefix Count | 74.555 | 69.801 | -6.38% | 2256 |
| Core prefix value-Match | 285.366 | 281.609 | -1.32% | 2256 |

[All costs, spreads and identities](cost-summary.json). The primary gains have
tight ranges; imports and the FIRST control are noisier. Keep the unchanged-route
observations without causal credit. No confirmation or tuning variant follows.
The frozen Full single-capture observation is 556.85us, versus 449.71us for this
experiment's baseline: that cross-run movement is not a producer speedup. No new
CPython ratio, catalog refresh or cumulative release claim follows from this A/B.

Focused Release checks pass on Windows/Linux for .NET10/11: Core 987,
PythonRe 1732 and PCRE2 1598 per target. New checks cover exact literal work counts,
every cutoff, EOF/truncation, captures/branch rollback, assertions, atomic scopes,
folding/RTL/scalars, borrowed retained results, malformed tails, original token and
deadline cleanup. An initial deadline fixture had only 256 literals, below the
existing 2048-instruction polling interval. It was corrected to 4096 before timing;
both failing and corrected logs are retained. Production polling is unchanged.

Canonical evidence under artifacts/experiments/ascii-literal-run-20261104/:

- evidence.tar.gz: `43C19536EABD2CD408DC61A7CFE486203F669F3CF9139027C8B8B8C5AC2E9939`.
- windows-correctness.zip: `6839A71334792219A0259511AA352ED05DF328E0531CCA5CF8E0FF4BD4654FAC`.

The raw archive retains both frozen graphs, driver/binaries/corpus, source patch,
predeclaration, all samples, preflights and Linux logs. Staged packages and the
dev.24 source 00a97561 Full catalog remain immutable. No package/platform matrix
was repeated for this pilot. The unexpectedly costly Core value-Match is a separate
candidate-discovery investigation, not a reason to retune this completed loop.
