# Lokad.Utf8Regex.PythonRe

`Lokad.Utf8Regex.PythonRe` is the optional `net10.0` Python-flavored adapter over
strict UTF-8 scalars, with primary byte coordinates and derived UTF-16 projections.

The qualified private **Core/PythonRe 0.4.0-scalar.88** pair comes from clean
producer `64c8f38b315b083f5b73a556477679b5725bd13b`; PythonRe requires exact Core
`[0.4.0-scalar.88]`. Same immutable bytes pass Windows/Linux consumers,
107capture/91semantic fixtures, API/member/symbol/provenance checks and a fresh
isolated public restore. [Producer CI](https://github.com/Lokad/Utf8Regex/actions/runs/39072816761)
is green. [Handoff](../../artifacts/core-python-scalar88-20261017/HANDOFF.md) and
[consumption](../../artifacts/core-python-scalar88-20261017/CONSUMING.md) record exact scope/identities.
Earlier87/86/85/84 and independentPCRE76/Core76 remain immutable.

Scalar.88 batches scalar URI Count, finite ASCII structural-family Count and
the Python brace/verbose grammar repair. Existing counters/verifiers omit VM
scratch under proved eligibility; full strict validation, scalar captures,
retained aliases and original cancellation/deadline/finite controls remain.
Python admits literal/unfinished braces, omitted lower bounds and verbose trivia
before quantifiers. Scalar.88 still has the aboveInt32 repeat-bound gap;
development source admits finite bounds through4294967294 with distinct
unbounded repetition, exact scalar captures and the existing controls. The new
source has not yet been packaged or performance-qualified. Core references
only permitted Unsafe.SizeOf, accepted by Lython; PythonRe none. Actual unchecked
reads, generated helpers, native imports and shipped BCL matchers/results stay absent.

[Selective exact88 current costs](../../artifacts/core-python-scalar88-20261017/vm-cost/RESULT.md)
use default runtime/tiering/R2R on10.0.12 with explicit finite allowances.
Ordinary unlimited/finite Count is18.7/42.4us for dense URI,8.3/185.3us for method
calls and80.7/82.9us for captured scalar Python. Finite Core allocates about112B;
the selected captured Python row5776/5888B. Finite structural cost remains about22x
unlimited, and qualified .NET controls expose substantial gaps. All selected
ordinary product lanes qualify; some compiledURI/.NET control lanes remain
unqualified after the sole confirmation. Both raw batches/uncertainty are retained;
no further timing or broad/CPythonStatus/cross-version claim.

Closed [URI source costs](../../artifacts/core-uri-count-current-20261017/RESULT.md) and
[structural source costs](../../artifacts/finite-structural-family-count-20261017/RESULT.md)
keep their FullOpts scope. Earlier exact86 noisy default-runtime evidence stays
attached to86. Source gains and historical tables do not replace current package costs.

Defaults/static calls remain unlimited. [Host composition](../../HOST-INTEGRATION.md)
explains modeled capacities and surviving owners. Full construction/lazy/default/
common-retention/host/SPEC coverage remains open. No PCRE88, publication, external
consumer pin/default change or actual adoption is implied.

## Support Scope

Scalar.88 fixes literal/unfinished braces, omitted-lower repeat
bounds and verbose quantifier attachment. Scalar captures and existing matching
paths remain, with metered construction lookahead. Development after scalar.88
extends finite repeat bounds through4294967294 without enlarging invocation
counter/frame/journal layouts. Invalid larger bounds use PythonRePatternException;
required nullable work remains interruptible and metered. See the
[contract](../../SPEC-PYTHONRE.md#brace-repetition-and-verbose-attachment) and
[evidence](../../artifacts/python-wide-repeats-20261018/RESULT.md). Immutable88
retains its original grammar and qualification scope.

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

Scalar.86 reuses Core's complete literal finder for exact,
capture-free `FullMatch` value and string results. It checks both suffix endpoints,
validates the entire subject and keeps the original deadline/token and all four
finite controls. These calls omit per-scalar VM dispatch and lazy full-program
preparation; detailed/capture-dependent and alternative routes retain their
existing handling. Required strings still need result-byte admission. Lower
actual work can admit tighter policies; resource units and defaults are unchanged.
One bounded FullOpts source comparison shows short ASCII/scalar value calls
55-83% faster and scalar string output44-51% faster, with unchanged allocations.
The offset case and controls differ by a few percent; those small differences
are not precise causal claims. These source comparisons do not establish package-default costs or CPythonStatus.
[Evidence](../../artifacts/python-literal-fullmatch-20261017/RESULT.md).

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
