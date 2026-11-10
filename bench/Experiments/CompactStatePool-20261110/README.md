# Compact the shared state pool's live entries

Accepted source0165e5cbd122a0839d73702da6f7fb0f82dcf675.

Keep the original multi-leaf pool, exact typing, reference clearing, duplicate
protection and modeled 16 KiB/type / 256 KiB/global limits. A live-entry count
and swap-removal maintain one dense prefix of the same fixed 64-entry table.
Rent, return and type accounting inspect live leaves instead of all 64 slots;
empty-slot discovery disappears. Eviction remains optional. No new owner, hash,
smaller leaf limit, allocation, public policy or resource-accounting semantics.

This preserves simultaneous same-type/capacity vectors required by PCRE2, which
invalidated the [one-leaf indexed trial](../IndexedStatePool-20261110/README.md).
Its production change and ordinary SKU figures were restored in06c25eee; do not
revive the indexed design or weaken existing zero-allocation guards.

One quiet Linux .NET11 RC3 before/after ABBA per validation policy, 14 public
lanes, five samples/process: **560 samples >=45.233ms**. Identical caller and
all three adapter DLL/PDB pairs; distinct Core. Eight CPUs at startup, affinity
CPU2 afterward. Every stored result/capture/checksum is consumed after timing;
original tokens, finite limits, deadlines and parent refunds stay covered.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| POSIX SKU |0.838 → 0.708 (-15.49%)|0.833 → 0.710 (-14.72%)|
| SKU finite/live |1.153 → 0.920 (-20.19%)|1.114 → 0.881 (-20.90%)|
| SKU funded/live |1.281 → 1.056 (-17.60%)|1.251 → 1.033 (-17.40%)|
| SKU deadline/live |1.430 → 1.183 (-17.29%)|1.364 → 1.127 (-17.38%)|
| POSIX cursor |1.083 → 0.911 (-15.88%)|1.032 → 0.875 (-15.26%)|
| PCRE2 marked backreference IsMatch |1.966 → 1.596 (-18.82%)|1.972 → 1.594 (-19.17%)|

Those ranges are disjoint. PCRE2 marked and two-capture controls remain zero B;
the unmarked backreference control retains its pre-existing 1328 B/call. Strict
unmarked control +2.15% (+0.020µs) is disjoint; trusted -2.62% overlaps. Preserve
mixed Python observations: strict strings -11.38%, bytes +10.57%; trusted strings
+6.47%, bytes -9.44%, split +8.56%, all overlapping. No Python or apparent strict
Core Count -30.13% speed claim: that baseline is bimodal/overlapping. Unchanged
strict literal moves +1.62%/-1.26%, overlapping. All measured allocation medians
remain unchanged apart from <0.04 B/call incidental traffic; destination replace
and literal stay zero. Complete process/range/allocation data are in summary.json.
Accept useful cross-profile savings with the original reuse/bounds contracts.
No confirmation or tuning campaign; no native-parity composite from these timings.

Windows focused Release per.NET10/11: Core350, PCRE2 workspace/backtracking/
resource154 (including both original zero-byte guards), Python lifetime/resource/
cursor209 and full POSIX239 pass. Graph tests independently verify dense prefix,
cleared suffix, physical byte totals, retained identities, bounds and aliases.
Linux Core350/POSIX239/PCRE2 workspace154 per target pass, including public zero-
allocation guards. Retained pool peak17824B and parent peak801/refund0 stay equal.
Linux logs/source patch/identities: `linux-checks.tar.gz`, SHA256
`C8DCDEC360E95FF55491BA380268E16D0CA0EF228F3620044649924EEF2E844E`.

The caller initially used a using declaration for a non-IDisposable PCRE2 type;
its compile failed before timing. An overbroad harness assertion then demanded
zero bytes from the already-allocating unmarked control. Keep that completed
first baseline; run only the seven missing observations after narrowing the
assertion to the two actual zero-byte controls. Caller/producers never change
after timing begins; no timed result is discarded. Seven-process remaining phase
135.715s; initial process cap30s proves total worker time <=165.715s. Repair time
is extra wall time, not hidden inside a claimed continuous 180-second run.

Baseline source06c25eee plus archived patch. Graphs/PDBs, protocol/caller, initial
failure logs, every sample and completed comparison are verified offVM:
`artifacts/experiments/compact-state-pool-20261110/evidence.tar.gz`, SHA256
`182DDAB07C8E23D7B13304F37112014F46D47249B43C28264F0DDE189AB8576E`.
Initial complete baseline/archive SHA256
`C3F9EF5283D58013F2B8CF0100822BF0AB3445ACF13E6CA414460DB7CF8302CA`.
One selected ordinary SKU refresh on the accepted source gives managed0.664 /
native0.161µs/call, paired ratio **4.10716x** (95%4.07797–4.16162), formerly4.88x.
Nine samples>=72.508ms. Finite/live0.897, funded/live1.021, deadline/live1.131µs.
Only SKU's case/four summary rows change; other historical evidence stays.
Existing result/source/PDB preflight, raw durations/orders, neighbor preservation
and generated Markdown pass; this is selective evidence, not a full current-
producer catalog qualification. Refresh graphs/raw snapshots/logs verified offVM:
`refresh-evidence.tar.gz`, SHA256
`3F837E222803574EB51E62A5C48ADE39C776EAB2DBEEA2F88DD48F88839072EC`.
Immutable published/staged packages stay unchanged; no Full/package repeat.
