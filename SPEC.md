# Lokad.Utf8Regex specification

`Lokad.Utf8Regex` is a `net10.0` library whose primary I/O surface is **UTF-8 `ReadOnlySpan<byte>` / `Span<byte>`**. Its matching unit is **one Unicode scalar**, and byte offsets and lengths are the primary result coordinates. The framework target and .NET-shaped API do not impose UTF-16 code-unit matching.

This target contract was revised by explicit user direction on 2026-10-06. Existing unit-subject and BCL compatibility paths are migration debt; the revised specification does not by itself qualify current implementation behavior. The active cutover, operation coverage, cleanup and resource gates are recorded in PLAN.md.

## 1. Charter

Build a managed, dependency-minimal, high-performance UTF-8 regex engine that owns supported construction and execution. Preserve useful regex grammar and supported constructs, including inline options, captures and histories, backreferences, balancing groups, conditionals, lookarounds, contiguous anchors and right-to-left matching. `RegexOptions.NonBacktracking` is a separate linear-time mode rather than the default engine.

Matching advances through complete Unicode scalars in either direction. Dot consumes one scalar subject to its newline options; quantifiers count complete atom matches. Successful match and capture endpoints are UTF-8 scalar boundaries. Supplementary scalars are not split into surrogate-half subject units. Combining marks remain separate scalars; matching does not implicitly normalize text or promote dot to grapheme matching.

Use one owned semantic front-end, with native search and execution plans derived from it. BCL Regex may be a comparator for overlapping grammar/behavior and performance, but it is not the semantic authority and must disappear from production execution. Do not retain a second engine or encoding just to reproduce exact UTF-16 parity.

## 2. Hard requirements

* Target framework: **`net10.0`** only.
* Dependencies: **none** beyond the base/standard libraries, **xUnit**, and **BenchmarkDotNet**.
* Implementation model: **fully managed**. No native code, no P/Invoke, no external regex engines, no Roslyn/source-generator package dependency.
* Options model: use **`RegexOptions`** directly.
* Timeout model: use **`TimeSpan`** and throw **`RegexMatchTimeoutException`** for timeout.
* Public primary surface: **UTF-8 spans and byte buffers**.
* Public semantic contract: Unicode-scalar matching over strict UTF-8, with primary byte coordinates. Qualify direct scalar fixtures; use .NET/other comparators only on declared overlapping semantics.
* Performance objective: beat decode-then-Regex and predecoded Regex on result-compatible workloads, while qualifying native UTF-8 cases under their scalar contract. Exclude semantically mismatched comparisons explicitly. Bound pathological backtracking with owned resource admission and cancellation.

## 3. Semantic contract

### 3.1 Pattern language and options

Retain the supported regex grammar and option model, including inline options, `RightToLeft`, `ECMAScript`, `CultureInvariant`, captures, backreferences, balancing groups, conditionals and lookarounds. Apply them to Unicode scalars. Define supplementary literals/classes/ranges and lone-surrogate pattern escapes explicitly as the owned front-end migrates; char-based parser behavior is not an implicit contract. Use .NET grammar observations where they agree with these rules. Reject unsupported syntax deliberately rather than hiding engine dependence behind a fallback.

Adjacent high/low surrogate spellings, raw or escaped, form one scalar within a literal run or one character-class member. Classes order range endpoints by numeric scalar value, including bounds above U+FFFF; descending supplementary bounds throw `ArgumentException`. An unpaired surrogate atom denotes no scalar and cannot match strict UTF-8. It can be repeated zero times, and negating a singleton class containing it admits every valid scalar. Groups, assertions and separate classes cannot join two such atoms into a scalar. Numeric intervals may span the surrogate gap, but matching considers only valid scalars. An explicit `[\u0000-\uffff]` includes BMP scalars; singleline dot includes every scalar plane.

An empty pattern has one match at every scalar boundary, including both ends:
Count on `😀` is two, and Count on `a😀b` is four. Non-overlapping discovery
advances past an empty result by one complete scalar in the selected direction.
Count has no coordinate result, so primary Count must not construct a whole-
subject UTF-16 map just to enumerate positions. Explicit UTF-16 start APIs may
resolve their scalar-aligned starting boundary as admitted interoperability work.

### 3.2 Replacement semantics

