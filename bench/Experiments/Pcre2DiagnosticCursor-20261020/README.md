# Test the PCRE2 production cursor directly

Parent347671b0. Two shipped diagnostic methods selected existing backtracking
programs and aggregated counters solely for benchmarks/tests. Move this replay
selection/aggregation into the existing BenchmarkInspectReporter.Pcre2 partial,
using existing compiled metadata and named tuples. Remove the two library result
records and now-unused ValidateSubjectAndStart helper. No new shipped hook, type,
dispatcher, owner, visibility expansion or public API.

Counters, budgets, production cursor/driver/matching bodies and public guards are
unchanged. Benchmark replay preserves original program selection, validation,
non-monotone guard and aggregation; no native/scalar public fast-path attribution
is inferred from a forced backtracking replay. If no backtracking program is
available, the existing public operation is used and counters remain default.

The existing VM fixtures now invoke Pcre2GlobalMatchCursor.CreateBacktracking
directly with the original validated input, candidate plan, request and diagnostics
flag. A bounded untimed runtime probe established all three Count fixtures select
Pcre2BacktrackingDirectProgram; the two IsMatch fixtures use regular-native
programs whose backtracking fallback is the same object as Match's fallback.
Every existing VM partition, candidate, repeat and workspace assertion stays.

The multiline fixture's old zero-counter assertion merely observed the diagnostic
wrapper's default result after delegating public IsMatch; it did not measure the
scalar VM. Replace that assertion with the actual selected Pcre2SliceDirectProgram
while retaining public success, offset and NotBol outcomes. No failing semantic
assertion was disabled, no new test forwarding factory was introduced.

Focused Release Pcre2 architecture/translation/source/resource/cancellation:148
pass. Core dependency inventory reuses matching Release binaries:10 pass. No
failures/skips. Incremental benchmark closure and existing simple/ab-plus inspection
smoke pass: public-compatible Count4, replay Count4, IsMatch true and retained VM
counters. No performance comparison, snapshot/Status rewrite or package matrix.

Source proof excludes only the two diagnostic records and unused facade helper;
all other architecture/facade text is unchanged. A test diff check confirms the
only removed Assert line is the default-zero multiline assertion described above.
Logs, runtime-selection data, deleted declarations and proofs are retained in
artifacts/pcre2-diagnostic-cursor-20261020. The tiny probe is local investigation
tooling and does not add a repository qualification framework.

Core/Python115, exactCore/PCRE94 and all older immutable staged/published artifacts
remain untouched. This source change can be batched with remaining Core diagnostic
cleanup; it does not block the already-qualified Core/Python dependency or claim
full R01-R10 completion.
