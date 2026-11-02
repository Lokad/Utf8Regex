# Count prepared repeated segments over mixed UTF-8

The existing repeated-segment plan proves disjoint ASCII leading/trailing and
separator projections. Owned Core Count now relates that plan to its complete
scalar root, then uses the original separator predicate for non-ASCII scalars.
No regex-string/corpus recognition, new engine, state field, buffer, execution
mode or unchecked read. Existing ASCII native consumers remain. Captured,
folded, RTL, NonBacktracking and unproved roots retain their existing paths.

Strict complete-input validation precedes execution. The same invocation admits
bounded2048-byte searches and64-byte/scalar drains, direct non-ASCII atom work,
original-token/deadline checks and final publication. Zero local working admission
still observes enclosing loans and the first denial. No VM arrays/frames are
needed. Numeric work/depth/working costs can decrease; [SPEC](../../../docs/SPEC.md)
and [migration](../../../docs/MIGRATION.md) document that revision. Returned
captures, cursor aliases and retained-result lifetimes are unchanged.

One frozen16-case public comparison took**61.466seconds**, quiet Ubuntu VM,
.NET11RC3/CPU2. Frozen27078runtime before; data-only parent407c plus archived
patch after. Four equal-result public lanes include predecoded and decode-then
.NET. Warm500ms/min4heavy or64small, independent60ms calibration, five alternating
samples (minimum46.637ms); completed count batches are consumed after timing.
No confirmation, variants, private-kernel claims or repeated package qualification.

| Public Count | Before us | After us | Change | B/op before -> after |
| --- | ---: | ---: | ---: | ---: |
| Actual7.38MB corpus, ordinary |129475.10|9377.18|-92.76%|11880 ->0|
| Actual corpus, compiled |128881.10|9379.83|-92.72%|11880 ->0|
| 32KiB finite/live |560.34|33.24|-94.07%|3688 ->120|
| 32KiB live |539.95|30.11|-94.42%|3688 ->120|
| 32KiB deadline/live |574.19|70.16|-87.78%|3736 ->168|
| Unicode separators |193.74|25.26|-86.96%|18208 ->0|
| Dense false starts |93.07|43.52|-53.24%|0 ->0|
| Failed short runs |1278.00|82.33|-93.56%|3568 ->0|
| Dense hits |745.55|54.66|-92.67%|18208 ->0|

**Promote.** Retain the literal-prefix control's +6.34% median/+7.85% maximum
paired penalty, within10%. Unicode-class +3.11%, folded +1.07%, captured -0.03%
and literal +0.26%; unchanged ASCII native control -11.64% is not attributed as
another optimization. Candidate spreads are at most1.042x. The actual corpus
contains nine Unicode whitespace scalars; applying ASCII whitespace semantics
would be invalid. Public Count remains11. In this batch compiled predecoded.NET
is13952.76us and decode-then17772.83us; final catalog qualification is separate.

Release checks pass on .NET10/11: full affected Core41217/runtime, final354focused
checks, adjacent5060earlier, Python688/PCRE544Count/resource controls. Selections
overlap; do not sum them. The new27cases cover scalar origins, min/max/tails,
Unicode and non-white separator members, declined grammars, finite cutoffs,
borrowed loans/first refusal, actual seek/drain/Unicode/final-publication
cancellation/deadline witnesses and malformed full-input tails.

Initial fixture mistakes (constructor/zero public budgets) are retained. Adjacent
checks found and fixed a real prototype bug: a redundant deadline poll followed
the last token check. The final token check now follows the last deadline poll.
The old storage test now admits the constant-space root under1B while preserving
VM-storage denial for a captured root. All fixes preceded the sole timing batch.

[All pairs, spreads and allocations](summary.json). Immutable raw under
`artifacts/experiments/capital-repeat-utf8-count-20261102/`:
`evidence.tar.gz` SHA256 `CB6E78196ABA1820BFB87B98565BE87BE387B9D22669DA059C38D8E53E1E4E48`;
`correctness-source.zip` SHA256 `83E9E6754C757104B744F8013AF43CF5F9086D811DE36DC05FFAA87D7E28EEDD`.
It preserves exact tested product/fixtures, logs and initial failures. Final
migration prose subsequently clarifies that declined roots retain existing paths.
Published/staged dev.8 and earlier bytes remain immutable. This parent comparison
does not replace the next coherent cumulative/package/platform delivery gate.
