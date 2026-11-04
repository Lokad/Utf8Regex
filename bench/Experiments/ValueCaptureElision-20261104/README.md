# Share unobserved-capture admission

**Accepted**, source `2280e74c`. Core Boolean/count/value matching share Count's
existing conservative guard for unobserved capture values. Eligible owned scalar
programs omit capture slots and trails. The exact original exclusions remain:
backreferences, conditions, lookarounds, balancing/shared/explicit declarations,
NonBacktracking and alternate/unknown programs keep their storage. Detailed and
cursor results request every capture. No new state, mode, opcode or unchecked API.

This removes duplicated guard logic and unnecessary transient payload, retaining
all other VM buffers, major admission, original cancellation/deadline and result
authority. [The contract](../../../docs/SPEC.md) records lower route-dependent
work/working requirements. Value Match is a coordinate struct and exposes no
capture payload; the Boolean operation publishes only existence.

One **94.8412476-second** public comparison uses accepted `078cbba9` Core versus
`2280e74c`; six adapter/benchmark companion files remain byte-identical dev.24 Full
assets. Normal eight-processor startup precedes CPU2 affinity. Managed warmup is
1.5s/minimum 128 calls, diagnostic 250ms/minimum 64, independent 60ms calibration and
five alternating samples. All 180 owned samples qualify, minimum **47.7188ms**.
The typed bridge adds no boxing/result allocation; all outputs are consumed after
timing. The cursor control creates, advances once and disposes in finally. Detailed
and cursor controls extract only coordinates; focused tests verify full captures.
Projected BCL lanes are composed diagnostics, not new catalog/CPython ratios.

| Public operation | Before us | After us | Change | Allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Native control | 2.498 | 2.509 | +0.41% | 0 → 0 |
| Early value Match | 0.558 | 0.288 | -48.42% | 2256 → 64 |
| Late value Match | 0.608 | 0.381 | -37.39% | 2256 → 64 |
| Finite/live value Match | 0.886 | 0.623 | -29.71% | 2368 → 176 |
| Deadline/live value Match | 1.104 | 0.822 | -25.55% | 2416 → 224 |
| Late Boolean match | 0.621 | 0.400 | -35.49% | 2256 → 64 |
| Finite/live Boolean match | 0.883 | 0.623 | -29.48% | 2368 → 176 |
| RTL value Match | 441.685 | 440.529 | -0.26% | 5744 → 3552 |
| Unicode value Match | 1.005 | 0.742 | -26.14% | 2256 → 64 |
| Count control | 63.510 | 65.751 | +3.53% | 64 → 64 |
| Backreference control | 1.354 | 1.457 | +7.55% | 5744 → 5744 |
| Lookaround control | 1.097 | 1.178 | +7.42% | 5744 → 5744 |
| Detailed-range control | 0.844 | 0.874 | +3.52% | 2720 → 2720 |
| Cursor-first-range control | 1.185 | 1.200 | +1.29% | 2992 → 2992 |
| NonBacktracking control | 178.695 | 177.498 | -0.67% | 1416 → 1416 |
| Python FindAll control | 429.716 | 450.047 | +4.73% | 112944 → 112944 |
| PCRE2 Count control | 67.893 | 71.468 | +5.27% | 2256 → 2256 |
| Contiguous value Match | 0.570 | 0.290 | -49.21% | 2256 → 64 |

[Full costs, spreads and identities](cost-summary.json). Keep Count +3.53%,
Python +4.73% and PCRE2 +5.27% with their tight ranges. Tiny dependency/detailed/cursor
controls are noisier; their adverse medians remain without a compiler attribution.
The large allocation reductions and repeatable targeted time gains support
promotion under the user's local-cost/major-gain policy. No confirmation, tuning
variant, favorable catalog refresh or new cumulative claim follows.

Focused Release checks pass on Windows/Linux for .NET10/11: Core 925,
PythonRe 1274 and PCRE2 815 per target. Tests cover scalar/RTL/offset semantics,
dependent capture exclusions, finite admission, each work cutoff, malformed tails,
original interruption, detailed data and cursor aliases. An initial RTL fixture
used the 256B LTR allowance; mirrored repeats still need frames/journals, so its
allowance became 4096B while detailed/cursor capture storage must still deny.
All other guards remain unchanged. Initial driver local-name compile errors are
also retained. The final preflight exposed raw .NET's low-surrogate quantification
for `é(😀+)X`; explicit `é((?:😀)+)X` grouping gives overlapping comparator results.
Only that fixture spelling changed before timing; scalar semantics did not.

Canonical evidence under artifacts/experiments/value-capture-elision-20261104/:

- evidence.tar.gz: `9D6890E1532040FC11CEA3C93EED4747215B5326F09B613E7DE9DC1CE51036A1`.
- windows-correctness.zip: `66034F82EFAA62C9F86324258EA0FBE8539194EC7AE31BA2FD9C34FCB3933E02`.

Raw evidence retains both frozen graphs, driver/binaries/corpus, source patch,
predeclaration, failed/corrected preflights and all samples/Linux checks. Existing
staged package bytes and source 00a97561 Full data remain immutable. The accepted
batch now proceeds to one fresh common package/delivery boundary.
