# Hex-color value Match

Accepted source **8a471caa** reuses the existing complete optional-# / three-or-six
hex-digit proof and `Utf8AsciiHexColorExecutor` in the existing ASCII value-Match
envelope. It adds no grammar, engine, helper allocation or accounting mechanism.
Whole-subject strict validation and the original invocation's before/after work,
cancellation and deadline checks remain. Detailed captures, non-ASCII subjects,
nonzero origins, RTL and NonBacktracking retain scalar execution.

The [inspection](initial-route.txt) establishes the existing direct-family proof;
its cold planning labels/timings are not throughput measurements. The actual
catalog has six calls / 39 bytes. [Driver](driver.cs.txt), [project](driver.csproj.txt),
[raw comparison](comparison.json) and [console](comparison.txt) compare exact
c9d9b685 baseline and 8a471caa DLLs on the quiet VM, .NET 11 RC3, CPU2. One
**68.21s** public A/B under a 120s cap: fourteen rows, one-second warmup,
independent calibration and three alternating samples, with equal per-subject
results and checksum consumption. Both predecoded and decode-then-.NET lanes use
the same flags and operation. No confirmation was needed.

| Public loop | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Catalog Match, ordinary | 6.739 | 0.885 | 34560 / 0 |
| Catalog Match, compiled | 6.565 | 0.874 | 34560 / 0 |
| Catalog Match, finite limits/live token | 6.912 | 1.248 | 35232 / 672 |
| Match misses | 11.878 | 1.241 | 51840 / 0 |
| Match misses, finite limits/live token | 12.324 | 1.631 | 52848 / 1008 |
| Three-digit/final-LF Match | 4.425 | 0.565 | 23040 / 0 |
| Unicode control | 5.204 | 5.018 | 28800 / 28800 |
| Wider-alphabet proof control | 5.042 | 4.802 | 28800 / 28800 |
| NonBacktracking control | 6.950 | 7.161 | 10032 / 10032 |
| RTL control | 5.963 | 6.135 | 34560 / 34560 |
| Detailed-capture control | 7.253 | 7.012 | 37344 / 37344 |
| Count control | 5.119 | 5.175 | 21408 / 21408 |
| IsMatch control | 6.193 | 6.263 | 34560 / 34560 |
| Literal control | 0.223 | 0.220 | 0 / 0 |

The three candidate detailed-control samples ran 17.26-19.27ms after calibration;
retain them as limited diagnostic evidence, without a performance claim for that
unchanged path. All other A/B samples exceed 20ms. The largest control increase
is +3.0%; negative control variation is not an extra optimization claim.

The [selected V3 refresh](selected-refresh.txt) changed only hex color's two mode
rows; five samples per lane, minimum **41.60ms**, exact clean source/PDB/DLL
identities retained. Compiled producer / predecoded .NET improves **11.31x ->
1.49x** (still Gap), with zero producer matching allocation. Matching ordinary
flags give **0.96x**, Win. The historical ordinary row's 11.07x used compiled
.NET; its denominator correction is separate from the producer gains established
by the A/B. Other historical script rows and their README caveat remain.

[Validation](verification.txt) passes on .NET 10 and 11: complete-grammar and
near-miss proofs, branch order, case folding/cultures, final LF/strict anchors,
offset spans, Unicode, retained groups/cursor aliases, work denial, zero capture
workspace, malformed tails and original-token/deadline checks, plus adjacent Core
resource/public-operation and PythonRe/PCRE2 guards. Two initial fixture mistakes
(enum name and a 64-step hook unreachable by this tiny operation) were corrected;
initial logs remain archived. Snapshot/API/managed-inventory checks and the
existing parity-report verifier pass. No full catalog/package matrix was repeated.

Immutable files under `artifacts/experiments/hex-color-value-match-20261029/`:

- `parity-hex-color-8a471caa.tar.gz`: exact baseline and candidate binaries/PDBs,
  host, source bundle, driver/binaries, build/preflight/raw timing and selected
  snapshots. SHA256
  `1A765CD622F71CA7BA1311656DFE68D603B3DB579FB0DF32D3BF30F1C5CA384D`.
- `correctness.zip`: initial/final correctness and verification logs. SHA256
  `4F7518A2A6E1886B10E0F7938E9FE68C32BA6E59A8216A0BF2E45DB6121D810D`.
