# Finite literal Match omits unused whole-subject description

Accepted: keep the existing finite literal finder and selected-range projector,
but prepare their subject with CheckSubject rather than Subject. The native branch
never consumes the discarded whole-input UTF-16 length/ASCII/supplementary flags.
Other routes retain their full metadata. No new executor, selector state, buffer,
allocation algorithm, public API or unchecked access. Eight net production lines.

This cause is distinct from the [rejected trusted helper hypothesis](../TrustedScalarPreparation-20261115/README.md):
that unmetered path already returned early. Finite policies enter the owned value
preparation first. A new public valid-input test failed on the original code with
ExecutionWorkSteps128/request129, before finding an early literal; it now succeeds
with exact byte/UTF-16 coordinates. Strict input still exhausts that small budget
during mandatory whole-subject validity. No malformed trusted-input guarantee.

One71.486s ordinary-process ABBA, nine public Match rows, five150ms-target samples
per row/process,>=20ms floor,150s cap, zero confirmations. All subjects are valid
unchanged UTF-8,~1MiB; finite rows share the same work/depth/working/result limits,
five-second per-operation deadline and original live token. Complete coordinates
are verified after timing. Labels distinguish Validate from AssumeValid.

| Public Match | Before µs | After µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| bmp-early-finite-trusted | 13.956 | 0.200 | -98.56% | Separated |
| supplementary-early-finite-trusted | 14.099 | 0.247 | -98.25% | Separated |
| ascii-early-finite-trusted | 13.957 | 0.201 | -98.56% | Separated |
| bmp-early-finite-strict | 23.533 | 24.183 | +2.76% | Overlap |
| ascii-early-finite-strict | 23.522 | 23.809 | +1.22% | Overlap |
| bmp-late-finite-trusted | 50.703 | 37.802 | -25.44% | Separated |
| family-late-finite-trusted | 54.880 | 42.959 | -21.72% | Separated |
| unlimited-bmp-trusted-control | 0.187 | 0.194 | +3.85% | Overlap |
| unchanged-dotnet-match | 0.076 | 0.076 | +0.43% | Overlap |

Accept the separated early trusted and late/family gains. Strict BMP+2.76% and
ASCII+1.22% overlap; retain those observations without claiming equivalence or
normalizing by controls. This focused comparison creates no canonical native
parity ratio or general throughput claim. All allocations are unchanged:160B
per finite operation,0B unlimited Core control and208B.NET control.

Frozen baseline is the actual dcfe880a0.5.0 Windows Core/PDB pair; prototype is
based on9b03f15d plus this patch. Distinct Core hashes and all343 compiler documents
per producer verify, including generated inputs. Caller/runtime controls agree.
Stable.NET11.0.0, normal8CPU startup then fixedCPU2, one producer per process;
no VM producer rebuild. Release FullOpts entry854→920B, inner finder/projector
3454→3454B. This is codegen evidence, not isolated-kernel throughput.

Windows and Linux.NET10/11 each pass967 focused Release cases, including strict
malformed tails, original cancellation/deadlines, finite refusal, offset/tail/RTL/
NonBacktracking behavior, captures/detailed results and selected coordinate tests.
Original invocation, strict validation authority, working/result admission and
bounded discovery/projection remain. Numerical work decreases when description
is omitted; admission is still enforced. The Core specification records this.

Raw: artifacts/experiments/finite-match-preparation-20261115/evidence.tar.gz,
SHA256 `F41EA3309D8435DD31EEAA43D7835021075B2C1675DB68F8FFD13AD21B2406D0`.
Local-validation archive alongside it retains the baseline failure, final tests,
accepted patch and all sample/process medians. Original worker/collector handles
are terminal0; raw verified offVM before keeper release. No package/Full refresh;
qualified0.5.0 and all historical packages remain immutable and do not contain
this later source improvement. A future changed delivery candidate needs its
own distinct common version/qualification at a coherent boundary.
