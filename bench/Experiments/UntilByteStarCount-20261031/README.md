# Count a greedy star excluding one ASCII scalar

Accepted source: `17e52f35`; baseline: `2844202e`. Public zero-origin span Count
now reuses the existing whole-pattern proof and byte-search algorithm for a
greedy unbounded star excluding exactly one ASCII scalar. Strict whole-subject
validation happens first; that byte cannot occur inside a wider UTF-8 scalar.
Each search block is at most256 bytes and carries the current nonempty run across
block/scalar splits. The original invocation meters validation, scans and final
publication, including unlimited calls. No new matcher, switch, owner or API.

The constant-space route no longer needs VM capture workspace. Tight finite
working/result/depth limits may therefore admit this Count; work units are bounded
scans rather than VM instructions. RTL/NB/ECMA/folded and unproved grammar retain
their existing routes. Detailed/captured results, offsets and funded owner entry
points are unchanged. See [resource contract](../../../docs/SPEC.md).

[Release checks](verification.txt) pass on .NET10/11: Core388, PythonRe129 and
PCRE285 per runtime, plus22 managed/API/class and3 snapshot checks. New coverage
includes NUL/DEL/general ASCII exclusions, empty/global progression, 255/256/257
and511/512 boundaries, borrowed canaries, mixed supplementary scalars, seeded
independent scalar-run counts, strict malformed tails, finite work refusal and
original mid-counter/final-publication cancellation/deadlines. Tests demonstrated
the former96-byte VM working admission and the new1-byte-policy success.
Failed initial test compilation and old-source runtime results remain archived.

One [public A/B](comparison.json), **93.305s**, eleven cases/four lanes, minimum
sample **37.872ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. The actual594930-byte
Sherlock corpus yields26105 matches. Every complete Count agrees with predecoded
and decode-then Regex; warm1.5s/min64 calls, independently calibrated60ms lanes,
five alternating samples and result batches checked after timing. No confirmation.

| Public Count | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Actual compiled | 4750.194 | 177.063 | -96.27% | 1680 / 0 |
| Actual finite/live | 5645.644 | 201.055 | -96.44% | 1800 / 120 |
| Actual deadline/live | 6197.719 | 230.206 | -96.29% | 1848 / 168 |
| Dense excluded bytes | 750.192 | 67.581 | -90.99% | 1680 / 0 |
| One long run | 59.603 | 0.686 | -98.85% | 1680 / 0 |
| NUL exclusions | 157.622 | 10.883 | -93.10% | 1680 / 0 |
| Folded control | 145.822 | 143.433 | -1.64% | 1680 / 1680 |
| Word control | 6.979 | 6.984 | +0.07% | 0 / 0 |
| Literal control | 0.563 | 0.573 | +1.74% | 0 / 0 |

Compiled and finite/live target spreads are at most1.01x; deadline after1.08x.
Actual ordinary7989.500->227.241us has1.69x/1.28x spreads, and the mixed-block
case is also noisy; their precise effect sizes are not promoted as stable.
The [summary](summary.json) preserves all rows and allocations. Explicit finite/live
and deadline guarantees retain120/168B costs; this is not a universal zero-allocation
claim, isolated-kernel gain or repayment of the separate warm-template debt.

The existing [selective refresh](refresh-case.txt) changed only
`industry/rust-sherlock-nonnewline-count`, ordinary/compiled, clean `17e52f35`
provenance. Producer medians224.454/219.826us and0B yield **0.164x / 0.258x**
predecoded ratios, formerly **4.84x / 7.89x**. Minimum sample32.052ms. Other rows,
identities and lifecycle/scaling evidence are unchanged; generated README ordering,
parity verification and both-runtime snapshot tests pass. These separate catalog
observations are not the A/B effect-size measurement.

[Driver](driver.cs.txt) and [identity manifest](identities.json) bind the A/B to
baseline `2844202e` plus its recorded production patch/new tests. The committed
refresh records its own clean source/DLL/PDB identities. Immutable archives:

- [Raw/binaries/refresh/source bundle](../../../artifacts/experiments/nonnewline-count-20261031/evidence.tar.gz):
  `DDE5501823445F679C41345D3015865426C6CFAA271FB846F93C5D10D0AE1580`.
- [Correctness logs/source snapshot](../../../artifacts/experiments/nonnewline-count-20261031/correctness-source.zip):
  `3C56FA3997F305BFB4FA61EA49BCAE2C9B89E343466C8B6B0DA2D76CFD8E9AA7`.

Published/staged packages remain untouched. Final-source cumulative operating
costs, complete sub-hour refresh and final package/platform/consumer acceptance
remain release-boundary work. The next Core queue begins with Chinese literal;
Python possessive and PCRE2 subroutine gaps remain visible in their own queues.
