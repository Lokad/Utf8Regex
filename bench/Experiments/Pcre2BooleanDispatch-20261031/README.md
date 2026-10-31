# PCRE2 Boolean dispatch frame

Accepted source `e1678514`; frozen baseline `035a621c`. Existing plain
backtracking, literal and character IsMatch arms now precede a private method
containing the specialized dispatch. Both paths share the original invocation
and one budget passed by reference. Validation, matchers, captures, work/depth/
working/result admission, cancellation and deadlines are unchanged. No new
executor, field, policy, execution mode or unchecked access.

Linux Release FullOpts disassembly reserves **12040 bytes (0x2F08)** before,
**1568 bytes (0x620)** after, with roughly 11KiB versus 1KiB of entry clearing.
[Before](before-disasm.txt) and [after](after-disasm.txt) are excerpts; complete
listings are in the immutable archive. This diagnostic disables tiering and has
no PGO data. It establishes a codegen hypothesis, not public throughput. The
before listing uses `9a79aefc`, whose production source is identical to `035a621c`.
Public measurements below restore default tiering/PGO.

[Existing production-route inspection](baseline-inspection.txt) ruled out
candidate search, fixed workspace and another fixed-repeat lowering as causes:
the anchored subroutine case has one candidate, eleven VM steps, an existing
three-character token scan and two control-stack rents. The negative conditional
cases have no workspace rents. Inspector single-shot elapsed/layout/allocation
readings are diagnostic; they do not measure warm public costs.

[Release validation](verification.txt): complete PCRE2 **6442 tests/runtime**
and updated snapshot checks pass on net10/net11. No forwarding executor or test
that mirrors the method split was added. The frozen comparison verifies identical
Core DLL bytes and matching before/after execution plans.

One [public IsMatch A/B](comparison.json), **63.605 seconds**, ten cases, quiet
Ubuntu VM/.NET 11 RC3/CPU mask 4. Warm 1.5s/minimum 64 calls, independent 60ms
calibration, five alternating samples per lane; minimum **39.47ms**. Full strict
validation included, construction excluded, results checked after timing.
Optional compiled predecoded/decode-then .NET lanes use equal overlapping
semantics; unsupported subroutine/ACCEPT grammar has explicit exclusions.
**No confirmation**: main gains and the unchanged control support the decision.

| Public IsMatch | Before us | After us | Change | Allocated B, unchanged |
| --- | ---: | ---: | ---: | ---: |
| Subroutine hit | 1.089835 | 0.955795 | -12.30% | 0 |
| Subroutine miss | 1.092958 | 0.955299 | -12.60% | 0 |
| Subroutine finite/live | 2.807330 | 2.536377 | -9.65% | 1760 |
| Subroutine deadline/live | 3.062106 | 3.252240 | +6.21%, unsettled | 168 |
| Conditional negative | 0.969293 | 0.827835 | -14.59%, unsettled | 0 |
| Conditional ACCEPT negative | 0.915140 | 0.798505 | -12.75% | 0 |
| Slice backreference sibling | 17.676587 | 17.725000 | +0.27%, unsettled | 14312 |
| Literal | 0.320679 | 0.263200 | -17.92%, unsettled | 0 |
| Character | 0.317440 | 0.195023 | -38.56%, unsettled | 0 |
| Cached URI unchanged control | 0.127774 | 0.127171 | -0.47% | 0 |

Subroutine hit/miss/finite spreads are <=1.05x, ACCEPT <=1.14x and the unchanged
control <=1.03x. Other lanes contain differing modes. In the deadline lane, four
of five paired samples are faster: paired median -4.70%, range -4.87% to +6.79%.
Its independent median increase is retained; no stable deadline effect or blanket
equivalence is claimed. [All samples and paired changes](summary.json) remain.
Finite limits: work 1 million, depth 1024, working/result 1MiB; deadline 1 second.

The backreference sibling selects Pcre2Slice and exercises the remaining large
helper. The URI control proves its cached Pcre2SeparatedRunsIsMatch program
bypasses the edited dispatcher. No allocation or resource-policy exemption funds
these gains. Broader fixed costs of the flexible VM remain.

Only four leading native-comparator rows were [refreshed](selective-refresh.txt)
from clean `e1678514`, once each. Independent rows plus nine-sample paired native
qualification preserve actual source/binary identities:

| Native comparison | Managed us | Paired ratio | Status |
| --- | ---: | ---: | --- |
| Subroutine prefix digits | 0.549338 | 5.027 [4.969, 5.298] | Inconclusive: spreads |
| Conditional negative | 0.711755 | 4.695 [4.607, 4.733] | Inconclusive: spreads |
| Conditional ACCEPT negative | 0.576214 | 4.532 [4.444, 4.969] | Inconclusive: spreads |
| Conditional lookahead | 0.480363 | 3.994 [3.924, 4.014] | Inconclusive: lane order |

These are separate point estimates, not the A/B effect above or qualified native
gap closures. No status-chasing rerun. The other 122 operation rows and all sixteen
scaling families are unchanged. Markdown generation/verification and both-runtime
snapshot checks pass. Current PCRE2 uncertainty is 42 Inconclusive, 1 Unqualified,
25 Excluded; preserve the remaining gaps and their historical qualified evidence.

[Driver](driver.cs.txt), [preflights](preflight-linux.txt),
[identities](identities.json) and [clean-source hashes](clean-linux-hashes.txt)
bind the precommit A/B and separate source refresh. Immutable evidence:

- [Raw/source/binaries](../../../artifacts/experiments/pcre2-boolean-dispatch-20261031/evidence.tar.gz):
  `3FA5FB7A0D845A94CED6EB22F8D0C243FA308B1241C9C20818FA2CF73B016A12`.
- [Correctness/source](../../../artifacts/experiments/pcre2-boolean-dispatch-20261031/correctness-source.zip):
  `1AB728DBB16A66B4A7F1BC4EBE76B722D6902FC8353DB5BAB5B5C45A9AB7ADBF`.

Published/staged packages remain immutable. No full catalog/package matrix was
repeated. Final-source cumulative costs, complete current sub-hour refresh and
fresh common-version package/consumer acceptance remain release work.
