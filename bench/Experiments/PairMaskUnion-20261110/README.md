# Combine short-pair masks before extraction

Accepted: shared-load candidate discovery combines its two correlated SIMD masks
before extracting bits. Full comparison checks both literals only at candidate
positions. No prepared fields, descriptor growth, allocations, unchecked access,
work charges or interruption checkpoints change. Ordered outer confirmation,
scalar/strict input contracts, captures and finite admission remain unchanged.

Exact completed-capture Tier1 bytes show two kmovq transfers per scan step before;
default Release candidate code uses korq and one kmovq, with the same checked
loads. Containing method code is10071 ->9863 bytes. This is codegen evidence,
not an additive phase timing or a new search algorithm. The earlier rejected
mask-storage/descriptor trial remains closed.

One131.794s quiet VM.NET11 RC3 ordinary-process ABBA per validation policy:
16 public execution lanes,5 samples/process,640 samples >=71.757ms. Identical
caller/corpus, one distinct Core producer/process,8CPU startup then CPU2 affinity,
no timing overrides. Every output is consumed after its timed batch. Actual river
Count retains2 results and3445 candidates/verifications. Descriptor stays408B.
No confirmation, retuning, full catalog or package qualification.

| Public Count | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| river compiled |2162.770 ->2182.499 (+0.91%, overlap)|1707.742 ->1621.824 (-5.03%, overlap)|
| river ordinary |2167.880 ->2188.791 (+0.96%, overlap)|1688.916 ->1624.848 (-3.79%, overlap)|
| river finite/live |2364.990 ->2334.500 (-1.29%, overlap)|1860.204 ->1774.936 (-4.58%, disjoint)|
| river allowance/live |2386.995 ->2378.353 (-0.36%, overlap)|2438.085 ->2343.207 (-3.89%, disjoint)|
| river deadline/live |2748.155 ->2730.451 (-0.64%, overlap)|2238.185 ->2150.865 (-3.90%, disjoint)|
| dense pair |36.910 ->35.442 (-3.98%, overlap)|37.710 ->34.484 (-8.56%, disjoint)|
| false prefixes |0.746 ->0.713 (-4.44%, disjoint)|0.691 ->0.649 (-6.05%, disjoint)|
| Holmes |109.491 ->107.376 (-1.93%, overlap)|99.881 ->91.804 (-8.09%, disjoint)|

All execution allocations remain unchanged:0B ordinarily,112B finite/allowance,
160B deadline. The supplied allowance is unused on this route: observed peak and
outstanding loan are0, not proof of a nonzero borrowing lifecycle. Preserve adverse
control evidence: trusted separator4.791 ->4.997us (+4.29%, disjoint); unchanged
strict literal0.901 ->0.857us (-4.85%, disjoint). Other control ranges overlap.
No normalization, universal speedup or conclusive unlimited-river gain is claimed.
Accept the smaller miss loop and useful trusted governed/dense gains.

Windows/Linux284 focused tests per.NET10/11 pass, including shared-anchor branch
priority, offsets/tails, work cutoffs and original interruption/lifetime guards.
Linux35 pair tests per target also pass with HW intrinsics disabled (scoped env).
No public API or accounting contract change. Published/staged bytes stay immutable.

Baseline Core SHA256
`B09AC1068B585196FD9FC7B1C43852BA6E68E918A0C1F9D9652E89F034252AA2`;
candidate `4C63CA6E86ECD0968B8CFB580A07B9508DD0179B3EAC9F2A90367E56F7730F16`.
Complete protocol, caller, patches, DLL/PDB graphs, raw observations and codegen:
artifacts/experiments/core-pair-mask-union-20261110/evidence.tar.gz SHA256
`0176B9A526CEBAAF3A3D839C0B9E4884BF9EA5331DCEF991ED55F2468AAD3BAB`;
Linux checks archive `7F79E578953F0BCAF567769957215FA774C343DE623D298F004AE17EE177A71B`.
Both copied offVM/hash verified; full summary.json is alongside. Baseline codegen
comes from the [current CPU capture](../CoreWindowCurrent-20261110/README.md).
