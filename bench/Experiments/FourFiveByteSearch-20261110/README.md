# Use checked BCL searches for four/five byte values

Baseline `f663d77b4335015f6917cbe61271740ab8c32223`, plus the archived
production patch. PreparedByteSearch now uses IndexOfAny/LastIndexOfAny for
exactly4/5 values. Both factories skip the unused owned byte-set object/matrix;
other arities retain their existing kernels. No new field, owner, cache, switch,
public API, execution backend or unchecked access. Candidate/matching loops,
scalar semantics, ordering, allocation during execution and controls remain.

The one current public name-family CPU capture first verified2598 matches on the
16,013,977-byte river corpus, route compiled_fused_ascii_literal_family_count.
AssumeValid,4352 calls/15119.634ms,8 CPUs then CPU2.997Hz cpu-clock:u capture has
16997 samples/17.038s:16371 JITmapped,626 unresolved,0 map ambiguities. Self samples
place55.09% in PreparedByteSearch.IndexOf and39.57% in the containing family Count
loop. This is attribution, including startup/warmup/runtime, not phase timing or a
speedup. The pinned local runtime source has checked span entry points backed by
optimized4/5-value searches. CPU archive SHA256
`CEEA6FF93BCE880B9D8E669CEAC006D5AF8A07336E6763760545BD8C06FD367D`
is offVM/hash verified in artifacts/experiments/core-name-family-20261110/.

One **132.354s** ordinary-process two-policy ABBA under180s,12 public lanes,
**480 samples >=127.789ms**. Common caller/PythonRe/Pcre2 DLL/PDBs, distinct
Core pairs.8 processors before CPU2 affinity, default runtime settings. All Count
results consumed after timing,2598 corpus results agree with predecoded .NET;
funded parent17 restored per call and0 at completion. No confirmation/tuned variants.

| Public operation | Validate before → after µs | Change | AssumeValid before → after µs | Change |
|---|---:|---:|---:|---:|
| core/name-compiled | 3958.141 → 3684.588 | -6.91% | 3435.190 → 3223.777 | -6.15% |
| core/name-ordinary | 3949.256 → 3691.093 | -6.54% | 3406.721 → 3218.024 | -5.54% |
| core/name-finite-live | 3535.629 → 3563.405 | +0.79%, overlapping | 3847.558 → 3023.673 | -21.41%, overlapping |
| core/name-funded-live | 3512.965 → 3504.100 | -0.25%, overlapping | 3533.115 → 3504.990 | -0.80%, overlapping |
| core/five-literal-corpus | 5198.733 → 5464.038 | +5.10% | 4657.905 → 4855.648 | +4.25% |
| core/dense-four | 51.873 → 57.153 | +10.18% | 51.551 → 57.431 | +11.41% |
| core/short-four | 0.146 → 0.147 | +0.56%, overlapping | 0.144 → 0.144 | +0.10%, overlapping |
| core/rtl-four | 753.943 → 766.230 | +1.63%, overlapping | 769.314 → 794.257 | +3.24%, overlapping |
| control/net-predecoded | 2965.889 → 3031.167 | +2.20%, overlapping | 3012.391 → 3008.159 | -0.14%, overlapping |
| control/core-strict-literal | 0.102 → 0.113 | +9.81% | 0.100 → 0.101 | +1.24%, overlapping |
| python/name-count | 3568.033 → 3585.281 | +0.48%, overlapping | 2524.237 → 2747.424 | +8.84%, overlapping |
| pcre2/name-count | 4110.803 → 3891.019 | -5.35% | 4672.010 → 4552.843 | -2.55% |

Accept the common primitive/preparation simplification and disjoint useful
name-family gains: save about0.19–0.27ms/call, with no execution allocation change.
PCRE2 Count also improves2.55–5.35%, disjoint. These are equal-result public
before/after observations, not a new ordinary-catalog/native-parity qualification.

Accept and retain adverse neighbors: five-literal corpus+4.25–5.10%
(about0.20–0.27ms/call), dense four-value+10.18–11.41% (about5.3–5.9µs/call), all
disjoint. This is already-checked-code performance work; PLAN allows justified
local tradeoffs, with no blanket10% veto. The gain is not universal and the
five-literal cost is material. Keep these as follow-up lanes at the next coherent
milestone, not as unreported debt. Sparse and dense candidate distributions have
different costs; no speculative density switch or another tuning campaign.

Finite/funded movements and Python results overlap. Trusted finite−21.41% is not
a settled gain; trusted Python+8.84% is not hidden. Unchanged strict-literal control
moves+9.81% (about0.010µs), disjoint; predecoded .NET+2.20/−0.14% overlaps. No
normalization or composition with historical gains. Allocations remain0B on
unlimited corpus/dense/short/reference/Python/PCRE2 operations,112B governed and
528B RTL. Exact working peaks/refunds/producer identities and all sample/process
ranges are in the derived summary beside the raw archive.

Construction admission still precedes the independent immutable value array.
Removed byte-set preparation lowers actual modeled bytes/work/depth for4/5-value
searches; numeric totals are route/version dependent. Other owned byte sets,
major-allocation bounds, instruction/state growth and bounded interruption remain.
Accounting tests now expect the required storage/frames while retaining every
byte/work/depth cutoff, parent reservation and original-token assertion. No
public resource promise was weakened to pass a timing gate.

Windows Release full Core 42227 per.NET10/11 pass; affected PythonRe 802/PCRE2 2089
per target pass. Linux focused Core 2563/PythonRe 802/PCRE2 2089 per target pass.
Boundary tests cover all256 byte values, duplicates, offset views with matching
outside sentinels, empty spans, front/middle/end hits and vector tails; finite
4/5-literal families cover supplementary input, order, working1 admission and work
refusal. Existing captures/replacement/cursors/cancellation/deadline suites remain.
Early failures were obsolete helper-object/extra-frame accounting expectations;
final test patch replaces the archived staging patch's last stale peak assertion,
without changing the measured production graph. Source CI remains separate.

Raw caller, corpus, DLL/PDB graphs, protocol, production/staging-test patches and
all observations: artifacts/experiments/four-five-byte-search-20261110/evidence.tar.gz,
SHA256 `510EB375CD067B33A2F8CF7BA260A5DF96B7EBF8F1D04500D1D8B53906D622A6`.
Linux final-tests patch/checks archive SHA256
`8C1241F7293935A4F2E3652994AF0ABA1EDFB844BE828CBCBDB9D52BCB46447F`.
Both are offVM/hash verified. No package/full refresh or changed immutable bytes.

The six all-byte offset/tail/reverse cases also pass per target with hardware
intrinsics disabled in a fresh host. The process environment is restored afterward;
no execution mode or runtime default was added.
