# Thirtieth review, 2026-10-23

Checkout 98e17a61; existing Release products identify source28b31f70. This review
examined absence/result handling, optional preparation and failure isolation,
public cursor ownership, benchmark error reporting, and current documentation.
Product source and PLAN are unchanged. Scratch executables reference existing
DLLs; only scratch projects were built. No throughput campaign, full-suite run,
package qualification, snapshot refresh, VM mutation or GitHub API call occurred.

## 1. Default Core contexts dereference an uninitialized coordinate map (P2)

Utf8MatchContext.IndexInBytes/LengthInBytes/TryGetByteRange (113/119/127) and
Utf8CaptureContext equivalents (46/52/70) call ResolveBoundary even when Success
is false. IsByteAligned returns true for absent values, then the getter accesses
the default Utf8BoundaryMap's null backing array. Capture.GetValueBytes, both
types' TryGetValueBytes and match.TryGetByteRange inherit that failure.

default-state-witness.json reproduces NullReferenceException through eight byte
accessors on the current DLL. Success is false; string getters already guard
absence. Default group/PythonRe/PCRE2 byte views return empty normally. Actual
matcher-produced unsuccessful contexts on ASCII/Unicode inputs work; this is
a default-value defect, not a claim that ordinary no-hit matching crashes.
Default Utf8ValueSplit instead throws an intentional-looking InvalidOperation
exception; its default contract needs a separate decision, not forced symmetry.

Recommendation: give absent match/capture byte accessors explicit zero/empty
handling, preserving IsByteAligned/Try contracts and established successful
projection. Do not weaken the boundary mapper to hide unrelated invalid states.
Preserve matcher-produced no-match group zero (GroupCount1), capture metadata,
retained-result validity and all scalar/UTF-8 behavior. The GroupCount XML comment
at140 currently says zero when no match, contradicting the actual deliberate
unsuccessful-group-zero behavior; correct that wording without changing results.
Focus validation on defaults, actual misses, missing groups, empty successful
captures, supplementary values and retained aliases.

## 2. No-hit native detailed matching materializes a useless subject-wide map (P2)

MatchDetailedCore validates through Utf8InputAnalyzer.Analyze, whose coordinate
map is lazy. Six no-hit branches nevertheless request analysis.BoundaryMap when
constructing an unsuccessful context (Utf8Regex.cs1765/1781/1800/1827/1854/1887).
That request allocates/fills the entire map after discovery already proved no
match. The owned route at557 already uses a valid empty map for the same failure
shape. Captured literal-family argument evaluation needs separate tracing before
generalizing the cleanup; successful coordinates still have real consumers.

Public witness, exact pattern z, no hit, ordinary defaults, valid subjects:

| Subject | Unlimited MatchDetailed B/call | Same pattern with finite work B/call |
| --- | ---: | ---: |
| 1,024 ASCII characters | 0 | 112 |
| 1,024 e-acute scalars (2,048 bytes) | 4,288 | 112 |
| 65,536 ASCII characters | 0 | 112 |
| 65,536 e-acute scalars (131,072 bytes) | 270,400 | 112 |

The allocation witness warms four calls, samples eight calls, and keeps instance,
subject and evidence-record construction outside the counter. Every call checks
Success=false, GroupCount1, zero byte range and successful empty TryGetByteRange.
This is an allocation/route witness, not a before/after timing or an estimate of
finite accounting overhead. No speedup is claimed.

Recommendation: reuse the owned route's empty-map unsuccessful representation
at proven misses, retaining complete-subject validation and the capture catalog.
Keep native matching algorithms and successful projection. A small bounded
implementation should verify malformed tails, ASCII/Unicode misses, anchors,
capture metadata, controls and hits, then use one focused public before/after
comparison with an unchanged hit/control. Do not change the result layout,
replace UTF-16 interoperability wholesale, or reopen closed layout/retention work.

## 3. BenchmarkDotNet errors still appear as successful script exits (P2)

Program.cs481 ignores BenchmarkSwitcher.Run's result and unconditionally returns
zero. The E03 unknown-command guard handles the first unrecognized command, but
does not cover errors after entering BenchmarkDotNet.

Two bounded --SkipBuild launches through bench.ps1 reproduce the false success:

- --job utf8regex-invalid-job --filter *Utf8RegexReviewNoSuchBenchmark*: prints
  that the base job is invalid, exit0.
- --job Dry --filter *Utf8RegexReviewNoSuchBenchmark*: prints that the filter
  returned zero benchmarks, exit0. No measurement actually ran.

The unchanged custom-command control rejects --utf8regex-review-invalid-command
with exit2. Both erroneous BDN launches terminate within two seconds; no timing
campaign was started. Logs and observed exits are saved beside this assessment.
This does not establish that published snapshots contain incorrect evidence.

Recommendation: propagate failed setup/validation/execution and unintended
zero-benchmark runs through the existing CLI exit status. Preserve legitimate
help/list/info, custom commands and original child exit propagation. Prefer the
existing BDN parser/results to extending the manually maintained whitelist or
adding another command/qualification framework. Focus checks on invalid jobs,
empty execution filters, genuine failure and informational/successful controls.

## 4. Two Count forwarders have the same redundancy as the deleted pairs (P3)

Utf8Regex.Compiled.cs147/152 contains CountViaCompiledExactLiteralEngine and
CountViaCompiledLiteralFamilyEngine. Each is exactly one call to
_compiledEngineRuntime.Count(input, default, budget). Each has only its respective
switch-arm caller at215/216; no outside/test/benchmark consumer was found.

The previous milestone's broad instruction to retain specialized Count logic
does not justify retaining these two identical forwarding methods. Correct that
characterization. Genuine Count routing, fused counters, validated-input handling
and Unicode/native helpers elsewhere remain useful.

Recommendation: combine the two switch arms and invoke the runtime with the
same default validation argument. This is a small code deletion with no claimed
performance benefit; existing compiled literal/family semantic/resource checks
are the appropriate validation. It should not become another campaign.

## Checks that did not justify more work

PythonRe FullMatch uses PublicationOnly Lazy: failed caller preparation is already
retryable and each cold caller keeps its own construction authority. Published
detailed name arrays are copied; snapshots do not expose mutable engine names.
The static Core cache includes construction culture in its key. Current release
docs distinguish frozen RC1 from development and preserve known publication
blocks. All documented benchmark commands are represented in benchmark source
(the64 documented tokens also include three SDK/test flags); split dispatch's owned/native distinctions have real
map, direction and cleanup consumers. None warrants deletion on appearances.

Closed checked-access/accounting/layout/retention investigations remain closed.
The review supports bounded result/failure-path fixes and a tiny forwarding
cleanup, not a new engine, policy, abstraction framework or package matrix.
