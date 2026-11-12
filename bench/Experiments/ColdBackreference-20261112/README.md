# Extract cold backreference comparison from the shared VM

Accept the extraction on e31690dd with the costs below. Move the existing
forward/RTL ordinal and folded comparisons into one private helper on the same
runner. The dispatcher always calls it for Backref. This removes duplicated
setup/failure handling: 199 lines removed, 87 added, net112 fewer lines. No
selector, engine, program metadata, mode, buffer, owner or allocation mechanism.

The original four comparison loops retain their per-byte/per-scalar work charges.
Preserve ECMAScript unmatched-empty references, completed/in-progress captures,
rollback, RTL widths, Unicode/profile folds, original cancellation/deadlines,
sticky denial, admission/refunds and cursor/result aliases. Input validation and
numeric work/depth/working/result contracts are unchanged. Existing tests cover
these rules; no tests merely preserve the new private method.

One actual public FindAllToStrings diagnostic, reusing the qualified dev.73
caller/Core/Python graph, identified the retained cold branch and entry clearing.
After extraction, the same complete2048-tuple public caller confirms:

| Final Tier1 shared RunSegment | Before | After |
|---|---:|---:|
| Code bytes |12846|11950|
| Stack subtraction |1624B|1464B|
| Entry clearing loop |1536B|1392B|

The repeated entry loop clears144 fewer bytes. A separate real public ordinal
Count confirms the extracted helper reaches final Tier1:2078 code bytes. This
is an additional method/code-size cost; do not claim total generated code shrank
or derive a public speedup from smaller entry work. Both diagnostics use normal
eight-CPU startup then CPU2, .NET11.0.0/default tiering+PGO.

Windows/Linux focused Release checks pass per.NET10/.NET11: Core1759, Python1225,
PCRE2180 and POSIX145. Coverage includes backreferences, captures/rollback,
assertions/balancing, RTL/folds, finite limits, validation, original interruption
and aliases. Both PE/PDB identities and342 Core documents per producer verify,
including six generated observations and exactly one changed production source.
Separate Windows raw and Linux LF manifests bind their actual source/PDB bytes.

One **131.592-second** fourteen-row, two-policy public ordinary-process ABBA
comparison retains560 samples, minimum67.880ms, maximum69.535MiB result batch.
Per-row warmup,75ms calibration target/20ms floor, normal runtime/affinity above.
Complete results are consumed after timing. Staged/loaded distinct Core DLL/PDB
pairs and common Python/caller/.NET bytes verify. The baseline is exact dev.73;
Core source is unchanged from EAE through e31690dd before this patch.

| Public operation | Validate before → after | AssumeValid before → after |
|---|---:|---:|
| General Core captured Count |423.579 →389.429µs|391.664 →397.205µs|
| Core forward ordinal backreferences |49.947 →46.793µs|48.130 →48.183µs|
| Core RTL ordinal backreferences |127.327 →127.042µs|124.953 →130.487µs|
| Core folded references, unequal UTF8 widths |119.237 →126.337µs|117.667 →127.116µs|
| Python folded-reference strings |249.447 →250.944µs|240.811 →251.854µs|
| Python many-capture strings |717.696 →610.923µs|627.876 →638.243µs|
| Python one-capture strings |287.773 →209.705µs|215.555 →221.683µs|

Strict Core captured Count−8.06% and forward ordinal−6.31% have disjoint ranges.
Accept disjoint folded-reference costs+5.95/+8.03% and trusted RTL+4.43% for the
smaller, simpler shared dispatcher and reduced entry work. Other table ranges
overlap. Python many/one medians−14.88/−27.13% strict but+1.65/+2.84% trusted
do **not** establish a capture-output gain. Preserve UTF8+4.07/+.57%, Split
+6.24/+8.28%, finite−4.13/−5.88%, funded/deadline−1.71/−4.35%, detailed cursor
+8.85/+6.48% and unchanged Core/.NET movements, all overlapping. No normalization,
native ratio or blanket10% veto. [All observations](cost-summary.json).

Allocations remain unchanged, including0B captured Core Count,64B forward/folded,
1328B RTL and37000B Python folded strings. Modeled shared peak357073B, held
program/parent45425B and original17B parent refund remain; final loans0. This is
not total heap/RSS. No confirmation, source tuning, new package or Full/catalog
refresh for this experiment; published/staged bytes and tables stay immutable.

Canonical hash-verified offVM evidence under
artifacts/experiments/cold-backreference-20261112:

- Gate/checks/source:7AC6C5E209CC3624ADD7A53721ED7EABBFE0945AF41454CB78A74A2CD8E760F9
- Public comparison/helper codegen:B9C48F81D2FFAAA5433874BD560FC82B12CC76432B8B6C27F3B61291AD77A535
- Windows:D6FBAB0B69CAE9166AFC836D6F3D9780E0F3AE5B9FB77D16F4EA8AF68C124821

All workers are terminal and keepers released. Broader warm Python/Core and
unresolved POSIX history costs remain open. Do not infer that outlining every
opcode is useful or start a helper-extraction campaign from this result.
