# Lokad.Utf8Regex specification

`Lokad.Utf8Regex` is a `net10.0` library whose semantic oracle is **`System.Text.RegularExpressions.Regex` on .NET 10**, while its primary I/O surface is **UTF-8 `ReadOnlySpan<byte>` / `Span<byte>`**. That choice is justified because the official regex APIs and span-based matching APIs are currently defined on UTF-16 `string` / `ReadOnlySpan<char>`, not bytes, and the open runtime request for first-class UTF-8 regex support is still in **Future / No due date**. .NET strings themselves are UTF-16 sequences, so this project must preserve **UTF-16 regex semantics** while changing the execution/storage model to UTF-8 bytes. ([Microsoft Learn][1])

## 1. Charter

`Lokad.Utf8Regex` must be a **managed, dependency-minimal, high-performance UTF-8-native regex engine** that stays aligned with the .NET regex language and behavior: same pattern language, same option model, same replacement language, same timeout model, same right-to-left behavior, and the same default backtracking semantics unless the caller explicitly requests `RegexOptions.NonBacktracking`. For the first released version, the semantic target is restricted to `RegexOptions.CultureInvariant` behavior; broader culture-sensitive parity is explicitly deferred. The .NET regex language is documented as Perl-5-compatible with additional .NET features such as right-to-left matching, and inline options are part of that language. ([Microsoft Learn][2])

The project goal is **not** “a different regex flavor that happens to be fast.” It is “the fastest possible managed UTF-8 implementation that behaves like .NET regex.” That means the default engine must preserve constructs and behaviors that `RegexOptions.NonBacktracking` intentionally does not: backreferences, balancing groups, conditionals, lookarounds, `\G`, and full capture history inside loops. The public `NonBacktracking` option should still be supported, but it is a separate mode, not the foundation of the default engine. ([Microsoft Learn][3])

Implementation priority may still be performance-first. In practice, that means the engine should optimize first for the overwhelmingly common aligned region where UTF-8 byte boundaries and the relevant UTF-16-observable boundaries coincide, especially ASCII and BMP-dominant workloads. Rare supplementary / surrogate-splitting edge cases may initially use slower native machinery, or explicit compatibility fallback during development, but the destination remains full .NET-semantic behavior rather than a weakened UTF-8-specific regex flavor.

This performance-first stance must not turn into parser accretion. `Lokad.Utf8Regex` should have exactly one semantic front-end, and native fast paths must be derived from that front-end rather than from independent pattern-string parsers. Temporary bootstrap heuristics are acceptable only as a short-lived development tactic before the vendored front-end is integrated; they are not the long-term semantic basis of the project.

## 2. Hard requirements

* Target framework: **`net10.0`** only.
* Dependencies: **none** beyond the base/standard libraries, **xUnit**, and **BenchmarkDotNet**.
* Implementation model: **fully managed**. No native code, no P/Invoke, no external regex engines, no Roslyn/source-generator package dependency.
* Options model: use **`RegexOptions`** directly.
* Timeout model: use **`TimeSpan`** and throw **`RegexMatchTimeoutException`** for timeout.
* Public primary surface: **UTF-8 spans and byte buffers**.
* Public semantic oracle: .NET 10 `Regex`.
* Performance objective: beat `UTF8 decode -> Regex -> UTF8 encode` on the intended workloads, especially ASCII and ASCII-dominant workloads, without changing semantics. The built-in regex engine is powerful and often fast, but Microsoft explicitly documents that backtracking can still become pathological, which is why timeout parity is mandatory. ([Microsoft Learn][4])

## 3. Semantic contract

### 3.1 Pattern language and options

The accepted pattern language must match .NET regex behavior on .NET 10. That includes the normal grammar, inline options, `RightToLeft`, `ECMAScript`, `CultureInvariant` behavior, backtracking behavior, captures, backreferences, balancing groups, conditionals, lookarounds, and the documented replacement syntax. Broader culture-sensitive parity is out of scope for the first version. ([Microsoft Learn][2])

### 3.2 Replacement semantics

The fixed replacement-string path must behave like `.NET Regex.Replace`, including the substitution language (`$1`, `${name}`, `$&`, `$```, `$'`, `$+`, `$_`, `$$`) and the numeric-group parsing rules. The evaluator path must behave like `MatchEvaluator`: the replacement callback is invoked once per match, and its result is substituted for that match. Right-to-left replacement must search from the end, and the segment assembly logic must preserve the same observable output order as the runtime. ([Microsoft Learn][5])

### 3.3 Culture and casing

For the first version, `Lokad.Utf8Regex` supports only `RegexOptions.CultureInvariant` semantics. In practice, that means `IgnoreCase` must match the runtime when `CultureInvariant` is in effect, and construction or execution for culture-sensitive modes should fail explicitly or route through an explicit compatibility fallback during development rather than silently approximating behavior. Do not hand-roll “close enough” case folding. Broader culture-sensitive parity, including `CurrentCulture`-dependent cases such as Turkish-I behavior, is a later expansion item. ([GitHub][6])

### 3.4 Timeout behavior

Timeout behavior must mirror .NET’s model: the timeout is **approximate**, and the engine throws `RegexMatchTimeoutException` at its next timing check after the interval elapses. Timeouts exist to prevent excessive backtracking from monopolizing the process. ([Microsoft Learn][4])

### 3.5 UTF-8 validity

The public UTF-8 APIs accept **well-formed UTF-8 only**. Invalid UTF-8 must not be silently repaired or replacement-decoded on the main API. The low-level .NET UTF-8 transcoder exposes invalid-data signaling when replacement is disabled; `Lokad.Utf8Regex` should follow that spirit and reject malformed input. Public failure mode: throw `ArgumentException` with a precise message and the offending byte offset when the input is ill-formed. ([Microsoft Learn][7])

