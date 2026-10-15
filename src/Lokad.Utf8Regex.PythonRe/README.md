# Lokad.Utf8Regex.PythonRe

`Lokad.Utf8Regex.PythonRe` is a `net10.0` side extension for `Lokad.Utf8Regex` that exposes a UTF-8-first Python `re` flavored API through `Utf8PythonRegex`.

It is intended for workloads where the input already exists as UTF-8 bytes and you need Python-style regular expression semantics on top of the base `Lokad.Utf8Regex` library.

The latest locally qualified private Core/PythonRe pair is **0.4.0-scalar.50**,
producer `3879bbb6bcfbee4b44771075e2417de8f1ab19ba`. It covers strict UTF-8 scalar
matching, authoritative capture completion, an explicitly owned persistent cursor,
finite invocation work/depth/working/result limits and covered eager construction
controls. Exact detached Windows/Linux consumers and CPython capture/digest checks
pass. Use `artifacts/scalar-candidates/0.4.0-scalar.50/candidate.json`, its immutable
feed/config and `artifacts/core-python-scalar50-20261015/HANDOFF.md`; the delivery
receipt records exact identities, coverage and current operating costs.

This pair is not published or accepted downstream automatically. Lython's Core DLL
unchecked-API gate remains unmet even though the PythonRe DLL has no unchecked API
references. Defaults and static cached calls remain unlimited; embedders select
finite policies explicitly. Complete construction/lazy/live-retention controls,
safe defaults, category-version reconciliation and broader profile acceptance
remain unfinished. PCRE2 qualifies independently; prior candidate bytes stay
unchanged. Development source beyond the candidate requires its own acceptance.

## Support Scope

- Semantic target: `Utf8PythonRegex` models the standard `re` surface, while remaining a managed .NET library.
- Implementation model: parser-first translation. Python-style patterns are parsed into a dedicated AST before any backend is selected.
- Native rendering: the AST emits Unicode scalar atoms/ranges for `Utf8Regex`; native execution receives scalar spellings instead of UTF-16 surrogate-class compensation.
- Global operations: admitted scalar programs serve captured iteration, Python empty-match retry, substitution and split over the original UTF-8 bytes, with one operation deadline/token and owned capture snapshots.
- Case folding: admitted Python ASTs lower to the shared UTF-8 scalar VM with owned CPython 3.13.2 / Unicode 15.1.0 data. Literal classes include extra fold partners; references use lexical simple lowercase, with ASCII scopes restricted to ASCII letters. The four formerly skipped case-folding regressions are active.
- Engine ownership: the adapter uses owned scalar execution and qualified core fast paths. Production BCL engines, UTF-16 progression and BCL result shaping have been removed from both adapter and core. Managed-compatible pattern rendering remains for benchmark comparators. Finite `MaxWorkSteps`, `MaxDepth`, `MaxWorkingBytes` and `MaxResultBytes` select owned scalar execution. Work covers validation, matching and required shaping cumulatively. Depth bounds live backtracking/continuation frames before acquisition. Working bytes admit modeled major active arrays/output controls and growth overlap; exclusive scratch releases synchronously. Result bytes charge modeled major array/text, capture/name, coordinate-map and staging/output capacity before acquisition, including growth/final roots. Charges can conservatively count reused storage again and never refund, including across memory-cursor moves. Exhaustion reports `ExecutionWorkSteps`, `ExecutionDepth`, `ExecutionWorkingBytes` or `ExecutionResultBytes`; the first denial stays authoritative. Compilation/preparation, caller callback execution/allocations, fixed values, borrowed subjects and later standalone getters have separate scope. These controls do not bound live caller retention or process memory. Incidental owner metadata and caller-retained results have separate working scope; category-version reconciliation remains unfinished.
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
They do not bound every allocation, lazy preparation or process memory.
Construction authority ends before expression publication; execution policy and
the token supplied to each operation remain independent.

This candidate additionally preserves the original call deadline through
cold scalar FullMatch and template substitution preparation. Construction/cache
checkpoints observe call interruption; interrupted preparation is retryable and
warm reuse has no preparation scope allocation. These fixes are included in the
qualified scalar.50 pair; later development is outside that immutable input.
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

Development after scalar.50 derives the lazy `FullMatch` program from existing
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
