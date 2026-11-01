# Deferred PCRE2 bounded literal-window proof

**Rejected and fully reverted.** No production, test, benchmark-table or package
change is promoted. Accepted source remains the qualified dev.4 engine at
`2ebc01a7`, followed by its data/receipt commit `65c3b761` (CI41306352565 green).
Published/staged artifacts remain immutable.

The [current diagnostic](before-route.txt) confirms CandidateSearch=None and
42candidate starts/111VMsteps over the47-byte date miss. Its cold elapsed time
is not public throughput evidence. The prototype let the existing ASCII prefix
proof admit WordBoundary/NonWordBoundary as zero-width conditions, reusing
BoundedLiteralWindow for the mandatory slash1..2bytes after a possible start.
There was no execution-runner, descriptor, candidate-kind, engine or mode change.
The previous rejected LeadingAsciiSet boundary experiment was not repeated.

New tests compared public IsMatch/Count/captures with the production VM at every
scalar origin, including Unicode boundary context, offsets, embedded NUL, width
limits, UCP/options exclusions, strict malformed tails and original cancellation.
All12new checks and the full6484-check PCRE2 suite passed per runtime on .NET10/11.
The complete prototype and matching binaries are archived. After reversion the
affected project was rebuilt;104 existing route/cancellation checks passed per
runtime. Prototype-only tests were removed with the rejected runtime change.

The [initial public comparison](comparison.json) completed79.544s,15cases,
minimum53.899ms samples, unchanged allocations. Date hit/miss improved61.29%/
57.45%; finite lanes cost4.27%/4.61%, deadline lanes6.29%/7.90%. UCP control was
**+10.001231%**, with paired observations8.45..12.77%, straddling the allowance.
These observations are retained, not rounded into an acceptance.

The [sole selected confirmation](confirmation.json) completed94.622s, minimum
54.623ms. Same engine/Core DLL/PDBs; seven selected workloads, warmup extended
from1to3seconds, otherwise the same independent60ms calibration/five alternating
samples. All result batches were consumed after timing. Allocations stayed equal.

| Public operation | Before us | Prototype us | Change |
| --- | ---: | ---: | ---: |
| Date IsMatch hit |1.769|0.720|−59.30%|
| Date IsMatch miss |6.626|3.062|−53.78%|
| Finite/live hit |2.728|3.139|+15.09%|
| Finite/live miss |9.305|10.891|+17.05%|
| Finite/live/deadline miss |10.328|12.059|+16.76%|
| UCP control |2.800|3.290|+17.51%|
| Literal control |0.145|0.147|+1.17%|

Repeated public penalties above10% justify deferral despite primary gains.
Their execution cause is unresolved; do not attribute them to JIT layout or
an algorithm without evidence. No third comparison or optimization variant ran.
The full dev.4 refresh/native date ratios remain unchanged. No package or catalog
qualification was repeated for a rejected change.

Immutable raw archives under
`artifacts/experiments/pcre2-bounded-literal-window-20261101-deferred/`:

- `evidence.tar.gz`: exact before/after DLL/PDBs, source patch, original and
  confirmation drivers/binaries, preflight/build logs and raw comparisons.
  SHA256 `088D29BC36C0A88396858A6B172132DACAD103BE165E2BFDB2F24D7D4468B1BD`.
- `correctness.zip`: complete prototype source/tests, matching Windows assets
  and initial/final correctness/reversion evidence; hash in [verification](verification.json).

Keep both date-search alternatives closed in this bounded pass. Remaining N4
gaps and deferred PCRE2 scalar projection/POSIX stay open.
