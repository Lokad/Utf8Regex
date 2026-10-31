# Controlled native entry without discarded UTF-16 metadata

Accepted source `5f5b714e`; baseline `debbe46b`. TryStartNativeLiteralInvocation
now calls the existing metered ThrowIfInvalidOnly instead of deriving and
discarding a full UTF-16 validation descriptor. Its IsMatch/Count/value-Match
callers consume bytes or independently project their selected result range under
the same authority. Full strict validation,2048-byte block admission/error
offsets, work, original token/deadline and required coordinates are preserved.
No new mode, field, owner, API or accounting mechanism; no metering removed.

Numeric finite Count already used validity-only validation and is an unchanged
control. Uncontrolled default paths likewise retain their established strategy.
This removes unnecessary work in existing controlled paths; it does not alter
the scalar, result/lifetime or resource contract or remove required UTF-16
interoperability coordinates.

[Focused Release checks](verification.txt) pass on .NET10/11: Core619/runtime,
PythonRe108 and PCRE244/runtime. New all-three-entry-point coverage includes
ASCII/folded/UTF-8 families, mixed supplementary prefixes at2045–2049 positions,
borrowed canaries, independent byte/UTF-16 coordinates, malformed surrogate-byte
tails and original pre-cancellation. Existing validation/search/publication
work/deadline and projection tests remain intact.

One [public A/B](comparison.json), **95.055s**, eleven cases/four lanes, minimum
sample **60.112ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Complete Count,
Boolean or UTF-16 start/end range digest agrees with predecoded/decode-then Regex
on overlapping semantics. Full strict validation and required projection are
included; setup/compilation excluded. Warm1.5s/min64 calls, independently
calibrated60ms lanes, five alternating samples; batches are checked after timing.
**No confirmation**: gains and unchanged controls already support the decision.

| Public operation | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Chinese Count,live | 1937.325 | 1183.283 | -38.92% | 120 / 120 |
| Chinese Count,deadline/live | 4222.681 | 3518.459 | -16.68% | 168 / 168 |
| Chinese IsMatch,live | 1633.581 | 847.508 | -48.12%, unsettled | 120 / 120 |
| Chinese value Match,live | 1958.334 | 1266.620 | -35.32% | 120 / 120 |
| Chinese value Match,deadline/live | 2318.184 | 1618.402 | -30.19% | 168 / 168 |
| Ordinary Count control | 135.994 | 139.004 | +2.21% | 0 / 0 |
| Numeric-finite Count control | 1909.039 | 1855.419 | -2.81% | 120 / 120 |
| ASCII IsMatch | 0.743 | 0.515 | -30.74%, unsettled | 120 / 120 |
| ASCII value Match | 1.548 | 1.169 | -24.48% | 120 / 120 |
| Folded mixed IsMatch | 17.857 | 10.004 | -43.97% | 120 / 120 |
| UTF-8 family value Match | 35.439 | 27.931 | -21.19% | 120 / 120 |

Chinese Count/value-Match target spreads are at most1.03x; controls at most1.03x.
Chinese IsMatch has1.24x/1.16x spreads, ASCII IsMatch1.68x/1.55x; do not promote
their exact median changes as stable effect sizes. The full [summary](summary.json)
retains every spread. No blanket equivalence/10% or universal zero-allocation
claim; explicit controls still cost120/168B. The earlier uncontrolled Chinese
corpus gap is not claimed solved by this controlled-only change.

[Driver](driver.cs.txt), preflights, raw console and [identity manifest](identities.json)
bind exact before/after DLL/PDBs to the precommit source/test patch. Immutable
archives also retain the accepted source snapshot/bundle and correctness logs:

- [Raw/source/binaries](../../../artifacts/experiments/native-validity-only-20261031/evidence.tar.gz):
  `E38A1FB3DE2C5B09CFC9DBB838ECDE567F76DB852B2F000514E12FA0EEB3F9A5`.
- [Correctness/source snapshot](../../../artifacts/experiments/native-validity-only-20261031/correctness-source.zip):
  `8302684BCB02077FCB22DB80ACBD3A468D3EA3BF6D31601FC401184CE9111DF1`.

Default catalogs are unchanged; no full catalog/package qualification was repeated.
Published/staged packages remain immutable. Final current-source resource and
performance refresh, cumulative fixed-baseline cost and fresh package/platform/
consumer acceptance remain release gates. Resume the primary ratio queue.
