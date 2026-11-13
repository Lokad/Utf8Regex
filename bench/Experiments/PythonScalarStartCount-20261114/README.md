# Checked scalar-start counting in existing32-byte work units

Accepted source `8ab77372`. The existing scalar-coordinate counter uses a checked
Vector256 read, continuation-byte equality mask and popcount for its full32-byte
unit. Scalar tails and disabled-hardware fallback remain. No new owner, width/
density selector, execution mode, allocation or public API. The work checkpoint
still precedes every32-byte unit; original authority and admission are unchanged.

## One public comparison

Quiet VM, .NET11.0.0. Exact bbf6d452 Python pair reused after source-equivalence
proof at7202ada0; Core/caller pairs identical. Changing Python hashes differ and
DLL/PDB/source checks pass. Valid unchanged UTF8, same public cursor creation,
range advance and whole/group scalar queries. Progressive rows repeat over4096
matches. Byte-only controls retain their matching/output algorithm and a distinct
coordinate contract; they are not native/CPython parity comparators.

| Public scalar-query operation | Before µs | After µs | Observed change | Allocated B, both |
|---|---:|---:|---:|---:|
| first-block | 0.449 | 0.450 | +0.1% | 592 |
| late-mixed | 14.284 | 3.098 | -78.3% | 880 |
| late-mixed-finite-live | 16.022 | 5.859 | -63.4% | 992 |
| progressive-mixed | 2834.878 | 1298.761 | -54.2% | 230440 |
| progressive-ascii | 846.180 | 818.646 | -3.3% | 229912 |

Mixed late/progressive and finite public ranges are disjoint; allocations are
unchanged across both lanes. First-block primary ranges overlap; do not claim a
distinct improvement there. Byte-only medians move−2.80%..+1.17%, with different
chosen processors. No normalized exact causal percentage. Finite/live uses
work1M/depth4096/working16MiB/result16MiB with an original live token.

The preceding prefix-growth comparison remains separate evidence: its adverse
progressive+31.12%/+376B is retained. Its after3625us and this baseline2835us
describe the same producer in different contexts. Do not compose percentages,
erase the prior result or claim allocations returned to the pre-growth baseline.

Nine samples/lane,1000ms warmup,250ms target, all samples >=233ms;120s/process
cap. At most64 integer result records retained; checks occur after timing and
all intermediate positions are independently checked before timing. One A/B,
no confirmation, further trial or package/native/full refresh for this helper.

## Checks and delivery

73 focused Release checks pass per runtime on Windows and Linux, plus35 Windows
disabled-intrinsics checks. New all-width fixtures cover borrowed offsets,32/
256-byte cuts and tails; existing half-million lifetimes, work/working refusal,
growth interruption/refunds, aliases, APIs and managed-member inventory pass.
Public optimized disassembly observes vector comparison/mask/popcount on both
hosts; checked source admission remains. [Acceptance](acceptance.json) retains
all ranges, identities and codegen evidence. Other library images are unchanged.

Raw archives under `artifacts/experiments/python-scalar-count-20261114/` were
hash-verified offVM before keeper release. Existing524 package/symbol assets
are unchanged; accepted dev83 excludes this source. Batch the accepted source
improvements into a fresh common candidate at the next delivery boundary.
