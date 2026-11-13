# Python Unicode-class search: prove only the skipped ASCII prefix

Source `0742998f`, .NET11.0.0 on the quiet VM. Baseline reused the exact Full83
Core/Python DLL/PDB pairs (`7b8c22d0`); its checkout `f6829830` differs only in
presentation. PDB/source checks pass for both producers. One bounded A/B, no
confirmation, package or catalog refresh. All524 staged assets remain immutable.

The constructor no longer classifies the complete subject to enable an ASCII
projection of a Unicode class. The existing2048-byte seeker proves only its
skipped ASCII prefix and returns non-ASCII candidates to the original Unicode VM.
Strict ASCII intervals keep their finder; real whole-subject map facts can be
reused. One readonly proof boolean fits existing padding: actual Unsafe.SizeOf
reports368 bytes before/after. There is no per-call allocation, public option or
additional execution/ownership layer. Work requirements can decrease.

Public admission reproduction: with work200, SearchToString(`\d+`) previously
denied an immediate1 followed by1MiB unused ASCII/Unicode suffix at requested201;
anchored Match succeeded. Both now succeed.5131 Release checks pass on each
target, including Unicode digit/word/space/folding, branches/assertions/nullable
neighbors, block edges/scalar floors, retained captures, admission and original
cancellation/deadline. Core producer pairs remain unchanged.

## Public observations

All inputs are valid unchanged UTF8. Early rows return123 before a1MiB suffix.
Finite/live uses work10000/depth4096/working8MiB/result8MiB and a live token so
both producers succeed. Bulk rows materialize128 ASCII or256 mixed Unicode digit
strings. Unicode miss searches24576 bytes. Anchored Match is an unchanged drift
control, not an equal-cardinality bulk or native profile comparator.

| Public operation | Before µs | After µs | Observed change | Allocated B before → after |
|---|---:|---:|---:|---:|
| early-ascii | 10.260 | 0.421 | -95.9% | 32 → 32 |
| early-unicode-suffix | 10.243 | 0.417 | -95.9% | 32 → 32 |
| early-finite-live | 14.802 | 0.513 | -96.5% | 144 → 144 |
| sparse-ascii-bulk | 24.304 | 28.995 | +19.3% | 5144 → 5144 |
| dense-unicode-bulk | 42.837 | 33.240 | -22.4% | 9240 → 9240 |
| unicode-miss | 811.986 | 513.931 | -36.7% | 0 → 0 |

Retain the sparse-ASCII adverse cost: +4.691us per128 results/+19.3%, disjoint
sample ranges and unchanged allocations. The prefix proof adds work to those
seeks. Accept this local cost for the large early-search benefit, improved Unicode
neighbors and lower finite admission; do not claim bulk throughput is preserved.
Anchored controls moved−1.6% to+6.2%; processors differ between ordinary processes.
Do not normalize moved controls into an exact causal percentage.

Nine samples/lane,1000ms warmup,250ms target, all measured durations≥240ms;
120s/process cap,16 retained outputs (<32MiB). Complete outputs are checked before
timing and every result is consumed afterward. Changing Python DLL hashes differ;
Core and caller hashes match. Baseline reused, only the affected Python producer
rebuilt. No further timing for this edit.

Raw SHA256 `DA9B1AEFDF5BF5C6E4D64FA7C0AD56666C8B526D031DC962221B726846C1032C`.
Archive retained under `artifacts/experiments/python-leading-prefix-20261113/`,
hash-verified offVM before releasing SSH. [Acceptance](acceptance.json) retains
medians/ranges, controls, exact producers, correctness and the tradeoff.
Dev83 remains the accepted dependency checkpoint and does not contain this later
source change. Future packages must use a fresh common version.
