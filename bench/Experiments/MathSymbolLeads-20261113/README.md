# Shared ASCII-symbol and Unicode-lead scan — rejected

Baseline17c4368c; complete prototype/tests restored. The existing256/128 ASCII
count followed by Unicode discovery remains. No new decoder, selector, mode,
framework, static-storage or accounting change survives. All484 historical
package/symbol assets remain unchanged; no Full/package/catalog refresh.

The trial reused each checked loaded vector for the same six ASCII symbol
comparisons and a Unicode-lead mask. It classified every lead once and advanced
the tail to max(full-vector-end,last-scalar-end), preserving2/3/4-byte scalars.
It removed the separate ASCII dispatcher/byte-set field and repeated non-ASCII
search. Strict whole-input validation, scalar classification,4096-byte admission,
original interruption/resource controls and allocation behavior stayed unchanged.

ONE127.011s/150s,14 public rows/two policies/ordinary-process ABBA,560samples
>=66.738ms, maximum31.537MiB timed allocation. Frozen unchanged timing caller/
PCRE2 pairs and reference; distinct loaded Core DLL/PDBs and identical inputs.
Default.NET11.0.0/tiering/PGO,8CPU startup thenCPU2. Complete Count results agree
with scalar oracles and are consumed after timing. No confirmation, normalization,
density/ISA/inlining/threshold variant or new parity Status.

| Complete public Count | Validate before -> trial us | AssumeValid before -> trial us |
|---|---:|---:|
| Real16MiB corpus |1509.584 ->1824.243 (+20.84%)|1000.601 ->1100.426 (+9.98%)|
| Funded/live real corpus |1538.616 ->1838.953 (+19.52%)|999.862 ->1186.933 (+18.71%)|
| Dense mixed Unicode |408.213 ->247.791 (-39.30%)|293.715 ->131.992 (-55.06%)|
| PCRE2 mixed Unicode |492.969 ->341.391 (-30.75%)|409.131 ->253.482 (-38.04%)|
| Small ASCII subject |.614 ->.727 (+18.35%)|.572 ->.722 (+26.09%)|

Every listed range is disjoint. Finite/deadline corpus also regresses; short
mixed improves16.03/14.28% disjoint, unchanged-route controls overlap. Allocations
stay equal (ordinary0B, finite/funded112B, deadline/PCRE2 controlled160B,
NonBacktracking232B); scoped parent peak17B and final0 remain. These modeled
loans are not total heap/RSS. [All compact observations/process medians](summary.json).

**Reject despite the substantial dense-Unicode gain:** the priority material
real-corpus gap worsens. No blanket10% veto applies. Do not preserve both
algorithms behind a density selector or reopen this as another small loop variant.
The old trusted corpus counter is already faster than the predecoded.NET control
in this focused context; default strict validity is additional work. This is not
an additive phase estimate, lower bound or current catalog ratio.

Windows/Linux Release per.NET10/11: candidateCore401/PCRE2314; softwareCore84/
PCRE24, forced256 Core84, six fixed-table/category checks. Existing ASCII mask
oracles plus new every-lane/offset/wider-scalar/tail oracles qualify semantics.
Default optimized code actually executes the new shared loop: enclosing body
1347->1660B plus841B256-bit helper; native size/engagement is not a speedup.
Both342-document Core source/PDB inventories verify, with only this source/
generated revision differing. Restored Core399/PCRE2314 pass pertarget/OS using
fresh matching Core/test binaries and reused adapters. No repeated software/perf.

Initial task-directory argument binding failed before source changes; corrected
setup is retained. Restoration normalizes verified index entries only, keeping
original compiled source bytes. All raw evidence is verified offVM before keeper
release under artifacts/experiments/math-symbol-leads-20261113/: gate67D3DD9B,
mainDF090D58, WindowsE891E118, restored668374BC, Windows-restored2E5D24ED.
Full SHA256 values live in artifact verification.json and task progress.
Close the local category-counter campaign. Remaining P0 gaps and coherent
post-dev78 downstream delivery remain active; this rejection is not completion.
