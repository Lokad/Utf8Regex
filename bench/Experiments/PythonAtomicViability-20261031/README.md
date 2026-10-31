# Impossible Python atomic literal runs

Accepted source: `f61502dc`; baseline: `193af7c3`. Sequence construction proves
that an atomic greedy unbounded unfolded literal repeat followed immediately by
the same required literal can never succeed. It emits the existing Fail node.
Scalar discovery checks an unconditional leading Fail after strict full validation
and the original publication checkpoint, before acquiring VM scratch. No new
matcher, retained field, owner, option or API. Complete parse metadata still
defines captures; alternatives, negative assertions and conditional captures
retain their semantics. Lazy/finite/folded/compound atoms keep verification.

The original construction budget admits one bounded adjacency scan. A proved miss
uses no VM capture workspace and may therefore fit tighter execution working/
result/depth caps. [Contract](../../../docs/SPEC-PYTHONRE.md) records this intentional
route-dependent admission change. Supplementary parser atoms are compound and
remain verified; this optimization does not claim every scalar spelling.

[Release validation](verification.txt): complete PythonRe4387/runtime and affected
Core68/runtime pass on .NET10/11; the selected snapshot check also passes both.
Tests cover actual a++a, ASCII/BMP and supplementary exclusions, lazy/bounded/
folded/captured hits, complete capture catalogs, alternatives/negative assertions/
conditionals, retained captures after funded close, strict invalid tails/byte
offsets, useful work denial and original cancellation/deadline publication.
The initial test incorrectly expected a compound supplementary atom to use this
literal proof; its failure remains archived and its semantic route is preserved.

The [initial public A/B](comparison.json) took80.452s, minimum23.492ms,
fourteen cases. Search has before/after and diagnostic predecoded/decode-then
compiled Regex lanes; construction+Dispose has only the two producer lanes.
Search retains/checks complete UTF-16 start/end digests after timing; setup and
compilation are excluded. Warm1s/min128 calls, independently calibrated80ms
lanes and three alternating samples. Quiet Ubuntu VM/.NET11 RC3/CPU mask4.
Core DLL bytes are identical between producer lanes. Official CPython is separate.

| Initial public operation | Before us | After us | Allocation B before / after |
| --- | ---: | ---: | ---: |
| Actual possessive miss | 2.408 | 0.219 | 3552 / 0 |
| Actual atomic miss control | 2.848 | 2.835 | 3552 / 3552 |
| Possessive hit | 1.807 | 1.874 | 3552 / 3552 |
| Finite-repeat hit | 1.625 | 1.704 | 3552 / 3552 |
| Lazy atomic hit | 1.632 | 1.652 | 3552 / 3552 |
| Captured negative assertion hit | 0.946 | 0.727 | 5744 / 4064 |
| Finite/live miss | 1.924 | 0.223 | 3672 / 120 |
| Finite/live hit | 1.356 | 2.202 | 3672 / 3672 |
| Construction, target | 99.886 | 230.171 | 50432 / 47888 |
| Construction, hit | 114.324 | 139.678 | 62200 / 62200 |

The finite/live-hit +62.32% and construction +130.43%/+22.18% medians were
unsettled: up to2.26x spreads and substantial first-sample drift. All unfavorable
results are preserved. An initial preflight also rejected .NET dot/astral
code-unit semantics; that diagnostic row uses BMP overlap, while production
correctness tests retain supplementary scalars. No timing used the wrong oracle.

The **sole [confirmation](confirmation.json)**, unchanged source/DLL/runtime,
selected six public cases/two producer lanes: warm4s, five alternating80ms samples,
58.229s total/minimum80.996ms. No further measurement of this prototype.

| Confirmation public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual possessive miss | 1.565 | 0.164 | -89.51% |
| Finite/live hit | 1.272 | 1.248 | -1.90% |
| Construction, target | 46.323 | 43.718 | -5.62% |
| Construction, hit | 82.072 | 82.997 | +1.13% |
| Literal Search control | 0.122 | 0.155 | +26.93%, unsettled |
| Literal construction control | 58.343 | 58.781 | +0.75%, unsettled |

Actual miss spreads1.08x/1.03x; finite hit1.08x/1.02x; target/hit construction
at most1.02x. Allocations are unchanged from the initial run. Literal Search
retains overlapping0.11–0.18us modes and1.58x/1.62x spreads; construction control
also drifts. No precise control regression/equivalence or blanket10% claim.
The stable target/hit/construction evidence supports promotion. Full raw spreads
and samples are in both summaries; do not substitute point estimates for verdicts.

Existing [selective refresh and official CPython qualification](refresh-case.txt)
changed only possessive/search-miss on clean matching `f61502dc` source/PDB/DLLs.
The prior qualified7.96x gap becomes a paired estimate **0.692 [0.527,0.718]**,
but the current verdict is **Inconclusive**: lane interquartile spreads1.111/1.264
exceed1.10. No qualified CPython-win claim or retry. The separate raw producer
median is1.140us, range0.601–1.369us,0B; it is not the paired timing or A/B result.
The byte-control engine verdict is NotApplicable: this is owned Python failure
execution, not a native Core result lane. Atomic miss remains unchanged/queued.
All other cases, lifecycle/scaling and metadata remain identical;
[scope check](snapshot-scope.txt), generated page verification and snapshot tests pass.

[Original driver](driver.cs.txt), [confirmation driver](confirmation-driver.cs.txt)
and [identity manifest](identities.json) bind the precommit prototype to its
production patch/tests. The committed refresh has separate clean identities.
Immutable archives:

- [Raw/source/DLL/PDB/paired qualification](../../../artifacts/experiments/python-atomic-viability-20261031/evidence.tar.gz):
  `487EFEF320C45D32D5B03C4DA10DFF0590D5447DA1BE8CD05E4D27B003B05149`.
- [Correctness logs/source snapshot](../../../artifacts/experiments/python-atomic-viability-20261031/correctness-source.zip):
  `6BF45777D0F9FBC92B3D39C37BFB468F3559A7DE55B8085E9DD8B3A1C6055253`.

Published/staged packages remain unchanged. Final-source cumulative operating
costs, complete sub-hour refresh and final package/platform/consumer acceptance
remain release work. Do not reopen this closed A/B/qualification campaign.
