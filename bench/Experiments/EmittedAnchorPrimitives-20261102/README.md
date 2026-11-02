# Small anchor span search: rejected

**Reverted.** Replacing small emitted anchor-set searches with span `IndexOfAny`
slowed the actual keyword corpus **17.76% ordinary /18.90% compiled**. Dense hits,
boundary failures and other representative workloads also exceeded10%. Sparse
misses improved, but the public gate failed. No confirmation or variant followed.

Read-only current cb1d443e binaries confirm the actual53,352-byte ASCII corpus,
Count73 in both modes, route `NativeStructuralFamilyEmitUpperWordIdentifier`,
anchor offset3 and exact bytes `osu`. Those noncontiguous bytes use the general
checked nibble-table scanner. The candidate substituted checked span membership
search for sets of at most three existing anchors. Larger sets, full prefix and
identifier verification, candidate order, boundary skipping and controlled routes
retained their algorithms. No state, buffer, engine, emission, accounting or
construction change was added. This was separate from the closed packed literal-
prefix filter and global short-triple selector trials.

One **67.333s** quiet-VM .NET11 RC3/CPU2 comparison reused the existing18-case
public driver. Baseline Core DLL/PDBs match cb1d443e; candidate Core was built from
data-only parent2b6c0d8d plus the archived patch. Normal eight-CPU startup preceded
in-process affinity. Four equal-result lanes, independently calibrated toward60ms,
500ms warmup each lane and five alternating samples. All360 samples exceed20ms;
minimum **53.066ms**. Integer output batches were retained and checked after
timing, capped independently of transient allocation traffic. Completed JSON rows
were flushed immediately.

| Public operation | Before us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Actual keyword Count, ordinary |66.36|78.15|+17.76%|
| Actual keyword Count, compiled |66.05|78.53|+18.90%|
| Dense hits |35.69|40.20|+12.66%|
| Boundary failure |35.76|40.61|+13.56%|
| Two anchors |49.16|55.16|+12.21%|
| Larger anchor control |15.21|17.67|+16.20%|
| Sparse anchor miss |2.91|1.33|-54.44%|
| Dense anchor miss |3.03|2.60|-14.31%|
| Literal control |6.33|6.33|+0.06%|

All allocation medians were unchanged. Finite/live/deadline controls moved by
+0.60/+0.79/+1.30%; their routes were unchanged. See the
[complete comparison](comparison.json), [summary](summary.json) and
[verification](verification.json). The exact cause of the target/collateral cost
is unproven; this does not establish that checked BCL primitives generally regress.
No fixed-original, catalog Status or cumulative acceptance claim is made.

An additional [untimed locality census](anchor-locality.json) retains all necessary
boundary candidates in order. Only10.82% of its corpus anchor hits are at the
current byte;30.19% fall within four bytes. It omits full-match advancement and
does not establish actual public Count helper-call frequency or throughput. This
does not justify another near-hit peeling pilot. Its source/build/output archive
is `artifacts/experiments/emitted-anchor-locality-20261102.zip`, SHA256
`34786E5B04BBCEF3B4E0EEE25EF5DB53CD2E701D8785558383A36B87AD16BBB9`.

Focused Release Core300/runtime passes on .NET10/11. Production emitted tests
cover one/two/three/four-anchor prefix families, unaligned spans, nonzero offsets,
tails, complete Count/FindNext sequences and public strict validation/interruption.
Both runtime/test changes were reverted locally and on the VM. Restored canonical
builds and299/runtime focused checks pass. No full suite, adapter matrix, catalog
refresh or package qualification followed the rejection. Immutable dev.12 bytes
and benchmark snapshots remain unchanged.

Immutable archives under `artifacts/experiments/`:

- `emitted-anchor-primitives-20261102-rejected/evidence.tar.gz`: exact engine
  DLL/PDBs, route proof, driver, patch, corpus, raw measurements and restored build.
  SHA256 `764F4DC365229F1F1FDAB2540FCE560DE8DC9083C0E7E16F41D915B80423DBAD`.
- `emitted-anchor-primitives-20261102-correctness.zip`: declaration, candidate
  source/test snapshots, local checks and comparison.
  SHA256 `F8BC4CF1480A1A2E55B9A06AD406F7F0757A7DBC60D1C6AAAD077E015D0D4D0E`.