Retain the .NET-style replacement language (`$1`, `${name}`, `$&`, `$```, `$'`, `$+`, `$_`, `$$`) and numeric-group parsing rules, expanding captures discovered under scalar matching. Invoke replacement evaluators once per match. Right-to-left replacement discovers matches from the end and assembles output in subject order. UTF-8 output remains well formed; string-returning APIs materialize the same scalar result as a .NET string. They are not surrogate-half compatibility escape hatches.

Encode each replacement literal and each string evaluator return independently as UTF-8. A valid UTF-16 surrogate pair within that fragment denotes one scalar; an unpaired surrogate becomes U+FFFD, as with `Utf8ReplacementWriter.Append(ReadOnlySpan<char>)`. Fragments cannot join surrogate halves across substitutions, matches or original-subject gaps. This string-boundary encoding rule does not relax strict validation of UTF-8 subjects, replacement-pattern bytes or completed byte-evaluator output.

### 3.3 Culture and casing

`IgnoreCase` compares complete scalars using qualified Unicode case-equivalence rules; pin the Unicode data and culture behavior as those owned paths migrate. `CultureInvariant` selects invariant behavior. Culture-sensitive options retain the construction culture and include it in cache identity, including scoped inline folds. BCL behavior can qualify overlapping cases but cannot supply the production engine or justify folding surrogate halves. Supplementary casing and Turkish-I need direct scalar fixtures.

For the I cluster, invariant folding pairs `I` with `i`, leaving `İ` and `ı`
distinct. Non-Turkic culture folding groups `I`, `i` and `İ`, leaving `ı`
distinct; Turkic folding pairs `I` with `ı` and `İ` with `i`. The owned program
retains this construction-time choice through classes, assertions and references,
including scoped options. Changing the ambient culture during matching cannot
change its case behavior. Supplementary comparisons operate on complete Runes.

### 3.4 Timeout behavior

Timeout behavior must mirror .NET’s model: the timeout is **approximate**, and the engine throws `RegexMatchTimeoutException` at its next timing check after the interval elapses. Timeouts exist to prevent excessive backtracking from monopolizing the process. ([Microsoft Learn][4])

### 3.5 UTF-8 validity

The public UTF-8 APIs accept **well-formed UTF-8 only**. Invalid UTF-8 must not be silently repaired or replacement-decoded on the main API. The low-level .NET UTF-8 transcoder exposes invalid-data signaling when replacement is disabled; `Lokad.Utf8Regex` should follow that spirit and reject malformed input. Public failure mode: throw `ArgumentException` with a precise message and the offending byte offset when the input is ill-formed. ([Microsoft Learn][7])

### 3.6 Scalar boundaries and coordinates

Every successful core match and capture on valid input has a representable UTF-8 byte range. For `😀`, dot returns one match at byte offset 0 with byte length 4; `^.$` succeeds and `^..$` fails. Dot on `a😀b` returns ranges (0,1), (1,4) and (5,1). `^(.)\1$` matches `😀😀`, retaining the complete first scalar in group 1. Dot on `e` followed by U+0301 returns two scalar matches.

Existing UTF-16 coordinate properties are derived interoperability coordinates. They do not affect matching width or permit a capture endpoint inside a scalar. Primary byte operations must not require whole-subject UTF-16 conversion merely to emulate .NET. Remove obsolete half-unit encoding, non-representable-match exceptions and parity-only execution/output paths as qualified scalar consumers replace them.

### 3.7 Cancellation and resource limits

Every execution operation has an explicit `CancellationToken` twin. The token is observed **before matching begins**: an already-canceled token throws `OperationCanceledException` carrying that token, ahead of input validation, limit checks, timeout checks, and matching. There are no optional token parameters; each twin is a distinct overload.

Lazy enumerators (`EnumerateMatches`, `EnumerateSplits`) observe the token when enumeration begins, not during enumeration. Evaluator-driven replacement (`Replace`/`ReplaceToString` with a callback) additionally observes the token on every match iteration, including the BCL fallback loop, so canceling from inside the callback aborts deterministically. Other in-scan cancellation polling is future work, not a current promise. Timeouts stay approximate per 3.4 and keep `RegexMatchTimeoutException`.

