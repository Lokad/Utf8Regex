# Omit unused journals around fused class repeats

Accepted source `a861c90d` runs the existing no-rewind proof before activating
repeat counters. Accepted pure-class fusion never revisits those counters; it
continues after the proof's validated RepeatExit. Both unused journal writes
disappear. Declined paths keep normal initialization/body/reset, with no new
proof, state, opcode, storage framework, mode or public API.

The [before](before.json)/[after](after.json) untimed actual-VM census confirms
1680→64allocated bytes for the short FullMatch program. Conservative working peak
falls1741→109bytes including the caller's13bytes; cleanup returns exactly13.
The96byte counter vectors remain admitted. The64byte journal table and1568byte
chunk are no longer acquired. These are diagnostic capacities, not public timing
or process-heap bounds. Sizes use existing actual SizeOf queries.

[One public comparison](comparison.json),91.727s,16cases/four lanes, minimum42.490ms.
Exact frozen `c909b8f5` Core versus that source plus the archived patch; PythonRe
bytes are identical. CPU2/.NET11RC3, isolated contexts/typed calls, actual-loop
warmup1s/min1000calls, independent60ms calibration, five alternating samples.
Results are checked after timing. FullMatch/.NET anchored Match consume success
and UTF-16 ranges; Core lanes consume Counts. Both .NET predecoded/decode-then
lanes use overlapping semantics. No confirmation or variants.

| Public operation | Before us | After us | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Short FullMatch miss |0.580|0.368|−36.50%|1680/64|
| Short FullMatch hit |0.563|0.386|−31.51%|1680/64|
| Finite limits/live token |0.696|0.482|−30.84%|1800/184|
| Deadline/live token |0.862|0.606|−29.66%|1848/232|
| Mixed BMP scalars |0.678|0.499|−26.38%|1680/64|
| Nonzero byte offset |0.626|0.459|−26.61%|1680/64|
| Core folded-class Count |101.537|88.775|−12.57%|1680/64|
| Unchanged literal Count |6.382|6.393|+0.17%|0/0|

[All costs, allocations, spreads and paired observations](summary.json) remain.
Primary gains hold in every pair. Largest adverse median1.48%; captured-body
spreads1.150/1.079 and maximum adverse pair6.62% remain uncertainty. Other controls'
allocations are unchanged; long-exact also falls1680→64B. Separate parent-trial
gains must not be multiplied or substituted for the fixed-original delivery gate.
No CPython/catalog Status refresh or package qualification ran for this slice.

Full Core41159 and PythonRe4515 checks pass per runtime on .NET10/11. Existing
capture/history/backtracking, required ends, aliases, finite cutoffs, admission,
funded lifetime and original interruption checks remain. New public tests show
256byte working admission,64byte vector denial and continued capture-scratch
denial after eager preparation under its separate construction authority. Their
initial fixture build used a nonexistent Compile overload; corrected logs remain.

Per-scalar/classification work and final original-token/deadline checks remain;
major allocations still require admission. Numerical work/capacity costs decrease
under the existing route/version-dependent policy. A declined proof can now spend
work before ordinary journal admission, revising resource-denial order while
preserving the first actual denial. [Core resource contract](../../../docs/SPEC.md)
clarifies the reduced capacity. Strict UTF-8/scalars, captures, RTL/NonBacktracking,
retained results and useful work/depth/working/result limits are preserved.

Immutable artifacts under `artifacts/experiments/fused-repeat-journal-20261101/`:

- `evidence.tar.gz`, SHA256 `61BF1DDF09B826DBA8AE664FD828D6ABFFD55DB0B29F0C37156F51C3550A2B51`.
- `correctness.zip`, SHA256 `4661A6FFB3EA792F868B8BAF80162087E96F2133F9DD73336F63B96978810BC8`.

The archive retains binaries/PDBs, parent source/applied/source-test patches,
driver/probe sources and identities, preflight and raw output. Staged/published
packages and authoritative snapshots remain unchanged. [Verification scope](verification.json).
