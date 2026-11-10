# Reduce descriptor copies in the live window loop

Accepted after `85daeeb1`. At the source level, the window candidate loop obtained its
408-byte prepared-searcher value for every candidate. Copy that immutable value
once per scan, then borrow the trailing-searcher array entry by readonly reference
for verification. No field, accessor, owner, algorithm, cache, mode or unchecked
operation is added. Scalar eligibility, prefix/branch priority, greedy suffixes,
all work/interruption checkpoints and admission/refunds remain. Managed refs stay
inside synchronous calls and keep their backing storage valid through callbacks.
The accompanying [diagnostic correction](../CoreWindowAttribution-20261110/README.md)
marks the existing live route; it does not select a different executor.

Actual Unsafe.SizeOf reports408B in both producers. One untimed code-generation
pair uses the same public calls, default JIT and .NET11 RC3 on CPU2:

| Tier1 method | Stack before / after B | Code before / after B |
|---|---:|---:|
| TryMatchAt |712 / 312|5808 / 5510|
| TryFindNext |664 / 648|5179 / 5198|

These are JIT stack/code sizes, not managed allocations or modeled working
capacity. Retain the small finder code-size increase. Other tier/OSR versions
remain in raw output; no timing process enables JitDisasm or tiering overrides.

One **49.513s** separate-process public ABBA within180s:12 lanes,240 samples,
minimum73.567ms. Normal8-processor startup precedes CPU2 affinity. Each lane
warms500ms, calibrates independently toward75ms and retains five observations
per process. One Core producer/default context per process; identical caller,
distinct explicit DLL/PDB pairs, equal corpus hashes,2 actual river matches and
3445 candidates/verifications precede timing. Full results and zero loans are
checked outside timing. No confirmation, tuned variant, native retiming or new
package/catalog/Full qualification.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
|river/compiled|3347.013|3247.454|-3.0%|
|river/ordinary|3350.643|3244.515|-3.2%|
|river/finite|3373.350|3399.223|+0.8%|
|river/funded|3356.511|3391.426|+1.0%|
|river/deadline|3820.095|3834.035|+0.4%|
|holmes/compiled|136.501|114.497|-16.1%|
|control/river-ascii|3327.448|3224.783|-3.1%|
|unicode/overlap|56.779|50.942|-10.3%|
|control/separator|5.152|5.089|-1.2%|
|control/large-gap|8.624|8.610|-0.2%|
|control/literal|0.882|0.882|0.0%|
|control/literal-ismatch|4.053|4.051|-0.1%|

Holmes, Unicode overlap and compiled river observations have disjoint before/
after ranges. Ordinary river process medians both improve, but retain one adverse
4261µs sample in its full range. The ASCII river lane shares the affected window
kernel; the literal Count/IsMatch lanes are unchanged controls. Finite/funded/
deadline costs move+0.77/+1.04/+0.36%; keep them without normalization. All
allocation medians and funded peaks remain unchanged; outstanding loans are0.
[Every lane/process/range](summary.json) is retained. These producer comparisons
are not refreshed native ratios or composed gains from earlier experiments.

Windows/Linux1561 affected Release checks pass per .NET10/11 target, including
independent Unicode Count expectations and existing window/cursor, finite-policy,
original cancellation and absolute-deadline cases. The wrong dispatch hypothesis
was fully restored before this candidate; a route assertion caught its lack of
public engagement. An initial fixture expectation correction is preserved in the
separate diagnostic receipt. Algorithms and allocation behavior stay unchanged.

Core before `F7FFDD3D76437A3F1BC206F76819F48FE5BEDB4374BC0DF41DD32C6082675A36`;
after `8618004195A86478A4611B38F4BB98B1C61ADE4913E94B9FD43C0DCC4D4330BC`.
Caller `C0518C4B6592C54D1DE6F08502902E1EB2B20C3CB8404105151393AE661C9439`.
Raw protocol, patch, staged graphs/PDBs, both codegen observations, checks and
samples verified off VM at
`artifacts/experiments/core-river-window-20261110/evidence.tar.gz`, SHA-256
`A9122B19B53CA90F1FE9EC89214B43B656A75D5B36A1889DC84714CF3A6D313B`.
Windows checks and [codegen summary](codegen-summary.json) are alongside the
archive. Immutable staged/published packages, including dev65, remain unchanged.
