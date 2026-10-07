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

Current construction backstop: scalar-lowering arrays share an owned admission
scope across synchronous nested core/adaptor construction. The provisional
maximums are 1,048,576 live instruction slots and 64 MiB of covered construction
capacity. The byte model charges actual element strides, a conservative 32-byte
array header and payload rounded to 16 bytes; shared empty arrays cost zero.
Python parser AST nodes and class items use the same header/rounding model over
field-state extents, including fields assigned by initializers. The node and its
64-byte retaining descriptor are admitted before either object is acquired;
referenced payloads have independent ownership. The shared immutable empty AST
is borrowed. The owner keeps acquired nodes alive through nested construction,
including discarded parser intermediates, and releases tracking roots when its
outer scope closes. Failed children restore the parent's object checkpoint and
byte/depth loans; admitted creation work stays spent. This is conservative live
construction capacity, not a retained-cache policy or process RSS bound.
Python parser branch/sequence/class-item arrays, capture/open-group metadata,
their collection/arena descriptors, and old/new growth and final-copy overlap
share admission. Completed AST arrays and immutable metadata tables retain their
loans through the enclosing construction owner; parser scratch releases on every
exit. Immutable metadata lookup/enumeration spends the current construction
owner's work, rather than a closed original owner. Its generic enumerable views
and boxed enumerators admit descriptor capacity before acquisition.
Scalar class folding uses sparse 256-code-point bit pages with admitted directory,
page/key vectors and sorting, replacing unadmitted SortedSet trees. It preserves
the profile's ASCII/Unicode closures and BMP/astral run ordering. Page scans,
membership updates, string-key hash/comparison extents and admitted metadata
enumeration charge covered work. Cleanup uses indexed release without allocating
or resuming a metered iterator. Other parser strings and preparation iterator
objects remain uncovered; this is not a complete construction policy.
Python parser group-name slices and literal-escape text, and translator rendered
patterns, generated names, class/range tables, numeric projection arrays and
ASCII selector output share construction admission. Character vectors admit
growth overlap; materialized strings admit their terminator and a retaining
descriptor before copying and remain rooted through outer construction closure.
Hex/octal digits and integer/hex formatting use borrowed spans or fixed stack
scratch. This preserves bounded string-pattern interoperability; scalar matching
and byte coordinates are unchanged. Exception-message formatting, preparation
iterator objects, other construction consumers and retained-cache policies
remain uncovered. Construction loans do not establish RSS or total GC bounds.
The Python raw parser now stores alternation/sequence/group continuations in an
admitted array of explicit frames, including old/new growth overlap; it creates
no compiler-generated iterator for those grammar walkers. Root depth is checked
before acquiring the frame-stack descriptor, and every exit releases scratch
arrays and live depth without spending cleanup work. Production parser objects
and their retaining descriptors admit their complete field-state extent before
acquisition; a parser explicitly supplied by an internal caller remains borrowed.
This does not cover the remaining core/conversion/emission iterators or diagnostic
formatting, and does not establish a complete construction or retained policy.
Python translation, reference/lookbehind checks, capture/iteration eligibility,
scalar width analysis and exact-literal extraction also use admitted value-frame
stacks. Their former yield continuations and captured closures are removed;
borrowed AST/class lists are traversed by index. Width memoization retains
reference identity for backreference DAGs, scalar widths and exact clamping.
Construction-vector enumeration uses a value enumerator without acquiring or
boxing a heap descriptor. Shared value-frame stacks check root/child depth before
descriptor/array acquisition and unwind depth on every exit. Other preparation,
core/conversion/emission iterators, diagnostic text and complete retained/RSS
policies remain outside this partial construction backstop.
The shared core grammar parser also uses admitted value frames for alternation,
concatenation, groups, lookarounds and conditional branches. Its six former yield
continuations and heap parse context are removed. Root depth and frame storage
admission precede parser-state acquisition; child growth admits overlapping arrays.
Failure releases live depth/scratch and restores parent object loans without
refunding work. Continuation-array scratch releases before capture numbering;
the admitted buffer descriptor remains owned through construction closure. The
frame's referenced nodes, text and collections retain separate ownership: their
acquisition admission, other shared preparation/emission walkers and retained
facade descriptors remain unfinished.

Shared scalar VM analysis and emission also use admitted value frames and borrowed
child cursors. Their yield iterators and captured analysis predicates are removed.
Analysis admits root depth/storage before acquiring its fact-map descriptor; both
walkers admit child depth and array overlap, meter traversal and unwind scratch on
failure. Nested assertion program frames precede lowerer-context acquisition.
Alternation/repeat patch buffers admit their descriptors; conditional expression
guards emit directly without synthetic IR nodes/branch arrays. Referenced IR,
other lowerer descriptors, program objects and numeric names still require their
separate admission; this does not close all preparation or retained storage.

