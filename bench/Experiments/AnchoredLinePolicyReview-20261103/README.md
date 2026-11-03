# Bounded line-start search: accepted workload trade-off

**Accepted** on current source under the user's policy allowing local regressions
for relatively major gains. The exact runtime patch from the
[earlier rejection](../AnchoredLinePair-20261102/README.md) is restored; that
historical evidence remains unchanged. Imports was the largest remaining Core
gap at **7.131x** matching predecoded .NET after the keyword improvement.

For multiline beginning anchors with an immediate mandatory unfolded ASCII
literal, the existing scalar seeker searches checked spans for LF/first-byte
pairs. Blocks are at most 2,048 bytes, overlapping one byte. The original VM
verifies candidates. Unsupported prefixes/options keep their existing search.
No retained metadata, heap allocation, engine, switch or unchecked access is added.

One **99.844s**, 16-case public Count/Match comparison used frozen `6f108ad7`
Core/caller DLL/PDBs against Core `8d4a19d8`. Only candidate Core was rebuilt;
benchmark/PythonRe/PCRE2 caller bytes are identical. Actual imports input remains
46,429 bytes, 19 matches, SHA256
`507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2`.

The existing warmup, independent calibration, alternating five samples, normal
runtime startup followed by CPU2, and post-timing result consumption are retained.
Minimum timed batch **29.399ms**. Reference Regex options now match each workload;
the old driver forced compiled/invariant diagnostic comparators. Producer
operations and sampling are unchanged. No confirmation, variant or matrix replay.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Imports Count, ordinary | 31.67 | 16.29 | -48.57% |
| Imports Count, compiled | 31.78 | 16.21 | -48.99% |
| Imports, finite/live token | 37.03 | 18.37 | -50.39% |
| Imports, deadline/live token | 62.92 | 19.42 | -69.14% |
| Dense newline miss | 83.74 | 1.60 | -98.09% |
| Dense false-prefix miss | 92.74 | 98.12 | +5.80% |
| Sparse anchored miss | 0.880 | 1.040 | +18.19% |
| Dense import hits | 101.79 | 100.58 | -1.19% |
| Unchanged literal control | 0.353 | 0.352 | -0.43% |

All allocation medians match. The primary operation saves about 15.5us; dense
false-prefix misses cost about 5.4us more, sparse misses 0.16us more. Python's
anchored control is +3.89%, PCRE2's -3.13%; all 16 results, allocations and spreads
remain in [the summary](summary.json). This is not a universal improvement.
Producer spreads do not block the large target gain or stable local costs.
The first ordinary decode comparator drifted during sampling; its raw data is
retained without a parity claim or rerun. The older literal-control movement
(-15.86%) is not reused as evidence for this source.

Focused Core checks pass **814 per target** on .NET 10/11; full PythonRe passes
**4,647**, full PCRE2 **6,486 per target**. Restored tests cover every ASCII first
literal, block edges/offsets/tails, LF/CRLF/Unicode, captures, unsupported options,
strict malformed-input rejection, work/working admission and original-token
cancellation/absolute deadlines through production entry points and the seeker.

Scanning steps/checkpoints now follow bounded candidate blocks, so exact finite
work requirements can change under the existing route/version contract. Useful
metering, bounded interruption, final checks, working/result admission and
capture/result/cursor lifetimes remain. No API or ownership contract changes.

Immutable raw evidence under `artifacts/experiments/anchored-line-policy-review-20261103/`:

- `evidence.tar.gz`: `D01B68B20092370FBF8C5FF132D0A02B34010E061EC70B4B9420256C816F1391`.
- `correctness.zip`: `C536C1BE78067714D3F0B1AB390E770726C13ABB666C682E018E7DF49BABBBE1`.

The comparison is separate from the catalog verdict and package qualification.
One selective six-lane refresh on documentation-equivalent source `3bd89b41`
updated only the ordinary/compiled import records. The compiled ratio fell
**7.131 -> 3.442x** predecoded .NET; ordinary is **2.408x**. Matching allocation
remains 3,584 B. The existing parity verifier passes; all three files were
hash-verified and imported with other rows unchanged. Raw `selective.tar.gz`:
`D53FCC60D707BC90A8E2DB08949EF53AE61DF583842822866F392DD58A827497`.
Dev.18 packages and their frozen 44m12s Full receipt stay immutable;
package/platform matrices remain batched at the next coherent delivery boundary.

Source CI `41706173935` later failed on an older test whose 64-step interruption
hook was never reached after skipping the whole input in one block. Test-only
`3012cc48` precharges the invocation to reach that actual checkpoint, retaining
original cancellation/deadline, exact step and borrowed-loan assertions. Both-
runtime focused checks pass; production code and measured evidence are unchanged.
