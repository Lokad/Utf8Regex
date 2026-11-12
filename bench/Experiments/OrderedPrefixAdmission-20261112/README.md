# Reuse ordered-window prefix admission — accepted

Baseline e583e497. The ordered-window branch already charges/checks its original
invocation before comparing a prefix. A checked span comparison now uses that
admission for at most2048 bytes; longer prefixes retain the shared chunked helper.
Keep all branch-order retries, boundaries, scalar gap widths, candidate search/
SIMD, allocation, lifetime and publication checks. No new plan, flag, executor or
framework. Numerical work can fall; basic bounded metering is preserved.

## Public costs

ONE97.705s quiet Linux.NET11.0.0 comparison:14 rows/two policies/ordinary-process
ABBA,560 samples, minimum56.360ms, maximum timed allocation8.101MiB. Common caller
and.NET reference, explicitly distinct staged/loaded Core DLL/PDBs. Normal eight
startup CPUs thenCPU2; default tiering/PGO,300ms row warmup/first1s,75ms independent
calibration/20ms floor/150s cap. Complete values consumed after timing, including
all1024 byte ranges collected through public live-token enumeration. No
confirmation, package/Full refresh or new benchmark Status.

| Public operation | Validate us, before -> after | AssumeValid us, before -> after |
|---|---:|---:|
| River compiled Count |2264.400 ->2198.065|1620.756 ->1579.391|
| River ordinary Count |2257.994 ->2189.838|1610.979 ->1574.824|
| River live-token Count |2427.078 ->2330.120|1757.193 ->1729.212|
| River deadline/live Count |3191.933 ->3035.622|2547.097 ->2440.942|
| Dense window Count |153.233 ->139.404|147.927 ->140.181|
| False-prefix window Count |68.011 ->60.795|63.809 ->61.082|
| Native live-token enumeration/collection |260.091 ->244.512|256.573 ->247.123|
| Native live-token IsMatch |1.031 ->1.019|.952 ->.937|

Compiled/ordinary river, both deadline rows and dense Count have disjoint ranges:
river2.24–3.02%, deadlines4.17–4.90%, dense5.24–9.02% lower. Strict live-token
river3.99%, false-prefix10.61% and enumeration5.99% are also disjoint; their trusted
counterparts overlap. IsMatch overlaps under both policies; no reliable gain.
Tiny windows-1.69% strict overlap/-6.16% trusted disjoint remain separate workloads.

Unchanged-route finite/funded controls overlap (-2.10/+0.40% and-1.73/-.34%).
Overlapping-prefix scalar control-5.31/-7.53% is disjoint but not attributed to
this helper. Unchanged literal Count+11.20% strict (~14ns)/-9.23% trusted is disjoint;
retain that process movement and accept its small absolute cost. Predecoded.NET
-.14/-2.53% overlaps. Do not normalize affected rows through those controls.
[Every observation, process median, range and identity](cost-summary.json).

Allocations stay equal: ordinary0B, live/finite/funded112B, deadline160B and
caller range collection8328B. Caller peak remains its held17B; final loans0.
These are scoped allowance observations, not total heap/RSS bounds.

## Correctness and evidence

Windows/Linux Release1004 focused checks per.NET10/11 target and software285
pass. New varied ASCII prefixes2047/2048/2049/4095/4096 cover borrowed offsets,
Unicode scalar gaps, long-prefix continuation and every work cutoff without
partial publication. Existing tests cover branch precedence, boundaries,
original-token/deadline phases, retained cursor results and caller reservations.
342 PDB documents per side verify except the intended source/generated revision;
DLL/PDB identities match. Six public Count/IsMatch/enumeration probes preserve
results, native candidate/verifier counts and validity authority. Probe Work
fields are the last observed64-step diagnostic checkpoint, not total work:
Count89728 ->82880 strict/81920 ->75072 trusted, enumeration1536 ->1472;
tiny Boolean calls observe0 in both pairs. No kernel-derived public speedup claim.

The initial fixed-repeat boundary fixture normalized onto another route; a varied
literal plus variable scalar gap establishes real ordered-window coverage. The
initial probe checker incorrectly required a smaller observed checkpoint for tiny
Boolean work; it was corrected for64-step granularity. Both corrections predate
performance qualification. All460 staged packages/symbols and generated tables
remain unchanged; no publication or original consumer adoption.

Canonical raw at artifacts/experiments/ordered-prefix-admission-20261112/:

- gate-evidence.tar.gz:B89C27AA8FA68249902DCE8C8B6A6D006CB80F5920922BBFDD334C03B96E5767
- public-evidence.tar.gz:5B612ED588D06A17E9BAAE300A8999ADBD3A8A994F6096433785590ABD822435
- windows-evidence.tar.gz:24F1350A42727AF984A46652283057C0A411B846A7837085EC7E5FAE624EE563

Both remote workers terminal0; archives verified offVM before keeper release.
This modest improvement leaves wider Core/Python/POSIX goals active; no sequence
of additional prefix/checkpoint micro-variants follows.
