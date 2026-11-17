# Migration guide

## 0.5.0: .NET 10 and .NET 11

Upgrade Core and every selected adapter together: adapters require the exact same
package version. Version 0.5 supplies `net10.0` and `net11.0` assets; published
0.4.0 packages target .NET 10 only. The stable SDK is pinned in
[global.json](../global.json). Benchmarks use .NET 11 as the primary baseline,
while historical rows retain their actual producer/runtime identities.

### Subjects are caller-valid text

All four profiles require already-valid, unchanged UTF-8 subjects. Remove
`inputValidation` constructor/factory arguments and `InputValidation` property
reads; `Utf8InputValidation` is removed. There is no policy replacement or public
validation token. Regex operations no longer check entire subjects or PCRE2's
former native UTF window.

Validate untrusted bytes where they become text, for example with
`System.Text.Unicode.Utf8.IsValid`, unless the host representation already guarantees
validity. Keep borrowed storage alive and unchanged during use. Malformed subjects
have unspecified matching results and no rejection/error-offset promise. Managed
bounds, scalar semantics, byte coordinates, synchronous cancellation/deadlines and
resource admission remain. Pattern, template, name and assembled-output checks
retain their separate strict rules. Numerical work and capacity needs can decrease
when validation, unit totals and obsolete preparation disappear.

