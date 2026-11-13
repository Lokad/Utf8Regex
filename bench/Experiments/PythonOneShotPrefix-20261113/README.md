# Python one-shot capture coordinate prefixes

Source `6ac208cf` → `260aa1cd`, .NET11.0.0 on the quiet VM. One public A/B and
one confirmation; each used the same staged DLL/PDB pairs and caller in separate
ordinary processes. No package or catalog was refreshed for this change.

One-shot owned details and borrowed capture contexts now use the existing map
only through the furthest participating capture end. Lookbehind, lookahead, empty
and nested captures retain absolute byte/UTF16 coordinates and LastIndex/LastGroup.
Prefix facts stay local; the private cursor is finished before a partial map can
be advanced. Bulk iteration, callbacks and durable cursors retain their shared map.
Work and modeled map capacity can fall; no new state field or public option.

318 focused Release checks pass on each target, including 64KiB working/result
limits with a 393KiB unrelated Unicode suffix, parent refund, retained output,
original-token/deadline, finite limits and cursor neighbors. Core DLL/PDB controls
and all516 staged package/symbol files are unchanged.

## Public observations

Each early-match subject is about114KiB. The finite context uses a live token and
work/depth/working/result limits. Both producers receive identical valid text,
options and output requests. Value Search is a drift control, not an equal-output
comparator. Dense FindIterDetailed materializes128 captures from896 bytes.

| Public operation | First before → after µs | Confirmation before → after µs | Allocated B before → after |
|---|---:|---:|---:|
| owned-early | 374.923 → 0.671 | 377.293 → 0.667 | 270896 → 560 |
| context-early-finite | 382.717 → 1.051 | 385.713 → 1.047 | 270952 → 616 |
| owned-lookahead | 379.471 → 0.788 | 384.718 → 0.778 | 271400 → 1064 |
| unchanged-dense-bulk | 51.169 → 68.518 | 48.982 → 68.543 | 25816 → 25816 |

The dense neighbor is adverse: +33.9% and +39.9%, with disjoint sample ranges and
unchanged allocations. Its source execution body is unchanged, but that does not
erase the measured cost. Value controls moved −11.4% to+4.0% in the first pair
and −15.7% to−5.5% in confirmation; processors differ between ordinary processes.
Retain the large one-shot work/allocation benefit and explicitly carry the dense
cost into the next coherent delivery catalog. No blanket throughput claim or
normalization of these controls into an exact causal percentage. No more trials.

Nine samples/lane,1000ms warmup,250ms target,all samples≥235ms;120s/process cap.
Only16 outputs retained per batch (<32MiB). Full values/coordinates/names are
checked before timing; every output contributes checksum evidence after timing.

The confirmation completed both timing processes but exited1 on its final
cross-process checksum assertion: the driver included unspecified NameEntries
iteration order. The two legal orders of `outer:1, inner:2` reproduce exactly
−4719034493949972928 and−4698733981499675968. Independent preflight group/text/
coordinate checks passed. This is a checker-order defect, not a runtime result
mismatch; preserve the exit1 raw record and derived classification separately.
The repeated dense cost remains valid adverse evidence. No retiming followed.

## Evidence

Raw archives are preserved under `artifacts/experiments/python-one-shot-prefix-20261113/`.
Original SHA256 `15C5F375C9F733C511F794E2E9CBD5AB0F41ADE00DE664DCB1E67EAF22F82719`.
Confirmation SHA256 `06B1B07CFCF6AA4DE6720F551F455CE33FF9B1BEDCEBB58ACE5B9304E55B0DCD`.
Both were hash-verified offVM before releasing their keepers. Structured medians,
ranges, controls, placements and the checker erratum are in [acceptance.json](acceptance.json).
Dev82 packages remain immutable and do not contain this later implementation.
