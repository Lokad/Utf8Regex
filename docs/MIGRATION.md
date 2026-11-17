# Migration guide

## 0.5.0: .NET 10 and .NET 11

Upgrade Core and every selected adapter together: adapters require the exact same
package version. Version 0.5 supplies `net10.0` and `net11.0` assets; published
0.4.0 packages target .NET 10 only. The stable SDK is pinned in
[global.json](../global.json). Benchmarks use .NET 11 as the primary baseline,
while historical rows retain their actual producer/runtime identities.

### Choose the subject input contract

| Profile | Subject contract in 0.5.0 |
|---|---|
| Core | Default `Validate` checks the entire subject at most once per operation/cursor. Immutable `AssumeValid` trusts valid unchanged UTF-8. |
| PythonRe | Models Python `re` over `str`: pass already-valid unchanged UTF-8 text; regex operations do not validate the whole subject. |
| PCRE2 | Default UTF checking covers start minus maximum scalar lookbehind through subject end. Immutable `AssumeValid` is the explicit trusted-input bypass. |
| POSIX | Pass already-valid unchanged UTF-8 text; malformed-suffix rejection is not an extra compatibility requirement. |

For PythonRe/POSIX, validate external bytes where they become text. Neither adapter
has an input-validation option/property. Core and PCRE2 select their policy once:

```csharp
using Lokad.Utf8Regex;

var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
// regex.InputValidation is read-only; every operation inherits it.
```

`AssumeValid` requires the caller's validity and borrowed-storage guarantees.
There is no per-call override or public validation token. Pattern, template,
capture-name and compatibility-profile callback output checks remain independent.
Managed bounds, scalar semantics, byte offsets, cancellation and admission stay
active. Required coordinate/output work can still inspect input.

PCRE2 can ignore malformed bytes before its native validation window. Its results
now expose only byte endpoints, without UTF-16 offsets or `HasUtf16Projection`.
Its byte-template substitution can preserve ignored prefix bytes;
text and callback output retain their independent well-formed UTF-8 requirements.
See the [PCRE2 input contract](SPEC-PCRE2.md#subject-input-validation).

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

Core removes the static `IsMatch`, `Count`, `Match`, `MatchDetailed`,
`EnumerateMatches`, `EnumerateSplits` and `Replace` families and their global
pattern cache. Construct or compile an expression once and retain it in the host:

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
malformed tail even where validation is required. Opted-in PCRE2 `\C` consumes one
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
use a conservative byte-derived string charge, and distant Python scalar queries
can require extra working space for a scalar prefix index.

Cancellation is synchronous and retains the original token. One absolute deadline
covers required validation, matching, shaping and cursor waits. Compatibility
profile callbacks cannot be forcibly interrupted; library copying/encoding
afterward remains covered.
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
no longer derives input UTF-16 coordinates. PCRE2 and PythonRe callbacks remain
available pending their separate surface cleanup.

For text output, decode `regex.Replace(input, template)` in the host with
`Encoding.UTF8.GetString`. The regex meters its byte result; the caller owns the
additional string allocation and any host-level admission for it.

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
