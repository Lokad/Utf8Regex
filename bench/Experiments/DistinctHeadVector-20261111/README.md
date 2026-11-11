# Correlated short distinct-head search

Accepted. Existing SingleLiteralBuckets search consumes correlated head/common-
anchor masks for three/four literals of 3–16 bytes on Vector512 hardware. It probes
the first eight starts before vector setup, verifies complete literals in order,
and consumes false candidates locally. Other eligibility and remaining tails use
the existing direct loop. No new prepared field, allocation, strategy, public
mode or control authority. Every vector read uses checked spans/MemoryMarshal.
The two-literal caller shares only the mask operation and retains its algorithm.

One 112.301-second quiet-VM .NET 11 RC3 ordinary-process ABBA under Validate and
AssumeValid: 13 public lanes, 520 samples >=72.520 ms, 75 ms calibration target
with one calibration confirmation, 160-second cap. Distinct Core DLL/PDB pairs;
identical caller/PythonRe/Pcre2 assets and unchanged counter eligibility. Eight
processors visible at startup, then CPU2 affinity; Vector512 active. One bounded
worker admits 1 GiB actual available RAM. Results/checksums consume outputs after
timing. Baseline 8486bfd541112fcc8fccda3aff2b591a6996973c plus archived producer patch.

| Public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| compiled name Count |3750.685 ->1580.322 (-57.87%)|3220.598 ->1049.015 (-67.43%)|
| uppercase corpus/pattern |43582.225 ->2857.681 (-93.44%)|43154.800 ->2343.912 (-94.57%)|
| lowercase corpus/pattern |43682.975 ->2837.122 (-93.51%)|43202.875 ->2336.668 (-94.59%)|
| dense four-name hits |246.208 ->181.372 (-26.33%)|245.912 ->180.173 (-26.73%)|
| rejected prefixes |219.267 ->37.631 (-82.84%)|220.206 ->37.724 (-82.87%)|
| Pcre2 name Count |3921.508 ->1601.823 (-59.15%)|4572.482 ->2210.259 (-51.66%)|

All listed ranges are disjoint. Allocations stay zero. These are scoped public
producer comparisons, not a regenerated ordinary catalog or general profile ratio.
Finite/live (-2.10/-0.09%), funded (-0.52/-0.45%), five-literal neighbor (+1.01/
-2.49%), two-pair control (-0.78/+1.28%), predecoded .NET (+0.25/-1.37%) and Core
literal (-2.17/+0.56%) overlap. Finite allocations remain 112 bytes, parent 17
held/final zero. No governed-path speedup is established by this change.

Python name Count +0.32% strict overlaps. Its trusted +27.47% median also overlaps:
before process medians 3178.054/3171.133 us, after 3146.708/4946.681 us. Preserve
the adverse bimodal observation. One 19.498-second trusted-only four-lane
confirmation (80 samples >=73.760 ms, same producers/shared filtered caller,
40-second cap) does not repeat it: Python 3195.960 ->3170.954 us (-0.78%, overlap).
Core name -67.07% remains disjoint; retained pair +0.26% and .NET +0.77% overlap.
No further confirmation or source tuning. No Python-wide speedup claim.

Separate normal Release codegen runs are not timings. Tier1 mask operations inline
in both consumers. The existing two-literal method remains 2,322 bytes with an
identical normalized instruction sequence; only PGO/header comments differ. The
new distinct-head helper is 1,814 bytes. Tier0 retains helper calls, so this is
warm codegen evidence; cold lifecycle remains part of delivery qualification.

Windows full Core 42,262 checks per .NET 10/11 pass. Windows/Linux focused Core
741, PythonRe 162 and Pcre2 89 per target pass, plus 106 hardware-disabled Core
checks per target/OS. Coverage includes borrowed offsets, prefix/vector/tail
boundaries, a missing last byte immediately outside the span, independent byte
oracles, public scalar ranges, resource admission and original cancellation/
deadlines. The generic bucket path was left unchanged; the new branch is confined
to the existing single-literal strategy. No package/catalog matrix or immutable
dev.67 artifact changes in this experiment.

Evidence copied off VM/hash verified in
`artifacts/experiments/distinct-head-vector-20261111/`:

- `evidence.tar.gz`: SHA256 0D60FF468C59356DADF683F76054424FCA01E63FAF1FEB4C2227FA53938D9D6C.
- `confirmation.tar.gz`: SHA256 B0A55399EC8CC3D132FECDE395B3DED2C1508A9B0C9C91B043F47436CD346826.
- `codegen.tar.gz`: SHA256 77EB92A2EF2A06F228757227C31DABF25ACCAA1762F1BC64F49F8E897012F339.
- `linux-checks.tar.gz`: SHA256 337C3C36440701B0EEF52A5EE8DAC235BADDCBA7600EADE9EDDD677BC3553069.

Full summaries retain process medians/ranges, identities and allocation samples.
Next is a coherent package/full-refresh boundary for the accepted batch; preserve
the one-shot sizing work and this kernel rather than packaging each small change.
