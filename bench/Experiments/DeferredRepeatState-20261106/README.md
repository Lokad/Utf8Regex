# Acquire root repeat state when execution needs it

**Accepted for allocation and usable admission benefits.** Parent
`61ae01ecced55a2c819e2f288cfd337a06227ea3`; two runtime files change. Root count/
position arrays are acquired at the first repeat that needs mutable counters.
Existing fused runs need no unused vectors. The existing stack reservation owns
their capacity; root cursors return the references to the workspace on success,
failure and assertion unwind, and retries reuse them. Assertion children retain
their separate eager arrays. NonBacktracking keeps its existing implementation.

No new field, program fact, cache, pool, engine, execution switch or accounting
framework. Major buffers are admitted before allocation; initialization polls in
bounded chunks. Strict whole-subject UTF-8/scalars, capture/history, aliases,
original cancellation/deadlines and synchronous release remain. Work/working
requirements can fall when state is avoided, and can change when it is acquired
later. The three specs and release notes describe the revised admission scope.

The frozen imports probe returns 19 matches and allocates 80B. Its 43-instruction,
four-repeat program's shared-verifier replay returns inactive vectors and no
journal entries at every match. This replay is not an unhooked production branch
census or timing claim. The real Linux corpus remains 46429B/SHA507F5A33.

One **84.884008722-second** bounded comparison includes four separate Default
producer processes in ABBA order, followed by independent .NET imports diagnostics.
Only Core DLL/PDB differs; six adapter/benchmark and both worker files are identical.
Normal eight-CPU startup precedes CPU2 affinity; default tiering/PGO, one-second/
minimum128 warmup, independent60ms calibration and five samples/row/process.
All **280 producer samples exceed57.4212ms**. Typed Count/Boolean results are
consumed and checked after timing. No confirmation, tuning or normalization.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Native literal control |12.466|12.458|-0.06%|0/0|
| Actual imports Count |11.298|11.251|-0.42%|80/0|
| Same, compiled |11.292|11.248|-0.39%|80/0|
| Same, finite/live |13.803|13.667|-0.99%|192/112|
| Same, deadline/live |15.259|15.154|-0.69%|240/160|
| Actual Python date miss |1.820|1.795|-1.41%|80/0|
| Actual named-reference FullMatch miss |0.538|0.527|-2.10%|464/400|
| Unfused compound backtracking Count |115.590|119.558|+3.43%|1056/1056|
| Captured unfused repeat Count |147.266|148.748|+1.01%|1456/1456|
| Assertion root Count |73.909|76.636|+3.69%|1072/1072|
| RTL imports Count |9343.193|9610.557|+2.86%|58312/58312|
| NonBacktracking imports Count |1631.007|1635.150|+0.25%|557872/557872|
| Unicode imports Count |77.253|76.498|-0.98%|80/0|
| Native PCRE2 Count control |49.511|49.606|+0.19%|0/0|

[All observations](cost-summary.json) retain per-process medians, ranges,
allocations and identities. Imports throughput barely moves; date and Unicode
process ranges overlap. The decision rests on eliminating unused allocations and
admitting genuinely buffer-free calls under small working ceilings. Counter-heavy,
assertion and RTL costs are accepted explicitly. Compound-unfused, RTL and assertion
after process medians exceed both before medians; do not dismiss those costs as noise. The
captured unfused estimate is less settled. No universal10% release rule or broad
parity gain is inferred. Finite imports still cost112B/2.416us above the unlimited
operation; deadline/live costs160B/3.903us more. These are observed operation costs.

Separate same-result .NET Count diagnostics report predecoded6.64us ordinary/
4.49us compiled, decode-then26.39/24.27us, allocations0/92848B. They do not update
the qualified catalogs or CPython ratios. Process peak RSS was142208–146128KiB
for producer workers and60244KiB for diagnostics. Lower per-call GC allocation is
not a promise of lower process RSS. Available RAM8356748KiB passed conservative
2GiB focused admission immediately; the unchanged Full reserve was not copied.

Linux Release .NET10/11 each pass Core41970, PythonRe4817, PCRE26522 and
differential1503 checks. Windows broad runs pass41947Core/4810Python/6521PCRE
cases before obsolete admission witnesses are repaired. Corrected focused runs
pass425Core then final67Core,112Python and23PCRE per target; differential1503
also passes. Initial failures and intermediate repair failure are preserved.
Repairs accept buffer-free successes while retaining genuine capture/overlap
storage denial, funded working loans, every work cutoff, unsigned repeat state,
assertion resume/history, retained results and original interruption checks.
Production code was unchanged during these test repairs.

Exact Core before:
`633DA3DDC5E9FDC471ED45C90A2055802F9603BB6E1260E5E016CAB367D4EDAF`;
candidate:
`9D72967C932FAC2FAC56753248D951EE6A9A69DC8D3584FB3A405307415C1B82`.

Immutable `artifacts/experiments/lazy-repeat-state-20261106/`:

- `evidence.tar.gz`: `3C6D61DFFDAC00A5BDD027EED34E43BC28E08A75AD44D267D5E3C89233B8893C`.
- `windows-evidence.zip`: `6B5FD80763CD2A5A8F09D1914120C06A2861DA7A358252F68528D78B19A0F49E`.

Raw archives retain exact graphs, source/test patches, driver/declaration,
preflights, every sample, process resource receipts and terminal zero. All eight
dev40 Full DLL/PDB identities, packages and published tables remain unchanged.
Package/platform/Full matrices stay at the next coherent delivery boundary.