PCRE2's caller-valid input contract deliberately differs from native default UTF
checking; equivalent native benchmarks use `PCRE2_NO_UTF_CHECK`. Explicit byte
atoms and special ranges retain their documented slice-decoding requirements.
See the [PCRE2 input contract](SPEC-PCRE2.md#subject-input-contract).

### Native patterns, replacements and metadata

Core, PythonRe and PCRE2 accept strict UTF-8 pattern/template spans directly:

```csharp
var emoji = Utf8Regex.Compile("(?<value>😀)+"u8);
byte[] output = emoji.Replace("a😀😀b"u8, "[${value}]"u8); // a[😀]b
```

Compilation owns retained source bytes; caller reuse cannot mutate the program.
`Pattern` explicitly projects text. Byte-pattern syntax diagnostics use byte
positions; text-pattern diagnostics retain their documented coordinate units.
A bare `null` at some overloaded Compile call sites becomes ambiguous: use a typed
string or named `pattern:` for text-null calls.

`CaptureGroupCount` excludes group zero. `TryCopyPatternUtf8` obtains required
capacity or copies source into caller-owned storage. Native name lookup is
profile-specific: Core `GroupNumberFromUtf8Name`, PythonRe `TryGetGroupNumber`,
PCRE2 `CopyNumbersForUtf8Name` (retaining duplicate numbers). These operations
validate their own bytes and use ordinal names without normalization.

### Byte-start enumeration

Core replaces `EnumerateMatchesFromUtf16Offset` with a byte-start overload:

```csharp
var regex = new Utf8Regex("x");
foreach (var match in regex.EnumerateMatches("é😀x"u8, startOffsetInBytes: 6))
{
    // match.StartOffsetInBytes == 6; match.EndOffsetInBytes == 7
}
```

Pass a scalar boundary in the original input, not a position in a sliced suffix.
Lookbehind and anchors retain the full subject; RTL uses the supplied byte start
as its right-hand search boundary. Empty progress advances by complete scalars.
An optional cancellation token remains active with the same deadline and cumulative
resource limits throughout enumeration. Whole-subject `Match` and enumeration
now return the existing `Utf8CaptureRange`: `Success`, `StartOffsetInBytes` and
`EndOffsetInBytes`. `Utf8ValueMatch`, its constructors, UTF-16 fields, alignment
flag and optional byte-range getter are removed. The default range is unsuccessful
with zero endpoints; a successful empty match has equal endpoints and `Success`
true. Callers derive character positions themselves. These results retain no
subject owner. Cursor copies retain independent positions and shared invocation
limits; already-published ranges remain valid after advance or failure.

Matching cursors no longer prepare coordinate maps or scan gaps for UTF-16 totals.
PythonRe also removes `TryGetScalarRange` and its optional scalar-prefix index.
Use capture byte endpoints and derive character positions in the host when needed;
that work is outside the regex invocation's resource and interruption controls.
PythonRe borrowed value/group/match views also remove `GetValueString` and the
`TryGet...ValueString` helpers. Read `GetValueBytes()` on a value, or
`Value.GetValueBytes()` on a group/match context. Resolve groups with `TryGetGroup`
or `TryGetFirstSetGroup` and check `Success` to distinguish unmatched from empty.
Decode the selected bytes in the host when required.
Replace PythonRe `SplitToStrings` and `SplitDetailed` with `SplitRanges`.
`Number == 0` denotes a segment; positive numbers identify captured separators.
Check `Success` before slicing: unmatched captures differ from successful empty
ones. Host decoding and string arrays are outside regex resource controls.
Replace PythonRe `FindAllToStrings` with `FindAllToUtf8`. Its `Shape`, `Count`,
scalar values and tuple order retain Python shaping; decode selected owned byte
arrays in the host. Unmatched findall groups remain empty values. The former
string-sharing/canonicalization behavior is removed; host text is outside limits.
The separate structural `FindAll` overloads and `Utf8PythonMatchData` are removed.
Use `CreateMatchCursor` for whole-match byte extents/captures, collecting ranges or
decoding text in the host. It borrows stable `ReadOnlyMemory<byte>`; callers with
only a transient span must retain suitable memory for those views. `FindAllToUtf8`
still accepts spans directly and returns Python-shaped owned byte values.
PCRE2 standalone views also remove `GetValueString`. Read `GetValueBytes()` on a
value/group, or `Value.GetValueBytes()` on a match/partial context. Preserve status
checks: opted-in `\C` can yield contiguous byte fragments that are not UTF-8 text,
and a noncontiguous reported span still rejects byte access. Inspect
`IsUtf8SliceWellFormed` before decoding; host decoding is outside regex controls.
Modeled work and working/result capacity requirements can decrease. Original
byte context, scalar progress, cancellation and deadlines remain enforced.

Split results now expose only `IndexInBytes` and `LengthInBytes`. Remove uses of
`IndexInUtf16`, `LengthInUtf16` and `IsByteAligned` on `Utf8ValueSplit`; Core split
segments already follow scalar boundaries. Controlled splits no longer allocate
subject coordinate maps, so their modeled working requirements can decrease.
Count/empty/RTL order, borrowed-view lifetimes and cumulative interruption remain.
Decode a requested segment explicitly with `GetValueString` when needed.

Core also removes `CountFromUtf16Offset`. Keep using `Count` for the whole subject;
for a partial count, aggregate one byte-start enumeration:

```csharp
var regex = new Utf8Regex("x");
var count = 0;
foreach (var match in regex.EnumerateMatches("é😀x x"u8, startOffsetInBytes: 6))
    count++; // 2
```

One enumeration preserves cumulative work, deadline, cancellation and resource
admission. Its matching scratch can require more work or storage than the
removed offset counter. The whole-subject Count implementation
is unchanged by this API removal.

### Byte-start matching with captures

Core removes the `IsMatchFromUtf16Offset` and `MatchFromUtf16Offset` overloads. Use the byte-start match
operation, passing an empty capture span when only existence is needed:

```csharp
var regex = new Utf8Regex("(?<=é)(?<value>😀)");
Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
var match = regex.Match("é😀x"u8, startOffsetInBytes: 2, captures: slots,
    endOffsetInBytes: 6);
// match and slots[1] are byte range [2, 6); lookbehind still sees é.
bool found = regex.Match("é😀x"u8, 2, []).Success;
```

Slot zero is the whole match; `GroupNumberFromCaptureSlot` resolves compact slots
to actual group numbers, including sparse numbers. Short spans do not change the
selected match. The optional end is a scalar boundary and the matching end, with
byte offsets relative to the original subject. Pass the token and working-memory
allowance to this one signature; funded program owners expose it too.
`MatchDetailed` and `MatchDetailedFromUtf16Offset` are removed, along with
`Utf8MatchContext`, `Utf8GroupContext` and `Utf8CaptureContext`. Use the memory cursor for capture
history, with a scalar-aligned byte start:

```csharp
var subject = "é😀x"u8.ToArray();
Utf8RangeMatch retained;
using (var cursor = regex.CreateMatchCursor(subject, startOffsetInBytes: 2))
    cursor.MoveNext(out retained);
// retained.Value and retained.GetCaptures(1) remain valid after disposal.
```

Keep the borrowed subject valid and unchanged while using retained results.
The cursor shares the original token, deadline and cumulative limits through
its lifetime. Its scratch and capture snapshots remain subject to working and
result admission; numeric needs can differ from the removed detailed API.

The operation creates no UTF16 map, captured strings or result-storage snapshot.
Existing output storage belongs to the caller and is not charged as engine-owned
result bytes. Scratch remains subject to working admission. Work and storage needs
can decrease compared with the removed UTF16-start operation; recheck finite
policies. Defined output slots clear on no match or failure after entry, while
pre-cancellation and excess entries leave caller storage unchanged.

### Caller-owned compiled expressions

PythonRe removes `SearchToString`, `MatchToString` and `FullMatchToString`.
Call `Search`, `Match` or `FullMatch`, check `Success`, then use the borrowed
`GetValueBytes()` span. Optional host decoding is outside regex resource limits
and cancellation; keep the subject unchanged while accessing the result.

Core removes the static `IsMatch`, `Count`, `Match`, `MatchDetailed`,
`EnumerateMatches`, `EnumerateSplits` and `Replace` families and their global
pattern cache. PCRE2 also removes its static `IsMatch`, `Match` and `Replace`
overloads, which compiled a fresh expression per call. Construct or compile an
expression once and retain it in the host:

```csharp
var words = new Utf8Regex(@"\w+");
bool found = words.IsMatch("é😀 hello"u8);
```

Construction captures the effective options and culture. Per-instance replacement
preparation remains bounded; explicit construction and invocation limits keep
their documented scopes. No process-wide program-cache allowance is implied.

## Scalar matching and coordinates, introduced in 0.4.0

```csharp
var dot = new Utf8Regex(".");
int count = dot.Count("A😀"u8); // 2 scalars
var emojiMatch = dot.Match("😀"u8); // byte index 0, byte length 4
```

Matching, classes, quantifiers, captures/references, lookarounds, RTL and empty
progress use complete Unicode scalars. Combining marks remain separate; text is
not normalized. Replace old surrogate-half expectations with scalar outcomes.
In `é😀x`, x starts at byte 6 and UTF-16 index 3. Do not pass UTF-16 positions to
byte APIs. The former FromUtf16Offset APIs are removed in 0.5 development.

Version 0.4 required strict whole-subject UTF-8 in all three published packages.
Version 0.5 uses the profile contracts above. Denial/interruption can precede a
malformed tail; current subjects must already be valid text. Opted-in PCRE2 `\C` consumes one
byte and can split a scalar; `\K` can also produce special ranges. Consult their
slice/projection flags before decoding or extracting text.

## Limits and failures

```csharp
using System.Text.RegularExpressions;

var limited = new Utf8Regex(@"(?<word>\w+)", RegexOptions.CultureInvariant,
    TimeSpan.FromMilliseconds(100), new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 256,
        MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
    });
using var cancellation = new CancellationTokenSource();
int words = limited.Count("café tea"u8, cancellation.Token);
```

These are examples, not universal recommended budgets. Null invocation limits
are unlimited; zero is invalid. Typed denial is
separate from no match and never means truncated successful output.

Work/depth/working/result controls enforce their documented stages. Memory
admission models major capacities, growth overlap and retained payloads, not exact
heap/RSS or every helper allocation. Cumulative result/staging traffic can count
shared storage again. Work units and capacity requirements vary by route/version;
recheck representative finite policies when upgrading. Short Unicode output can
use a conservative byte-derived string charge. Character-position projections
performed by the host are outside regex resource controls.

Cancellation is synchronous and retains the original token. One absolute deadline
covers required input inspection, matching, shaping and cursor waits. Compatibility
profiles expose cursors for host computation. The regex controls each advance;
host computation and final output checks are the host's responsibility.
Byte-capped Core/PythonRe timeout diagnostics omit subject text.

Use Compile for separate construction caps/timeout/token. Ordinary constructors
retain fixed backstops. Compilation tokens are not retained for later matching.
Program/cache retention, cold preparation, standalone getters, caller callbacks
and host storage have separate scopes. See [resource controls](SPEC.md#cancellation-deadlines-and-resource-limits)
and [host admission](SPEC.md#host-admission).

## Program owners, cursors and retained results

Core's funded Compile returns a disposable `Utf8RegexProgramOwner`:

```csharp
using var program = Utf8Regex.Compile(pattern, RegexOptions.None,
    matchTimeout, resourcePolicy, constructionPolicy, constructionTimeout,
    compileToken, programMemoryAllowance);
int count = program.Count(subject.Span, callToken, workingMemoryAllowance);
Span<byte> destination = stackalloc byte[256];
var status = program.TryReplace(subject.Span, "[$0]"u8, destination,
    out int bytesWritten, callToken, workingMemoryAllowance);
```

Calls refund temporary loans synchronously and do not retry a refused allowance.
Core/PCRE2 funded programs conservatively prepay their bounded replacement cache
maximum (currently 4MiB payload plus metadata), including before first use. This
is modeled admission, not eager allocation. Cold template work and output stay
admitted through final copying. Short caller-owned destinations remain unchanged.

Persistent memory cursors can cross calls/awaits:

```csharp
using var cursor = program.CreateMatchCursor(subject, startOffsetInBytes: 0,
    cancellationToken: lifetimeToken, workingMemoryAllowance: workingMemoryAllowance);
while (cursor.MoveNextRetained(out var result, currentCallToken, resultMemoryAllowance))
{
    using (result)
    {
        ReadOnlyMemory<byte> bytes = result.Match.GetValueMemory();
        // Consume bytes while the unchanged subject and its memory manager live.
    }
}
```

The creation token/deadline survives waits; an advance token applies only to that
call. Aliases of an exclusive memory cursor share position/lifetime. Dispose on
early abandonment. Closing a program rejects new uses and lets admitted active
uses finish. Retained metadata survives advances and program/cursor close; subject
bytes remain borrowed. Releasing a result receipt ends producer retention funding,
not the validity of copied ranges; subsequent retention is the host's concern.

PCRE2 supplies its own program owner, flags, last-capture ranges, duplicate names
and consumed-range progression. PythonRe `SubnRetained` returns a disposable
output receipt. Existing Core value-cursor copies keep independent positions and
shared allowance/deadline; copying never creates another budget.

Owned snapshots survive later calls, failure and disposal. Default Core values
are empty/zero; a default view has no groups, while an unsuccessful operation
retains group zero. Python default/missed detailed capture/name arrays and text
are empty. Successful supplied arrays retain their documented identity. Mutable
output arrays remain independent even when immutable equal-range strings share.

Core removes `ReplaceToString`, callback replacement and both evaluator delegates.
Use `CreateMatchCursor` to read byte ranges and capture histories, then assemble
custom output in the host. Byte-template replacement and destination output keep
their resource controls and original cancellation/deadline. Their scalar worker
no longer derives input UTF-16 coordinates. PythonRe now likewise removes all
callback replacement forms, both evaluator delegates, `ReplaceToString`,
`SubnToString` and `Utf8PythonSubnResult`. Use byte-template `Replace`/`Subn` or
`SubnRetained` for controlled library output. Custom output uses the real range
cursor and host-owned buffers; each advance preserves regex controls, while host
callback execution, final output/cancellation checks and memory admission belong
to the host. PCRE2 likewise removes both callback overload families/evaluator
delegates, and the now-unused Core `Utf8ReplacementWriter` is removed.

For text output, decode `regex.Replace(input, template)` in the host with
`Encoding.UTF8.GetString`. The regex meters its byte result; the caller owns the
additional string allocation and any host-level admission for it.
PCRE2 likewise removes its template `ReplaceToString` overloads while retaining
byte `Replace`/`TryReplace`. With opt-in `\C`, byte output may be malformed UTF-8;
validate it before decoding. Byte templates preserve PCRE2 substitution's special
`\K` empty-match disposition; ordinary host range iteration keeps the cursor's
documented disposition.

## Profile scope and other compatibility changes

| Profile | Supported scope | Important exclusions/differences |
|---|---|---|
| Core | .NET-style scalar grammar, captures/history, RTL and supported NonBacktracking | No UTF-16 parity or POSIX selection |
| PythonRe | CPython 3.13.2 str profile; Unicode 15.1 data; opening-order captures and last-closed metadata | No bytes-regex semantics, LOCALE, Unicode-name lookup or endpos |
| PCRE2 | Selected 10.47 standard profile, final capture slots and partial Probe | No DFA, callouts, invalid-UTF mode, split or full history; partial decisions have limited scope |
| POSIX | Case-sensitive ERE, leftmost-longest selection and hierarchical capture priority | Fixed ASCII classes; no locale collation, backreferences, lookarounds, replacement or history API |

Choose POSIX for longest selection: `(a|aa)(a*)` on `aaa` captures `aa` then `a`.
It fills caller-owned spans and returns coordinate values, without producer-owned
result payload. Positive result-byte budgets therefore admit those values; working
limits cover matching/selection. Ambiguous patterns can require substantial work
and quadratic interval storage. No linear-time or complete GNU/BSD guarantee.

Unsupported PCRE2 Hard partial decisions and generic marked partial misses can
throw `NotSupportedException`; do not treat this as NoMatch. Ordinary None-mode
matching and supported Soft full matches keep their documented behavior.
Public `Utf8CountDiagnostics`/`Utf8IsMatchDiagnostics` records were removed; their
producers were internal diagnostics. Compiled uses owned non-emitted execution
when dynamic code is unavailable. NativeAOT and trimming remain unqualified.

Consult the [Core](SPEC.md), [PythonRe](SPEC-PYTHONRE.md), [PCRE2](SPEC-PCRE2.md)
and [POSIX](SPEC-POSIX.md) contracts for exact admission and lifetime rules.
Historical benchmark rows retain their versions; they are not a universal speed
or full-equivalence promise for a new package.

### PythonRe cursor ranges

`Utf8PythonMatchCursor.Current` and no-output `MoveNext` overloads are removed.
Use `MoveNext(out Utf8PythonRangeMatch match, token = default)` and keep that
value, or use `MoveNextRetained` with a caller-owned working allowance. Both
advance the same cursor position. Capture/last-closed metadata, original and
per-advance cancellation, deadlines and cumulative limits remain in force.

Range metadata survives advances, failure and disposal. Its byte views borrow the
subject, which must stay unchanged with its manager alive while read. Decode or
copy selected bytes in the host when owned text is needed; host allocation and
final publication checks are outside regex controls. One-shot owned detailed
results are removed as described below.

### PythonRe owned-text capture results

SearchDetailedData, MatchDetailedData, FullMatchDetailedData, FindIterDetailed,
Utf8PythonDetailedMatchData and Utf8PythonGroupMatchData are removed. For a
synchronous match, use SearchDetailed/MatchDetailed/FullMatchDetailed and read
their borrowed byte capture contexts. For incremental or retained captures, use
CreateMatchCursor and its real range/retained advances. Keep subject bytes unchanged
and their manager alive while reading borrowed bytes.

Decode selected capture bytes and collect results in the host. Regex resource
limits cover matching and admitted capture metadata/byte output; host string and
collection allocation, copying and final publication checks belong to the host.
The removed invocation string-sizing/decoding helpers provide no host memory cap.
Scalar progression, offsets, capture completion, synchronous cancellation,
deadlines and meaningful byte/work/depth limits remain on the retained APIs.

### Constructor signatures

Constructors now use the existing optional nullable-timeout signature: one per
supported string/UTF-8 pattern representation. Short source calls still work;
previously compiled callers of removed overloads must be recompiled, including
adapters. Core UTF-8 compilation remains on Compile; no public span constructor
is added. Explicit construction limits, cancellation and program funding retain
their factory contracts.

An omitted or null timeout inherits DefaultMatchTimeout (infinite for POSIX).
A positional bare default in that nullable slot now has the same meaning.
TimeSpan.Zero and default(TimeSpan) still fail timeout validation. Options,
resource-policy ordering, strict pattern checks and scalar matching are unchanged.

### PythonRe operation signatures

Redundant short overloads are removed from value/context/count, FindAllToUtf8,
string replacement/substitution, SplitRanges and cursor creation. Their existing
long token-bearing entries now default offsets/counts to zero and cancellation to
None. Ordinary short source calls still work. Previously compiled callers must
be recompiled; method groups targeting shorter delegates need an explicit lambda,
such as `subject => regex.CreateMatchCursor(subject)`.

Matching/output workers and limits are unchanged. Cancellation still precedes
program use and input/offset work. The separate explicit cursor allowance entry
continues to reject null, preserve original-token precedence and refuse storage
without an uncapped retry. No public validation policy or new execution mode.

### PCRE2 operation signatures

Short value/context/count, span enumeration, probe, substitution, destination and
batch/group overloads now bind their existing long token-bearing entry using
optional starts/options/cancellation. Recompile old binaries. For TryReplace,
the old options-only fifth argument is now `substitutionOptions: options`; partial
mode remains explicit. Other ordinary source calls retain their short forms.
Cancellation precedes input/offset/option work. Literal shortcuts, finite limits,
explicit funding, captures/MARK, partial and transactional destination behavior
remain supported. Subject bytes are already-valid and unchanged.

### Core Match entry

Core now exposes one Match signature with optional byte start, caller-owned
capture slots, token, working allowance and end bound. A null start uses zero
forward or the selected end in right-to-left mode. `regex.Match(subject)` keeps
its existing no-capture paths; `regex.Match(subject, captures: slots)` fills the
existing capture slots with a direction-aware default start. Explicit byte starts,
end-bound context, capture history on cursors, funding and interruption remain.
Recompile old binaries. Change an old positional token call to
`regex.Match(subject, cancellationToken: token)`.

### Core operation signatures and optional working allowance

Core IsMatch/Count, span match/split enumeration and text/UTF-8 template outputs
now expose one optional token-bearing signature per supported form. Recompile old
binaries. Name an old positional enumeration token with `cancellationToken:`.
Match enumeration's null start selects the direction's default; explicit byte
starts retain their meaning. Split count defaults to int.MaxValue, and zero
continues to mean unlimited. Ordinary short source calls keep their forms.

IsMatch/Count's optional null working allowance now selects the existing engine
numeric-policy route; the former explicit-null rejection is removed. Numeric
limits/deadlines still apply. Non-null funding uses the existing admitted worker;
refusal is terminal with no uncapped retry, and covered loans refund synchronously.
Program owners continue to supply their account when no separate working account
is specified. Captures, original cancellation, byte output and aliases are unchanged.
