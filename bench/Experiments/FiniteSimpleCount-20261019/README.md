# Reuse controlled branch discovery for finite ASCII Count

Parent958bb0e7,111-dev. The preferred fixed-width SIMD counter is unchanged.
After it declines, an existing complete nonempty ASCII branch plan can Count
through the controlled finder already used by operation cursors.35 lines reuse
that finder; no new engine, classifier, preparation, scratch owner or search.
Strict validation and the existing finite/LTR/nonfolded/nonempty/nonNB/no-context /
no-shared-declaration/no-explicit-number gates remain. Captured Count discards no
observable values; operations publishing captures retain their original paths.

Each candidate search is bounded2048bytes; existing comparison/branch/token work
and depth checks remain. Count publishes its local integer after the original
token/deadline check. No VM buffers are needed, so numeric work/depth/working
requirements can fall; no exact heap/RSS or new resource-policy promise. SPEC
records this deliberate accounting revision. Original authority, scalar semantics,
NonBacktracking and retained snapshots are unchanged. Actual SizeOf stays.

## Public comparison

Reuse the exact [previous one-off fixture](../AsciiRejection-20261019/Program.cs),
same driver45F6D773..., only Core bytes change. Quiet Xeon8573C CPU2,.NET10.0.12,
FullOpts/R2R-off,AB order, five alternating samples per lane,120ms warmup/80ms
calibration target,60s child/140s batch cap. Sole pair17.43s,all120 samples>=67.78ms,
max spread1.047x; no confirmation or further timing. Constructors/startup excluded.

Public **Count** medians,microseconds (ordinary/compiled):

| Subject | Unlimited before -> after (control) | Finite before -> after | Allocated bytes U/F |
| --- | --- | --- | --- |
| optional hit,13KB/2000matches |1729.69->1744.67 /1729.79->1740.89|1339.92->558.91 /1343.13->560.38|U1792 unchanged;F3664->112|
| optional miss,13KB/0matches |838.87->841.31 /839.68->843.34|2142.41->656.68 /2126.69->654.01|U1792 unchanged;F3664->112|
| unchanged fixed control,14KB/2000matches |7.556->7.759 /7.634->7.795|20.055->20.064 /20.209->19.990|0/112 unchanged|

Finite50M work/depth1024/64MiB working/128MiB result are experimental allowances,
not safe defaults. Hit~58% and miss~69% lower in this scoped FullOpts observation;
controls-1.1..+2.7%. Remaining scalar/unlimited and captured materialization gaps
are open. No default-PGO/PythonRe/immutable110 speedup, universal finite overhead,
whole-engine parity, Status or package-qualification claim.

[All raw samples/identities and summary](measurement.json). BaselineCore
7F240AF14BF6E780F5356B6223BF779932960F21AEB0C5D406DBDC9B47C00B90;
candidate3EF12214612E0FE2E2102BA8EA6CF5BF04859022992D809007441499D2808D1C.
Evidence archiveBD0D48AECE847942F4EBC0B9837D4609EB11B4B239D774F14780102709E8AEC1
was downloaded and SHA256-verified. Both cells finish0; quiet gateCPU2=0%,other
observed dotnet CPU tick deltas0. Staged/published bytes are untouched.

## Focused acceptance

31 new cases exercise public counts,borrowed offsets/tails,ordered/anchored/
captured patterns,scalar/context/RTL/NB decline,one-byte capacity,work cutoffs,
malformed suffixes,original terminal/post-validation cancellation/deadline,parent
loans and retained captures. The .NET10 NonBacktracking comparator can miss the
first real match after failed optional prefixes; tests comment the discrepancy
and use the stable interpreted ASCII oracle. Owned scalar NB keeps both matches.

Source receipt:artifacts/finite-simple-count-20261019/RESULT.md. The initial
full Core run was aborted by concurrent profile-build lock recovery, not counted
as a pass; validation is serialized and the complete suite is rerun. This is
source qualification, not another package/platform matrix.

The serialized full Core rerun passes39746/39746 without skips; focused570,
differential1504,Python165 and PCRE80 pass. All tested Core DLLs equal the measured
candidate. No library change follows those tests/measurements.
