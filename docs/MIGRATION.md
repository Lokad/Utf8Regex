# Migration guide

## Upgrading from 0.4 to 0.5

Upgrade Core and all selected adapters together. Adapters require the exact Core
version. Version 0.5 supplies .NET 10 and .NET 11 assets and adds
`Lokad.Utf8Regex.Posix`. Recompile callers after upgrading. Use named arguments
when moving older positional tokens, match options or output buffers.

### Validate UTF-8 at the text boundary

All four profiles borrow already-valid, unchanged UTF-8 subjects. Move subject
validation to the point where external bytes become text. A host can use
`System.Text.Unicode.Utf8.IsValid` when its representation needs that check.
Malformed subjects have unspecified matching results. Remove any
`Utf8InputValidation` arguments or property reads from development versions.
Pattern, template and assembled-output checks retain their separate contracts.

### Pass byte offsets and choose a result form

Version 0.4 established scalar matching. A dot matches 😀 once, across four
bytes, and combining marks remain separate scalars. Version 0.5 removes the
UTF-16 offset overloads and result fields. Convert any character positions in
the host. Use scalar-aligned byte offsets relative to the original subject, so
lookbehind can still read the prefix.

```csharp
using Lokad.Utf8Regex;

using var regex = Utf8Regex.Compile("(?<=é)(?<word>😀)"u8);
Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
var match = regex.Match("é😀x"u8, startOffsetInBytes: 2, captures: slots,
    endOffsetInBytes: 6);
// The match and slots[1] both cover byte range [2, 6).
```

- Use an empty capture span for whole-match ranges or offset-aware existence
  checks. Omit the start for the direction's default. To count from an offset,
  count results from one `EnumerateMatches` call.
- Use a memory cursor for capture history or when matching must continue across
  calls. Replace Core's detailed match APIs with `CreateMatchCursor` and
  `MoveNext(out match)`. Returned ranges and capture snapshots survive cursor
  advance and close. Their bytes still borrow the subject and its live memory
  manager.
- Core slot zero is the whole match. Translate compact capture slots with
  `GroupNumberFromCaptureSlot` and copy names with `TryCopyGroupNameUtf8`.
  Resolve a UTF-8 name with `GroupNumberFromUtf8Name` before reading the group's
  numbered result. Split segments expose `IndexInBytes`, `LengthInBytes` and
  `GetValueBytes()`.

### Compile once and pass a policy

Compile or construct expressions in the host to replace static matching and the
global pattern cache. Core, PythonRe and PCRE2 accept UTF-8 source through
`Compile` and can copy it back with `TryCopyPatternUtf8`. POSIX accepts a string
pattern. Move timeout, resource, construction and program allowance arguments
into `Utf8RegexPolicy`. Keep profile options and PCRE2 compile settings on their
own parameters. `Compile` accepts a separate construction cancellation token.

```csharp
using var regex = Utf8Regex.Compile(@"(?<word>\w+)"u8, policy: new Utf8RegexPolicy
{
    MatchTimeout = TimeSpan.FromMilliseconds(100),
    ResourcePolicy = new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 }
});
int count = regex.Count("café tea"u8);
```

Unset limit fields are unlimited. A resource refusal is a typed failure.
Cancellation is synchronous, with the original token, and an absolute deadline
continues through cursor waits. Memory limits cover modeled capacities and
retained payloads. Caller destinations, decoding and other host allocations are
outside those limits. Recheck finite policies after upgrading.

Creation with a program allowance returns the same disposable regex type as
ordinary creation. Pass a working allowance on supported operations or inherit
the program account. Dispose a funded regex to close it to new uses. Borrowed
span enumerators require an open funded regex. Persistent memory cursors retain
program admission until completion or disposal, and their aliases share position
and lifetime. Dispose retained result receipts to release their charges. Keep
subject storage valid and unchanged while reading any borrowed bytes. See the
[resource and ownership contract](SPEC.md#cancellation-deadlines-and-resource-limits).

### Keep replacements and output in bytes

Pass `u8` literals or encode dynamic text for replacement templates. Use byte
`Replace` or, in Core and PCRE2, `TryReplace` with a caller-owned destination.
In PythonRe, use `Subn` for a replacement count and `SubnRetained` for caller-funded
retained output. For custom replacements, advance a cursor and assemble bytes
in the host. Decode output in the host when a string is required. PCRE2's enabled
`\C` byte atom can produce fragments that need validation before decoding.

## PythonRe compilation policy

`Utf8PythonRegex` uses `Utf8RegexPolicy` for both constructors and factories.
An omitted match timeout inherits `DefaultMatchTimeout` and an omitted
construction timeout is infinite. Dispose funded instances. The [PythonRe
contract](SPEC-PYTHONRE.md) describes compile options and supported Python behavior.

- Use `Match` with a caller-owned capture span. Its default mode is anchored
  `Match`. Select `Utf8PythonMatchMode.Search` or `FullMatch` for the former
  methods. The cursor defaults to `Search`. Both accept a scalar-aligned
  optional byte end.
- Replace detailed matches and FindAll result collections with a memory cursor
  when captures or closing order matter. The result exposes numbered ranges
  and `LastIndex`. Resolve names once with `TryGetGroupNumber` or
  `TryCopyGroupNameUtf8` on the expression. To reproduce Python's `lastgroup`,
  map `LastIndex` to the corresponding named group in the host.
- Use `SplitRanges` to distinguish segments (number zero), captured separators
  and unmatched captures. Check `Success` before slicing.

## PCRE2

Pass `Utf8RegexPolicy` alongside `Pcre2CompileOptions` and
`Utf8Pcre2CompileSettings`. Unset shared policy limits replace native zero
limit inputs. `DefaultExecutionLimits` is a read-only PCRE2-unit projection.

Use `Match` with `Span<Utf8Pcre2CaptureRange>` for numbered capture slots, and
the memory cursor for snapshots. Read `Mark` on the match value or cursor result.
Use `Probe` for supported partial cases. Inspect `HasContiguousByteRange` and
`IsUtf8SliceWellFormed` before slicing or decoding results from `\C` or `\K`.
For duplicate names, use `CopyNumbersForUtf8Name` and test the slots in order,
or export ordered name and number pairs with `TryCopyNameEntryUtf8`.
See the [PCRE2 contract](SPEC-PCRE2.md) for supported options, special ranges,
substitution and partial matches.

## POSIX

Use `Utf8PosixRegex` for leftmost-longest ERE matching. A synchronous `Match`
or cursor `MoveNext` can write captures to caller-supplied slots. Case-sensitive
matching is the default. `PosixCompileOptions.IgnoreCase` uses invariant
single-scalar case equivalence. See the [POSIX contract](SPEC-POSIX.md) for
subexpression priority and the supported grammar.
