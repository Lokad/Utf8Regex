# PythonRe retained UTF-8 template substitution

Producer **4cab6712ffedcfb25f24840f07ba9995385345be**, baseline
**46efb4112f2cc2930982c1bdcc18bead3fe35d18**. This baseline has the same production
source as the intervening evidence-only 8ee5a23f commit. One bounded comparison
and one control confirmation; no package or catalog refresh.

`SubnRetained` requires caller-funded Compile and returns one disposable receipt
over the final byte array/count. It keeps eligible native exact-literal replacement
and scalar substitution, transferring their existing admitted output charge without
another copy or release/reacquisition. Temporary match/writer storage refunds.
The receipt conservatively admits 128 modeled bytes before acquisition, adds
48 actual managed bytes per call, drops its own payload on explicit disposal and
refunds once. Previously obtained arrays remain valid and caller-owned after
release. No finalizer or ownership framework. Result traffic remains cumulative.
See the [profile contract](../../../docs/SPEC-PYTHONRE.md).

Full PythonRe Release passes **4170 per runtime**, including the public API
snapshot. All **24 new cases per runtime** pass, including the strengthened
final-acquisition cancellation/deadline witnesses. Covered cases include native,
scalar, supplementary captures, offsets, count/empty progression, invalid templates
on no match, partial admission, live retained refusal/refund, all four numeric
controls, strict malformed tails, concurrent disposal and escaped array validity.
A weak-reference test proves the disposed receipt drops its payload; refunds do
not depend on collection. Initial fixtures incorrectly expected a named-reference
exception and bypassed writer traffic; fixes preserved existing contracts.
The archived failed logs also retain a too-small working-limit fixture that hit
template construction first; the final witness specifically reaches execution.
[Verification](verification.txt) summarizes the passing runs. Core/PCRE2 production
source is unchanged; their package/platform matrices were not repeated.

The quiet VM ran .NET 11.0.0-rc.3.26522.148 on Ubuntu 24.04.4, Xeon Platinum 8573C,
CPU affinity mask 4. Independent lane calibration, three alternating samples,
public calls through typed bridges in isolated assembly contexts. Outputs stay
alive through timing; equality/checksums run afterward. Every expression/account
is disposed and checked for balance. Comparators use overlapping scalar semantics;
byte-output rows include UTF-8 output encoding. Regex.Replace does not produce a
replacement count; candidate Subn produces one but this output consumer ignores
it. These are feature/control observations, not catalog Status or parity claims.

The [first comparison](comparison.json) took **22.89s** under a 120s cap, with
500ms warmup and a 40ms calibration target. One comparator sample shortened to
14.56ms, so its baseline is not qualified as a warm parity result. The tiny
unfunded byte control was +10.49%; a single [confirmation](confirmation-comparison.json)
used four rows, 1s warmup and an 80ms target under a 90s cap. It took **26.52s**,
minimum batch **56.02ms**. No further measurement campaign.

| Public workload | First before / after, us | First change | Confirmation before / after, us | Confirmation change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Unfunded byte Replace control | 1.682 / 1.859 | +10.49% | 1.727 / 1.862 | +7.80% | 168 / 168 |
| Funded byte Replace control | 2.144 / 2.250 | +4.92% | 2.120 / 2.336 | +10.19% | 632 / 632 |
| Funded text Replace control | 14.509 / 14.128 | -2.63% | 14.667 / 13.800 | -5.91% | 5816 / 5816 |
| Native retained output | 2.125 / 2.320 | +9.16% | 2.113 / 2.407 | +13.93% | 632 / 680 |
| Scalar retained output | 14.776 / 14.177 | -4.06% | — | — | 5496 / 5544 |
| Unicode/captures retained output | 17.921 / 17.735 | -1.04% | — | — | 6752 / 6800 |
| Finite/live-token retained output | 16.961 / 16.374 | -3.46% | — | — | 5504 / 5552 |

Legacy byte calls have a measurable small-call cost; unchanged allocations do not
establish timing equivalence. Retained rows add ownership authority absent from
the baseline. Scalar timings are observed complete-operation differences, not a
claimed isolated speedup from receipt acquisition. Preserve this cost in the
final fixed-baseline cumulative assessment; the completed checked-access campaign
and its separate allowance are not reopened. Remaining hosting work includes
scalar coordinates and structured positioned diagnostics, followed by the existing
performance and actual consumer/package release gates.

Raw [driver](driver.cs.txt), [project](driver.csproj.txt),
[console](comparison.txt), [preflight](preflight.txt),
[confirmation driver](confirmation-driver.cs.txt) and
[confirmation console](confirmation-comparison.txt) are retained here.
Immutable local evidence under artifacts/experiments/python-retained-subn-20261030/:

- python-retained-subn-4cab6712.tar.gz:
  `E19276A66BDA7C350398803B7455F4E4CCD4960796C115E1AC51ADFA3E2F151D`.
- correctness-logs.zip:
  `0A71EB01BCAD075029335D776764B14D4A071D9FE6D62CE6039C04BF4909662A`.

The archive includes source bundle, both DLL/PDB pairs, exact drivers and their
binaries, all raw samples/preflight/build output. Published/staged artifacts are
unchanged. API snapshots and packaged contract documentation are updated in source;
package qualification remains at the release boundary.
