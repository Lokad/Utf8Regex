# Core inspection is metadata only

Parent `97475793865f26772e4e055bc4cc5fbfc3105729`. Delete input-taking
execution, dispatch and predicate forwarders from `Utf8Regex.Diagnostics.cs`.
Read-only plan/runtime metadata remains; duplicate private metadata mirrors are
inlined into the existing inspection getters. Remove redundant emitter re-lowering
and fresh-budget probes, private replacement/split-count replays and imitation
post-validation/native-token/anchored-validator benchmark lanes.

Tests call actual production implementations. Split fixtures enumerate the public
cursor, retaining native eligibility and owned capture-catalog assertions.
Replacement fixtures now compare exact public UTF-8 bytes, replacing private
length-only checks. Native Count tests call the existing packed strategy or Count
router directly. The removed Unsupported assertion duplicated that same selection
predicate; public comparator and selected-kernel result checks remain. No semantic
failure is disabled. Scalar residual tests keep supplementary/final-LF/anchor
outcomes and prepared adapter checks.

Move selected Count dispatch and prefiltered IsMatch/Match replay into the existing
benchmark `Utf8MatchInspection`. Borrow the existing emitted runtime directly.
Real `TryMatchEmittedWhole`, fallback guidance, fused selectors, replacement
parsers and full validators have production consumers and stay.

Intentional internal tradeoff: five existing methods (Count/IsMatch/Match routers
and two split predicates) and one readonly packed strategy field become internal
for existing friend assemblies. No new accessor, type, state, public API,
compatibility mode or execution framework. Source proof shows their complete
production files are unchanged after undoing only those visibility changes.
Matching algorithms, allocation, scalar/strict full-input UTF-8, captures, NB/RTL,
original cancellation/deadlines, admission, work/depth/working/result limits and
cursor/result lifetimes are unchanged. Actual Unsafe.SizeOf queries stay.

Final focused Release checks: Core 2765, split differential 40, PythonRe 280,
PCRE2 148 pass, no failures/skips. Earlier overlapping checks are retained in logs
and are not added to those totals. PythonRe compilation reports nine existing
nullability/analyzer warnings, zero errors. Incremental benchmark dependency
closure builds without warnings/errors. Existing Count attribution smoke agrees
at 30 matches across raw/public/comparator lanes; public replacement and split
deep commands also complete. Their tiny diagnostic samples are smoke checks,
not public performance evidence or Status. No live-path before/after timing was
needed because production bodies are identical. No package/platform matrix or
immutable artifact was rerun or altered for this deletion batch.

Source/declaration proof and logs: `artifacts/core-replay-retirement-20261020`.
Retired fallback/runtime and requested orphan names are absent from shipped source
and tests. PCRE2's remaining Debug method formats execution-plan metadata; no
subject-taking diagnostic executor remains in Core/PythonRe/PCRE2. Public counters
and actual metering remain. The checked-access campaign stays closed.

This completes the focused diagnostic retirement with the adjacent PCRE2 cursor
migration. Scalar.115 and exact Core/PCRE2.94 remain immutable READY dependencies;
source scalar.116-dev is separate. Broader R01-R10 work is not completed by cleanup.

Delivery addendum: source387b226e plus adjacent PCRE97475793 was pushed as one
source CI boundary. Run39367560373 is terminal success on Windows/Linux, closed.
Freeze e83d1942 changes metadata/docs only; immutable Core/Python scalar.116 now
passes same-byte platform/downstream/oracle/member/provenance gates and one bounded
current-byte cost observation. [Handoff](../../../artifacts/core-python-scalar116-20261020/HANDOFF.md).
No package or matching throughput gain is inferred from deleting nonlive wrappers.
