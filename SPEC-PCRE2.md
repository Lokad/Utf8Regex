## Side-by-side PCRE2 support in `Lokad.Utf8Regex`

### 1. Top-level decision

Keep the existing `Utf8Regex` type **unchanged** and permanently bound to **.NET regex semantics**. Add a sibling type, `Utf8Pcre2Regex`, for **PCRE2 standard matcher semantics** on UTF-8 input. Do **not** add a flavor flag to `Utf8Regex`, and do **not** try to unify `.NET` and PCRE2 behind one option bag. This is required because the semantic seams are real: `.NET` exposes full capture history through `Group.Captures`, whereas PCRE2’s standard matcher returns only the last portion matched by a repeated capture; PCRE2 also supports duplicate names and name-table-based lookup rules that do not fit the existing `.NET`-shaped by-name APIs. ([Microsoft Learn][1])

`Utf8Pcre2Regex` targets the **PCRE2 standard matcher** only. The alternative DFA matcher is explicitly **out of scope** for this API family because PCRE2 documents it as a different, non-Perl-compatible algorithm with different behavior. ([PCRE][2])

`Utf8Pcre2Regex` is a **strictly managed .NET profile layer**. It must not bind to upstream native PCRE2 through P/Invoke, `NativeLibrary`, external executables, RID-specific native assets, or any other non-managed dependency. The only acceptable non-BCL implementation dependency is the existing `Lokad.Utf8Regex` library itself.

### 2. Core design rules

Semantics are chosen by **type**, not by a runtime mode flag. `Utf8Regex` remains the `.NET` profile. `Utf8Pcre2Regex` is the PCRE2 profile. Shared implementation is allowed below the public surface, but semantic front-ends, option models, detailed result models, and replacement grammars remain flavor-specific. ([Microsoft Learn][1])

For PCRE2, the detailed public result model must be **numeric-slot-first**, with explicit access to the **name table**. Singular by-name lookup may exist only as a convenience and must be clearly documented as “first set group for that name,” not as the primary truth. This follows PCRE2’s own behavior: with duplicate names, `pcre2_substring_number_from_name()` can report “not unique,” while the convenience by-name extractors scan the groups for that name and return the first one that is set. ([PCRE][3])

PCRE2 match values must be modeled using **reported start and end offsets**, not just `Index + Length`. This is necessary because PCRE2 documents that `\K` inside positive lookahead can yield a successful match whose reported start offset is greater than its end offset. ([PCRE][3])

Partial matching must be exposed through a **dedicated tri-state probing API**, not smuggled into normal “successful match” APIs. PCRE2 documents partial matching as a distinct outcome (`PCRE2_ERROR_PARTIAL`), and when it occurs only substring 0 is defined; the rest of the ovector is undefined. ([PCRE][4])

PCRE2 replacement must use **PCRE2 substitution semantics**, not `.NET` substitution semantics. By default, only `$...` forms are special in PCRE2 replacement strings; backslash processing becomes special only when extended substitution is enabled. Supported default forms include `$$`, `$n`, `${n}`, `$0`, `$&`, ``$````, `$'`, `$_`, `$+`, `$*MARK`, `${*MARK}`, and `$<name>`. ([PCRE][3])

Because this library is text-centric and UTF-8-native, the default PCRE2 profile must **forbid `\C`** and must **not** opt into the legacy `\K`-inside-lookaround compatibility mode. Both remain available as explicit opt-ins. This is principled because PCRE2 documents that `\C` matches a single code unit even in UTF mode and can split a multi-code-unit character, and it documents that lookaround `\K` is forbidden by default and only re-enabled by an extra compile option. ([PCRE][5])

### 3. Proposed public surface

