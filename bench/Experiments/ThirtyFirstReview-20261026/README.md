# Host capabilities, callback arguments and replacement preparation

Development source `9ea498ad`, still `0.4.0-rc.2-dev`. No packages or published
benchmark snapshots were refreshed. Frozen unpublished RC1 remains unchanged
and must not be released. `review.md` preserves the October 24 assessment;
implementation and final checks below were performed October 26.

## Delivered changes

- `344dd319`: optional IL preparation declines when
  `RuntimeFeature.IsDynamicCodeSupported` is false. Gates cover all actual
  emission factories: whole validators (four shapes), deterministic structural
  search, structural kernels and literal-family counters. Ordinary constructors
  and `Compiled` retain existing canonical non-emitted execution. JIT-capable
  hosts still prepare measured emitted paths. No catch-all exception fallback,
  public mode or second engine was introduced. NativeAOT publishing/trimming
  remains unqualified; this is a disabled-capability JIT-host check.
- `6ecb574c`: shared replacement workers reject null delegates before subject
  discovery/output acquisition across Core, PythonRe and PCRE2. This intentionally
  changes invalid calls: both misses and hits now throw
  `ArgumentNullException("evaluator")`. An already-cancelled explicit token still
  wins. Valid delegates, profile-specific return-value rules, callback order,
  captures, writer resets/aliases and useful resource controls are preserved.
- `9c446e96`: removed standalone `ResolveGroupReferences`, both `TryGetLiteralText`
  entry points/their helper, two standalone lowerer wrappers, duplicate lowerer
  name lookup, the unused token `GroupName` projection and change-only comparisons
  in `ResolveTokens`. The three production files remove 110 lines and add eight,
  a net reduction of 102. Useful
  pinned semantic, sparse-reference, cache/admission/interruption fixtures now
  exercise the production analyzer/resolver/lowerer. Eliminating comparisons
  also eliminates their work charges: construction work can be lower. Major
  buffers, expansion and bounded interruption remain admitted. The decoded
  interpreter has a real consumer and remains.
- `9ea498ad`: repaired help/version response-file exits. Pinned BDN's public parser
  expands response files but discards its informational parse result. On the
  no-summary failure path only, compare its output with its own canonical
  help/version transcripts. This avoids a duplicated response tokenizer, raw
  flag classification and a maintained BDN option whitelist. Genuine parse
  errors still return 2; failed/empty execution 1; informational requests 0.
  SPEC, migration, release notes and packaged READMEs record changed contracts.

Strict complete-subject UTF-8, scalar matching/advancement, captures, derived
coordinates, RTL/NonBacktracking, retained aliases/results, original-token
synchronous cancellation, absolute deadlines and work/depth/working/result/state
limits remain. Actual `Unsafe.SizeOf` and native fast paths remain. Closed
checked-access, layout/accounting and retention campaigns were not reopened.

## Validation

One final Release solution build at the coherent source boundary; no incidental
packing. Affected projects were built incrementally during experiments and
matching binaries reused afterward. Full Core: 39,652 pass in 1m23s. Final
replacement/cancellation/resource/API/inventory focuses: PythonRe 330, PCRE2 633.
The earlier Core focus passed 891; the disabled-host Core fixture passed seven using
the final binaries. The generated runtimeconfig is restored byte-for-byte.

The actual disabled-host witness reports both dynamic-code features false, its
`DynamicMethod` control throws `PlatformNotSupportedException`, and all seven
public library cases succeed. Before this change ordinary Core family and
structural construction, plus the Compiled date case, threw. Normal-host cases
also pass. The persistent fixture checks ordinary/Compiled/NB/RTL, scalar
captures, strict malformed input, finite controls and original pre-cancellation;
RTL backreference semantics are not equated with LTR.

All fourteen null-callback witness rows (including the overlapping ASCII BCL
controls) now report `ArgumentNullException/evaluator`; before, library misses
succeeded and hits threw `NullReferenceException`. Focused tests also cover
finite work/result admission and token precedence. Core/PCRE2 valid null text
returns and PythonRe valid empty text returns retain their prior behavior;
PythonRe null text returns were not accepted by the existing implementation and
were not changed here.

