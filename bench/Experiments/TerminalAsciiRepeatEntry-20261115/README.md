# Share the bounded ASCII range scan with proved capture roots

Accepted implementation `bd21caf3`, witness correction `368e4b4f`; frozen Core
baseline `b50bb5e3` / unchanged PythonRe dev95. Complete ASCII capture roots reuse
the existing <=2KiB range scan without entering generic repeat/atom eligibility
setup for every capture. Generic ASCII repeats share that same scan. Dot, excluded
pair, bounded/scalar-counted repeats and unsupported roots keep their algorithms.
No descriptors, modes, state, ownership, public API, unchecked access or allocation
mechanism is added. The private helper and callers add 28 net source lines.

ONE **51.722s** ordinary-process ABBA, eleven public workloads, five 150ms-target
samples per process, 20ms floor, 180s cap, no confirmation. Normal eight-processor
.NET 11.0.0 startup before fixed allowed CPU2 placement. One producer per process;
Core DLL/PDB pairs differ, Python/caller/.NET controls are identical. Every compiler
document and checked managed-member inventory passes. Complete returned strings
and shapes are checked outside timing. [All observations](cost-summary.json),
[acceptance](acceptance.json). No new CPython/native parity comparison or canonical
benchmark-table update is claimed.

| Public string findall | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| Many captures | 378.400 | 362.837 | -4.11% | separated |
| Failed first candidates | 126.886 | 123.698 | -2.51% | separated |
| Lazy root contrast, affected final repeat | 193.171 | 184.497 | -4.49% | separated |
| One capture | 2648.304 | 2534.294 | -4.31% | wide, overlapping |
| One capture, finite/funded/deadline/live | 645.618 | 634.807 | -1.67% | overlapping |
| Many captures, finite/funded/deadline/live | 1192.566 | 1216.667 | +2.02% | overlapping |
| Unicode false candidates | 64.204 | 63.201 | -1.56% | overlapping |
| Long first run | 21.238 | 21.097 | -0.67% | overlapping |
| Missing delimiter | 1.259 | 1.242 | -1.32% | overlapping |
| Unchanged Core literal path / .NET | 12.186 / 22.955 | 12.152 / 22.808 | -0.28% / -0.64% | overlapping |

Accept the many-capture gain; retain finite +2.02% and all other uncertainty.
The first one-capture row ranges ~1.21–4.02ms before and ~1.07–4.12ms after, well
above the separate canonical warm-throughput observation. Its unstable timing
establishes no speedup, steady cost or updated native parity. No normalization,
causal tier/GC claim or follow-up timing campaign. Ordinary/finite allocations
are unchanged; the long row fluctuates around 131.49kB/op (~2.5 measured bytes
between medians), with no new producer allocation or exact equality claim.

Separate FullOpts code generation: root 1957→2070B, generic repeat 2176→2146B,
new shared scan 950B: about 1KiB more generated code, deliberately retained for
public capture throughput. The other disjoint-repeat method stays 5042B. These
sizes are not isolated-kernel timing or a native parity claim.

Preserve the identical original entry/<=2KiB block/completion charges and checks,
preallocation admission, minimum-one versus minimum-zero handling, Unicode stops,
capture closing order, LastIndex and retained mutable/immutable owners. Numerical
work and public contracts are unchanged. Existing every-work-cutoff, scalar offset/
tail, false/missing delimiter, retained-result, original-token and forced-deadline
tests pass: Windows net10/net11 and Linux net11 each Core951, PythonRe1408,
PCRE2 891, POSIX266. Only Core builds; only the changed interruption witness needs
its test-host rebuild, all other matching binaries are reused.

The initial Windows witness named the old private helper; its two assertions
failed to inject. The corrected witness observes either generic setup or the
shared scan and still requires original exceptions/reservations. The VM setup
then rejected authored README changes before any build/timing; preserve that
archive and reuse the SAME frozen baseline in the measurement task. Neither
setup recovery repeats a benchmark or rebuilds the baseline.

No package, solution/platform matrix, Full refresh, published/staged byte or
consumer adoption changes. Qualified dev95 remains immutable. Raw verified offVM
before keeper release in artifacts/experiments/terminal-ascii-repeat-entry-measure-20261115/;
all original workers/collectors are terminal0. Frozen baseline/setup failure live
beside their original task evidence; hashes are in acceptance. This bounded pilot
is closed; do not tune decoder, polling or repeat variants from these observations.