```csharp
namespace Lokad.Utf8Regex;

using System.Buffers;

[Flags]
public enum Pcre2CompileOptions
{
    None            = 0,
    Caseless        = 1 << 0,
    Multiline       = 1 << 1,
    DotAll          = 1 << 2,
    Extended        = 1 << 3,
    ExtendedMore    = 1 << 4,
    Anchored        = 1 << 5,
    EndAnchored     = 1 << 6,
    DollarEndOnly   = 1 << 7,
    Ungreedy        = 1 << 8,
    NoAutoCapture   = 1 << 9,
    Ucp             = 1 << 10,
    FirstLine       = 1 << 11
}

[Flags]
public enum Pcre2MatchOptions
{
    None            = 0,
    Anchored        = 1 << 0,
    EndAnchored     = 1 << 1,
    NotBol          = 1 << 2,
    NotEol          = 1 << 3,
    NotEmpty        = 1 << 4,
    NotEmptyAtStart = 1 << 5
}

[Flags]
public enum Pcre2SubstitutionOptions
{
    None            = 0,
    Extended        = 1 << 0,
    UnsetEmpty      = 1 << 1,
    UnknownUnset    = 1 << 2
}

public enum Pcre2PartialMode
{
    None = 0,
    Soft = 1,
    Hard = 2
}

public enum Pcre2NewlineConvention
{
    Default = 0,
    Cr,
    Lf,
    Crlf,
    Any,
    AnyCrlf,
    Nul
}

public enum Pcre2BsrConvention
{
    Default = 0,
    AnyCrlf,
    Unicode
}

public enum Pcre2BackslashCPolicy
{
    Forbid = 0,
    Allow = 1
}

public readonly struct Utf8Pcre2CompileSettings
{
    public Pcre2NewlineConvention Newline { get; init; }
    public Pcre2BsrConvention Bsr { get; init; }

    public bool AllowDuplicateNames { get; init; }

    // Default: Forbid
    public Pcre2BackslashCPolicy BackslashC { get; init; }

    // Default: false
    public bool AllowLookaroundBackslashK { get; init; }
}

public readonly struct Utf8Pcre2ExecutionLimits
{
    // 0 means "use engine default / unlimited for this library profile".
    public uint MatchLimit { get; init; }
    public uint DepthLimit { get; init; }
    public ulong HeapLimitInBytes { get; init; }
}

public sealed class Utf8Pcre2Regex
{
    public Utf8Pcre2Regex(string pattern);
    public Utf8Pcre2Regex(string pattern, Pcre2CompileOptions options);
    public Utf8Pcre2Regex(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default);

    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8);
    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options);
    public Utf8Pcre2Regex(
        ReadOnlySpan<byte> patternUtf8,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default);

    public static TimeSpan DefaultMatchTimeout { get; set; }

    public string Pattern { get; }
    public Pcre2CompileOptions Options { get; }
    public Utf8Pcre2CompileSettings CompileSettings { get; }
    public Utf8Pcre2ExecutionLimits DefaultExecutionLimits { get; }
    public TimeSpan MatchTimeout { get; }

    public bool IsMatch(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public int Count(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public Utf8Pcre2ValueMatch Match(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public Utf8Pcre2MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    // Partial matching is a separate probing API.
    public Utf8Pcre2ProbeResult Probe(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public byte[] Replace(
        ReadOnlySpan<byte> input,
        string replacement,
        int startOffsetInBytes = 0,
        Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public byte[] Replace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        int startOffsetInBytes = 0,
        Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Pcre2MatchEvaluator<TState> evaluator,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public string ReplaceToString(
        ReadOnlySpan<byte> input,
        string replacement,
        int startOffsetInBytes = 0,
        Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Pcre2Utf16MatchEvaluator<TState> evaluator,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten,
        int startOffsetInBytes = 0,
        Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    // Name-table-first metadata
    public int NameEntryCount { get; }

    public int CopyNameEntries(
        Span<Pcre2NameEntry> destination,
        out bool isMore);

    public int CopyNumbersForName(
        string name,
        Span<int> destination,
        out bool isMore);

    // Convenience only: returns the first set group for the given name.
    public bool TryGetFirstSetGroup(
        ReadOnlySpan<byte> input,
        string name,
        out Utf8Pcre2GroupContext group,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public int MatchMany(
        ReadOnlySpan<byte> input,
        Span<Utf8Pcre2MatchData> destination,
        out bool isMore,
        int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None);

    public Utf8Pcre2Analysis Analyze();

    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern,
        Pcre2CompileOptions options = Pcre2CompileOptions.None,
        Utf8Pcre2CompileSettings compileSettings = default,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default,
        int startOffsetInBytes = 0);

    public static Utf8Pcre2ValueMatch Match(
        ReadOnlySpan<byte> input,
        string pattern,
        Pcre2CompileOptions options = Pcre2CompileOptions.None,
        Utf8Pcre2CompileSettings compileSettings = default,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default,
        int startOffsetInBytes = 0);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement,
        Pcre2CompileOptions options = Pcre2CompileOptions.None,
        Utf8Pcre2CompileSettings compileSettings = default,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default,
        int startOffsetInBytes = 0,
        Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None);
}

public readonly struct Pcre2NameEntry
{
    public string Name { get; }
    public int Number { get; }
}

public readonly ref struct Utf8Pcre2ValueMatch
{
    public bool Success { get; }

    // Raw PCRE2-reported offsets, exclusive end.
    public int StartOffsetInBytes { get; }
    public int EndOffsetInBytes { get; }

    // True when StartOffsetInBytes <= EndOffsetInBytes.
    public bool HasContiguousByteRange { get; }

    // True when the contiguous byte range is well-formed UTF-8.
    public bool IsUtf8SliceWellFormed { get; }

    // True when exact UTF-16 coordinates can be projected.
    public bool HasUtf16Projection { get; }

    public int StartOffsetInUtf16 { get; }   // throws if !HasUtf16Projection
    public int EndOffsetInUtf16 { get; }     // throws if !HasUtf16Projection

    public ReadOnlySpan<byte> GetValueBytes(); // throws if !HasContiguousByteRange
    public string GetValueString();            // throws if !HasUtf16Projection
}

public readonly struct Utf8Pcre2MatchData
{
    public bool Success { get; }
    public int StartOffsetInBytes { get; }
    public int EndOffsetInBytes { get; }
    public bool HasContiguousByteRange { get; }
    public bool IsUtf8SliceWellFormed { get; }
    public bool HasUtf16Projection { get; }
    public int StartOffsetInUtf16 { get; }
    public int EndOffsetInUtf16 { get; }
}

public readonly ref struct Utf8Pcre2MatchContext
{
    public bool Success { get; }
    public Utf8Pcre2ValueMatch Value { get; }

    // Highest capture slot count known to the compiled pattern.
    public int CaptureSlotCount { get; }

    public Utf8Pcre2GroupContext GetGroup(int number);
    public bool TryGetGroup(int number, out Utf8Pcre2GroupContext group);

    public int NameEntryCount { get; }
    public int CopyNameEntries(Span<Pcre2NameEntry> destination, out bool isMore);
    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore);
    public bool TryGetFirstSetGroup(string name, out Utf8Pcre2GroupContext group);

    // Last encountered named backtracking verb on the matching path, if any.
    public string? Mark { get; }

    public string GetValueString();
}

public readonly ref struct Utf8Pcre2GroupContext
{
    public bool Success { get; }

    public int Number { get; }

    public int StartOffsetInBytes { get; }
    public int EndOffsetInBytes { get; }
    public bool HasContiguousByteRange { get; }
    public bool IsUtf8SliceWellFormed { get; }
    public bool HasUtf16Projection { get; }

    public int StartOffsetInUtf16 { get; }   // throws if !HasUtf16Projection
    public int EndOffsetInUtf16 { get; }     // throws if !HasUtf16Projection

    public ReadOnlySpan<byte> GetValueBytes(); // throws if !HasContiguousByteRange
    public string GetValueString();            // throws if !HasUtf16Projection
}

public enum Utf8Pcre2ProbeKind
{
    NoMatch = 0,
    FullMatch = 1,
    PartialMatch = 2
}

public readonly ref struct Utf8Pcre2ProbeResult
{
    public Utf8Pcre2ProbeKind Kind { get; }

    // For FullMatch or PartialMatch, this is substring 0.
    public Utf8Pcre2ValueMatch Value { get; }

    // May be set for success, partial, or no-match.
    public string? Mark { get; }

    public Utf8Pcre2MatchContext GetMatch();          // valid only when Kind == FullMatch
    public Utf8Pcre2PartialMatchContext GetPartial(); // valid only when Kind == PartialMatch
}

public readonly ref struct Utf8Pcre2PartialMatchContext
{
    // Substring 0 only. Other capture groups are not exposed.
    public Utf8Pcre2ValueMatch Value { get; }
    public string? Mark { get; }

    public ReadOnlySpan<byte> GetValueBytes(); // throws if !Value.HasContiguousByteRange
    public string GetValueString();            // throws if !Value.HasUtf16Projection
}

public delegate void Pcre2MatchEvaluator<TState>(
    in Utf8Pcre2MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

public delegate string Pcre2Utf16MatchEvaluator<TState>(
    in Utf8Pcre2MatchContext match,
    ref TState state);

public readonly struct Utf8Pcre2Analysis
{
    public bool IsFullyNative { get; }
    public bool IsExactLiteral { get; }
    public int MinRequiredLengthInBytes { get; }

    public bool HasDuplicateNames { get; }
    public bool UsesBranchReset { get; }
    public bool UsesBacktrackingControlVerbs { get; }
    public bool UsesRecursion { get; }

    // True when \C is permitted or otherwise the pattern may split a UTF-8 scalar.
    public bool MayProduceNonUtf8Slices { get; }

    // True when the pattern may report StartOffset > EndOffset.
    public bool MayReportNonMonotoneMatchOffsets { get; }

    public int CopyRequiredLiterals(
        Span<Utf8RequiredLiteral> destination,
        out bool isMore);
}
```

