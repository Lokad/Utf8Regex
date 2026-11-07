# PythonRe literal string replacement output

Keep the existing exact ASCII-literal substitution path and remove work that
string output does not need. On a miss, decode the already validated input
directly instead of materializing a discarded byte array. On hits, reuse the
validated input/replacement UTF-16 lengths to size the final string, avoiding
another validation pass. The ASCII needle replaces complete scalar ranges.
Byte and retained results still receive independent owned storage.

No public API, algorithm, dispatch eligibility, owner or execution mode changes.
Full-input and replacement validation, template errors, counts, offsets and
scalar boundaries remain. Output allocation is admitted before acquisition;
parent loans and retained aliases keep their lifetimes. Removing intermediate
storage and repeated validation can lower modeled work/result traffic and peak
working requirements. Finite limits, cancellable tokens and deadlines keep the
existing scalar substitution path. The [contract](../../../docs/SPEC-PYTHONRE.md)
records this distinction.

## Public costs

ONE separate-process ABBA completed in **66.197 seconds**, under its 120-second
cap: 14 public operations, 280 samples, minimum 25.978 ms. Each ordinary Default
load context contains one producer graph. The identical caller warms for at
least 750 ms/32 calls, calibrates independently toward 75 ms and takes five
samples with a 20 ms floor. Results are consumed after timing. Each sample's
retained roots are capped at 128 MiB. Startup sees eight CPUs, then affinity
selects CPU2. No confirmation, tuning or control normalization followed.

| PythonRe public output | Before us | After us | Change | B/op before → after |
| --- | ---: | ---: | ---: | ---: |
| Actual catalog no-match string, 6,656 ASCII bytes | 4.38 | 2.78 | -36.42% | 20,016.22 → 13,336 |
| Unicode no-match string | 15.68 | 9.32 | -40.58% | 13,872 → 8,728 |
| ASCII matched string | 7.63 | 6.20 | -18.65% | 5,808 → 5,808 |
| Unicode matched string | 31.68 | 19.53 | -38.36% | 6,832 → 6,832 |
| Unicode string, one replacement | 11.37 | 7.20 | -36.64% | 6,072 → 6,072 |
| Byte-only no-match owned copy | 1.73 | 1.82 | +5.00% | 6,680 → 6,680 |

All five intended string gains persist in both processes with disjoint sample
ranges. Actual no-match process medians 4.031/4.431 us become 2.688/2.919 us.
The byte-only adverse estimate is +86.50 ns with overlapping ranges and mixed
process movement. Preserve it without claiming a stable regression or equivalence.

The unchanged PCRE2 Count control moves +6.87%/+11.064 ns with disjoint ranges;
its DLL is identical, while the shared Core DLL differs. This is an adverse
control observation without an established cause. Core Count is +0.05%/+0.159 ns
with overlapping ranges. These movements are not used to normalize the gains.

Excluded finite/live miss is 3.08 → 3.04 us, live token 3.06 → 3.13 us and
deadline 3.39 → 3.43 us; each overlaps and retains its allocation median.
After-change finite/live output costs 13,448 B/op versus ordinary 13,336 B/op.
Offset, capture-template and empty-pattern exclusions also overlap. Keep every
row, process median, sample range and fractional allocation observation in the
[summary](cost-summary.json) and [acceptance](acceptance.json). Local adverse
costs are acceptable for these substantial gains; there is no universal 10% gate,
uniform-speedup claim or new catalog Status.

## Validation and preservation

Focused Release checks pass on Windows/Linux and .NET 10/11: Core 556 and
PythonRe 155 per target/platform. Coverage includes Unicode output sizes/counts,
byte ownership, retained results, strict malformed-input/template errors,
offset/empty/excluded paths, admission, interruption and parent loans. The new
miss check establishes that internal discovery acquires no output storage;
public byte results remain independent of later input mutation.

The baseline is the exact qualified dev.54 package graph, source `553acbdf`.
Candidate source is metadata parent `cd338e24` plus the archived patch. Core
`D92F46FB` becomes `D0C083D0`, PythonRe `364C5548` becomes `559EEC99`; fixed
PCRE2 is `A9A23020`, caller `D6D953F6`. Full identities and the declared bounds
are in [comparison-declaration.json](comparison-declaration.json).

All 294 staged package/symbol assets are unchanged. No package qualification or
Full refresh was repeated. Immutable raw evidence is copied off VM and verified:

- `artifacts/experiments/python-literal-text-output-20261107/evidence.tar.gz`:
  `F574EF079D7E550E722F0D505389F197C0A7E1E76BAAFE722E12F172A298935D`.
- `windows-evidence.zip` there:
  `5B54D66CEA8FC119447DB04CF59A5D7A926260674E1D11F0D329957C9B15BD98`.

An initial archive command reported a source-directory timestamp change; writing
the archive to a sibling path fixed archiving. The experiment was not repeated.
No NuGet publication.