Eight launcher cases pass: direct help; help/version/info response files;
invalid-job response 2; empty-filter response 1; missing-response 2; unknown-command 2.
The precommit receipts use the same final source code; a matching final-producer
help-response/invalid-job-response smoke also passes. No timing was inferred
from these informational/error checks. All three existing generated-page
verifiers pass without writes.

Windows/Linux full source CI is green at `9ea498ad` in
[run 40292330956](https://github.com/Lokad/Utf8Regex/actions/runs/40292330956).
Both existing jobs also run the focused disabled-capability fixture on the same
binaries; there is no new matrix or qualification framework. `ci-source.json`
preserves the jobs/steps. Two spaced status requests were used.

## One bounded source cost comparison and one noise confirmation

Frozen baseline product `56042eb1` versus final `9ea498ad`, same SDK 10.0.401 and
observed .NET 10.0.14 runtime. The runtime has advanced since October 24; both
versions here use the same runtime. Isolated assembly load contexts bind each
version's Core/PythonRe/PCRE2 identities. The shipped libraries were not rebuilt
for the experiment. `binary-identities.json`, driver/host source and raw samples
preserve what ran. No package or cumulative release acceptance is claimed.

Eight public lanes use 64-byte supplementary/ASCII subjects, captures, byte/text
callbacks, all four finite controls, a seventeen-key cycle through the existing
sixteen-entry replacement cache, emitted structural Count and an unchanged
compiled-literal Count control. The cache cycle makes production preparation
live every operation. Results are checked before timing and checksums during
timing. Each version warms for 2.5 s/lane, calibrates to 40 ms samples, then
alternates nine pairs on one processor. First comparison: 48.90 s.

| Public operation | Initial median change | Allocations before/after |
| --- | ---: | ---: |
| Core byte callback | +1.75% | 23,640 / 23,640 B |
| Core text callback | +1.77% | 6,640 / 6,640 B |
| Core finite byte callback | +2.45% | 23,752 / 23,752 B |
| PythonRe text callback to bytes | +1.56% | 4,040 / 4,040 B |
| PCRE2 byte callback | +3.01% | 38,504 / 38,504 B |
| Core template cache churn/preparation | +3.83% | 6,728 / 6,728 B |
| Core ordinary emitted structural Count | -4.37% | 3,552 / 3,552 B |
| Unchanged compiled-literal Count | +2.20% | 0 / 0 B |

The PCRE2/template rows had wide sample spreads. Their single allowed
confirmation uses seventeen alternating pairs, reversed first, with only those
two rows and the unchanged control. Medians: PCRE2 +0.01%, preparation -1.61%,
control -8.83%; allocations unchanged. Noise remains visible in the raw samples.
No further campaign was run. Source medians stay within the 10% allowance; the
control movement prevents treating small differences as causal speedups or
claiming exact equivalence. Lightweight adapter tests overlapped the initial
comparison's early warm/sampling period; the confirmation had no concurrent
tests. Source observations do not replace final package/host/cumulative costs.

Construction is separate diagnostic evidence, not matching throughput: exactly
sixteen ordinary instances per family/structural pattern per version, including
first-instance cost. Remaining-instance medians are approximately -2.2%/+1.3%,
with identical 78,248/113,312 B. No self-calibrated dynamic-construction loop.
The confirmation does not repeat construction.

## Remaining work

No required implementation remains in this milestone. All 18 protected snapshot,
RC1 and original TODO files retain exact SHA256 bytes; PLAN/history remain ignored.
Deferred: ordinary out-of-process BDN ancestor/archive discovery; VM disk space;
a fresh common-version immutable package and downstream/platform acceptance;
actual-package finite costs and cumulative 10% gate against fixed `844dcbad`.
Existing archives, packages and historical evidence are preserved.
