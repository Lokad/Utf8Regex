# Deferred committed-close compaction

**Deferred; production and trial tests restored.** Baseline Core/Python is frozen
dev59 `d6120197`. An assertion-free latest-value root with no live frame, balancing
or opening journal can retain only its latest actual close in the existing trail.
Later frames bookmark that committed marker. History/assertion drivers keep every
entry. The trial adds no second journal, program metadata or execution mode.

Untimed production probes confirm the live opportunity: the actual two-capture
scalar program has zero frames and2 close records; compaction retains1 with the
same slots/LastIndex. Four sequential captures fall4→1, nested outer-last order
remains correct, and a repeated ambiguous program with live frames retains its
original10 records. These are state observations, not public-operation gains.

One **71.601-second** ordinary-process ABBA on the quiet .NET11 RC3 VM, within the
declared180s cap. Identical Python adapter and worker, changed Core only. Five
samples/process/lane after500ms warmup and independent75ms calibration; all360
samples exceed27ms. Full tuples/strings are checked after timing. Funded output
preserves its existing compiled-program loan exactly and reports temporary peaks
above that baseline. No confirmation: the storage reduction is demonstrated,
but the main consumer improvement is not established.

| Public operation | Before (µs) | Trial (µs) | Change |
|---|---:|---:|---:|
| Actual many-capture strings | 813.62 | 833.53 | +2.4% |
| Actual many-capture strings, finite/live | 1193.22 | 1178.96 | −1.2% |
| Range cursor, finite/live | 1072.63 | 1124.90 | +4.9% |
| One-capture control | 471.51 | 478.10 | +1.4% |
| 64 captures, strings | 62.95 | 61.59 | −2.2% |
| 64 captures, finite/live | 102.29 | 104.96 | +2.6% |
| 64 captures, funded/live | 115.31 | 116.86 | +1.3% |

The64-capture lanes allocate1152B less (35224→34072B unlimited); funded temporary
peak falls59968→58816B. Ordinary catalog lanes have unchanged allocations. Core
Count moves−1.3%, early search−0.9%, FullMatch−3.5%; preserve these controls without
normalization. Small process/sample ranges overlap. The trial's extra hot-path
branch/helper and limited storage win do not justify promoting it toward the
principal captured-output gap. This is not an application of a universal10% gate.
[Summary](summary.json) retains process medians, ranges, allocations and parent
loans. No source tuning, second timing, package or catalog qualification follows.

Focused Core checks pass5299 per target on Windows/Linux, including capture
history, rollback, assertions, balancing, RTL/NB and original resource authority.
Whole PythonRe passes5013 per target/platform. Trial cases pin1 retained committed
close across2/31/32/33/65 groups while preserving all public capture histories;
four independent completion cases exercise post-commit failed branches, nested
outer-last order, empty and supplementary captures. They remain in the raw patch
rather than testing a reverted storage requirement. Two initial test builds used
incorrect Core group-coordinate member names; the final test uses IndexInBytes.
No producer change was needed for those build errors; their logs are preserved.

Raw protocol, fixed driver, probes, exact source/tests patch, matching graphs,
Linux checks and observations are copied off VM at
`artifacts/experiments/committed-capture-close-20261109/evidence.tar.gz`, SHA-256
`DE6FE15420186178FF9294F1C809EE0F3F969DCC63C4630B9F8181E9C27E5BE1`.
Windows logs are alongside it. Immutable packages/snapshots and the accepted
conditional proof stay unchanged. Do not repeat this exact trial merely because
the capture-output ratio remains open.