Development owned execution additionally retains its invocation token/deadline
through qualified validation, search, projection and lazy advances. Metered
invocation transfers and copies share cumulative work/depth counters from entry;
copying a value enumerator preserves its independent position without restarting
that work allowance or the diagnostic polling cadence. Separate operations create
separate counter owners. This synchronous internal owner is not a public shared
meter and does not support concurrent advancement of copies. Infinite None-token
calls without diagnostics retain inline counters unless a forkable cursor needs
a shared storage owner. Controlled invocation copies share their aggregate ledger.
Owned mapped cursors and split views retain shared leases for coordinate and decoded
payloads. EOF or failure drops one holder; payload remains charged
while another copy or view holds it. After the last holder is collected, cleanup
queues a release which the synchronous ledger drains before its next read or
admission. Abandoned cursors require no explicit disposal. Setup failure before
publication releases its payload loans immediately, preserving borrowed storage.
Split coordinate maps and prepared decoded text have independent payload loans.
When a whole-segment getter returns that prepared string, a weak attachment keeps
its decoded loan alive after the cursor/view disappears; the unused map can still
be collected. The attachment does not root the text. Empty shared strings carry
no payload loan. Independent substring results do not retain the prepared subject;
their result-materialization admission is separate. Alias-registry entry/table
overhead also remains a retained-metadata admission gate.
These leases cover exact managed payloads, with no pooled/native cleanup in their
finalizers. Lazy getter/result bounds, other execution routes and fixed object
overhead still require admission work; this does not enable the core's unsupported
finite public policies or establish a complete memory cap.

The core library defines no work, depth, or memory budgets; the only configured cap is the instance `MatchTimeout` (default `Regex.InfiniteMatchTimeout`, i.e. unlimited). The shared `Utf8ResourcePolicy` (nullable work-steps, depth, working-bytes, and result-bytes budgets plus an `Unlimited` singleton) is accepted by an explicit constructor overload for forward compatibility, but any finite budget is rejected loudly with `NotSupportedException`: this profile enforces only match timeouts. Finite behavior beyond timeouts is opt-in future API, never silent and never safe-by-default without evidence.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8Regex("cat|horse");
Assert.Throws<OperationCanceledException>(() => regex.IsMatch("cat horse"u8, canceled.Token));
```

## 4. Public API

The public API is split into:

* the baseline .NET-mirroring surface
* intentional `Lokad.Utf8Regex` extensions

## 4.1 Baseline .NET-mirroring surface

Use this public surface.

```csharp
namespace Lokad.Utf8Regex;

using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

public sealed class Utf8Regex
{
    public Utf8Regex(string pattern);
    public Utf8Regex(string pattern, RegexOptions options);
    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static TimeSpan DefaultMatchTimeout { get; set; }

    public string Pattern { get; }
    public RegexOptions Options { get; }
    public TimeSpan MatchTimeout { get; }

    public bool IsMatch(ReadOnlySpan<byte> input);
    public int Count(ReadOnlySpan<byte> input);
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input);
    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input);
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input);
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input);
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count);

    public byte[] Replace(ReadOnlySpan<byte> input, string replacement);
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8);
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator);

    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement);
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator);

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten);

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten);

    public int GroupNumberFromName(string name);
    public string GroupNameFromNumber(int i);
    public string[] GetGroupNames();
    public int[] GetGroupNumbers();

    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern);
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static int Count(ReadOnlySpan<byte> input, string pattern);
    public static int Count(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static int Count(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, string pattern);
    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, string pattern);
    public static Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, string pattern);
    public static Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern);
    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern, int count);
    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern, int count, RegexOptions options);
    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern, int count, RegexOptions options, TimeSpan matchTimeout);

    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement);
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, RegexOptions options);
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, RegexOptions options, TimeSpan matchTimeout);
}

public readonly struct Utf8ValueMatch
{
    public bool Success { get; }
    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }
    public int LengthInBytes { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }
}

public readonly ref struct Utf8MatchContext
{
    public bool Success { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }

    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned

    public int GroupCount { get; }
    public Utf8GroupContext GetGroup(int number);
    public Utf8GroupContext GetGroup(string name);
    public bool TryGetGroup(int number, out Utf8GroupContext group);
    public bool TryGetGroup(string name, out Utf8GroupContext group);

    public string GetValueString();
}

public readonly ref struct Utf8GroupContext
{
    public bool Success { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }
    public int CaptureCount { get; }

    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned
    public ReadOnlySpan<byte> GetValueBytes();   // throws if not byte-aligned
    public string GetValueString();

    public Utf8CaptureContext GetCapture(int index);
}

