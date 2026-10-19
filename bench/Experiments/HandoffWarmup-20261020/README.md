# Policy/handoff diagnostic warmup and sample durations

Parent bbb7ce88. Scalar.114 remains the exact READY dependency. This changes only
the existing shared policy measurement helper and its usage documentation. No
library code/bytes, package, ownership, metering, snapshot or Status changes.

The old handoff used requested counts literally. Its frozen114 observation has
samples as short as0.6568ms and1K Detailed tiering transitions up to9.792x. That
observation remains unchanged. The helper now performs at least256 public warm
calls before its existing timed warmup, calibrates each lane toward50ms using the
existing probe, then checks duration three times with sampling's GC preflight.
It rescales from the fastest check within2M calls or a larger explicit minimum.
Requested counts stay minima. Raw rows include effective iterations and elapsedMs;
alternating order, result probes/profile agreement, sinks and allocation lanes stay.
No new runner, qualification framework, switch or helper type.

## Bounded validation, closed

One implementation pilot runs the existing handoff20/3 and slice20/1 commands
sequentially on the quiet Xeon8573C VM CPU2/.NET10.0.12/default tiering. Both use
bench.ps1 SkipBuild with120s per-command caps. The first implementation lacks final
duration checks: results agree but10 samples miss20ms (minimum11.081/5.0204ms).
Retain this failure; the single probe can overestimate settled cost. Source also
shows it omits sampling's GC preflight, without proving the cause of each outlier.

The bounded correction adds the three final checks. Its one final pilot passes:

| Command | Lanes / raw samples | Minimum sample |
| --- | ---: | ---: |
| Handoff bulk20/3 |48 /144|31.4557ms|
| Slice policy20/1 |130 /130|43.4308ms|

Every result and capture/range preflight agrees, both commands exit0, and emitted
per-operation time agrees with elapsedMs/effective count. Core/PythonRe exact114
DLLs and the source-only PCRE DLL hash match before/after both benchmark-only
builds and VM runs. No library rebuild or package/platform qualification is done.
BuildProjectReferences=false reuses the matching outputs; builds4.29-6.35s.

1K Detailed spreads are1.269x unlimited /1.037x finite /1.035x token; highest
remaining spread1.614x is a cursor row. This repairs short-sample preparation and
exposes duration; it does not qualify precise warm performance for every row or
show an engine speedup. No further pilot, tuning, old-data reclassification or
closed engine/checked-access/GetText campaign follows. [Every observation](measurement.json).

Downloaded archive SHA256, both verified:

- First:E6C7008BEBB2AC804B8FA0BBF9A5451F5A47DD8DE5F509BC492C31D191258D26.
- Final:EAE8B33C4615AA8DA3A72BB7942710C7EB9CE0C2ECEE3386190119E1E1E3D697.

Private source patches, exact library/host identities, raw logs and preflight are
in `artifacts/handoff-diagnostic-warmup-20261020/`. Quiet preflights and process
exits pass. Existing commands reproduce the new preparation behavior; use matching
Release binaries through bench.ps1. These frozen receipts document the bounded
repair, not an instruction to repeat it. Full R01-R10 remains open.
