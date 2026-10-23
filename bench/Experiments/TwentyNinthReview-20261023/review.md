# Twenty-ninth review, 2026-10-23

Checkout 9579aab0; reused Release product producer 3a77e699. Source and PLAN are
unchanged. The review concentrated on policy/preparation mismatch, failure and
pool lifetime paths, toolchain selection, and retired maintenance constraints.
Scratch projects reference the existing DLLs. No product build, throughput
campaign, package qualification, full-suite rerun or GitHub API request occurred.

## 1. Fixed-budget Core instances emit code that their public routes cannot use

The Core constructor installs immutable ResourcePolicy, then calls
Utf8RegexProgram.Compile(pattern, options) without that policy. Compilation
unconditionally creates the verifier/runtime and optional emitted machinery
(`Internal/Execution/Utf8RegexProgram.cs:45-66`).

Conversely, fixed numeric limits route IsMatch, Count and Match to owned metered
execution (`Utf8Regex.cs:605/893/1426`); GetOwnedSliceProgram similarly fences
result/replacement/enumeration routes at lines 475/483. The direct metered literal
routes borrow search payloads rather than these emitted whole-match delegates.
ResourcePolicy cannot be relaxed later on the same public instance.

`finite-preparation-witness.json` records current construction allocation and
retained emitted roots for three patterns, ordinary/compiled and unlimited/four-
finite policies. Both finite compiled examples below retain _emittedWholeMatcher:

| Fixed finite policy | Ordinary constructor bytes | Compiled constructor bytes |
| --- | ---: | ---: |
| ^ab[0-9]{2}$ | 52,224 | 62,080 |
| ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ | 70,384 | 86,000 |

These are current option comparisons, not measurements of an implemented fix.
The differences include other Compiled preparation and do not prove exactly
9,856/15,616 bytes can be removed. CLR/JIT/native code storage is not measured.

`emitted-canary-witness.json` strengthens the reachability evidence. On isolated
instances, reflection replaces the retained emitted callback with a throwing
delegate and verifies its installation. All seven public operations retain
correct results with the finite policy: IsMatch, Count, Match, MatchDetailed,
EnumerateMatches, ReplaceToString and TryReplace. Neither finite example invokes
the canary. The unrestricted date-pattern control does invoke it, proving the
instrumentation is effective and the unrestricted path has a real consumer.

Recommendation: start with the demonstrated policy-ineligible emitted preparation,
then assess other preparation made permanently unreachable by fixed instance
limits. Preserve Options identity, semantic/scalar programs, candidate helpers,
all metering/admission/interruption and unrestricted measured native paths. Do
not globally remove compilation because one invocation supplies a token; a later
call on an unlimited instance can use it. No dormant replacement engine or broad
preparation framework is warranted. Focus validation on finite-policy construction
and all public routes, with unrestricted compiled controls and one bounded
constructor/public-operation comparison during implementation.

## 2. The shared SDK request is still a floating preview selection

global.json requests 10.0.300-preview.0.26177.108 with latestMajor and
allowPrerelease=true. This host currently resolves that preview; the installed
stable 10.0.204 is below the requested feature band. See dotnet-info.txt and
installed-sdks.txt. This is SDK selection, independent of the net10.0 runtime.

Microsoft documents latestMajor as selecting the highest installed eligible SDK,
including later majors:
https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#rollforward

This is an existing unresolved hazard, not a newly discovered runtime regression.
PLAN.HISTORY.md around lines 1933-1942 records the same repository request selecting
SDK 11 on the quiet VM, and an explicit SDK-10 DLL workaround. Those historical
observations are not a claim that the VM was accessed or its installation checked
in this review. The ordinary scripts still use bare dotnet; BDN worker builds can
also inherit the floating selection. The workaround lives in historical notes.

Recommendation: choose an intentional common supported .NET 10 SDK policy and
make ordinary local/CI/VM builds resolve it consistently before the next release
boundary. Verify installed prerequisites rather than blindly replacing the
version or installing a newer major. Keep explicit SDK/runtime comparison lanes
explicit. Validate the selected compiler once on Windows/Linux at the coherent
boundary; preserve historical bytes/results rather than refreshing them for a
config edit. No new provenance framework is required.

## 3. Retired style constraints still leave misleading source and small wrappers

The post-RC1 receipt explicitly records deletion of the single-caller semantic-
compilation style gate (bench/Experiments/PostRc1Simplification-20261020/README.md:15).
Current source nevertheless claims that helper extraction is forbidden by that
ceiling, e.g. Utf8Regex.cs:1496/2603, Utf8OperationMatchCursor.cs:418/485/1144,
Utf8SliceRunner.cs:218, and the owned parser/prelude headers. These are obsolete
maintenance constraints. Do not reorganize the 3,177-line parser merely to replace
them with another metric; static locals can have legitimate lexical/state roles.

The tracked tests/Lokad.Utf8Regex.Pcre2.Tests/CSharpDebt.Shipped.txt also has no
repository consumer. It looks like current mechanical qualification but is not
read by the active fixtures/scripts. Other comments still claim a BCL RTL
enumerator and unfinished general NonBacktracking loops, despite the owned engine
and its dependency/semantic checks (Utf8RtlEnumerationTests.cs:12 and
EngineRouteCoverageMatrixTests.cs:67).

There is a small concrete code deletion too. Utf8Regex.Compiled.cs contains four
single-call forwarding methods: the ExactLiteral/LiteralFamily IsMatch pair and
the equivalent Match pair. Each pair has identical bodies and arguments. Their
callers can combine the corresponding switch arms and invoke the same runtime
method directly. source-cleanup-witness.json preserves the exact bodies. Keep the
specialized Count helpers: their bodies have actual route/validation behavior.

Recommendation: delete those four forwarders and the orphan debt file, and correct
the stale rationales/coverage comments. Preserve real matcher dispatch, scoped
validation/default arguments, diagnostics and semantic/resource checks. No parser
rewrite, replacement debt ledger or broad benchmark campaign is needed.

## Priority, scope and integrity

The fixed-policy emitted preparation is the highest-value live issue. The small
forwarder/material cleanup is bounded; SDK convergence belongs with release
preparation. No new matching-semantic/cancellation failure or additional dormant
global engine was demonstrated. The reviewed pooling, immutable NonBacktracking
viability tables, cursor cleanup and original authority have real uses.

The private single-occurrence scan found no candidates; it is a conservative
source screen, not a whole-program reachability proof. Keep the recently completed
API/byte-offset/checked-access work and closed experiments closed.

integrity.json verifies six product DLL/PDB files and all 18 protected benchmark/
frozen-package/original-TODO artifacts unchanged. Only the original five TODOs are
untracked outside ignored review material. Fresh immutable packages, isolated
consumers and cumulative operating-cost acceptance remain separate release gates.