### 3.6 Unavoidable representability edge case

This is the one place where the spec must be explicit: .NET regex semantics are defined over **UTF-16 code units**, and .NET strings are sequences of `char`, with supplementary Unicode characters occupying **two UTF-16 code units**. Because of that, a .NET-semantic match, capture, or replacement can theoretically begin or end **between the two surrogates** of one supplementary scalar. A byte-oriented UTF-8 API cannot represent that boundary as a valid byte slice. Therefore the engine must keep exact **internal UTF-16 semantics**, but if a public **byte-oriented** result is not representable as valid UTF-8, the byte API must **fail explicitly** rather than silently diverge. Provide a slow-path compatibility API that can still return the exact UTF-16 result as `string`. This is a design consequence of the documented UTF-16 string model and char-based regex surface. ([Microsoft Learn][8])

This requirement should not be misread as an instruction to optimize the rarest edge cases first. It is a statement of the final semantic invariant. The implementation should prioritize the common aligned cases first, while keeping these non-byte-aligned edge cases correct, measurable, and progressively moved from fallback/slower paths into the native engine.

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
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count = int.MaxValue);

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

    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.None,
        TimeSpan matchTimeout = default);

    public static int Count(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.None,
        TimeSpan matchTimeout = default);

    public static Utf8ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.None,
        TimeSpan matchTimeout = default);

    public static Utf8MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.None,
        TimeSpan matchTimeout = default);

    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern,
        int count = int.MaxValue,
        RegexOptions options = RegexOptions.None,
        TimeSpan matchTimeout = default);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement,
        RegexOptions options = RegexOptions.None,
        TimeSpan matchTimeout = default);
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
* Options whose semantics depend on runtime compilation strategy rather than regex behavior are rejected explicitly. In particular, `RegexOptions.Compiled` is not accepted.
* Overloads without an explicit timeout use `Utf8Regex.DefaultMatchTimeout`, whose initial value must be `Regex.InfiniteMatchTimeout`.
* `Utf8ValueMatch` is the cheap, storable coordinate result. Rich group/capture inspection is provided by `MatchDetailed(...)` and by evaluator contexts.
* Successful matches and captures may be non-byte-aligned. In that case `IsByteAligned == false`, UTF-16 coordinates remain exact, and byte-oriented accessors throw `InvalidOperationException`.
* `Utf8GroupContext.CaptureCount` and `GetCapture(int index)` must match .NET capture history ordering.
* `Utf8ValueSplitEnumerator` must mirror `.NET 10 Regex.EnumerateSplits` exactly: `count` has the same stopping semantics, empty elements are preserved, and `RightToLeft` yields the same observable ordering. Match captures are not surfaced as separate split elements unless the baseline span API does so.
* `replacementPatternUtf8` overloads interpret bytes as UTF-8 encoded .NET replacement patterns, not as literal bytes.
* All `ref struct` match/group/capture/split views are ephemeral over the original input span and cannot outlive the calling frame or input buffer.
* `TryReplace` returns `Done` or `DestinationTooSmall` only for destination sizing. On `DestinationTooSmall`, `bytesWritten == 0` and callers must treat `destination` as unchanged. Invalid UTF-8, invalid replacement text, invalid regex construction, timeout, and non-representable byte results still throw.
* Evaluator-based replacement is currently the allocating replacement path; allocation control is provided only for fixed replacement-pattern overloads.

## 4.2 Intentional API extensions beyond baseline .NET Regex

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

* one runtime-compatible semantic front-end
* separate lowerings for candidate search and full execution
* internal UTF-16-exact semantics over UTF-8 input
* native `RightToLeft`; never emulate it by reversing the input
* `RegexOptions.NonBacktracking` remains a separate official mode, not a hidden default optimization
* `Utf8Regex` instances are immutable and thread-safe

Implementation details may evolve, but fast paths must be derived from the same semantic front-end rather than from separate pattern parsers.

## 6. Performance and benchmarking

Performance rules:

* production paths must stay UTF-8-native; do not route normal execution through `Encoding.UTF8.GetString(...)` + built-in `Regex`
* search and full matching are distinct concerns; search-first execution is expected
* prefer BCL vectorized primitives first, then `SearchValues<T>`, then manual intrinsics only when measured
* avoid whole-input string materialization except for explicit compatibility APIs such as `ReplaceToString`

Testing and benchmarks:

* differential behavior is measured against .NET 10 `Regex`
* tests must cover parser parity, matching, replacement, right-to-left behavior, timeout behavior, and non-byte-aligned edge cases
* benchmark methodology must use warm throughput, not cold-start `Dry` numbers, for parity decisions
* benchmark suite must report both baselines:
  * **Baseline A**: `Encoding.UTF8.GetString(input)` -> built-in `Regex` -> UTF-8 re-encoding where relevant
  * **Baseline B**: built-in `Regex` on a predecoded `string`
* benchmark catalog must include both native-target families and explicit fallback families

## 7. Acceptance criteria

The implementation is done when all of the following are true:

* For supported features on valid UTF-8 input, the engine matches .NET 10 `Regex` behavior.
* `ReplaceToString` remains the exact compatibility escape hatch.
* Byte-oriented APIs either return the correct UTF-8 result or fail explicitly when the exact UTF-16-semantic result is not representable as UTF-8.
* `RegexOptions.NonBacktracking` behaves like the official mode, including its documented restrictions and capture differences.
* Hot native paths are allocation-light, and the benchmark suite meets explicit per-scenario thresholds against both baselines.

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
