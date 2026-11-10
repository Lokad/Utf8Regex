# Reject exact POSIX state pooling without eligible public-workload capacities

Baseline `e5e51d265589788c92e067ddcf6a9984d2e07a50`. The trial replaced
PosixWorkspace's fresh int[] acquisition/release with the existing exact-state
pool, preserving numeric admission, exact capacities, full reset and existing
retention eligibility/caps. It added no owner, cache, bucket or public policy.

**REJECTED; production fully restored.** One **121.836s** two-policy ordinary-
process ABBA under180s,11 public lanes, **440 samples >=76.875ms**. Common
Core/PythonRe/Pcre2/caller, distinct Posix DLL/PDBs;8 CPUs then CPU2 affinity;
default runtime settings. All public ranges/captures/checksums consumed after
clock; original funded parent17 returns to17 per call and0 at completion, peak801
unchanged across all8 processes. No confirmation or retiming.

| Public operation | Validate change | AssumeValid change | Allocated B/op |
|---|---:|---:|---:|
| SKU Match |+2.97%, overlapping|+0.87%, overlapping|96 →96|
| SKU finite/live |+1.28%, overlapping|+1.11%, overlapping|856 →856|
| SKU funded/live |+1.30%, overlapping|+1.50%, overlapping|856 →856|
| SKU deadline/live |+0.49%, overlapping|+2.13%, overlapping|904 →904|
| two-result cursor |+5.16%, disjoint|+2.25%, overlapping|1160 →1160|

No intended allocation benefit exists. The first audit incorrectly inferred a
16-int SKU vector from allocation size. An exact read-only compiled-program probe
finds18 integers (7 tags,2 repeat counters), cursor2 (1 tag,0 repeats), and history6
(2 tags,1 repeat). None fits the pool's16–256 power-of-two capacities. Rounded
array/GC extent does not establish element count. Keep these actual counts rather
than a manual memory-size estimate. Other patterns could have eligible capacities,
but that does not justify promoting a change failing its selected public workload.
Do not turn this into bucket/rounding variants without a separately justified
capacity/admission tradeoff; no such experiment is proposed here.

Unchanged controls remain visible: Core scalar Count+0.83/+3.22%, destination
replacement+1.04/−12.29% (trusted disjoint), strict literal+4.56/−2.94%, marked
PCRE2+4.91/+1.43% (strict disjoint), ordinary backreference+1.12/−1.13%, two
captures+0.52/−0.74%; other ranges overlap. PCRE2 marked/two-capture0B stays0,
ordinary backreference1328B stays1328. Retain all process observations and pool
charges in the derived summary; no normalization or native-parity claim.

First prototype broke release of a default workspace; existing refusal tests
caught it before timing. Null handling was fixed, then the unchanged239 Release
checks/target passed. Two independent active-workspace/reset/borrowed-loan checks
are retained. Candidate and restored production pass241 checks per.NET10/11 on
Linux; restored241 also pass both targets on Windows. Default/repeated release,
original cancellation, admission refusal, retained aliases and capture priority
remain. The retained tests exercise ownership, not the discarded pooling choice.

Raw candidate graphs/PDBs/caller/protocol/patch/samples:
`artifacts/experiments/posix-state-reuse-20261110/evidence.tar.gz`, SHA256
`6DB85343A9E6CB6429B25884F9275C30A3B8E6EE64E285D254044A8FA47DF0CF`.
Candidate Linux checks SHA256
`91D0FA1EDA01CBA4ADC95134C5DC67EE18B95729540077639DA33960C4A03C2D`;
restored Linux checks SHA256
`FCEE9D26B72A1312A9796837549356B73F4017BF670BB4AEC199212F0E334E65`.
All are offVM/hash verified; summary and exact read-only probe results are beside
the archives. Probe source/observations remain in tmp/posix-state-reuse-20261110/
probe and exact-capacities.jsonl. All workers closed. No production pooling,
package/full refresh, revised resource contract or immutable-artifact change.
Return priority to supported larger absolute gaps rather than pool tuning.