public readonly ref struct Utf8CaptureContext
{
    public bool Success { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }

    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned

    public ReadOnlySpan<byte> GetValueBytes();   // throws if not byte-aligned
    public string GetValueString();
}

public readonly ref struct Utf8ValueSplit
{
    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }

    public ReadOnlySpan<byte> GetValueBytes();   // throws if not byte-aligned
    public string GetValueString();
}

public delegate void Utf8MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

public delegate string Utf16MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref TState state);

public ref struct Utf8ReplacementWriter
{
    public void Append(ReadOnlySpan<byte> utf8);
    public void Append(ReadOnlySpan<char> utf16);
    public void AppendAsciiByte(byte value);
    public void Append(Rune value);
}
```

Baseline API notes:

* This surface mirrors span-based `.NET Regex` APIs where possible, but with `ReadOnlySpan<byte>` as the primary input. Materialized `Matches` / `Split` collections are intentionally deferred. `TryReplace` uses `OperationStatus`. ([Microsoft Learn][1])
* `RegexOptions.Compiled` selects an execution strategy with the same scalar semantics; it must not change the accepted language or results.
* Overloads without an explicit timeout use `Utf8Regex.DefaultMatchTimeout`, whose initial value must be `Regex.InfiniteMatchTimeout`.
* `Utf8ValueMatch` is the cheap, storable coordinate result. Rich group/capture inspection is provided by `MatchDetailed(...)` and by evaluator contexts.
* Successful core matches and captures on valid input are byte-aligned at scalar boundaries. Byte accessors expose their ranges; UTF-16 properties are derived coordinates. An unsuccessful group still follows its documented unsuccessful-access contract.
* `Utf8GroupContext.CaptureCount` and `GetCapture(int index)` must match .NET capture history ordering.
* `Utf8ValueSplitEnumerator` preserves the span API's count/empty-element/RTL ordering conventions, using scalar matches. Captures are not emitted as separate split elements. Compare with `.NET Regex.EnumerateSplits` only when delimiter matching semantics overlap.
* `replacementPatternUtf8` overloads interpret bytes as UTF-8 encoded .NET replacement patterns, not as literal bytes.
* All `ref struct` match/group/capture/split views are ephemeral over the original input span and cannot outlive the calling frame or input buffer.
* `TryReplace` returns `Done` or `DestinationTooSmall` only for destination sizing. On `DestinationTooSmall`, `bytesWritten == 0` and callers must treat `destination` as unchanged. Invalid UTF-8, invalid replacement text, invalid regex construction and timeout still throw. Successful scalar matches do not introduce non-representable byte results.
* Evaluator-based replacement is currently the allocating replacement path; allocation control is provided only for fixed replacement-pattern overloads.

## 4.2 UTF-16 offset compatibility APIs

These APIs accept explicitly named **UTF-16 coordinate offsets** for interoperability with .NET strings. The offsets must resolve to complete scalar boundaries; they do not request UTF-16-unit matching. An offset inside a supplementary scalar throws `ArgumentOutOfRangeException`, naming the offset parameter. The preferred native surface uses UTF-8 spans and byte coordinates.

```csharp
public sealed class Utf8Regex
{
    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
}
```

Rules for these compatibility APIs:

* The `utf16Offset` parameter is measured in UTF-16 code units, exactly like
  `.NET Regex`.
* Resolve these offsets into scalar boundaries and execute the same scalar engine. No alternate char-unit matching mode is implied.
* Method names must keep the UTF-16 coordinate system explicit; do not expose
  these as plain `startat` overloads on otherwise byte-native methods.

## 4.3 Intentional API extensions beyond baseline .NET Regex

```csharp
public sealed class Utf8Regex
{
    public int MatchMany(
        ReadOnlySpan<byte> input,
        Span<Utf8ValueMatch> destination,
        out bool isMore);

    public Utf8RegexAnalysis Analyze();
}

public readonly struct Utf8RegexAnalysis
{
    public bool IsFullyNative { get; }
    public bool MayFallbackOnNonAsciiInput { get; }
    public bool IsExactLiteral { get; }
    public int MinRequiredLength { get; }

    public int CopyRequiredLiterals(
        Span<Utf8RequiredLiteral> destination,
        out bool isMore);
}

