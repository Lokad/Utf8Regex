# Thirty-first review, 2026-10-24

Checkout a49d6395; existing Release products identify source56042eb1. Review
focused on host capabilities, invalid callback arguments, cold/warm preparation,
early-return validation, actual replacement consumers and CLI response files.
Product source and PLAN are unchanged. Only scratch projects referencing existing
DLLs were built. No throughput campaign, full suite, package qualification,
snapshot refresh, VM operation or GitHub API request occurred.

## 1. Optional IL preparation becomes a hidden Core host requirement (P2)

There is no RuntimeFeature.IsDynamicCodeSupported guard in shipped source.
Utf8DynamicIlBuilder.Create calls DynamicMethod at99; its callers include
preparation acquired without RegexOptions.Compiled. Utf8RegexProgram61-72 also
attempts a direct structural emitted kernel for an ordinary unrestricted owner.

The same scratch DLL is launched with normal runtime configuration and with
IsDynamicCodeSupported/IsDynamicCodeCompiled disabled in an isolated runtimeconfig.
Its DynamicMethod control confirms real PlatformNotSupportedException behavior.
Normal mode passes all eight cases. With dynamic code disabled:

| Public case | Result |
| --- | --- |
| Core ordinary abc | Matches successfully |
| Core ordinary abc\|xyz | PlatformNotSupportedException during construction |
| Core ordinary structural keyword/identifier pattern | PlatformNotSupportedException during construction |
| Core Compiled anchored date | PlatformNotSupportedException during construction |
| Same Core structural pattern with fixed finite work policy | Matches successfully |
| PythonRe/Pcre2 structural controls | Both match successfully |

Thus support depends on pattern and preparation eligibility, even for ordinary
Core construction. Existing scalar/non-emitted implementations can serve these
demonstrated cases; this is not evidence that matching itself requires IL emission.
Public docs currently do not state a dynamic-code host requirement.

Recommendation: investigate capability-gating optional emission at its existing
selection/TryCreate boundaries, preserving the normal JIT fast paths and existing
canonical non-emitted execution. Keep Options identity, scalar semantics,
captures, cancellation/deadlines and construction/execution admission. Do not
catch unrelated grammar/resource exceptions or add a public runtime mode.
If a host restriction is intentional, state it clearly instead of leaving a
pattern-dependent constructor surprise. Focus checks on disabled/enabled host
capability, ordinary/Compiled and the demonstrated native/scalar/control cases.

This is a JIT process with dynamic-code support disabled, not a native publish
or trimming qualification. A capability fix must not claim complete NativeAOT
support without separate evidence. The runtime rejection is consistent with
the official [.NET10 DynamicMethod implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Reflection/Emit/DynamicMethod.cs).
dynamic-code-normal.json, dynamic-code-disabled.json, disabled.runtimeconfig.json
and the probe sources preserve the observation.

## 2. Null replacement callbacks have input-dependent failure (P2)

Core byte/text callbacks enter ReplaceEvaluatorCore/ReplaceToStringEvaluatorCore
(Utf8Regex.cs2846/2903) without delegate validation. Utf8SliceEvaluatorExpansion
invokes the captured delegate at its match boundary. PythonRe's evaluator workers
and PCRE2's callback expansion wrappers have the same missing entry check.

Public null-callback witness uses pattern a and one-byte hit/miss subjects, for
both byte and string output across all three profiles. Every miss succeeds and
returns length1. Every hit throws NullReferenceException. The overlapping BCL
text control rejects the null evaluator with ArgumentNullException/evaluator in
both cases; it is a comparator, not a reason to restore UTF-16 matching semantics.

Recommendation: validate non-null callback delegates at shared entry points
before discovery/output acquisition. Preserve pre-cancelled-token precedence and
document ArgumentNullException consistently. Do not reject legitimate empty/null
callback return values merely because the callable itself must exist. Retain
scalar matches, captures, callback count/order, writer resets/aliases and resource
authority. Focused tests need hit/miss, both output forms, original pre-cancellation
and meaningful finite controls, not an exhaustive overload matrix.

