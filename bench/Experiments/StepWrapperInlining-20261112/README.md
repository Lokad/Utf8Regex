# Existing step-wrapper inlining — rejected

Restore the two-line annotation change on 380434c3. It removes executed calls but
does not establish a useful public gain. There is no changed accounting policy,
new execution mode, layout, allocation mechanism, owner, API or algorithm. The
existing wrapper body, per-step units, original token/deadline, saturation/sticky
denial, checkpoint hooks and current-owner selection remain identical.

An initial read-only gate reuses exact qualified dev.73 Core/Python bytes and the
existing public FindAllToStrings caller. One 1.2s diagnostic verifies every 2048
two-capture tuple. It confirms a final Tier1 call to invocation.TryChargeStep in
RunSegment's main instruction loop and four other sites. Core source is unchanged
from EAE through380434c3; loaded pairs/input hashes verify. This is attribution,
not public throughput evidence or a replay of the closed cursor-factory/token
experiments.

Adding AggressiveInlining only to the existing invocation wrapper removes those
five calls. Final Tier1 RunSegment grows 12846→15766 bytes, **+22.7%**; block-input
checkpoint calls remain. MoveNextCore changes 7454→7439 bytes. The standalone
wrapper/budget bodies still compile as 295/213 bytes. These code-generation facts
do not establish a speedup or justify adding hints to more helpers.

Windows/Linux Release checks pass per .NET10/.NET11 target: Core 761, Python 1030,
PCRE2 213 and full POSIX 361. Existing step/saturation/first-denial, original-token
cancellation, absolute deadline/hook, admission/refund, validation, scalar/capture
and cursor-alias tests are unchanged. No new tests mirror the annotation.

One **133.068-second** fourteen-row, two-policy public ordinary-process ABBA
comparison retains560 samples, minimum 65.064ms, maximum 68.162MiB result batch.
Each row warms separately; 75ms calibration target/20ms qualification floor.
Runtime .NET11.0.0/default tiering+PGO, eight startup CPUs then CPU2 affinity.
Complete results are consumed after timing. Typed caller/Python/.NET bytes are
identical; Core DLL/PDB pairs are distinct, with staged/loaded hashes verified.
Both PE/PDB identities and 342 source documents per producer verify, including
six generated observations and exactly one changed production source. Separate
Windows raw and Linux LF source manifests preserve actual PDB identities.

| Public operation | Validate before → after | AssumeValid before → after |
|---|---:|---:|
| Many-capture strings, 2048 matches |674.550 →610.482µs|614.958 →714.277µs|
| One-capture strings, 1024 matches |225.210 →213.083µs|209.466 →236.554µs|
| Many-capture UTF8 |645.189 →648.658µs|650.575 →641.436µs|
| Literal Split strings |250.475 →215.121µs|212.569 →237.435µs|
| Finite/live many strings |939.987 →875.845µs|896.500 →926.574µs|
| Funded/deadline/live many strings |1604.103 →1506.777µs|1537.346 →1552.902µs|
| Detailed durable cursor |1128.194 →1240.904µs|1247.762 →1364.296µs|
| General Core captured Count |434.086 →435.080µs|427.457 →418.522µs|

All rows above have overlapping ranges. Main many-string medians move −9.50%
strict but +16.15% trusted; one-string −5.38/+12.93%, Split −14.11/+11.70%.
These are unresolved movements, not qualified gains or stable slowdowns.
Trusted Unicode output +8.95% is the only disjoint row; its strict −2.30% overlaps.
Retain trusted holey/folded +19.41/+10.33%, cursor +9.99/+9.34%, long-field
+0.04/+0.43%, native Core −4.57/+1.21% and .NET −0.36/+0.16%, all overlapping.
[All rows and individual process medians](cost-summary.json). No normalization,
native ratio, phase-derived speedup or blanket 10% veto.

Allocations stay unchanged throughout: many strings/UTF8 242008B, one 45168B,
Split 159896B, finite 246208B, funded 246280B, cursor 607144B and Core Count 0B.
Modeled shared peak 357073B and program/parent holding 45425B remain unchanged;
the original 17B parent is refunded and final loans are 0. This is not heap/RSS.

Reject the extra generated code without a consistent important public benefit.
No confirmation, attribute variant, accounting bypass or further timing. Restore
the original single source and matching four-profile test binaries on both OS/
runtimes. Restored Core 328/Python 245 focused checks per target/OS pass; PCRE2/
POSIX both-target builds pass. No annotation, test-only mode or contract change
remains. Measured producer pairs are preserved unchanged.

Canonical offVM raw evidence:

- Read-only audit, artifacts/experiments/vm-metering-audit-20261112/evidence.tar.gz:
  BB05AE1C7A6C14C1F5000271A42746B98BD897940DDF177035C9AD1AF7CC4DFE
- Under artifacts/experiments/step-wrapper-inline-20261112, codegen/checks:
  1577F9AFB875ACBB0009D632FFD792DDEED25EDF3E75E538DE137E0D205AB01B
- Comparison/source:E502436C19CAF2CB9F94B825D706E1C4EC29BA0322DD5E17C8F78960899AD618
- Windows candidate:5A942F58D435C557867F0AC5D415769D3DF56FDA4E2FC4D43C18792AAD00C8C8
- Linux restoration:86D7259957076D4ADB8D55BA8271559C7108330894CA865660835A82FA120CC9
- Windows restoration:9F85B3822292898C2DD13A30A096D0FD33C4AE0EF0CDEF2E456200F9435CE0EB

Hashes verify; all workers/collectors are terminal with keepers released. No
Full, package or catalog rerun and no immutable artifact/table changes. Broader
shared VM/output and POSIX history work remains open; removing a helper call
alone is not sufficient evidence for another annotation experiment.
