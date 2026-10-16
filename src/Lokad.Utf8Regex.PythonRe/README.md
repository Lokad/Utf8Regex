# Lokad.Utf8Regex.PythonRe

The **0.4.0-scalar.64 Core/PythonRe candidate** bundles the replacement-index
error correction, simpler groupless/literal preparation and the original deadline
through native bulk validation, byte-floor projection and output shaping.
Its immutable candidate manifest and handoff define qualification status and exact
identities. Earlier scalar.63 evidence remains scoped to those earlier bytes;
PCRE2 delivery remains independent. No publication or downstream adoption is implied.

`Lokad.Utf8Regex.PythonRe` is a `net10.0` side extension for `Lokad.Utf8Regex` that exposes a UTF-8-first Python `re` flavored API through `Utf8PythonRegex`.

It is intended for workloads where the input already exists as UTF-8 bytes and you need Python-style regular expression semantics on top of the base `Lokad.Utf8Regex` library.

The qualified private Core/PythonRe pair **0.4.0-scalar.63**, producer
`a24a39837df63f73383276d860dc0f24ef880ba1`, covers strict UTF-8 scalars,
authoritative captures, explicit cursors, four finite invocation controls and
covered eager construction. Original same-byte detached Windows/Linux consumers,
104 capture fixtures and 91 catalog/supplementary digests pass; producer CI is
green. The actual nuspec requires Core `[0.4.0-scalar.63]`. See
`artifacts/scalar-candidates/0.4.0-scalar.63/candidate.json` and
`artifacts/core-python-scalar63-20261016/HANDOFF.md` for exact identities and scope.

Within immutable63, PythonRe implementation and pinned Unicode 15.1 class/case/identifier data are unchanged;
38 source/blob witnesses reuse prior full scalar-data evidence. Core's owned
supplementary Unicode 16.0 case lookup and simpler metered family verification are qualified
separately from that unchanged-code claim. Scalar.62's Python mandatory-empty-
iteration correction remains; its 34–37% same-result nullable Count source cost
is explicit and does not revise the completed checked-access 10% gate.

One selective current-package observation covers finite/public Count, UTF8 tuples
and cursor early disposal. All 135 samples exceed 50 ms; raw timings/allocations and
BCL exclusions are retained. No broad performance/containment claim or historical
snapshot relabeling. Previous 62/61/60/55 and independent PCRE2/Core 59 stay immutable.

Core only retains accepted `Unsafe.SizeOf`; PythonRe has no unchecked references.
Lython accepts SizeOf, per the user's 2026-10-16 confirmation. Frozen 63 README assets
record producer-stage readiness; its original manifest/handoff report completed
qualification without repacking. No publication, consumer pin/default change or
adoption is implied. Defaults/static cached calls remain unlimited; complete
construction/lazy/default/live-retention and broader profile gates are unfinished.
PCRE2 qualifies independently.

Development additionally corrects oversized decimal replacement references:
`\g<2147483648>` reports `PythonRePatternException` through the existing admitted
error builder, rather than leaking `OverflowException`. Unknown names/valid
leading-zero references and original construction/token/deadline controls remain.
Immutable63 retains its old failure classification; the separate source receipt
and erratum do not repack or qualify new bytes as that same candidate.

Development also establishes the original deadline before bulk subject validation
and byte-floor projection for finite-timeout calls without finite budgets/tokens.
Native discovery and required shaping share it; native entry omits an unnecessary
full coordinate map. Existing finite/token and infinite routes remain. This fixes
a source-proved scalar.63 limitation without changing its immutable bytes; see
`artifacts/python-findall-offset-deadline-20261016/SCALAR63-ERRATUM.md`.
No new package or whole-profile/performance-equivalence claim follows this fix.

The earlier scalar.52 pair completed the Core/PythonRe checked-access batch. Selected Core costs
against the fixed original fit10%, with unchanged allocations and a narrow
9.91% emitted-search row. Python's exact-package 17-pair confirmation has per-row
median changes -6.89..+2.16%; some large tails and materialization pool variation
remain explicit. This is selected public-workload evidence, not broad equivalence.
Finite work/depth/working/result and cursor contracts are preserved. Lython's
former SizeOf disagreement is resolved by the user's acceptance confirmation.