### 4. Semantic contract

`Utf8Regex` stays exactly as it is today. No existing constructor, option, result type, replacement grammar, or exception contract changes. The PCRE2 extension is additive only.

`Utf8Pcre2Regex` uses **PCRE2 standard matcher semantics in UTF-8 mode**. The DFA matcher is deferred. `RightToLeft` does not exist on the PCRE2 side and must not be emulated. All public start positions on the PCRE2 side are expressed as **byte offsets into the UTF-8 input**, not UTF-16 code-unit offsets. ([PCRE][2])

The PCRE2 side owns its own compile options, match options, substitution options, compile settings, and execution limits. Do not reuse `RegexOptions` and do not pretend `TimeSpan` is the only resource-control model. `TimeSpan MatchTimeout` is allowed only as an additional managed guard for consistency with the library’s broader shape; the primary PCRE2-facing resource knobs are `MatchLimit`, `DepthLimit`, and `HeapLimitInBytes`. ([PCRE][6])

All byte-start-offset APIs validate that `startOffsetInBytes` is in the inclusive range `[0, input.Length]`. If the offset is out of range, they throw `ArgumentOutOfRangeException`. If the offset falls in the middle of an ill-formed or incomplete UTF-8 sequence and the operation requires UTF-aware semantics, the operation throws the same input-validation exception family used elsewhere by the library for invalid UTF-8 input.

