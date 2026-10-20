# Scalar migration cleanup

Historical review through **scalar.112-dev**, 2026-10-19. Later development
cleanup is recorded in [the first receipt](bench/Experiments/PostRc1Simplification-20261020/README.md)
and [the second receipt](bench/Experiments/SecondSimplification-20261020/README.md).
The staged common release is [0.4.0-rc.1](artifacts/utf8regex-0.4.0-rc.1-20261020/HANDOFF.md);
source is 0.4.0-rc.2-dev. Coverage/gate statements below describe that historical
checkpoint; common public construction controls have since landed. Original
receipts retain their revisions and are not requalified by this page.
The matching contract is complete Unicode scalars over strictly validated UTF-8;
byte coordinates are primary. This report describes source at that checkpoint and executable
checks, rather than treating the absence of a name as proof of semantic coverage.

## Retired mechanisms

ByteSafeLinear, CompiledFallback and SearchGuidedFallback runtime selection was
restricted to FallbackRegex plans, which public scalar dispatch already handles.
Those runtimes, their exclusive tree compiler/interpreter, anchor preparation and
existence-only tests are removed. Public scalar regressions remain. The live ASCII
DFA and its input instructions now belong to the isolated PCRE2 project. General
candidate verification and Count census replays belong only to benchmark tooling.
Shared structural search, start transforms, native kernels and scalar verification
remain with their real production consumers.

The later preferred-compare Count sweep and its exclusive short replay/getter
also live only in benchmark tooling (`fda37b17`); shared vector search and folded
verification remain in production. The move removes 99 net shipped source lines.
A preexisting replay-only skipped-candidate bug is corrected, with each compare
alternative checked against production Count before timing. Nine regression cases
cover rejected anchors, borrowed offsets and incomplete tails; affected Core,
PythonRe and PCRE2 public checks pass. No matching-performance claim follows from
the diagnostic correctness smoke.

Two Python result-release forwarding methods used only by a test are removed
(`c5354e94`). The storage test now exercises the marker release used by production
substitution and durable cursors, including cumulative result traffic after working
release. Core's array-release helper stays because UTF-8 result rendering consumes
it. These deletions change no live method body, public contract, resource admission
or ownership rule; no package matrix or live-path timing is repeated.

| Mechanism | Evidence at that checkpoint |
| --- | --- |
| Production BCL matching/results | `EngineDependencyInventoryTests` checks shipped source for Regex construction/member execution and consumed core types for Regex/Match/Group/Capture owners. PythonRe's managed descriptor and BCL value enumerators are absent. RegexOptions, timeout exceptions/constants and Regex.Escape are compatibility utilities; tests/benchmarks retain overlapping comparators. |
| Whole-subject decoded fallback owner | `Utf8DecodedInput`, decoded storage fields/getters and string-alias leases are absent from consumed core; dependency/compilation architecture tests pin their retirement. General fallback, native verification and compiled consumers execute the admitted owned scalar program. |
| UTF-16 matching units/VM selectors | The current scalar VM instruction set and runner consume Rune values and UTF-8 byte positions. Former surrogate-half routing tests were replaced with independent byte/scalar witnesses in `Utf8AstralSemanticsTests` and related scalar suites. Impossible surrogate atoms compile as failing atoms; no dormant code-unit engine is selected. |
| Compatibility capture/result engine | Match/group/capture views borrow owned capture histories and original subject storage. BCL engine/result roots are forbidden by dependency checks. Source/package receipts preserve captures, retained aliases and scalar restart behavior. |
| GC-timed accounting leases | Prior simplification receipts preserve numeric cursor reservations and deterministic reference-cursor cleanup. No finalizer/identity graph was introduced by this cleanup. Resource denial, cancellation and deadlines remain part of the owned paths. |

## Removed in scalar.33

The following symbols had definitions but no references in production, tests or
benchmark C# source. Their bodies and reflective/emitted entry lists were reviewed
before removal; none is an extension-method container or a public API. Removing
them preserves selected algorithms, SIMD, allocation behavior and resource controls.

