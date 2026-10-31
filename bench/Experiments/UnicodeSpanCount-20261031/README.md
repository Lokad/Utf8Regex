# Shared sequence search for remaining UTF-8 literal Count

Accepted source `e80b9db3`; baseline `afb260d2`. The remaining uncontrolled exact
UTF-8 BMP-leading Count branch now calls its existing local CountViaSpanSearch,
already used for ASCII. Removed the scalar-anchor/recheck implementation,
benchmark forwarding probe/phase and obsolete census replay. The census now
describes the live SpanSequenceSearch path. Five files:38 insertions/108 deletions,
including31 new public boundary/dense test lines. No new matcher, state, mode,
unchecked API or shipped diagnostic API. Existing fused/correlated strategies,
strict validation and finite/live/deadline authority/metering are preserved.

**This does not close the published Chinese-corpus gap.** Promotion is a bounded
simplification with clear gains on representative dense-prefix/mixed-width public
workloads, accepting the recorded small costs below. It is not a corpus speedup,
kernel-based performance claim or blanket10%/equivalence verdict.

[Release checks](verification.txt) pass on .NET10/11: final Core159/runtime,
affected PythonRe89 and PCRE244/runtime, plus3 snapshot checks/runtime. Coverage
includes full strict malformed-tail rejection, dense false prefixes, partial
tails, nonoverlap, offset canaries and original controlled publication.
The public regression tests from the earlier partial-anchor bounds defect remain.
TryGetLeadingUtf8SegmentLength retains genuine family-construction consumers.

The [initial public A/B](comparison.json) took **92.385s**, eleven cases/four
lanes, minimum **38.916ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Actual813478B
corpus,23-byte needle,30 matches. Complete public Count agrees with ordinary/
compiled predecoded and decode-then Regex. Warm1.5s/min64 calls, independently
calibrated60ms lanes, five alternating samples. Full validation included;
compilation/setup excluded; result batches are checked after timing.

| Initial public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual ordinary | 187.946 | 189.631 | +0.90% |
| Actual compiled | 195.219 | 199.007 | +1.94% |
| Actual finite/live | 1221.405 | 1223.297 | +0.15% |
| Actual deadline/live | 2921.103 | 2880.331 | -1.40% |
| Dense false prefixes | 28.457 | 2.773 | -90.26% |
| Dense complete hits | 6.195 | 5.728 | -7.55% |
| Short mixed-width needle | 9.691 | 6.476 | -33.18% |
| Fused-three-byte control | 5.578 | 5.585 | +0.12% |
| Correlated-two-byte control | 15.022 | 16.698 | +11.16%, unsettled |
| Supplementary control | 5.533 | 5.449 | -1.52% |
| ASCII control | 0.616 | 0.651 | +5.66% |

Allocations are unchanged:0B except finite/live120B and deadline/live168B.
Correlated control has1.43x/1.38x spreads and shifting modes. Actual ordinary also
has a1.42x before spread; its exact initial median difference is not promoted.

The **sole [confirmation](confirmation.json)** took **55.573s**, six selected
cases/two producer lanes, minimum **43.436ms**. Same source/DLL/runtime, warm4s,
five alternating independently calibrated60ms samples. No further measurements.

| Confirmation public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual ordinary | 188.680 | 190.131 | +0.77% |
| Actual compiled | 189.146 | 190.540 | +0.74% |
| Dense false prefixes | 28.479 | 2.673 | -90.61% |
| Short mixed-width needle | 11.545 | 7.470 | -35.30% |
| Correlated-two-byte control | 20.928 | 22.578 | +7.88%, before remains unsettled |
| ASCII control | 0.599 | 0.629 | +5.10% |

Actual/ASCII spreads are at most1.07x; correlated before1.39x. Dense/mixed gains
are clear despite after spreads1.62x/1.22x. Retain initial+11.16% and the
overlapping modes; the control median does not prove a universal<=10% bound.
The initial confirmation launch failed before warmup outside the copied corpus;
its diagnostic is archived. The same driver was then launched beside its data.
The final cleanup only removed the now-unused kernel/diagnostics and updated the
census; it did not change the timed sequence-search algorithm. Final tests pass.

The [selective refresh](refresh-case.txt) updates only literal/sherlock-zh in the
ordinary/compiled sections on clean matching `e80b9db3` binaries/PDB/source.
Current observations:136.381/137.669us,0B; ratios **6.247x / 5.186x**, both Gap;
minimum sample47.947ms. Earlier full-refresh7.01x/5.50x and this separate refresh
are not an A/B effect size. Every other row/identity/lifecycle evidence remains
unchanged; generated ordering, parity verification and snapshot tests pass.

[Initial driver](driver.cs.txt), [confirmation driver](confirmation-driver.cs.txt),
both raw summaries and [identity manifest](identities.json) bind the precommit
prototype. Committed final source has separate clean refresh identities. Immutable
archives include exact baseline/prototype/final DLL/PDBs, source/patch, raw samples,
refreshes and the setup diagnostic:

- [Raw/binaries/source bundle](../../../artifacts/experiments/unicode-span-count-20261031/evidence.tar.gz):
  `E6C0644B87D1C9884B181E19D346254D4081609B852EEA0608289F43AA0E42CF`.
- [Correctness/source snapshot](../../../artifacts/experiments/unicode-span-count-20261031/correctness-source.zip):
  `040BA417ACBD282C1C6B6E9CABC7DD8A27FDBE7048C921278C629D6B148CD5B8`.

Published/staged packages are unchanged. Final-source cumulative operating costs,
complete sub-hour refresh and final package/platform/consumer acceptance remain
release gates. No repeat of this closed A/B or package campaign for this slice.