The frozen scalar.52 pair has a known class-version limitation: with host Unicode
16 data, `\w` admits U+1C89/U+1E5D0 and `\d` admits U+1E5F1, while the pinned
CPython oracle leaves them unassigned. Development fixes predefined classes,
complements, brackets and word boundaries using compact owned predicates in the
existing class instructions. ASCII masks and bounded scalar lookups avoid
expanding data into patterns. Unproved host-category native paths are ineligible.
The rejected expanded-pattern prototype remains historical evidence. See
[the specification](../../SPEC-PYTHONRE.md) for current source scope; frozen
packages do not acquire this correction automatically.

Development separately fixes group-name admission using pinned CPython identifier
data, shared by declarations, named references/conditionals and replacement
references. General categories incorrectly admitted U+037A/new host assignments
and rejected valid identifier-only characters. Validation now follows the pinned
identifier rules while preserving the exact name spelling. Matching kernels and
invocation controls are unchanged. This source correction is beyond the frozen
scalar.52 pair and is included in the qualified scalar.55 input.

## Support Scope

- Semantic target: `Utf8PythonRegex` models the standard `re` surface, while remaining a managed .NET library.
- Implementation model: parser-first translation. Python-style patterns are parsed into a dedicated AST before any backend is selected.
- Native rendering: the AST emits Unicode scalar atoms/ranges for `Utf8Regex`; native execution receives scalar spellings instead of UTF-16 surrogate-class compensation.
- Global operations: admitted scalar programs serve captured iteration, Python empty-match retry, substitution and split over the original UTF-8 bytes, with one operation deadline/token and owned capture snapshots.
- Case folding: admitted Python ASTs lower to the shared UTF-8 scalar VM with owned CPython 3.13.2 / Unicode 15.1.0 data. Literal classes include extra fold partners; references use lexical simple lowercase, with ASCII scopes restricted to ASCII letters. The four formerly skipped case-folding regressions are active.
- Engine ownership: the adapter uses owned scalar execution and qualified core fast paths. Production BCL engines, UTF-16 progression and BCL result shaping have been removed from both adapter and core. Managed-compatible pattern rendering remains for benchmark comparators. Finite `MaxWorkSteps`, `MaxDepth`, `MaxWorkingBytes` and `MaxResultBytes` select owned scalar execution. Work covers validation, matching and required shaping cumulatively. Depth bounds live backtracking/continuation frames before acquisition. Working bytes admit modeled major active arrays/output controls and growth overlap; exclusive scratch releases synchronously. Result bytes charge modeled major array/text, capture/name, coordinate-map and staging/output capacity before acquisition, including growth/final roots. Charges can conservatively count reused storage again and never refund, including across memory-cursor moves. Exhaustion reports `ExecutionWorkSteps`, `ExecutionDepth`, `ExecutionWorkingBytes` or `ExecutionResultBytes`; the first denial stays authoritative. Compilation/preparation, caller callback execution/allocations, fixed values, borrowed subjects and later standalone getters have separate scope. These controls do not bound live caller retention or process memory. Incidental owner metadata, shared fixed profile data and caller-retained results have separate working scope. Development predefined classes use the pinned profile data; remaining common resource gates remain unfinished.
- Dependency model: the only non-BCL implementation dependency is `Lokad.Utf8Regex`.
- Primary I/O model: the main API surface operates on UTF-8 `ReadOnlySpan<byte>`.
- Capture-aware APIs use PythonRe-specific result/context types, independent from the PCRE2 sidecar.
- Detailed owned matches and borrowed contexts expose nullable `LastIndex` and `LastGroup`: the Python number of the last capture to close on the successful path, and that capture's name. Nested `((a))` closes group 1 last; an unnamed last capture has a null name. Failed and nonparticipating matches have null completion metadata. Fixed-width lookbehind evaluates its body forward from the preceding scalar boundary, preserving Python capture values and close order even for repeated or nested groups.
- Culture model: the profile is culture-invariant by construction; Python `re.LOCALE` is intentionally unsupported.
- Named Unicode escapes: `\N{...}` is intentionally unsupported to avoid embedding a large Unicode-name table in the package.
- `FindAll(...)` is a structural full-match collector.
- `FindAllToStrings(...)` provides CPython-shaped `re.findall(...)` string result shaping.
- `FindAllToUtf8(...)` provides the same host-facing shape with UTF-8 byte outputs.
- `SearchToString(...)`, `MatchToString(...)`, and `FullMatchToString(...)` provide host-friendly scalar helpers.
- `SearchDetailedData(...)`, `MatchDetailedData(...)`, `FullMatchDetailedData(...)`, and `FindIterDetailed(...)` provide non-`ref struct` match snapshots for runtime hosts.
- `ReplaceToString<TState>(...)` / `SubnToString<TState>(...)` support evaluator-driven replacement over match snapshots.
- `Subn(...)` / `Subn<TState>(...)` provide UTF-8 replacement outputs with replacement counts for bytes-first hosts.
- `Replace<TState>(...)` / `Subn<TState>(...)` also expose byte-oriented evaluator overloads for hosts that want to stay UTF-8-native end to end.
- `SplitDetailed(...)` exposes split output with segment/capture metadata.

