# Reuse the prepared first-byte search for Python candidates

Accepted source lends the existing positive byte union to large, exact forward
Python literal families. The original bounded family loop still verifies every
branch before handing a candidate to the scalar VM. Core boundary/suffix
predicates remain outside this necessary-start search. Existing raw pair and
three-to-eight-needle algorithms retain their source and call signatures; the
new internal entry has one real Python consumer. No stored state, allocation,
public API, new threshold, mode or accounting framework.

This differs from the [rejected prefix-only pilot](../PythonFamilyPrefix-20261101/):
branch confirmation remains. The original-program census and untimed public
checkpoint census showed repeated scans of the same first byte in expanded
families. The latter's last checkpoints were44032→76800 for captured FindAll;
these lower bounds and diagnostic hooks are attribution, not public timing.

One95.605-second public A/B,16workloads/four equal-result lanes, .NET11RC3/CPU2 on
the dedicated VM. Frozen `2ff58324` Core/PythonRe versus `061515ae` plus the archived
patch. Typed calls, actual-loop warmup1s/minimum1000calls, independent60ms
calibration, five alternating samples of at least41.877ms, output checks after
timing. Predecoded/decode-then BCL projections use overlapping semantics.

| Public FindAllToStrings | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Dense captured values |780.135|698.959|−10.41%|
| Finite limits/live token |974.682|851.127|−12.68%|
| Deadline/live token |1487.810|1159.532|−22.06%|
| Dense false common prefixes |85.448|53.205|−37.73%|
| All-false common prefixes |168.135|104.587|−37.80%|
| Lexically folded prefix |714.574|401.023|−43.88%|

[All costs, allocations, spreads and paired changes](summary.json). Allocations
are unchanged in every case; the all-false miss remains64B. Largest adverse
control median2.09%, maximum adverse pair4.96%; maximum candidate spread1.073x.
No confirmation or variants. These parent gains do not establish acceptance
against the fixed original `a40bda03`, repair the other cumulative failures, or
refresh CPython/catalog Status. Packages and snapshots are unchanged.

[Actual program/hint census](programs.json) explains the labels: `folded-control`
is affected, since lexical folding already renders16exact UTF-8 prefixes while
the program-wide IgnoreCase flag is false. The other-family control has two
needles; complete-native, long-prefix and literal controls retain their previous
routes. Both versions retain the same instructions/hints. No folding semantics
were changed.

Focused Core563 and full PythonRe4530 Release checks pass per runtime on.NET10/11.
New checks cover branch priority, Core suffix exclusion, bounded slices/windows,
UTF-8 offsets, captures, false prefixes, malformed tails, retained aliases,
every work cutoff/sticky denial, caller loans and original-token/deadline
interruption. Numeric work charges fall under the existing route/version-dependent
resource contract; comparison/publication checkpoints and all major admission,
working/result/depth scopes remain. No public contract change or package matrix.

Immutable artifacts under `artifacts/experiments/python-family-byte-union-20261101/`:

- `evidence.tar.gz`: SHA256 `4CD1104A1314F21B3EFBBA1A19052733D4C6F9C4B012C0E8AABE6DDAEE8A2945`.
- `correctness-attribution.zip`: SHA256 `009D17EC0847384719E033FBFFC6D361B9B463E88597AA0CC36D1579A75B4508`.

The archives retain measured binaries/PDBs, original source/applied patch, driver,
predeclaration, identities, preflight, raw samples, tests and untimed census sources.
