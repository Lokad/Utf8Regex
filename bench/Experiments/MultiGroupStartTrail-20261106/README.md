# Omit unused multi-group capture-start undo entries

**Accepted.** Parent 1be94e16b21a29ebce1e1da207282b4980e73b58. One eligibility
condition is removed from the existing scalar runner. In an assertion-free,
latest-value root, a plain capture start needs undo only while a frame can restore
it, independently of group count. Closing entries retain Python completion order.
History/assertion drivers, opening journals and balancing retain their entries.
No field, buffer, opcode, executor, mode or accounting mechanism is added.

Instruction work, original synchronous tokens/deadlines, capture/alias lifetimes
and admission before required allocations remain. Avoided helper work and chunks
can lower numeric work/working requirements, as documented in the Core contract.
The production-state probe checks capture ranges and LastIndex with zero live
frames: the actual two-group case retains 4→2 entries and 320→320 modeled bytes;
twelve linear groups retain 24→12 entries and 704→544 bytes. Disposal refunds all.

## Public operation costs

ONE **91.821036601-second** quiet-VM Default-process ABBA comparison, normal
eight-CPU startup then CPU2 affinity, .NET11 RC3/default tiering and PGO. Only Core
DLL/PDB changes; Python and caller bytes are identical. Fifteen public rows,
300 samples, independent 75 ms calibration target and 20 ms floor; minimum
32.3722 ms. Complete result batches are checked after timing. Outer cap120s,
producer cap35s. No confirmation, tuning, normalization or catalog refresh.

The real two-capture FindAll subject is 18,432 bytes/2,048 tuples. Selection uses
the qualified 715.96 µs/1.91× CPython gap; these new private-fixture observations
do not establish a new CPython ratio or benchmark Status.

| Public operation | Before µs | After µs | Change | B/op before → after |
|---|---:|---:|---:|---:|
| actual-two-capture-strings | 629.97 | 590.38 | -6.28% | 242008 → 242008 |
| actual-two-capture-utf8 | 662.27 | 623.86 | -5.80% | 242008 → 242008 |
| actual-two-capture-finite-live | 1004.69 | 945.68 | -5.87% | 246208 → 246208 |
| actual-two-capture-detailed | 899.83 | 875.04 | -2.75% | 545112 → 545112 |
| unicode-two-capture-strings | 680.30 | 639.91 | -5.94% | 242008 → 242008 |
| optional-two-capture-detailed | 253.10 | 247.47 | -2.22% | 224104 → 224104 |
| two-group-backtracking | 244.57 | 245.18 | +0.25% | 50504 → 50504 |
| twelve-linear-capture-strings | 110.19 | 105.76 | -4.02% | 53928 → 53776 |
| one-capture-strings-control | 390.19 | 372.00 | -4.66% | 94344 → 94344 |
| one-group-backtracking-control | 191.96 | 191.10 | -0.45% | 17736 → 17736 |
| unchanged-core-literal-count | 0.32 | 0.32 | -0.25% | 0 → 0 |
| unchanged-python-literal-findall | 0.35 | 0.35 | +0.50% | 72 → 72 |
| unchanged-named-fullmatch-miss | 0.43 | 0.43 | -0.08% | 272 → 272 |
| small-two-capture-fullmatch | 0.65 | 0.62 | -4.80% | 544 → 544 |
| nested-completion-order | 76.97 | 75.11 | -2.42% | 57640 → 57640 |

Both candidate process medians are below both baselines for actual strings,
UTF-8, finite/live and detailed results. Retain all process ranges in summary.json.
The one-capture row is a shared-method neighbor, not a control proving an isolated
multi-group effect. Separate Core literal Count −0.25% and Python literal FindAll
+0.50% controls retain allocations; named-reference miss −0.08% has unchanged
opening-journal behavior. No control normalization or broad parity claim follows. Separate compiled BCL
tuple diagnostics report predecoded661.93us/1,294,712B and decode-then369.57us/
1,331,600B. Their counterintuitive order and single-process scope limit inference;
they establish no new baseline or parity status.

## Validation

Release Windows/Linux, each .NET10/11: Core7573, Python995 plus878 policy/
interruption cases, PCRE2841 plus67 policy/interruption cases. Five new public
cases cover nested close order, unmatched/empty groups, repetition, alternatives
and retained results under finite policies. Existing checks cover rollback,
history/balancing, RTL/NB, resource denials, malformed full tails and original
tokens/deadlines. All pass without runtime or test-budget repairs.

Initial benchmark preflight rejected baseline name-entry ordering and compared
normalized strings through private struct fields. The checker now compares public
group properties and name mappings independently of order. Its diagnostic output
and original fixture are preserved; no timed run had started. Both frozen graphs
then passed all15 public preflights before the single comparison.

Preceding read-only imports audit found no duplicated discovery: its actual plan
is FixedDistanceAsciiSets with literal guidance disabled. Current hot Tier1 VM
work charging is already inlined; helper calls belong to backreference paths.
The accepted line-start pair seeker is already present. No imports prototype or
benchmark was warranted; raw audit is in the Windows evidence archive.

Raw identities and CI status are in validation.json. All eight frozen Full
benchmark assets and staged dev.44 packages remain unchanged. No full refresh,
package qualification, new dependency or NuGet publication is part of this batch.