## Public API

Constructor errors are checked before UTF-8 pattern decoding: explicit resource
policy first, then compile flags and timeout. Unsupported/incompatible flags and
invalid timeouts spend no construction allowance. With valid arguments, malformed
UTF-8 and pattern syntax retain their existing failures. Construction itself still
uses separate fixed backstops, rather than the finite invocation policy.

The explicit text/UTF-8 `Utf8PythonRegex.Compile` factories accept
`Utf8ConstructionPolicy`, a construction timeout and a construction token.
Positive work/depth/instruction/byte limits tighten the fixed backstops for
covered eager decoding, parsing, translation, compilation and capture metadata.
Scalar.52 also prepares the derived FullMatch program under
that same owner before returning. This pays its instruction-copy/metadata cost
upfront, even if FullMatch is unused, and tighter budgets can consequently deny
more compilations. Constructors remain lazy; replacement preparation, incidental
acquisitions and process memory remain outside this partial construction bound.
Construction authority ends before expression publication; execution policy and
the token supplied to each operation remain independent.

This candidate additionally preserves the original call deadline through
cold scalar FullMatch and template substitution preparation. Construction/cache
checkpoints observe call interruption; interrupted preparation is retryable and
warm reuse has no preparation scope allocation. These fixes are included in the
qualified scalar.51 pair and preserved in scalar.52.
See [SPEC-PYTHONRE.md](../../SPEC-PYTHONRE.md) for the exact scopes and exclusions.

```csharp
using System;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

var regex = new Utf8PythonRegex(
    pattern: @"(?P<word>foo)-(?P=word)",
    options: PythonReCompileOptions.None,
    matchTimeout: TimeSpan.FromSeconds(1),
    resourcePolicy: new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = 1024,
        MaxWorkingBytes = 16 * 1024 * 1024,
        MaxResultBytes = 16 * 1024 * 1024
    });

bool isMatch = regex.IsMatch("foo-foo"u8);
var first = regex.Search("xx foo-foo yy"u8);
int count = regex.Count("foo-foo x foo-foo"u8);
byte[] replaced = regex.Replace("foo x foo"u8, "<\\g<word>>");
Utf8PythonMatchData[] matches = regex.FindAll("foo-foo xx foo-foo"u8);
var iter = regex.FindIterDetailed("foo-foo xx foo-foo"u8);
string?[] parts = regex.SplitToStrings("foo-foo xx foo-foo"u8);
```

String-returning helpers such as `SearchToString(...)`, `ReplaceToString(...)`, and `FindAllToStrings(...)`
are also available as host-friendly conveniences when a runtime needs scalar string results.

`CreateMatchCursor(ReadOnlyMemory<byte>)` discovers one detailed snapshot per
`MoveNext` instead of materializing the complete `FindIterDetailed` array:

```csharp
ReadOnlyMemory<byte> subject = System.Text.Encoding.UTF8.GetBytes("é😀");
using var cursor = new Utf8PythonRegex("(?P<scalar>.)").CreateMatchCursor(subject);
while (cursor.MoveNext())
{
    Utf8PythonDetailedMatchData match = cursor.Current;
    // The supplementary scalar occupies four bytes and is one match.
    Console.WriteLine(match.Value.ValueText);
}
```

