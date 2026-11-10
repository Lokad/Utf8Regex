# Share checked loads in short exact-pair discovery

Accepted source `86440ee007cf4dd42e0ff5fbad9bb2d7d3804ee9`. Raw forward exact
pairs of3–16-byte literals can share two checked Vector512 loads per block.
Each literal keeps its own correlated first/offset comparisons, and every possible
hit is fully verified inside the <=2048-byte primitive. Dense starts keep short
comparisons; the BCL tail and unsupported-hardware/other-shape paths remain.
No prepared fields/tables, allocation, cache, state or new execution backend.
PreparedSearcher stays408B; constructor allocation55864B stays unchanged.

This differs from the [rejected mask-storage trial](../BoundedPairPrefilter-20261110/README.md):
no nibble shuffles, extra preparation or enlarged shared descriptor. Do not revive
that trial. The accepted design removes repeated input loads without exposing
false candidates to the outer invocation loop.

One **138.743s** quiet Linux.NET11 RC3 ordinary-process ABBA per policy,17 public
lanes/policy,5 samples/process: **680 samples >=40.581ms**. One Core producer per
process; identical caller/inputs; distinct Core DLL/PDBs;8CPU startup then CPU2
affinity, default runtime settings. Actual public river route/2 results/3445
candidates and verifications agree. Separate untimed public-river-only codegen
confirms helper execution, Tier1 zmm loads/equality and managed bounds checks.
No timing process enables JIT diagnostics; no confirmation/tuned variant follows.

| Public Count | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| river compiled |3211.671 → 2171.501 (-32.39%)|2664.691 → 1636.304 (-38.59%)|
| river ordinary |3215.533 → 2172.268 (-32.44%)|2663.684 → 1633.817 (-38.66%)|
| river finite/live |3404.203 → 2400.987 (-29.47%)|2866.571 → 1935.415 (-32.48%)|
| river funded/live |3388.770 → 2385.155 (-29.62%)|3412.998 → 2511.207 (-26.42%)|
| river deadline/live |3821.155 → 2777.461 (-27.31%)|3293.180 → 2329.341 (-29.27%)|

All those ranges are disjoint except trusted finite/live. Every allocation/peak
and zero outstanding loan is unchanged. Keep adverse observations: tiny trusted
fallback0.242->0.261µs (+7.79%, disjoint); default Holmes+5.86%, Unicode+8.08%/
+8.49%, trusted dense+4.41%, trusted constructor+8.44% overlap. Name-family
-1.11%/+0.70% and unchanged literal controls mostly overlap. No normalization or
causal claim explains away local costs; accept roughly1ms useful savings/call.
The full summary preserves all17 lanes, process medians/ranges and allocations.

**Intentional accounting revision:** a combined bounded primitive replaces two
separate scan charges. Ordered outer branch confirmation, terminal publication,
original-token/deadline authority and major working/result admission remain.
No input-dependent workspace or result payload is introduced. Core SPEC records
this route-dependent reduction; exact work totals are not version-stable.

Windows Release Core42202 and Python5045 checks per.NET10/11 pass; affected PCRE2
59/target pass. Linux affected731/target exercise actual Vector512 hardware;
explicit disabled-AVX512 fallback32/target passes. Final earlier-longer-start
coverage adds16-byte versus3-byte overlaps around63/64-lane edges, alongside1024-
byte lookahead and summary checks:9 per target on both platforms. Retained aliases,
strict malformed suffixes, scalar offsets, RTL/NonBacktracking, every work cutoff,
original cancellation/deadlines and live loans remain covered by existing suites.

One selected ordinary river refresh on the same VM/engine with current .NET11
controls qualifies9 samples, minimum74.372ms. Compiled managed2166.800µs versus
predecoded compiled.NET1528.632µs: **1.41748x**, formerly2.12631x. Ordinary
managed2146.580/predecoded.NET32199.985µs is0.06666x. Only those two summary rows
and their sorted README positions change; all other rows/sources and lifecycle/
scaling evidence remain. This is Validate; no trusted-native ratio is composed.

Refresh exposed P0's policy labels preceding formerly pure JSON. Tool fix
`df3fc454` reads the final machine-result line and rejects missing rows;8 actual
mixed-policy outputs/plain-JSON compatibility/missing-row smoke pass. The first
refresh timed all samples but could not import its lost stdout result; preserve
its stderr durations. One repaired refresh completed; no retuning or full matrix.
An initial native PowerShell comma-list invocation only emitted CLI help. Neither
attempt is substituted for the completed qualified refresh.

Causal raw graphs/PDBs, source patch, public caller, codegen and every sample:
`artifacts/experiments/short-pair-shared-loads-20261110/evidence.tar.gz`, SHA256
`A3AF229132A92B5E93EF9B7F353EBFD53BD11E416892BD84A281A1BC819F529C`.
The trial graph isfab38fe7 plus the archived source patch; that exact library
change lands in86440ee0. Refresh/raw snapshots, matching graph/PDBs, corpus,
parser repair bundle, initial failed logs and Linux checks:
`refresh-evidence.tar.gz`, SHA256
`5E46EE06499CDF8E5C14E72BA9D8C3CAD7388B1CF4151CEC4BEE4BF3088179A7`,
in the same artifact folder. Derived complete summary and Windows logs accompany
these verified offVM archives. No package/Full repeat or immutable dev66 change.
The roadmap remains active for Python, remaining Core gaps and POSIX costs.
