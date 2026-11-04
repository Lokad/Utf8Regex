# First-scalar continuation-pair Count: rejected

The checked pair-search candidate improves the actual Chinese literal Count by
4.43–5.06%, but costs 98.39% on repeated overlapping prefixes. The small corpus
gain does not justify that regression and the additional loop. Production retains
the existing whole-needle search. Boundary tests and all observations are retained;
there is no confirmation, tuned variant or new parity claim.

The proposed change affected only the existing unlimited exact-UTF8 Count branch
for literals beginning with a three-byte scalar, after earlier fused priorities.
It searched the first scalar's two continuation bytes, verified the whole literal,
and used the original whole-needle search for the remainder after eight failed
verifications between matches. It added no metadata, buffers, allocations, owner,
engine, mode or unchecked operation. Complete strict validation stayed in place.
Finite/live-token/deadline invocations kept their existing routes.

An untimed inspection of the frozen 813478-byte Chinese corpus found 53 candidate
shapes for the fixed offset-one pair, versus 751 first/last-byte shapes, with 30
verified matches. This is a census, not an executed BCL branch count or speedup.
Selection used the pattern only. Inspection archive SHA256 is
`2FF0737CF65238E8ED64049E21376333803602FFF2D7F61CB4C7A55AA6EFF807`.

One **78.8568399-second**, 15-case public before/after comparison ran on .NET 11 RC3
on the quiet VM. Core baseline is `e53ec824`; all six companion DLL/PDB files match
the frozen `ba457088` Full graph. Only the Core overlay differs, despite sharing
the same informational revision. [Observations](cost-summary.json) retain exact
binary hashes, every case, allocations and spreads.

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual Chinese |93.23|89.10|-4.43%|
| Actual Chinese, compiled |94.53|89.75|-5.06%|
| Actual Chinese, finite/live control |1184.66|1146.43|-3.23%|
| Actual Chinese, deadline/live control |2143.89|2090.55|-2.49%|
| Dense mixed literal |396.96|403.99|+1.77%|
| Frequent wrong lead sharing the pair |18.35|18.19|-0.91%|
| Frequent wrong pair sharing the lead |65.96|18.17|-72.45%|
| Repeated overlapping prefixes |105.42|209.14|+98.39%|
| Constrained three-byte scalar |160.08|162.70|+1.64%|
| Python exact mixed literal |401.69|406.74|+1.26%|
| ASCII control |2.47|2.44|-1.10%|
| Four-byte control |129.92|130.48|+0.43%|
| Two-byte control |98.05|98.62|+0.58%|
| Short fused three-byte control |166.40|173.68|+4.38%|
| PCRE2 control |148.92|166.40|+11.74%|

Allocations are unchanged for every case: zero except finite/live 112 B/op and
deadline/live 160 B/op. No causal credit is assigned to unchanged-route controls.
Their movement, including the PCRE2 increase, limits attribution of small costs;
it does not erase the repeated-prefix result. Spreads are at most 1.074 before
and 1.038 after. The false-pair win is synthetic, not a catalog-wide improvement.
Local regressions are permitted for substantial gains; this rejection does not
restore a universal 10% gate.

Normal eight-CPU runtime startup preceded CPU2 affinity. Managed warmup was at
least 1.5s/128 calls, BCL diagnostics 250ms/64 calls, followed by independent 60ms
calibration and five alternating samples. All **150 owned samples exceed 55ms**.
Typed public results are checked after timing. Same-options predecoded and
decode-then-Regex lanes have equal results on the supplied overlapping subjects;
they are diagnostics, not refreshed benchmark Status or CPython qualification.

Windows and Linux .NET 10/11 each pass focused Core 780, PythonRe 176 and PCRE2
165 checks. They cover false pairs, nonoverlap, sliced storage, offsets and tails,
strict malformed suffix rejection, constrained/supplementary scalars, RTL/NB,
original cancellation and resource controls. Production source was restored and
the 18 new boundary checks rerun on both platforms/runtimes. Existing staged
packages, Full tables and all published bytes remain unchanged.

Immutable local archives under `artifacts/experiments/utf8-leading-pair-count-20261104/`:

- `evidence.tar.gz`: `4585EACC443FBDE943E2B9902F888CE0F091F7E963F3C7F24BC144FC339D4FFE`.
- `windows-correctness.zip`: `AE4FC13A193BF362255717E8051B1CEFC35C2CC8DD426BDDE7061B8A76FB4491`.
- `restored-correctness.zip`: `D518C0C3832C0F5853CFCE9B94BFDFD9BA9DA71FBA8DFD9E8A1DFEAE9C3D1107`.

The timing archive includes source overlay, declaration, driver, before/after
binaries, raw samples and Linux validation. No full refresh or package matrix
was repeated for this experiment. The completed checked-access campaign stays
closed.