| File | Removed symbols |
| --- | --- |
| `Internal/Execution/Utf8EmittedDeclarationIdentifierMatcher.cs` | `TryGetPrefixMatchAtAnchor` |
| `Internal/Execution/Utf8LiteralCompiledEngineRuntime.cs` | `HasLeadingUtf8ScalarLength`, `FindLastUtf8LiteralAlternationViaSearch`, `FindLastAsciiIgnoreCaseLiteralAlternationViaSearch` |
| `Internal/FrontEnd/Utf8FrontEndSearchAnalyzer.cs` | `TryExtractWindowAnchor`, `IsAsciiWhitespaceByte`, `ContainsAsciiWordChar` |
| `Internal/Execution/Utf8StructuralLinearProgram.cs` | Unused `Utf8AsciiFixedTokenLinearExecutor` forwarding type; the active instruction executor stays. |
| `Lokad.Utf8Regex.Pcre2/Pcre2ExecutionArchitecture.cs` | Unused `Pcre2BackendCapability` enum; active backend/program selection stays. |

The first four paths are relative to `src/Lokad.Utf8Regex/`; the final path is
relative to `src/`. These are proved orphaned helpers, not a claim that every one
originated in UTF-16 parity work. The reference inventory and review notes are in
`artifacts/scalar-cleanup-20261013/`.

## Required survivors

The scalar.50-dev cleanup also retires `Utf8EmittedSearchGuidedFallback`, its
capability/policy and optional runtime field. Public `FallbackRegex` discovery
selects the owned scalar program before compiled-runtime dispatch. Direct
internal search-guided consumers retain the canonical managed candidate search
and scalar verifier; the search mode remains available. The unused emitted-family
designation is removed. This is a reduction in implementation and construction
state, without a public matching speedup claim.

Admission/rollback/interruption tests now exercise the live literal-family
emitter. Candidate-window verification still proves cumulative work and caller
loan preservation. Retired-type/field absence and every remaining retained-layout
check pass. Focused Core Release, PythonRe Release and Core Debug evidence is in
`artifacts/fallback-emitter-cleanup-20261015/`. No package qualification or new
package designation follows from this source cleanup.

| Area | Why it remains |
| --- | --- |
| `Utf8BoundaryMap`, `Utf16Boundary`, projection cursors | Public UTF-16 coordinates and string APIs are derived interoperability. Maps resolve scalar boundaries and reject a caller's surrogate-split offset. They do not choose matching units or decode a whole subject for a second engine. `Utf8MappedEntryInvocationTests` checks scalar/split mappings and finite admission; boundary/astral tests check public results. |
| Match/group/capture UTF-16 fields and string extraction | These implement the existing derived-coordinate surface and retained capture history. Production constructors consume owned scalar outcomes. Changing their storage is a separate operating-cost decision, not dead-code deletion. |
| Pattern/replacement string processing | C# string patterns, Unicode escape parsing and replacement-fragment repair remain useful grammar/interoperability. Scalar parsing and byte emission determine execution. A `char` loop alone does not prove code-unit matching. |
| `Fallback*` names and public inspection labels | They name scalar/general verification or native specialization declines. `Utf8Regex.Fallback.cs` calls owned scalar matching; `NativeExecutionKind.FallbackRegex` is a public diagnostic label. Renaming it without consumer need would add churn and can break stored reports. |
| Extension-method containers | `Utf8FallbackStartTransformExecutor` and `Utf8ExecutionRouteText` have live extension-syntax consumers. The now-unused PreparedFallbackCandidate source/executor pair is retired; structural searches keep their production implementation. A type-name count alone is insufficient evidence. |
| PCRE2 `CodeUnit` | This is the explicitly opted-in UTF-8 byte escape `\C`, forbidden by the default profile. `SPEC-PCRE2.md` and `Pcre2BackslashCCompilerTests` define split-byte results and projection rejection. It is isolated PCRE2 behavior, not a .NET UTF-16 parity engine or a change to core/PythonRe scalar semantics. |
| Historical benchmark/parity snapshots | Preserve provenance and previous evidence. Their numbers do not qualify the current engine. Current cost/publication claims require the independent delivery receipts. |

## Validation and remaining work

Focused compiled/emitted, RTL, boundary, dependency and architecture checks validate
the removed paths. Full source and package checks at the cleanup milestone verify
the current artifact; exact results and source identities belong to its receipt.
No performance campaign is needed to time unreachable methods, and no performance
equivalence claim follows from this report.

This closes the current migration-cleanup inventory and removes the proved orphans.
It is not a formal proof that all possible dead code is absent. Broader Unicode/
culture/options/profile coverage, common public construction/default/retention
policies, current-catalog/.NET11 comparisons and final R01-R10 acceptance remain
separate plan gates. Preserve accepted core/PythonRe downstream delivery while
addressing a concrete remaining defect or consumer cost.
