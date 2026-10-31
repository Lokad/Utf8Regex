# Rejected literal-block repeat prototype

**Rejected and fully reverted.** Frozen baseline `e1240a20`. No execution,
dispatch, tests or contract changes from this prototype remain in the repository.
The existing qualified Unicode FullMatch snapshot remains unchanged.

The [warmed public inventory](inventory.txt) confirms 13312 bytes, 43752B/call
and a twelve-instruction FullMatch program: one greedy repeat of seven unfolded
literal scalars, RepeatEnd/RepeatExit, VeryEnd and Accept. The prototype extended
the existing no-rewind dispatcher to encode at most 64 literal scalars into a
fixed 256-byte stack span and use checked StartsWith comparisons before VeryEnd.
Outer captures retained their original trails; inner captures/choices, folding,
other tails and large/RTL/NB shapes retained ordinary execution.

The proposed route admitted bounded encoding, each comparison and publication
under the original input-work/token/deadline authority. It deliberately consumed
fewer work units and rewind records, while retaining base workspace and result
admission. These proposed contract changes were reverted with the code.

[Prototype Release checks](verification.txt) passed on net10/net11: complete
Core 41017/runtime, PythonRe 4446/runtime and PCRE2 6442/runtime. New witnesses
covered offsets, 64/65-scalar bodies, NUL/newline/four-byte scalars, partial tails,
nullable/wide bounds, captures/retained results and interruption/refund/resource
admission. Correctness did not justify the measured control regressions.

One [public FullMatch A/B](comparison.json), **68.438 seconds**, eight cases/four
equal-result lanes, Ubuntu VM/.NET 11 RC3/CPU mask 4. Warm 1.5s/minimum 64 calls,
independent 60ms calibration and five alternating samples; minimum **42.364ms**.
Primary byte and derived UTF-16 ranges are consumed and verified. Diagnostic
compiled .NET lanes include byte-coordinate projection; they do not establish
the official CPython comparison or a new benchmark Status. Construction and
cold derived-program preparation are excluded. Full validation remains included.

| Public FullMatch | Before us | Prototype us | Change | Before / prototype allocated B |
| --- | ---: | ---: | ---: | ---: |
| Actual Unicode full hit | 144.387891 | 42.077441 | -70.86% | 43752 / 1680 |
| Actual finite/live | 163.393945 | 46.543457 | -71.51% | 43872 / 1800 |
| Actual deadline/live | 173.692480 | 58.763525 | -66.17%, unsettled | 43920 / 1848 |
| Supplementary offset | 0.942374 | 0.706902 | -24.99%, unsettled | 3552 / 1680 |
| Partial tail | 28.307617 | 5.458240 | -80.72% | 12120 / 1680 |
| Captured-body control | 13.360583 | 15.745483 | **+17.85%** | 7816 / 7816 |
| Folded-body control | 11.100122 | 13.659424 | **+23.06%** | 12120 / 12120 |
| Native literal control | 0.133263 | 0.119203 | -10.55%, unsettled | 0 / 0 |

The captured control has <=1.015x spreads; every paired sample is 17.30–18.64%
slower. The folded control has <=1.025x spreads; every paired sample is
22.48–24.09% slower. These are demonstrated public-operation penalties, not
isolated-kernel estimates or mode-selection noise. **No confirmation** was needed
to reject the prototype. The native-literal control is bimodal (1.67/1.69x), so
its apparent gain is not causal evidence. [Full summary](summary.json) preserves
the paired effects and all spreads. No method-order/inlining cause is proven.

Actual input SHA-256:
`2CD7BD658BD1B142A30FDF94DF801CFFD3CBA9778D4A9557B6B1B43A0EB2F5FB`.
Finite policy: work 1 million, depth 4096, working/result 16MiB; deadline 1 second.
The clear target gain does not pay for these regressions on unoptimized shapes.
Future work needs a distinct placement/codegen cause; do not retune this pilot
or introduce an eligibility switch to hide the cost.

[Driver](driver.cs.txt), [preflights](preflight-linux.txt) and
[manifest](identities.json) bind the actual before/after DLLs. The manifest also
hashes the new execution source, which was untracked when the ordinary patch was
captured; complete rejected execution/test files are preserved in the archive.
The initial inventory uses correctness-equivalent `035a621c` binaries; Core and
PythonRe production source was unchanged between that revision and `e1240a20`.

- [Raw/source/binaries](../../../artifacts/experiments/python-literal-repeat-20261101/evidence.tar.gz):
  `1E8B8CD60867FDEA34B303E9CD318121CA635D74718BBDB81EAF53282EB1743D`.
- [Correctness/source](../../../artifacts/experiments/python-literal-repeat-20261101/correctness-source.zip):
  `FA7D905EE46771AD36E4A57BEFD3711E3E5AE65E148EF70E2E59AA771D205603`.

No official CPython qualification, catalog refresh, package or platform matrix was
run for the rejected candidate. Published/staged artifacts remain immutable.
Resume the existing Core window queue; final-source cumulative/current full
refresh/package/consumer gates remain outstanding.
