# Publish complete capture-root outcomes once

Keep intermediate RunClassRepeat results local inside the existing complete-root
helper. Inspect success/breach/end locally and publish final success once with
explicit null history fields. Failure still copies its full result. Preserve
root eligibility, matching/repeats, capture slots/closing trail, initialized and
admitted workspace, all work/interruption checkpoints, lifetimes and general
history payloads. No new type, flag, owner, helper, allocation, API or accounting
contract. Baseline body 20174326, restored compilation 5cce9347, metadata f383e557.

Existing post77 CPU/JIT bytes motivated the trial; no new CPU profile was recorded.
Fresh current public FindAll codegen proves four successful intermediate
CORINFO_HELP_CHECKED_ASSIGN_REF calls become zero. Tier1 native code falls
6899 -> 6833 bytes; stack subtraction stays 712 bytes. This is a codegen
observation, not a speedup projection. Failure copies remain.

## Public decision

One 109.607s ordinary-process ABBA, fourteen complete public operations, both
input policies, 560 samples, minimum 20.970ms, maximum 235.835MiB per retained
batch; 140s outer cap. One allowed 59.612s confirmation, three operations/both
policies/120 samples, minimum 96.099ms, maximum 57.007MiB; 85s cap. Reuse the
existing callers and identical producer pairs; no caller rebuild or tuned variant.
Linux .NET 11.0.0, normal eight-CPU startup then CPU2, default tiering/PGO.
Loaded DLL/PDB/caller/.NET identities, complete outputs and loan refunds verify.

| Public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| Count, main |146.399 -> 134.928|144.993 -> 132.670|
| Count, confirmation |141.426 -> 129.879|142.344 -> 128.924|
| Capture strings, main |441.319 -> 565.992|547.570 -> 465.259|
| Capture strings, confirmation |549.174 -> 534.538|597.101 -> 505.687|
| Unchanged .NET Count, confirmation |74.005 -> 74.760|75.217 -> 73.968|

Accept the public Count improvement: main -7.84/-8.50% and confirmation
-8.16/-9.43%, all disjoint. String ranges overlap in both runs: retain adverse
main +28.25% strict and -15.03% trusted, then -2.67/-15.31% confirmation, without
a settled string-gain claim. Main byte outputs -3.58/-5.98%, finite strings
-3.44/-2.37%, deadline -1.25/-3.26%, funded cursor -1.65/-2.49% all overlap.
One-capture, Unicode, excluded-root, tiny FullMatch and unchanged Core/.NET
controls also overlap; .NET strings +5.48/+11.91% and confirmation .NET Count
+1.02/-1.66% are not normalizers. Preserve all rows/process medians in raw summaries.

Allocations stay 0B/Count, 242008B/string and byte tuples, 246208B/finite,
246256B/deadline, 230296B/funded cursor. Tiny main FullMatch has one incidental
sub-byte amortized allocation observation. Modeled funded peak stays 417B,
including caller-held 17B; final loans 0. These are admission, not heap/RSS bounds.
Current catalog ratios and qualified dev.77 packages are unchanged.

## Validation and evidence

Windows/Linux focused Release checks pass per .NET 10/11: Core 4694, PythonRe 681,
PCRE2 874. Reuse existing independent history/slot/cutoff/alias/resource and
original-interruption checks; no new mirrored tests. Build only Core/Core tests,
reuse adapters. Both 342-document PDB/PE inventories verify the single intended
production change and generated revision. No package/Full qualification.

The initial gate exits 1 after passing builds/tests and baseline public codegen:
its text guard expected OptimizedTier1; disassembly labels the code Tier1. Preserve
that archive/error, then complete only candidate codegen and export. No repeat
build, test or baseline diagnostic. The reused diagnostic caller's historical
CPU-capture label does not mean a profiler was attached to these codegen launches.

Canonical artifacts/experiments/capture-root-outcome-20261112/:

- Initial gate 22A8FC1755EB9912027B0E440F87C04BA145975B80AD49DE52FAFE746C6E04D0.
- Completed gate C93ACF31C64DAA20C61AD57B3765816F41A077EC65640DAF23C5626A84A49C1B.
- Main 0BB78AE1EB5544F8679373BBD68C0D47FB8BE4EBA53FB28FCEDAF60D89E5240F.
- Confirmation 0A9A51443BBD96E811435F0FC1C25DC1C185A723810E10B15003564E98BE616F.
- Windows 2C1A9FF25AF8677372E46498B4AA8C69762BCC5B831BF1539CA2DFC6C31E8552.

Archives verify offVM before keeper release. All 476 immutable package/symbol
assets stay unchanged. Material requested-output/Core gaps and general POSIX
histories remain active; this is one accepted matching improvement.
