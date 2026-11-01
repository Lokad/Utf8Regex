# Deferred single-token subroutine proof

**Rejected and reverted.** Accepted production source remains the dev.4 engine
`2ebc01a7`, followed by data/receipts through `ce3a4e40`. Its Windows/Linux
CI41307940052 is green. No production, test, package or benchmark-table change
is promoted from this experiment.

The [owned diagnostic](route.txt) shows the qualified4.96x native gap is an
anchored five-byte match, `^(?1)\d{3}(a)`: one candidate and11VMsteps. Candidate
filtering is not its demonstrated cost. Cold diagnostic elapsed is not throughput.

The prototype reused the existing finite-ASCII IsMatch compiler for subroutine
targets whose lowered body was exactly CaptureStart/one ASCII Token/CaptureEnd/
Return for the same slot. It borrowed existing target/name tables, with no new
retained metadata or recursive expansion. Existing construction caps/admission,
capture operations and controlled VM execution were preserved. Numeric, relative
and named calls were checked; complex/recursive callees and capture-dependent
languages remained excluded.

New public checks covered scalar origins, strict malformed tails, original
cancellation, work denial, retained captures and exclusions. All22new checks,
34focused checks including the existing bounded compiler, and the full6494-check
PCRE2 Release suite passed per runtime on .NET10/11. After reversion the affected
projects were rebuilt;62 existing bounded/subroutine/cancellation checks passed
per runtime. Prototype-only tests were removed with the rejected proof.

An initial harness attempt aborted before completing its first row. The old
one-million-call cap could not reliably admit a fast sample. Original logs/driver
are preserved. The [calibration repair](CALIBRATION.md) raised only the disposable
result-batch cap to16million calls/64MiB and improved failure diagnostics. Engine
DLL/PDBs and all workload/protocol settings stayed frozen.

The [completed comparison](qualified-comparison.json) took **62.826s**,17public
operations, minimum30.484ms samples. Same Core bytes, isolated before/after
contexts, warm1.5s/min1000calls, independent60ms calibration, five alternating
samples and full result batches consumed after timing. No .NET comparator was
used for PCRE2-only subroutine syntax. Allocations were unchanged in every lane.

| Public operation | Before us | Prototype us | Change |
| --- | ---: | ---: | ---: |
| Subroutine IsMatch hit |0.506|0.014|−97.16%|
| Subroutine IsMatch miss |0.505|0.017|−96.69%|
| Finite/live hit |1.480|0.960|−35.13%|
| Finite/live/deadline hit |1.616|1.101|−31.89%|
| Dense unanchored search |280.32|5.91|−97.89%|
| Complex-call control |0.546|0.681|+24.80%|
| UCP control |0.632|0.757|+19.81%|
| Captured Count control |40.072|47.058|+17.43%|

The adverse controls have narrow spreads and paired penalties consistently above
10% (complex24.61..25.32%, UCP18.79..20.44%, Count16.33..18.66%). These fail the
public cost gate despite primary gains. No noise confirmation was justified and
none ran. The execution cause is unresolved; do not attribute this to JIT layout
or a particular algorithm without evidence. Keep this proof closed for the pass.

Exact binaries, source patch, both harness versions, failed attempt and completed
measurements are sealed in
`artifacts/experiments/pcre2-ascii-subroutine-20261101-deferred/evidence.tar.gz`,
SHA256 `4C51C00E6F4209647B7BCBD87C83C85FF0DC0589299BF41D7DA09A4826AF9A7A`.
`correctness.zip` preserves complete prototype source/tests, matching Windows
assets and initial/final checks, SHA256
`47FBFDE8B3B20B979589684200FB59E1E894BD4E6F2DB171FB36309DD334510F`.

No native/catalog/package refresh was repeated. Existing qualified snapshots and
staged packages remain unchanged. Next inspect shared VM execution/code generation
before selecting another change, rather than expanding this specialization.