Keep the subject unchanged and its memory manager alive until exhaustion or
disposal. Dispose when stopping early. The cursor has one shared position and
lifetime across reference aliases, rejects concurrent/reentrant access, and has
no finalizer. EOF and failure release scratch and subject references immediately.
Returned snapshots own their arrays and text and remain valid after advance or
disposal. Sequential advances may cross host calls or asynchronous waits; those
waits count against the original absolute match deadline. The offset/token
overloads preserve scalar-aligned byte offsets and the original invocation token.
Existing span APIs retain their existing contracts.

Scalar.5 reuses the cursor's existing private VM workspace across
moves. Scratch remains charged while positioned, including during projection and
host waits; EOF, failure and disposal release it synchronously. It never backs
published snapshots. Scratch/publication overlap can raise the required finite
working cap. The larger persistent owner has a fixed allocation cost; measured
bulk improvements do not imply every first-result path is faster. Scalar.4
package bytes predate this change.

A finite work or result-capacity policy applies cumulatively through cursor construction,
validation, each move and required snapshot publication. Result capacity covers
modeled major arrays/text, coordinate maps and staging/output growth, with no refunds.
Depth caps live backtracking/continuation frames and can be combined with these
cumulative limits. Exhaustion throws `Utf8ResourceLimitExceededException` with
`ExecutionWorkSteps`, `ExecutionDepth`, `ExecutionWorkingBytes` or `ExecutionResultBytes`; faulted cursors
release scratch immediately. Separate calls get separate counters. Work units are
bounded engine/checkpoint steps and may change between releases. Compilation and
replacement-plan preparation use separate construction backstops. Arbitrary user
callback execution and later standalone result getters are outside this cap;
encoding/copying callback returns is charged. Cancellation and deadlines still apply.
Working bytes bound modeled major active capacity and old/new overlap, with reusable exclusive scratch and invocation-long published cursor credits. Working publication does not bound caller retention; incidental owner/deadline metadata is outside this model. Depth does not bound pattern
nesting, workspace, result size or cumulative work. Result capacity traffic
has separate scope from live caller retention; these controls do not complete safe defaults.

The [host integration recipe](../../HOST-INTEGRATION.md) composes finite working
and result caps with an existing host batch owner. Its reservation must survive
retained snapshots and fresh calls need new admission. It covers only the already
modeled payloads; complete host containment and downstream acceptance remain open.

Scalar.52 leaves `RegexMatchTimeoutException.Input` empty when
working or result bytes are bounded. Timeout failure therefore does not copy the
subject into diagnostic text. Existing pattern/duration metadata and cancellation
remain; this is not a complete exception or process-memory cap.

Scalar.51 derives the lazy `FullMatch` program from existing
immutable instructions and shares predicate/assertion tables. It no longer
retains the parse tree for a second conversion. Cold preparation still observes
the original call's cancellation/deadline and admits new instruction storage;
complete construction and retained-storage policies remain separate.

```csharp
var bounded = new Utf8PythonRegex("(?P<word>\\w+)", PythonReCompileOptions.None,
    TimeSpan.FromSeconds(1), new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxDepth = 1_024, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });
```

Last-capture completion metadata remains an open compatibility item in PLAN.md.

## Development Notes

- Benchmarks: [BENCHMARKS.md](./BENCHMARKS.md)
- Packaged qualification: run `./test-packaged-pythonre.ps1 -Configuration Release` from the repository root to rebuild a version-coherent core/companion pair, verify NuSpec/assembly/SourceLink revisions and PDB identities, and execute the complete PythonRe suite from an isolated NuGet cache. The output includes `package-provenance.json`; `-Archive` requires a clean source revision and preserves the packages, symbols and report hashes.
- Project: [Lokad.Utf8Regex.PythonRe.csproj](./Lokad.Utf8Regex.PythonRe.csproj)
- Main runtime: [Utf8PythonRegex.cs](./Utf8PythonRegex.cs)
- Core match/result types: [PythonReCoreTypes.cs](./PythonReCoreTypes.cs)
- Parser: [PythonReParser.cs](./PythonReParser.cs)
- Translator: [PythonReTranslator.cs](./PythonReTranslator.cs)
- Support spec: [SPEC-PYTHONRE.md](../../SPEC-PYTHONRE.md)