The default compile settings for `Utf8Pcre2Regex` are:

* `AllowDuplicateNames = false`
* `BackslashC = Forbid`
* `AllowLookaroundBackslashK = false`
* `Newline = Default`
* `Bsr = Default`

Those defaults are deliberate. PCRE2 allows duplicate names only when requested, except for the special same-number branch-reset case; `\C` is dangerous for UTF text because it matches a single code unit and can split a UTF character; and lookaround `\K` is forbidden by default in modern PCRE2 and only re-enabled by an extra compatibility option. ([PCRE][7])

The detailed PCRE2 group model exposes **only the final value of each capture slot**. There is no `.NET`-style `CaptureCount` / `GetCapture(i)` surface on PCRE2 detailed results. This is intentional and must not be approximated, because `.NET` exposes full capture history for repeated captures while PCRE2 standard matching returns only the last portion matched by a repeated group. ([Microsoft Learn][1])

By-name metadata is **name-table-first**. `CopyNameEntries(...)` and `CopyNumbersForName(...)` are the primary APIs. `TryGetFirstSetGroup(name, ...)` is allowed only as a convenience and must follow PCRE2’s by-name convenience behavior: when duplicate names exist, it returns the first set group for that name. ([PCRE][3])

Normal matching APIs (`IsMatch`, `Count`, `Match`, `MatchDetailed`, `EnumerateMatches`, `Replace`, `MatchMany`) do **not** expose partial-match behavior. Partial matching is available only through `Probe(...)`. A partial probe result exposes only substring 0 plus `Mark`; it does not expose numbered or named capture groups. This matches PCRE2’s documented partial-match contract, where partial is a distinct outcome and the rest of the ovector is undefined. ([PCRE][4])