Shared find min/max/longest-literal postorder facts, grouped-literal spans,
selected branch/optional-loop projections, nested expansion and the facade
capture predicate also use admitted value frames. Parent cursors save before
child admission; branch scratch contexts are acquired after frame admission.
Temporary fact/result maps use admitted descriptors and arrays. Scratch arrays,
descriptors and live depth release on successful results as well as decline or
resource failure, without refunding work. The shared yield-based preparation
driver and its heap iterator continuations are removed; the two duplicate
literal walkers share one implementation. These frames borrow existing root
walkers, strings, builders and result collections, whose separate admission is
still incomplete. The byte-safe verifier compiler also uses admitted value
continuations and a temporary minimum-length memo. Root depth/storage precede
memo and builder acquisition; child frames admit before nested builders, and
all admitted scratch releases on success, decline or failure without refunding
work. Its former heap compiler context, request objects and six yield iterators
are removed. Its fixed-text/alternation cursors and leading-mask unions now
use admitted frames too, with saved child cursors and failed-child mask rollback.
Shared wrapper unwrapping and ASCII validation/copy, plus compiler suffix and
prefix comparisons, spend cumulative construction work. Successful/declined
scan scratch releases descriptors, arrays and depth without refunding work.
Deterministic prefix, literal-family, leading-mask, byte-safety and guard walks
also use admitted value frames and metered visits, comparisons and ASCII copies.
They preserve local partial-prefix stops and completed-sibling masks. Whitespace
guard preparation borrows the remaining children instead of manufacturing a
node/array. Ignore-case anchor validation has no input-sized stack scratch; it
admits only the successful folded byte array and transfers that loan to its
enclosing construction owner. Other program/text arrays, builders and preparation
scans retain separate
admission work. ASCII class projections defer non-ASCII subjects to
scalar execution; surrogate-half class membership does not determine loop
admission or minimum scalar counts.
Front-end quoted-run/quote checks, window phrases, finite byte widths and line
counts, trailing literals and deterministic prefix sets also use admitted value
frames. Branch loop/alternate discovery, feature census and NonBacktracking
grammar checks share the same construction owner for storage, depth and visits.
Saved cursors retain source-order choices and reverse-source diagnostic order;
failed children stop later work. Successful, declined and exceptional exits release
scratch without refunding work. Whitespace prefix analysis borrows a child cursor
and preserves the owned tree's parent links instead of manufacturing a parent.
Output builders, projection strings/spans and other preparation payloads remain
separate admission work.
Feature census reads grammar nodes directly. The former per-node atomicity,
capture-containment, backtracking and loop-membership analysis, its four sets,
retained semantic field and orphaned backtracking predicate are removed. Its
only production loop query was redundant with the admitted feature cursor's
own loop-kind census. Tests retain live grammar/features, capture metadata,
deep parent ownership and scalar operation/resource invariants instead of
pinning unused BCL runtime facts.
Required-literal family preparation also admits wrapper/collector value frames,
temporary family/candidate/member vectors, ordinal membership maps and arena
tracking. Single-character text admits its string and retaining descriptor;
existing node text remains borrowed. Sorting, membership hashing/equality,
selection and copying spend shared work. Only the selected reference array
transfers its payload loan; candidate arrays/maps and live depth unwind on every
exit. Acquired object/string descriptors stay admitted until their owner closes.
Decline/failure restores parent loans without refunding work. These search facts
are heuristics, not scalar matching widths or complete retained-cache policies.
Native eligibility and simple-plan case/tail checks admit root/ancestor value
frames and every resume; opposite case checks share one traversal. Literal
validation charges each decoded pattern scalar before the bounded decode.
Pure boolean scans restore all temporary loans and descriptors on return.
Flat NonBacktracking admission finishes its scan before scalar lowering, shares
the same owner and rolls back a declined proof's lowered payload as well as
scan scratch; only an accepted program transfers. The unused lookaround
eligibility helper is removed rather than given a second implementation.
These checks gate optimizations, not supported scalar grammar or general policies.
Native literal-family preparation admits ancestor frames, its local heuristic
budget, scratch branch/product/reference arrays, copied single-character and
concatenated/folded text, builders and retaining descriptors. Final reference or
ASCII byte arrays transfer their loans; scratch and failed speculation recover
under the shared owner, without refunding work. Folded set-to-literal conversion
requires a positive whole class; negation/subtraction cannot use exclusion ranges
as a literal proof. ASCII-whitespace wire subtraction uses admitted segment text
and vectors rather than the legacy predicate's raw buffers. Node/character/query/
product/copy work is metered before execution. Pattern text stays interoperability
input, not a UTF-16 subject unit. Other native producers and general retained
policies remain incomplete.
Native quoted-relation LF counting uses admitted ancestor continuations without
the former stack/map or copied single-character strings. Its LF bound is distinct
from front-end CR-or-LF line safety. Shared construction class queries retain
borrowed plain wires, admit complete subtraction segments once, and meter queries
before scanning. ASCII structural literal/alternate bytes and suffix-part arrays
have admitted output loans; failed suffix or parent-family speculation recovers
all child loans. Quote/separator proofs unwind temporary loans, retain wrapper
depth and avoid quote-string copies. Prefix comparisons spend work before byte
scans. Existing local grammar, branch order, zero-minimum separators and checked
line-count overflow are retained.
Native literal/lookahead preparation admits final UTF-8 bytes and alternative
reference arrays under the same speculative owner as its child text. Encoding
validates complete scalars before acquisition and meters both passes; it cannot
turn an impossible surrogate atom into U+FFFD. Lookahead concatenation uses the
admitted text buffer. Flat captured/alternative patterns use borrowed spans,
without Split or substring intermediates, and scan alternatives in linear work.
Literal/alternative wrapper ancestors remain in live-depth admission; late
boundary, empty, unsupported or folded-Unicode decline restores all child loans
without refunding work. Successful scratch arrays unwind and output arrays/text
retain their loans.
Search-fact initializers are value carriers; their immutable reference storage
and retaining descriptors are admitted before acquisition using the complete
field-state extent. Default/shared empty facts borrow one static carrier. Byte,
alternative, set and window payloads remain independently owned by their producers;
this storage admission does not admit an unconverted payload retroactively.
Simple literal expansion admits reference vectors, Cartesian byte copies and
final arrays, retaining original order and expansion limits. Positive ASCII and
folded class proofs use scalar value masks rather than raw byte copies/HashSet
scratch. Prefix replacements admit old/new overlap and release superseded arrays;
failed children cannot mutate their parent's vector. The native singleton route
reuses its final byte payload. Its admitted ASCII string serves literal-pattern
diagnostics/classification only; no subject is decoded and bytes are not re-encoded.
The enclosing speculative attempt recovers child arrays/text/fact storage on late
decline without refunding work. Other simple-plan branch/token/search extraction,
structural plan containers and complete retained/shared policies remain open.
Find preparation admits trailing-anchor/leading-literal ancestor frames and
longest-literal postorder frames/maps, with cumulative visits and literal-run
selection work. Literal builders and copied character/run strings share byte
admission with their retaining descriptors. Existing pattern strings are
borrowed. Returned text transfers its loans; pure anchor results and declined
literal scans restore scratch/descriptors. Frame/map arrays and live depth
unwind on every exit; acquired successful text/object descriptors remain owned
until construction closes. The unused leading-anchor fact/producer and redundant
cached tree root are removed. Search widths bound consumed scalar UTF-8 bytes:
literal pairs decode once per scalar, plain ranges exclude surrogate code points,
and complex classes/case fibers retain sound conservative bounds. Conditions
measure their yes/no branches with an implicit empty else; references have no
independent finite maximum. Arithmetic saturates the lower bound at int.MaxValue
and makes an overflowing upper bound unknown. A fixed distance requires equal
known byte bounds for every preceding node and checked addition. Variable prefixes
end the proof instead of contributing their minimum. Fixed-literal preparation
admits its width map, ancestor frames and copied character text; the old raw stack
and result holder are removed. ASCII-set offsets include complete encoded literal
prefixes; folded variable-width sets cannot supply an ASCII-only fixed certificate.
Finite ordered-window gaps reuse these scalar byte bounds, including folded
literals, Unicode classes and dot. Combined gap arithmetic declines on overflow
instead of wrapping into a false maximum. Line-break counts are an independent
optional proof; plain class-wire membership is metered before scanning, and class
subtraction declines that proof without invoking its legacy allocating predicate.
The duplicate window-width formulas and unused line-safety frame state are removed.
Alternate/set results and the facts container still require complete admission;
these facts do not prove complete construction/retained bounds or that other
native width producers and remaining BCL routes meet the scalar contract.
Byte-safe execution-tree and program lowering also admit value frames, visits,
temporary reference maps, copied child arrays, node/tree/program objects and
their retaining descriptors. Program vectors charge aggregate instruction slots
as well as bytes, including growth/final-copy overlap. Fixed-capture layout
preparation admits its eligibility scan, frames, temporary index map and compact
capture array; the retained hash table is removed. Completed results transfer
payload loans, while decline or failure restores parent loans/depth without
refunding work. Other preparation producers and retained-cache policies remain
uncovered; these charges do not establish complete construction bounds.
Python scalar conversion also uses admitted value frames, deleting its captured
yield continuation. Its arena/map descriptors, direct structural IR nodes and
retaining descriptors admit before acquisition; node charges include the base
Kind and initializer fields. Numeric capture-name strings admit their buffers,
copy and retaining descriptor; the immutable whole-match name is borrowed.
Failed or declined conversion restores parent loans/depth without refunding
work, and releases frame-array scratch before lowering. Core fragment-parser
and leaf-conversion IR objects, other core producers and lazy retained preparation
remain separate admission work; these charges do not establish complete policies.
Adapter UTF-8 pattern entries borrow bytes until the shared constructor scope.
Validation/counting precedes decoded-text admission and acquisition. The string
charge is a 32-byte header plus its UTF-16 payload and terminator rounded to 16
bytes; shared empty text costs zero. That retained loan remains live through
parsing, nested core compilation and lowering. Caller-owned string patterns are
borrowed. Profile-specific UTF-8 errors and policy-validation ordering remain.
Pattern text is parser interoperability; matching consumes Unicode scalars.
Covered construction work shares a cumulative counter: a byte per validation,
counting or decode pass; an element per admitted array initialization/copy; an
iteration per covered core/PythonRe parser and owned tree-conversion loop; and
one unit per map probe/rehash, sort comparison/swap or preparation resume.
Core parser bulk delimiter scans and literal copies admit conservative extents.
The owned in-place heap sort admits each comparison/swap and propagates resource
failures directly. Core/PythonRe parsers and preparation walkers share live
explicit-frame depth; denied pushes restore their parent and release admitted
scratch, and cleanup spends no work. Python alternation, sequence and group parsing suspends child
work on that stack; conditional branches reuse the same sequence parser.
Python rendering, reference/lookbehind validation, scalar-width analysis and
capture/iteration/literal fast-path checks also suspend child traversal on that
stack. Width memoization uses reference identity and admitted temporary maps;
lookbehind validation shares its memo across the translation. Literal extraction
flattens once into its consumer's text rather than building recursive intermediate
strings. Translator loops and prefix comparisons admit work; group-name ordering
and range merging use the admitted heap sort.
The core facade's capture census uses the same explicit admitted traversal,
including capture-free nested trees; it no longer recurses on the CLR stack.
Python scalar widths use UInt128 intermediates and the CPython 3.13.2 2^64
getwidth clamp/sentinel. Fixedness does not collapse at int.MaxValue; fixed
lookbehind widths above the 32-bit code limit report "looks too much behind".
Provisional limits are 67,108,864 work units and 65,536 covered live frames.
Exhaustion reports ConstructionWorkSteps/ConstructionDepth with checked requested
totals. Failed children roll back capacity/depth but never refund admitted work;
neither nested construction nor a declined speculative plan can reset it.
These are partial counters: other parsing/rendering/optimization/preparation
work, other descriptors and object/string capacity remain uncovered. Translator
StringBuilder/string/list/dictionary storage and iterator objects are not yet
admitted. These counters do not establish complete
work/depth controls or safe defaults.
It covers core/PythonRe tree-to-scalar conversion maps/frames, child/literal
arrays, decoded class/subtraction payloads, instruction vectors, analysis/capture
maps, lowering/preparation frames, auxiliary vectors, nullable-graph discovery,
epsilon-order preparation and newly acquired program metadata arrays. Temporary
IR arrays release after lowering; class tables and other escaping arrays retain
their byte loans with the completed program. Old/new growth and final-copy overlap, completed
assertion programs and source/lifted NonBacktracking graphs remain charged
while live. Admission precedes acquisition. Exhaustion throws
`Utf8ResourceLimitExceededException` with resource kind, maximum and requested
capacity, and never selects a BCL engine. A failed construction restores its
parent's admission state. This partial buffer backstop is not the complete
common construction policy. It excludes other pattern conversion, core parser/IR
node objects, other created strings, independently borrowed payloads, collection/iterator descriptors, independent
PCRE2 programs and lazy retained storage. Work/depth, public construction
controls and qualified safe defaults remain open. The byte charge bounds the
defined live covered capacity, not process RSS or cumulative GC allocations.

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
  Zero-width repetition preserves its predicate and final capture without work
  proportional to the numeric bound. Nullable loop progress is part of the owned
  graph; empty completions exit without epsilon cycles. Parsed constructs outside
  this mode's grammar fail before planning, with no BCL validation engine.
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
