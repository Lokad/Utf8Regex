# Reuse the mandatory ASCII prefix filter in scalar Count

**Accepted.** The owned scalar Count loop now reuses its existing bounded
straight-prefix filter after finding the first required ASCII bytes. Literal
and positive ASCII-set mismatches reject an impossible start before VM storage
and execution. Captures are transparent to the filter; branches, repeats,
anchors, folding and wider Unicode operations stop its optional proof. Every
surviving candidate still runs the original scalar verifier.

There is no new plan, payload, selector, executor, ownership system or mode.
Full strict UTF-8 validation precedes discovery. Finite work, original-token
cancellation, deadlines and final publication checks remain. A false prefix can
avoid local VM working admission; a real match still admits its required storage.
Captures, retained results, borrowed offsets, RTL and NonBacktracking are preserved.
[The specification](../../../docs/SPEC.md) records the route-dependent cost change.

Frozen dev31 inspection confirms the real Ruff tweaked case uses an owned scalar
plan, fixed-distance ASCII sets and no direct family. Its 32,514,627-byte subject
has UTF-8 SHA256 `FB6F37144103CC533B20FC8C735F4C65B7146D9FBD9931F3D0A231B12BC4A257`.
An untimed census found 116,678 `#` starts, 89,251 `# ` starts and only 20 full
mandatory prefixes. These are candidate counts, not instrumented VM-call counts.
The longer filter already served whitespace-led and anchored-line discovery.
The predeclaration's whitespace-only description omitted the anchored-line
caller; both existing consumers remain unchanged.

One **108.408-second** public comparison covers eight cases. Each ordinary
Default-context process loads exactly one producer, in A0/B0/B1/A1 order, using
the same typed caller, frozen companions and subjects. The baseline is the
[dev31 Full](../DeliveryDev31-20261105/README.md) Core at a88922d6, SHA256
`62ACA72F50679ADF601B2618CD692D2A0A1DDE3AE7EE8C1D8DEA9ED2A017BFE8`.
The candidate is built on parent49b424f6 with source overlay
`596A7AFBEAA63CF446F5CFC4C3266765D3EFBC446F543542DA0014B7D0202FAC`,
Core SHA256 `C9422DB8C7259B2E82A51B97716C4880A1A25FF1C8388FBCBBDB1B6E53435683`.
The six companion DLL/PDB files and original Full graph remain byte-identical.

[All observations and process spreads](cost-summary.json):

| Public Count | Before µs | After µs | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Real Ruff, ordinary |10106.62|4420.32|−56.26%|3584 / 3584|
| Real Ruff, compiled |10133.87|4393.74|−56.64%|3584 / 3584|
| Real Ruff, finite/live token |11345.53|5037.17|−55.60%|3696 / 3696|
| Real Ruff, finite deadline/live token |13323.38|5991.41|−55.03%|3744 / 3744|
| Dense true prefixes |1218.75|1039.46|−14.71%|3584 / 3584|
| Dense false prefixes |713.14|134.56|−81.13%|96 / 0|
| ASCII literal control |2.48|2.49|+0.21%|0 / 0|
| Whitespace-prefix control |67.88|67.62|−0.39%|3584 / 3584|

The actual corpus returns 20 matches in every lane. Diagnostic predecoded and
strict decode-then BCL Count lanes use matching pattern/options/results; finite
producer policies have no BCL resource equivalent. Those lanes do not establish
a new catalog Status. Retain all control movement without causal credit on the
unchanged implementations. No confirmation or source tuning follows this result.

Normal eight-CPU startup precedes CPU2 affinity. Producer warmup is at least
1.5s/16 calls; BCL diagnostics at least 250ms/four calls to bound large-subject
decode preparation. Each lane calibrates independently to 60ms, with five
alternating samples per process and typed checksums consumed after timing.
All 160 producer samples exceed **55.5977ms**. Summaries pool ten observations per
version/case and average the two middle values for an even-sized median.

Focused Release checks pass on Windows/Linux and .NET10/11: Core744, Python176,
PCRE165 per runtime. Public regressions cover padded borrowed spans, window/tail
offsets, positive unions, captures/backreferences, optional/branch/repeat/anchor
and scoped-folding stops, RTL/NB, retained results, malformed tails, work denial
and original cancellation/deadline checks through dense false prefixes. A miss
succeeds with a one-byte working limit, while a true candidate still fails VM
storage admission under that limit. Initial Windows leading-prefix66 and broader
Core744 runs reuse matching binaries; no full/package matrix is repeated.

A separate [selected refresh](selected-refresh.json) on committed206691c4 updates
only Ruff's ordinary/compiled rows. Their public Count costs are4382.85/4466.00µs,
ratios0.541/0.977 to predecoded .NET11, both Status=Win under the existing rules.
All30 six-lane samples exceed49.7022ms. Every unselected measurement, including
lifecycle/scaling evidence, is preserved. Three generated files were hash-imported;
six descending tables/178 ratios and two snapshot hosts/six checks pass SkipBuild.
The initial shell invocation passed comma-separated arguments as one flag and
exited2 before measuring; the corrected invocation reuses its matching build.
Both attempts remain archived. The previous43m24s Full remains its frozen source's
receipt; this selected update does not claim a new complete Full/package boundary.

Immutable artifacts/experiments/ruff-straight-prefix-20261105:

- evidence.tar.gz: `3172A2698FFA9F4C3B80C766FFA3348368B435892F1B830012C072E95B492B60`.
- windows-evidence.zip: `17D9D5C3DE4FA93E21B8CF0401B3B39822D116A1E78C25E7E92BF93085C55BAB`.
- selected-evidence.tar.gz: `2FB51CB836DE95997A22479F858C93F0058E9865AE19ACF54AB530B3CF40AACE`.

Raw evidence retains the declaration, exact source, both graphs, caller and
preflights, every sample, focused logs, source/companion checks and terminal0.
Published/staged package bytes and previous refreshes are unchanged. The next
package/full boundary remains batched with the remaining delivery work.