`Utf8Pcre2ValueMatch` and `Utf8Pcre2GroupContext` use raw **reported start and end offsets** in bytes, with an exclusive end. `HasContiguousByteRange` is `true` only when `start <= end`. This is required because of `\K` semantics in positive lookahead. When `AllowLookaroundBackslashK` is `false`, the engine rejects such patterns or pattern features at compile time. When it is `true`, `Match` and `MatchDetailed` may return non-monotone offsets; `GetValueBytes()` then throws because there is no contiguous byte slice. `Utf8Pcre2ValueMatch` is therefore a `ref struct` carrier that remains tied to the input span for byte and string extraction. ([PCRE][3])

`HasUtf16Projection` is `true` only when the reported match/group can be projected exactly to UTF-16 coordinates and to a `string`. This is usually true in the default profile. It can become `false` only when the caller explicitly allows `\C` and the match/group splits a UTF-8 scalar. PCRE2 documents that `\C` matches a single code unit in UTF mode and can break up multi-unit characters. ([PCRE][5])

PCRE2 replacement uses PCRE2 substitution syntax. By default, only dollar-based substitutions are recognized. Backslash escapes in replacement text are processed only when `Pcre2SubstitutionOptions.Extended` is set. The initial supported default substitution forms are:

* `$$`
* `$n`
* `${n}`
* `$0`
* `$&`
* ``$````
* `$'`
* `$_`
* `$+`
* `$*MARK`
* `${*MARK}`
* `$<name>`

This is a semantic requirement; do not use `.NET` replacement parsing on the PCRE2 side. ([PCRE][3])

`Replace` and `TryReplace` operate left-to-right only. If the pattern was compiled with `AllowLookaroundBackslashK = true` and a successful match would end before it starts, replacement APIs throw `NotSupportedException`. This mirrors the fact that PCRE2 substitution explicitly does not support those successful `\K` lookaround cases. The same restriction applies to APIs that inherently require forward global progress over successive matches: `Count`, `EnumerateMatches`, and `MatchMany` also throw `NotSupportedException` when the next successful match in the requested iteration would report `EndOffsetInBytes < StartOffsetInBytes`. Single-match APIs (`IsMatch`, `Match`, `MatchDetailed`, `Probe`) remain supported. ([PCRE][3])

For global left-to-right operations (`Count`, `EnumerateMatches`, `MatchMany`, and all replacement APIs), the iteration contract is explicit:

* iteration begins at `startOffsetInBytes`
* after a successful match with `EndOffsetInBytes > StartOffsetInBytes`, the next search begins at `EndOffsetInBytes`
* after a successful empty match with `EndOffsetInBytes == StartOffsetInBytes`, the next search begins at the next UTF-8 scalar boundary after `EndOffsetInBytes`
* if there is no next scalar boundary, iteration stops
* `Pcre2MatchOptions.NotEmpty` and `Pcre2MatchOptions.NotEmptyAtStart` are applied on each restart exactly as ordinary per-match options
* partial-match behavior is never consulted during these loops because partial is available only via `Probe(...)`

This rule is required so the public global APIs have deterministic, loop-safe behavior independent of PCRE2’s one-shot matcher entrypoint.

The `Mark` property on `Utf8Pcre2MatchContext` and `Utf8Pcre2ProbeResult` returns the last encountered named backtracking verb on the relevant path, if any. It is available after success, partial, or no-match, matching PCRE2’s documented behavior. ([PCRE][3])

Failure contracts must be explicit:

* invalid pattern text, invalid compile-option combinations, unsupported constructs under the selected library profile, or invalid compile settings cause construction to throw a dedicated PCRE2 compile exception
* match-time resource exhaustion (`MatchLimit`, `DepthLimit`, `HeapLimitInBytes`) throws a dedicated PCRE2 match exception that preserves the modeled PCRE2 failure kind
* managed timeout expiration throws the same timeout exception family already used by the library’s `.NET`-semantic surface
* invalid replacement syntax or replacement-time substitution errors throw a dedicated PCRE2 substitution exception
* `Probe(...)` reports only `NoMatch`, `FullMatch`, or `PartialMatch`; resource exhaustion and other operational failures are not folded into `Utf8Pcre2ProbeKind` and instead throw