This deliberately revises invalid-argument behavior: a null callback becomes a
deterministic argument error instead of optional success or an engine-shaped
NullReferenceException. null-callback-witness.json and the scratch source record
all fourteen observations. No valid-callback defect is claimed.

## 3. Tests keep a standalone replacement-preparation surface alive (P3)

Production Utf8FrontEndReplacementAnalyzer resolves tokens once and calls
Utf8ReplacementPlanLowerer.Lower(storage,resolved.Span,[],[]). Repository-wide
caller inspection finds no shipped or benchmark callers of:

- Utf8ReplacementPattern.ResolveGroupReferences (255), which reacquires a session
  and can create another pattern holder;
- its two public TryGetLiteralText overloads and private extraction helper (346+);
- Lowerer's two standalone Lower(pattern,...) overloads;
- Lowerer's separate named-group resolution path: production receives already
  resolved numeric group tokens and empty group metadata.

These paths are used by Utf8OwnedReplacementTests, preparation-admission tests,
cache-charge setup and the sparse-group fixture. They preserve alternative
preparation entry points and extra name/literal-resolution plumbing in the shipped
assembly, even though production no longer takes that path.

Recommendation: retarget these semantic/admission fixtures to the production
analyzer/ResolveTokens/lowering sequence, then remove verified standalone wrappers
and lowerer-only name resolution. Keep the real shared token resolver, byte
instructions, captures/sparse numbering/unknown-reference behavior, major-buffer
admission and interruption checks. This should be a bounded deletion of roughly
one hundred lines, with exact scope established during implementation; no new
preparation abstraction or finer accounting. Do not delete useful test coverage.

The decoded Utf8ReplacementPlanInterpreter.ApplyOwned has a real shipped caller
in TryReplaceTemplateWithOwnedGroups and must remain. Its literal text already
repairs surrogate fragments. This is not another proposal to remove the live
decoded worker or reopen closed layout/accounting/retention work.
replacement-surface-witness.json records the source caller evidence.

## 4. Last milestone's help classifier misses response files (P3)

Program.cs473-483 asks BDN to parse an empty-summary request but then identifies
help/version by inspecting the original raw arguments. BDN already expands
response files; the raw @file argument contains neither flag.

Bounded launches through bench.ps1 -SkipBuild show:

- direct --help: prints help, exit0;
- @help.rsp containing --help: prints the same help, exit2;
- @version.rsp containing --version: prints version, exit2;
- @info.rsp containing --info: prints information, exit0.

No benchmark runs. This is a small regression in our last exit-code change,
not a newly discovered BDN failure. The invalid-job/zero-benchmark failure rule
remains valuable; preserve it. Prefer classifying the effective parsed request
over extending raw flag special cases. Keep this correction small and subordinate
to the public dependency issues; no new CLI/qualification framework or broad
command-symmetry campaign is justified. Response-file logs, inputs and observed
exit codes are preserved in response-files-result.json and adjacent artifacts.

## Checks that did not justify more removal

Core/PythonRe controlled replacement-cache access already isolates foreign
preparer cancellation/deadline failures. PythonRe FullMatch's PublicationOnly
lazy preparation remains retryable. The exact-literal replacement helper's large
IndexOf calls have public consumers gated to unlimited/no-token/no-timeout lanes;
they are not evidence of a controlled-path metering defect. Split creation
validates the complete subject and retains maps with real coordinate consumers.
Writer reset behavior is explicit and has real lifetime/resource tests. Existing
out-of-process BDN archive discovery and VM disk-space limitations were already
recorded and are not presented as fresh findings.

The review supports two bounded public-contract fixes and small replacement/CLI
cleanup. It does not justify a new engine, policy framework, another checked-
access campaign or repeated package qualification.
