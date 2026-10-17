# Lokad.Utf8Regex.PythonRe

`Lokad.Utf8Regex.PythonRe` is the optional `net10.0` Python-flavored adapter over
strict UTF-8 scalars, with primary byte coordinates and derived UTF-16 projections.

The qualified private **Core/PythonRe 0.4.0-scalar.85** pair pins Core
`[0.4.0-scalar.85]`, producer `fce0fbb4531543c1b0f45d904a2ffd3807dad6bc`.
Same-byte Windows/Linux consumers,104 capture/91 semantic fixtures, symbols,
provenance/member/API checks and a fresh isolated public restore pass.
[Producer CI](https://github.com/Lokad/Utf8Regex/actions/runs/39050443092) is green.
[Handoff](../../artifacts/core-python-scalar85-20261017/HANDOFF.md) and
[consumption](../../artifacts/core-python-scalar85-20261017/CONSUMING.md) preserve
identities and scope. Earlier84 and independent PCRE76/Core76 remain immutable;
no PCRE85, publication, consumer pin/default change or adoption is implied.

Scalar.85 reuses complete native literal/family discovery for captured `IsMatch`
with or without invocation controls. A boolean needs no capture slots or VM
attempts. Capture-free unmetered calls retain their cursor route. Generic,
capture-dependent, folded, empty and assertion patterns retain established routes;
captured output operations and held snapshots keep their behavior.
Native Count improvements, direct string Split staging, conservative construction
accounting and explicit cursor lifetime remain. Whole-input/scalar validation,
original deadlines/tokens and work/depth/storage admission stay intact.
Less real work/storage can admit tighter policies; defaults/static calls remain
unlimited. Parser/compiler/Unicode inputs and API snapshots are unchanged from84.
Deferred captured-search prototypes are absent.

[Exact85 current costs](../../artifacts/core-python-scalar85-20261017/vm-cost/RESULT.md)
include boolean calls, Count and required captured UTF8 output, all-four finite
caps/live token and both .NET controls under default tiering/PGO/R2R.225 raw samples
include the sole selective confirmation. Noisy/short lanes remain unqualified;
the ASCII boolean row differs sharply across processes and stays uncertain.
Closed FullOpts source pairs record the boolean gains and6-9% higher finite Unicode
costs in the follow-up; they do not qualify current default-runtime costs or
update CPythonStatus. The [historical snapshot](BENCHMARKS.md) stays separate.

Core only retains permitted `Unsafe.SizeOf`, accepted by Lython; Python has none.
No unchecked reads, generated helpers, native imports or shipped BCL matcher/
results return. Full construction/lazy/default/common-retention/host/SPEC coverage
remains open; see [host composition](../../HOST-INTEGRATION.md).

## Support Scope

- Semantic target: `Utf8PythonRegex` models the standard `re` surface, while remaining a managed .NET library.
- Implementation model: parser-first translation. Python-style patterns are parsed into a dedicated AST before any backend is selected.
- Native rendering: the AST emits Unicode scalar atoms/ranges for `Utf8Regex`; native execution receives scalar spellings instead of UTF-16 surrogate-class compensation.
- Global operations: admitted scalar programs serve captured iteration, Python empty-match retry, substitution and split over the original UTF-8 bytes, with one operation deadline/token and owned capture snapshots.
- Case folding: admitted Python ASTs lower to the shared UTF-8 scalar VM with owned CPython 3.13.2 / Unicode 15.1.0 data. Literal classes include extra fold partners; references use lexical simple lowercase, with ASCII scopes restricted to ASCII letters. The four formerly skipped case-folding regressions are active.
- Engine ownership: the adapter uses owned scalar execution and qualified core fast paths. Production BCL engines, UTF-16 progression and BCL result shaping have been removed from both adapter and core. Managed-compatible pattern rendering supplies the established timeout-exception pattern identity and benchmark comparators. Finite `MaxWorkSteps`, `MaxDepth`, `MaxWorkingBytes` and `MaxResultBytes` select owned metered execution, including qualified prepared literal-family routes. Work covers validation, matching and required shaping cumulatively. Depth bounds live backtracking/continuation frames before acquisition. Working bytes admit modeled major active arrays/output controls and growth overlap; exclusive scratch releases synchronously. Result bytes charge modeled major array/text, capture/name, coordinate-map and staging/output capacity before acquisition, including growth/final roots. Charges can conservatively count reused storage again and never refund, including across memory-cursor moves. Exhaustion reports `ExecutionWorkSteps`, `ExecutionDepth`, `ExecutionWorkingBytes` or `ExecutionResultBytes`; the first denial stays authoritative. Compilation/preparation, caller callback execution/allocations, fixed values, borrowed subjects and later standalone getters have separate scope. These controls do not bound live caller retention or process memory. Incidental owner metadata, shared fixed profile data and caller-retained results have separate working scope. Development predefined classes use the pinned profile data; remaining common resource gates remain unfinished.
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
Scalar.82 excludes individual charges for temporary
immutable-metadata views and enumerator boxes. Their backing tables, traversal
work and original cancellation/deadline checks remain covered.
Construction authority ends before expression publication; execution policy and
the token supplied to each operation remain independent.

Scalar.72 checks that same construction authority between
scalar-aligned4096-byte count/decode blocks for UTF8 patterns. Strict decoding,
absolute malformed-byte offsets, original numeric charges and one admitted string
remain. Failed decoding releases unpublished text capacity; interruption may win
before a later malformed suffix. Inner decoder wording can differ for later
blocks. This change is outside immutable Core/Python69 and PCRE71 packages;
it does not complete construction/default/live-retention or host containment.

Scalar.72 also constructs existing semantic BMP literal nodes directly through
the admitted scalar-node factory, omitting per-atom Core parsing. Python case
expansion, surrogate rejection, scalar VM, captures and original construction
authority remain. Less actual preparation lowers numeric work/depth/byte needs;
explicit caps still deny. One public source comparison reduces8KiB byte-pattern
Compile48.7->19.4ms and33.0->11.9MB; the class-only control is unchanged within
noise. Remaining cost and raw observations are explicit. The private
Core/PythonRe scalar.72 input includes this and bounded UTF8 construction decoding;
its handoff records qualification. Immutable69/71 bytes remain unchanged.

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
Scalar.82 stages `SplitToStrings` references directly
in the existing metered result buffer. `SplitDetailed` keeps its capture metadata
route. Full validation, captures/nulls, scalar starts, original interruption and
admission remain; less actual staging can admit tighter work/byte policies.
One bounded source comparison reduces dense/captured allocations by 24–30%,
with an explicit 4.8–7.5% ASCII execution-time cost and 3.5–4.4% faster captured
Unicode. These are FullOpts source results, not new default-PGO CPython Status
or package results. [Evidence](../../artifacts/pythonre-split-string-staging-20261017/RESULT.md).
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

## Development Notes

- Benchmarks: [BENCHMARKS.md](./BENCHMARKS.md)
- Packaged qualification: run `./test-packaged-pythonre.ps1 -Configuration Release` from the repository root to rebuild a version-coherent core/companion pair, verify NuSpec/assembly/SourceLink revisions and PDB identities, and execute the complete PythonRe suite from an isolated NuGet cache. The output includes `package-provenance.json`; `-Archive` requires a clean source revision and preserves the packages, symbols and report hashes.
- Project: [Lokad.Utf8Regex.PythonRe.csproj](./Lokad.Utf8Regex.PythonRe.csproj)
- Main runtime: [Utf8PythonRegex.cs](./Utf8PythonRegex.cs)
- Core match/result types: [PythonReCoreTypes.cs](./PythonReCoreTypes.cs)
- Parser: [PythonReParser.cs](./PythonReParser.cs)
- Translator: [PythonReTranslator.cs](./PythonReTranslator.cs)
- Support spec: [SPEC-PYTHONRE.md](../../SPEC-PYTHONRE.md)
