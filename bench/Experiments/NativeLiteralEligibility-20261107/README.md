# Immutable literal eligibility: not retained

**Rejected and restored.** The trial computed the existing primary-program
`CanFindNativeLiteralForResult` proof once in Core's constructor. A get-only
property replaced repeated classification; Count and derived-program proofs
were unchanged. Windows/Linux FullOpts getters changed from a 16-byte tailcall
to a five-byte field read, with identical untimed eligibility answers.

ONE [public comparison](cost-summary.json) completed in **108.562946031s**:
separate Default-process ABBA, 21 rows/420 samples, minimum 66.0643 ms. Core
`D91EC6B9` became `4760831D`; PythonRe, PCRE2 and corrected caller bytes were fixed.
Normal eight-CPU startup preceded CPU2 affinity, default tiering/PGO, 750 ms/minimum
32-call warmup, independent 75 ms targets and five samples. Results were checked
after timing. The 120-second outer and 35-second producer caps remained unchanged.

| Public operation | Before us | Trial us | Change |
| --- | ---: | ---: | ---: |
| Folded Search miss | 0.383 | 0.373 | -2.54% |
| Same finite/live | 0.520 | 0.528 | +1.45% |
| Folded Search hit | 0.380 | 0.367 | -3.45% |
| Unicode literal miss | 0.386 | 0.385 | -0.24% |
| Mail Count | 2250.07 | 2230.57 | -0.87% |
| Same finite/live | 3392.92 | 3329.67 | -1.86% |
| Same deadline/live | 3596.04 | 3566.02 | -0.83% |
| Core construction plus Options getter | 31.13 | 31.53 | +1.28% |
| PythonRe construction plus Options getter | 140.39 | 139.76 | -0.45% |

The miss saving is only 9.73 ns with overlapping ranges; finite/live miss is
slightly slower with overlapping ranges. The hit saves 13.12 ns consistently
with disjoint ranges. This small benefit does not justify another retained
property without a useful overall target improvement. Core construction costs
0.3983 us more, also with overlapping ranges. All 21 allocation medians remain
unchanged, including both construction observations.

Core literal Count improves 4.28% with disjoint ranges on its unchanged algorithm;
the PCRE control's +5.27% and other adverse rows overlap. Preserve all
[process observations](process-observations.json) without normalization or
unmeasured causal attribution. This is a bounded rejection, not proof that
precomputed eligibility generally regresses. No confirmation, tuning variant,
package qualification, Full refresh or new benchmark Status followed.

Prototype Release checks pass on Windows/Linux, each .NET10/11: Core 1,434,
PythonRe 442, PCRE2 122. Restoration passes Core 512/PythonRe 71 per runtime/OS;
Windows Core matches its baseline bytes. Frozen Full50 assets, dev.50 packages
and all historical staged/published bytes are unchanged.
[Decision](decision.json), [declaration](declaration.json) and
[restoration](restoration.json) retain identities and scope.

The initial untimed preflight expected `None` instead of the default Core
constructor's `CultureInvariant`. One expected-value correction preceded the
sole timed comparison; no producer or timing setting changed. The first archive
was created inside its source directory and reported a directory-mtime warning;
it remains readable, immutable and hash-verified off VM. Final export moved outside
that directory. [Preparation corrections](preparation-corrections.json) preserve
these details; the restoration supplement also retains a corrected local guard.

Canonical off-VM evidence under `artifacts/experiments/native-literal-eligibility-20261107/`:

- `evidence.tar.gz`: `E69707FF0F6CDBB3ABBDA8AE64E2706F0337EDBE97FC69857018BC6998567F03`.
- `failed-preflight-evidence.tar.gz`: `DCFFBC78B47A1E8BE08B6AFD9E57762646F5D93894F1CA039984886B58A25AA3`.
- `restoration.zip`: `9FFA71F97BC69F1BC2E1A8D4AC8C46C0AE5E497AB562C5A60B277D6195302B45`.

All build/test/comparison handles are terminal and closed. Production retains no
part of this trial. Broader performance work remains active.