public readonly struct Utf8RequiredLiteral
{
    public bool IsCaseInsensitive { get; }
    public int FixedOffsetInBytes { get; }   // -1 when not fixed
    public ReadOnlyMemory<byte> Utf8Bytes { get; }
}
```

These additions are intentional:

* `MatchMany(...)` is the allocation-control multi-match API: it writes up to `destination.Length` matches, returns `written`, and sets `isMore` when additional matches exist beyond what fit.
* `Analyze()` exposes a small stable search/planning surface for higher-level engines: native eligibility, minimum required length, exact-literal detection, and required literals suitable for prechecks and indexing.

Rules for these extensions:

* `MatchMany(...)` does not imply a continuation protocol. `isMore` is only a clean termination signal for callers that intentionally cap result count.
* `CopyRequiredLiterals(...)` exposes only stable prefilter facts. It is not a public dump of the internal execution plan.
* `Utf8RequiredLiteral.FixedOffsetInBytes == -1` means the literal is required but not at a fixed byte offset.
* `Analyze()` is advisory for planning and prechecks; matching semantics are still defined only by the regex APIs themselves.

## 5. Architecture

The architecture must keep these invariants:

* one owned semantic front-end for the declared scalar language
* separate lowerings for candidate search and full execution
* scalar-aware UTF-8 subject traversal and primary byte ranges
* native `RightToLeft`; never emulate it by reversing the input
* `RegexOptions.NonBacktracking` is a separate owned linear-time scalar search mode
  whose discovery guarantee covers the complete Count, enumeration, split or
  replacement invocation, including repeated empty matches. Any shared batch
  storage must be admitted before allocation and remain charged while a copied
  cursor can still use it.
* `Utf8Regex` instances are immutable and thread-safe

Implementation details may evolve, but fast paths must be derived from the same semantic front-end rather than from separate pattern parsers.

## 6. Performance and benchmarking

Performance rules:

* production paths must stay UTF-8-native; do not route normal execution through `Encoding.UTF8.GetString(...)` + built-in `Regex`
* search and full matching are distinct concerns; search-first execution is expected
* prefer BCL vectorized primitives first, then `SearchValues<T>`, then manual intrinsics only when measured
* avoid whole-input string materialization except for explicit compatibility APIs such as `ReplaceToString`

Testing and benchmarks:

* direct scalar fixtures define semantic acceptance; .NET 10 and other engines are differential comparators only for overlapping cases
* tests cover grammar, scalar matching, supplementary ranges/captures, replacement, RTL, empty advancement, malformed input, boundaries, resources and cancellation; retarget old surrogate-half witnesses to scalar outcomes
* benchmark methodology must use warm throughput, not cold-start `Dry` numbers, for parity decisions
* benchmark suite must report both baselines:
  * **Baseline A**: `Encoding.UTF8.GetString(input)` -> built-in `Regex` -> UTF-8 re-encoding where relevant
  * **Baseline B**: built-in `Regex` on a predecoded `string`
* benchmark catalogs identify equal-result lanes, exclusions and owned routes; historical fallback or UTF-16-parity rows do not qualify scalar execution

## 7. Acceptance criteria

The implementation is done when all of the following are true:

* Supported features obey the declared Unicode-scalar contract over strict UTF-8, with direct coverage across all public operations.
* String-returning APIs materialize the same scalar results as byte APIs.
* Every successful core match and capture has a correct scalar-aligned byte range. There is no surrogate-half execution mode.
* `RegexOptions.NonBacktracking` uses scalar matching with ordered alternatives,
  greedy/lazy choice and the regular grammar's documented restrictions. Each
  public group exposes its last completed capture, including an empty capture;
  nested/shared declarations retain their own openings. Runtime capture defects
  and timeout rounding do not define this contract.
* Supported construction/execution is owned, common construction/invocation/result/retained bounds work, and synchronous cancellation interrupts ongoing work. Obsolete parity machinery and production BCL engine dependence are removed.
* Hot native paths are allocation-light, and the benchmark suite meets qualified per-scenario thresholds against semantically comparable baselines.

[1]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=net-10.0
[2]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions
[3]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-options
[4]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.matchtimeout?view=net-10.0
[5]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/substitutions-in-regular-expressions
[6]: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/RegexParser.cs
[7]: https://learn.microsoft.com/en-us/dotnet/api/system.text.unicode.utf8.toutf16?view=net-10.0
[8]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding
[9]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions?view=net-10.0
[10]: https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1?view=net-10.0
