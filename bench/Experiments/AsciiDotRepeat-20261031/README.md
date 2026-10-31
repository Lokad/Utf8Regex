# ASCII Dot repeat pilot

**Closed and reverted.** This was the one final ASCII-predicate variant after
the [negative-mask pilot](../NegatedSetRepeat-20261031/README.md). It reused the
same repeat/proof/charging loop for ASCII Dot, with LF/Singleline qualification;
positive-set eligibility and non-ASCII decoding were unchanged. It did not
materially improve imports. No further local variants or confirmation follow.

Before `13ff4c5f`; candidate `fe975000` plus
[source/test patch](../../../artifacts/experiments/ascii-dot-repeat-20261031/candidate.patch), SHA256
`BC0429FB0D5B1DD6B67EA8178F082F62D63B23ED5EB51C482378DE03550337CF`.
The candidate was not committed or packaged. Both production/test files were
restored; experimental tests are preserved in the patch/logs.

[One public A/B](comparison.json), **88.160s**, 12 cases/four equal-result
lanes, minimum sample26.372ms. Warm1.5s/min1000calls, independent50ms calibration,
five alternating samples; complete integer result batches consumed after timing.
Setup/compilation excluded. All output preflights and timed checks pass against
predecoded/decode-then-.NET. Allocations and instruction counts stay unchanged.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual imports, ordinary | 29.425 | 30.305 | +2.99% |
| Actual imports, compiled | 58.638 | 58.760 | +0.21% |
| Actual imports, finite/live | 60.897 | 60.245 | -1.07% |
| Dense ASCII | 104.647 | 109.560 | +4.69% |
| Dense mixed Unicode | 62.022 | 66.592 | +7.37% |
| Positive-set control | 69.279 | 70.991 | +2.47% |
| Folded control | 57.442 | 57.319 | -0.21% |
| Unchanged literal control | 94.553 | 95.363 | +0.86% |

See [full summary](summary.json). Some rows remain noisy: folded control has
~1.83x spread, compiled imports1.12x/1.20x. There is no useful target gain to
confirm; the source is restored. This result does not diagnose historical
selective/full-refresh differences or erase the earlier disjoint-repeat gain.

[Current instruction census](instructions.txt) retains the same45-instruction
imports shape, including the negative Set and trailing Dot repeats. No new IR
or lost suffix-proof change was established. The probe is diagnostic only,
outside timing; its source is retained separately. An initial probe launch
used a renamed assembly with the old dependency identity and was corrected to
its own Inspect assembly name before the recorded census.

Focused Release checks passed on both targets: Core177/PythonRe149/PCRE2114,
including explicit ASCII work cutoffs and original-token/deadline interruption.
Keep raw results as an unsuccessful pilot, not a producer gain or package claim.

[Immutable raw/source/DLL evidence](../../../artifacts/experiments/ascii-dot-repeat-20261031/evidence.tar.gz),
SHA256 `96DA808D3A2FB850DF823485087EFAA417A758A0F96AAE6E95F599FE434D4EC1`.
