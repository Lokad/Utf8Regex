# Indexed shared-state retention

Accepted: replace hot 64-entry scans, identity searches, per-type summation and
clock eviction in `Utf8ExactStatePool` with one indexed slot per exact array type
and capacity. Keep the same shared owner, fixed table and nonblocking lock.
No additional cache, ownership layer or execution mode.

Retention is optional: each of the five capacities can retain one leaf of at
most floor(16 KiB / 5) modeled bytes. This proves the existing per-type ceiling;
64 leaves plus the table also fit the 256 KiB global ceiling. Collisions evict
and larger leaves are dropped, so some workloads may allocate more. Full active
buffer admission, exact array typing, reference clearing before publication,
exclusive loans and finite calls' no-new-retained-root rule are preserved.
Core SPEC documents the retention change; public limits/semantics are unchanged.

One **145.074-second** quiet Linux .NET 11 RC3 separate-process ABBA comparison
per input policy: 13 public lanes, five samples/process, **520 samples >=40.809ms**.
Eight CPUs at startup, affinity CPU2 afterward; identical caller, Python/POSIX
companions and inputs; distinct Core DLL/PDB pairs. Typed results are retained
outside the clock and every capture/checksum is checked after timing. Governed
lanes retain live original tokens, finite limits, deadlines and parent refunds.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| POSIX SKU |0.846 → 0.740 (-12.59%)|0.822 → 0.707 (-14.05%)|
| SKU finite/live |1.137 → 0.945 (-16.86%)|1.088 → 0.915 (-15.94%)|
| SKU funded/live |1.283 → 1.083 (-15.55%)|1.234 → 1.058 (-14.27%)|
| SKU deadline/live |1.410 → 1.215 (-13.84%)|1.340 → 1.154 (-13.92%)|
| POSIX ambiguous captures |0.619 → 0.496 (-19.94%)|0.594 → 0.486 (-18.14%)|
| POSIX cursor |1.078 → 0.937 (-13.04%)|1.037 → 0.894 (-13.79%)|

SKU and ambiguous-capture ranges are disjoint; cursor ranges overlap. Trusted
Unicode-prefix captures improve 3.76% with disjoint ranges. Python string/byte/
split, Core scalar count/replacement and unchanged strict literal controls mostly
overlap. Retain adverse observations: strict Python split +4.51%, trusted Core
replacement +4.63%, strict literal control +2.77%; no normalization or claimed
Python gain. Complete process medians/ranges and allocations are in summary.json.
Measured allocations remain effectively unchanged (occasional fractional traffic
below 0.04 B/call); destination replacement/literal stay zero. Retained pool bytes
peak at 13,600 versus 15,648; parent peak 801 bytes/refund to zero stays unchanged.
These bounded workloads do not promise collision-free reuse in other processes.
Accept the useful per-call savings and simpler invariants; no confirmation/tuning.

Windows Release: full Core 42,205, Python 5,045 and POSIX 239 checks per .NET10/11;
affected PCRE2 resource/cursor/frame checks 102/target. Linux pool/ownership/
concurrency/resource/native-fast-path checks 350 and full POSIX 239 per target.
No complete refresh/package repeat; immutable dev.66 artifacts remain unchanged.

Baseline source `0c76549905739cc0ac06fef4f91c2717b86d572b`; frozen before library
metadata is `86440ee0` with the same production C#. Candidate is baseline plus
the archived patch. Graphs/PDBs, caller, protocol and every observation:
`artifacts/experiments/indexed-state-pool-20261110/evidence.tar.gz`, SHA256
`2FD5ADF26A2F146530AF90942DD2D416F04FF4552BA51B5DE74C4EDB8C68D415`.
Linux checks: `linux-checks.tar.gz`, SHA256
`582943EAABAE624A5D541B9EFAD4DAA691AA3B03345F60C9A55CD543BA3F07BA`.
Initial launcher used Worker.dll instead of the actual friend-assembly filename;
it stopped before timing. Preserved initial archive SHA256
`39355AFF73B34FEAD71DEA04D75201731993B16BE971436E59CC0055A65C3A7C`.
Correcting only the launch path reused both frozen graphs; no timed run discarded.