Supported-profile boundary for completion is also explicit:

* public APIs must either execute successfully, throw a documented dedicated PCRE2 exception, or throw `NotSupportedException` for an intentional `SPEC-PCRE2` rejection
* raw public `NotImplementedException` is acceptable only for genuine implementation backlog that has not yet been classified into supported vs rejected behavior
* non-monotone iterative `\K` cases are rejected for left-to-right global APIs (`Count`, `EnumerateMatches`, `MatchMany`, all replacement modes), even when single-match APIs remain supported
* `Probe(...)` is intentionally a smaller surface than `Match(...)`; patterns outside the curated partial-probe profile must fail explicitly rather than silently approximating managed `.NET` behavior
* replacement APIs are intentionally narrower than single-match APIs for special native patterns; unsupported replacement modes for otherwise-compilable patterns must fail explicitly rather than falling through unpredictably
* the executable corpus plus `Utf8Pcre2CompletionLedgerTests` are the closure mechanism for this boundary; newly supported or newly rejected cases must be reflected there so the ledger remains a deliberate snapshot instead of drifting implicitly
* performance work must prefer reuse of existing managed `Lokad.Utf8Regex` infrastructure where semantics allow it; do not introduce a second regex engine architecture under the PCRE2 profile

### 5. Deliberate non-goals for the first PCRE2 release

Do not add PCRE2 callouts yet.

Do not add the DFA matcher yet.

Do not attempt a fake unification of `.NET` and PCRE2 result types beyond shared low-level internals such as UTF-8 validation, byte/UTF-16 coordinate translation, vectorized literal search, timeout polling, and `Utf8ReplacementWriter`.

Do not add `.NET`-shaped group-name APIs such as `GroupNumberFromName(string)` on the PCRE2 type. The name-table APIs above are the required public shape.

### 6. Implementation isolation rules

PCRE2 support must be implemented as an isolated feature slice so that it can be removed later without destabilizing the `.NET`-semantic core.

Required rules:

* all PCRE2 implementation files live under `src/Lokad.Utf8Regex.Pcre2/`
* every PCRE2-specific public type, internal type, namespace, folder, and test fixture name must contain `Pcre2`
* PCRE2-specific helpers must not be added anonymously to the core project; if a helper exists only for PCRE2, it belongs in the PCRE2 project
* any unavoidable cross-project hooks in the core library must be minimal, explicitly documented, and marked with a stable comment tag such as `PCRE2-INTEGRATION-POINT`
* flavor-agnostic sharing is allowed only for genuinely shared low-level utilities; do not introduce generic “regex flavor” abstractions unless both sides truly need them
* the PCRE2 profile must remain fully managed; no P/Invoke, no native library loading, no external PCRE2 binary, and no RID-specific native packaging
* `Lokad.Utf8Regex` is the sole acceptable implementation dependency beyond the .NET BCL; any performance work should first look for reuse of existing managed search, validation, span, projection, and replacement infrastructure

Preferred structure:

* `src/Lokad.Utf8Regex.Pcre2/` for implementation
* `tests/...Pcre2...` for PCRE2-specific tests

Packaging and loading policy must also preserve removability:

* there is no native PCRE2 loading, RID packaging, or “library unavailable” behavior in this project profile
* the base `Lokad.Utf8Regex` project must not require PCRE2 to load or function


[1]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.group.captures?view=net-10.0 "Group.Captures Property (System.Text.RegularExpressions) | Microsoft Learn"
[2]: https://pcre.org/current/doc/html/pcre2matching.html?utm_source=chatgpt.com "pcre2matching specification - Perl Compatible Regular Expressions"
[3]: https://pcre.org/current/doc/html/pcre2api.html "pcre2api specification"
[4]: https://pcre.org/current/doc/html/pcre2partial.html "pcre2partial specification"
[5]: https://www.pcre.org/current/doc/html/pcre2unicode.html "pcre2unicode specification"
[6]: https://www.pcre.org/current/doc/html/pcre2_match.html?utm_source=chatgpt.com "pcre2_match specification"
[7]: https://www.pcre.org/current/doc/html/pcre2pattern.html "pcre2pattern specification"
