# Parity batch and four-profile dev.58 delivery

Frozen producer `41e05e76`, common `0.5.0-dev.58`, targets .NET 10/11 for Core,
PythonRe, PCRE2 and POSIX with exact adapter-to-Core pins. No publication.
Qualified historical bytes remain immutable; this candidate uses a fresh version.

## Accepted production changes

- [POSIX root history](../PosixRootHistory-20261109/README.md) reuses the whole
  trace's known root bounds. One 67.534-second ordinary-process ABBA improves
  history lengths 8/12 by 6.5–6.8% and empty captures 5.3%, with unchanged allocations.
  Ambiguous captures move 3.4% slower; small deltas retain their uncertainty.
- [PCRE2 word boundaries](../PcreWordBoundary-20261109/README.md) extends the
  existing possessification proof for positive-minimum word-only repeats before
  required `\b`. One 68.064-second ABBA improves date hit/miss 46/33% and funded/live
  date hit 29%; Count improves 21–22%. Funded date/word-hit allocation falls 1,592→112 B
  and covered workspace 1,504→0 B. Explicit finite policies retain their old lowering;
  timing movements on those unchanged plans are not attributed to this proof.

Both changes preserve strict scalar UTF-8, capture selection, original authority,
working admission and bounded metering. PCRE2's zero-minimum boundary counterexample,
mixed/negated classes and Unicode modes are covered. No framework or executor.

The [Python search-preparation trial](../PythonSearchPreparation-20261109/README.md)
was reverted. One 48.098-second ABBA plus its single 48.180-second confirmation did
not establish stable main captured-output gains; both producers show large
between-process variation. Count/Unicode gains do not settle the overall trade-off,
and range cursors would allocate 8 B more. No third timing or decoder retrial.

## Delivery evidence

One [Full](full-refresh.json) passed all **85 steps in 2,790.610 seconds
(46m30s)**, below the unchanged one-hour cap. One Release build serves both-runtime
source/disabled-dynamic checks, all four .NET 11 catalogs, paired comparators,
lifecycle/scaling, resource costs and final evidence checks. Eight generated
README/summary files are imported once. Five snapshot checks per target pass
afterward using matching reused binaries; no repeat build or refresh.

The current paired PCRE2 date ratios are **2.08/2.77**, versus 4.36/4.29 in dev57;
these are fresh equal-result comparisons, not composed isolated gains. Subroutine
prefix digits remains 4.26× and negative conditionals 3.66–3.70×. POSIX empty/nullable
captures remain 9.02/5.71×, and the separate SKU control 7.65×. Full histories at
4/8/12 bytes cost 2.06/14.36/108.34 µs; native captures differ, so no ratio. Python
many-/one-capture output remains 2.48/2.06× and early literal search 2.87×.

Five Python rows are Unqualified due to removable comparator harness floors.
Limited replacement/split are Inconclusive; keep the reasons rather than rank
uncertain ratios. Core algorithms did not change: one-node backtracking moves
about 0.08→0.10 µs (+21–22%) and compiled full AWS keys 9.40→10.71 ms (+13.9%). Retain
those adverse catalog observations without causal attribution or normalization.

Raw Full evidence is copied off the volatile VM and hash-verified at
`artifacts/experiments/delivery-dev58-20261109/full-evidence.tar.gz`, SHA-256
`0AED256BD339F674BEB68CEF30A2AC3E0082702609268C5EEA146A904C7C9D73`.
No archive service/upload gate is required. Compact summaries serve offline CI.

Windows qualification passed in 450.654 s: ten detached fixtures, both targets,
110,700 checks against the exact eight package/symbol files. Original Awk19/
Lython118 per target and public hosting probes pass with zero root loans. Twelve
restore identities and all 477 actual compiler inputs, including generated files,
are captured. Source CI `42636096807` is green on Windows/Linux. Performance,
source CI and exact-package/consumer acceptance remain separate evidence.

The first original-consumer invocation used a relative feed path; restore failed
before tests. Correcting it to an absolute path ran all original fixtures once
successfully. The failed restore log remains in the ordinary artifact directory;
no producer rebuild/repack or already-passed fixture was repeated.

Linux accepted the same ten detached fixtures/110,700 checks and original
Awk/Lython/public probes on both runtimes without producer rebuild/repack. All
eight assets, 28 restore identities and 14 TRX identities passed their audit.
All 316 older staged assets are unchanged; eight new package/symbol files bring
the total to 324. Only three exact pins changed in the copied original sources.

Linux evidence is copied off VM and verified at
`artifacts/experiments/delivery-dev58-20261109/linux-evidence.tar.gz`, SHA-256
`62BA927D47D40730A054A4C85485BAE7C3F059F33B8804000B95F8AEACC48BA2`.
The [acceptance record](acceptance.json) separates package/consumer/performance
evidence from publication, downstream adoption and complete flavor parity.

The imported summaries and authored candidate links follow the frozen producer
source; they do not change accepted package bytes. Final documentation CI remains
pending. No repeated package or benchmark matrix is needed for these edits.
