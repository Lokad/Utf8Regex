# Python scalar coordinates prepare only the queried prefix

Accepted source `bbf6d452`. Existing cursor-owned scalar prefixes initialize only
through requested offsets. ASCII proof covers that prefix, never discovery's
whole-subject description. The superseded whole-subject ASCII helper is removed.
No new owner, token, public API, execution mode or subject-validity pass.

Capacity grows geometrically, bounded by the subject's possible entries, with
old/new capacity admitted before allocation and bounded copying. Old capacity
refunds synchronously after successful growth; failure closes/refunds both.
Early work/capacity can decrease; later peak can overlap arrays. The
[contract](../../../docs/SPEC-PYTHONRE.md) records those accounting semantics.
Fixed cursor metadata adds8B in actual public allocation observations.

Public reproduction: a capture after300 ASCII bytes fits work2000 on a short
subject but previously refused at requested2001 with an unused1MiB suffix.
All six original ASCII/Unicode capture/suffix cases now succeed under the same
policy; borrowed retained values survive. No performance percentage is inferred
from an operation that the baseline refuses.

## One public comparison

Quiet VM, .NET11.0.0. Reuse exact Python0742998f after proving source equivalence
at a6303c45. Core and caller pairs are identical; changing Python hashes differ.
Both DLL/PDB/source checks pass. Public operation creates a cursor, advances to
ranges and queries whole/group scalar coordinates. Progressive case repeats
that over4096 matches. Byte-only controls retain their matching/output algorithm
but share cursor construction; their coordinate contract differs. All supplied
text is valid unchanged UTF8. This is not a new CPython/profile parity result.

| Public scalar-query operation | Before µs | After µs | Observed change | Allocated B before → after |
|---|---:|---:|---:|---:|
| first-block | 0.463 | 0.454 | -2.0% | 584 → 592 |
| ascii-prefix | 925.736 | 0.447 | -100.0% | 17000 → 592 |
| mixed-prefix | 1176.368 | 0.839 | -99.9% | 17000 → 624 |
| mixed-prefix-finite-live | 1104.797 | 1.118 | -99.9% | 17112 → 736 |
| late-mixed | 14.328 | 14.214 | -0.8% | 872 → 880 |
| progressive-mixed | 2764.812 | 3625.185 | +31.1% | 230064 → 230440 |

**Retain the adverse progressive result:** +31.12%/+860.37us per4096 matches
and +376B/cursor, with disjoint ranges; its byte-only control is effectively
unchanged. Replacement index arrays add368B total allocation in that fixture;
all byte-only controls add8B from cursor metadata. Old/new capacity overlap
remains within the existing32768-byte half-million-query test. No claim of
unchanged progressive performance, allocations or exact causal percentage.

Accept this local cost for the large early-query benefit and newly usable finite
cases. Other byte-only medians move−6.69%..+1.22%; processors differ. First-block
and late-input primary ranges overlap, so no distinct gain is established there.
Progressive ASCII is not timed in this bounded comparison; its lifetime/finite
controls remain correctness evidence. Do not normalize controls or retime away
the adverse result.

Finite/live timing uses work1M/depth4096/working16MiB/result16MiB with a live
token, sufficient for both producers. Nine samples/lane,1000ms warmup,250ms
target, actual durations >=176ms;120s/process cap. Only64 integer result records
are retained and checked after timing. Full intermediate positions are verified
before timing. One A/B, no confirmation or further trial.

## Checks and delivery

All5137 PythonRe Release tests pass per target on Windows and Linux. Existing
half-million-query and block-crossing fixtures pass; added cases cover prefix
facts followed by detailed UTF16 output, backward lookbehind, repeated queries,
growth refusal/original-token cancellation, parent17-byte refunds and retained
values. Pattern/text contracts, cumulative work/depth/result admission, original
deadline, aliases and synchronous closure are preserved. Other producer pairs
remain unchanged. No public API change or unchecked access.

[Acceptance](acceptance.json) records all medians/ranges, identities and tradeoffs.
Raw archives under `artifacts/experiments/python-scalar-prefix-growth-20261113/`
were hash-verified offVM before releasing SSH. All524 staged package/symbol files
remain immutable. Dev83 excludes this source; a future package needs a fresh
common version. No package, canonical catalog or Full refresh for this edit.
