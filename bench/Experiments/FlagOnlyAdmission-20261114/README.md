# Flag-only Core subject admission — accepted

Producer `fbf58099af4801d0e1e2d00754985cfd0db9ed70` versus44f35394, both retaining the accepted match-outcome
constructor hint. Three Core gates requested a full subject description only to
read IsAscii: controlled simple IsMatch, controlled signed-decimal IsMatch and
unbounded-word Count. Reuse existing SubjectIsAscii, deleting three description
locals and unused UTF-16/supplementary preparation. No new helper, field, cache,
framework, selector, algorithm, native eligibility or public switch.

Validate still checks the complete subject once, including malformed suffixes.
AssumeValid trusts valid unchanged borrowed text; necessary ASCII classification
can stop at the first non-ASCII block. The scalar fallback shares the original
invocation and needs no coordinate totals. Capture/scalar semantics, original
synchronous cancellation/deadlines, allocation admission and basic metering remain.
Removing unused work intentionally lowers some admission thresholds, documented
in [Core](../../../docs/SPEC.md). The public128-step probe remains denied in strict
mode; trusted early IsMatch changes from work-limit refusal to the correct match
without scanning an unrelated megabyte suffix. This untimed admission probe is
separate from equal-output timing rows, which use ample finite limits on both sides.

ONE89.620-second separate-process ABBA,12 public rows,5 samples/process/row,
240 samples above20ms,150ms target,180s cap,0 confirmations. Normal startup then
existing processor placement, .NET11GA/default tiering/PGO. All fixed valid inputs,
options, finite/live policies and output checks match within each A/B row. Separate
frozen DLL/PDB/source pairs; one identical Core-only caller, no adapter dependency.
[All process medians/ranges, hashes and allocations](acceptance.json).

| Public operation | Before us | After us | Observed change | Sample ranges |
|---|---:|---:|---:|---|
| word-ascii-strict | 15.580 | 15.205 | -2.41% | overlap |
| word-ascii-trusted | 15.574 | 15.289 | -1.83% | overlap |
| word-mixed-strict | 883.468 | 874.201 | -1.05% | overlap |
| word-mixed-trusted | 876.094 | 890.804 | +1.68% | overlap |
| simple-mixed-early-strict | 19.738 | 14.254 | -27.78% | separated |
| simple-mixed-early-trusted | 12.491 | 0.266 | -97.87% | separated |
| decimal-mixed-miss-strict | 20.436 | 14.943 | -26.88% | separated |
| decimal-mixed-miss-trusted | 13.131 | 0.754 | -94.26% | separated |
| simple-ascii-strict | 19.531 | 14.286 | -26.85% | separated |
| decimal-ascii-strict | 40.082 | 33.963 | -15.27% | separated |
| unchanged-core-literal-count | 12.304 | 12.206 | -0.79% | overlap |
| unchanged-dotnet-count | 22.728 | 22.149 | -2.55% | separated |

Accept the large trusted early-operation gains (97.87%/94.26%) and strict Boolean
observations (15.27–27.78%) for this removal of unused preparation. Word-count
ranges overlap: retain trusted mixed+1.68% and the other small movements without
claiming exact gains. Unchanged.NET control improves2.55% with separated ranges;
Core literal control-0.79% overlaps. Do not normalize by controls or claim every
percentage is isolated causal cost. Allocations are unchanged for every row,
including112B simple/ASCII work,1168B mixed word and1184B mixed decimal fallback.
These focused producer comparisons do not update canonical profile Status or
turn strict/trusted controls into another benchmark matrix. Campaign closed.

Windows Release.NET10/11 and Linux.NET11 each pass1752 focused checks. New cases
cover mixed/supplementary inputs, both policies, borrowed byte-slice bounds,
whole-subject bad-tail offsets and bounded trusted early matching. Existing
Boolean/word/validation/resource/interruption suites remain. No test-only executor
or implementation-mirroring assertion. Pattern/replacement validation is separate.

First VM preflight stopped BEFORE build/timing because it assumed a canonical
NuGet feed on the benchmark VM. That feed resides on Windows. The corrected run
preserves actually resident assets; it uploads or manufactures no package feed.
Both failure and completed evidence are verified offVM. Windows dev87's eight
package/symbol files still match qualification/canonical hashes;556 staged files
remain, with no package/Full rerun. An initial private caller generation used
Windows' default text decoding; corrected to explicit UTF8 before compilation
or measurements. Core code/tests were unaffected.

Raw `artifacts/experiments/flag-only-admission-20261114/evidence.tar.gz`, SHA256
`2D45065CD6B17336C278E79F1F0DFACD13C92223973F306A68FA861C303D0090`; preflight `90DC427CB93EAF5354B578068C65021DD54D2438E15D002ABCC319323AB7AEAF`.
All worker/collector/SSH keeper handles are terminal. The current mixed-word cost
is dominated by scalar execution rather than the removed description; inspect
that live path before a new design. The Chinese literal/family routes already
use validity-only admission, so this is not a Chinese search improvement claim.
