# Migration guide

## 0.5.0 development: .NET 10 and .NET 11

All three packages now build `net10.0` and `net11.0` assets. Adapters still require
exactly the same Core version; NuGet selects the asset matching the application.
The build SDK is pinned in `global.json`; its .NET 11 version is currently a
release candidate. Matching units, validation, coordinates and resource contracts
are unchanged. Benchmark runs default to .NET 11; use `-Framework net10.0` with
the test/benchmark scripts for .NET 10 controls. Historical measurements retain
their original runtime labels. Published 0.4.0 packages remain .NET 10-only.

Core also accepts native UTF-8 patterns, including the funded program overload:

```csharp
var emoji = Utf8Regex.Compile("(?<value>😀)+"u8);
using var matches = emoji.CreateMatchCursor("a😀😀b"u8.ToArray(), 1);
matches.MoveNext(out var match); // whole match: byte range [1, 9)
```

Compilation borrows the source only during the call and owns its retained bytes.
It uses the existing scalar grammar and execution controls without a whole-pattern
UTF-16 bridge. Pattern syntax errors identify byte offsets. `Pattern` explicitly
projects a new string on request; native construction timeouts leave the
exception's pattern text empty. The added span overloads make a bare `null` at
some `Compile` call sites ambiguous: use a typed string or named `pattern:` when
testing text-null errors. Existing string constructors remain available.

All three profiles expose `CaptureGroupCount` excluding group zero and
`TryCopyPatternUtf8(destination, out bytesRequired, token)`. A zero-length
destination obtains the required capacity; `Done` copies exact source bytes.
The caller owns the destination, including its admission and retained lifetime.
Funded Core/PCRE2 owners expose these members without revealing program storage.
See the [metadata contract](SPEC.md#native-pattern-metadata) for capacity,
unrepresentable legacy text and cancellation outcomes.
Native named lookup is profile-specific: Core `GroupNumberFromUtf8Name`, PythonRe
`TryGetGroupNumber`, PCRE2 `CopyNumbersForUtf8Name` (preserving duplicate numbers).
Each validates strict UTF-8, compares ordinal keys without normalization or
allocating temporary strings, and accepts a current-call token. Core and PCRE2
funded owners expose those lookups while retaining program admission.

## 0.4.0: Unicode scalars

Version **0.4.0** changes matching from UTF-16 units to Unicode scalars.
Both adapters require exactly Core **[0.4.0]**: upgrade selected packages together
rather than overriding dependencies. All target .NET 10.
See [release notes](../CHANGELOG.md) for the public release history.

## Match scalars; pass byte coordinates

```csharp
using Lokad.Utf8Regex;

var dot = new Utf8Regex(".");
int count = dot.Count("A😀"u8); // 2
var emoji = dot.Match("😀"u8); // IndexInBytes == 0; LengthInBytes == 4
```

Dot/classes, quantifiers, captures/references, lookarounds, RTL and empty progress
use complete scalars. Combining marks remain separate; text is not normalized.
Core alternatives retain ordered first success, without POSIX longest selection.
Replace old surrogate-half expectations with explicit scalar outcomes.

In `é😀x`, x starts at byte 6 and UTF-16 index 3. Do not pass UTF-16 indices
to byte APIs; explicit FromUtf16Offset APIs reject a split scalar. PCRE2's opted-in
`\C` and non-monotone `\K` have separate range/projection flags.

The entire subject must be strict UTF-8, even after an early match.
Invalid input rejects rather than replacement-decoding. Denial/interruption can
precede a malformed tail; successful matching cannot bypass it.

## Use explicit limits and handle distinct failures

```csharp
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;

var regex = new Utf8Regex(@"(?<word>\w+)", RegexOptions.CultureInvariant,
    TimeSpan.FromMilliseconds(100), new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 256,
        MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
    });
using var cancellation = new CancellationTokenSource();
int count = regex.Count("café tea"u8, cancellation.Token);
```

These are examples, not recommended universal budgets. Null invocation limits and
static cached calls are unlimited; zero is invalid. Work/depth/major working and
result-capacity admission are enforced, but units depend on algorithm/version.
Result/staging traffic is cumulative without refunds; overlap/shared storage can
count again. Typed denial is not no-match or truncated success.

Cancellation is synchronous and carries the original token. One absolute deadline
covers validation, matching, required shaping and cursor waits. Callbacks cannot
be forcibly interrupted; library copying/encoding of returned output is covered.
Core/PythonRe byte-capped timeout diagnostics omit Input to avoid subject decoding.

Use Compile for separate construction caps/timeout/token; ordinary constructors
retain fixed backstops. Pass execution tokens again later. Cold preparation,
program/cache retention, standalone getters, callback allocations and host storage
have separate scope. See [resource controls](SPEC.md#cancellation-deadlines-and-resource-limits)
and [host admission](SPEC.md#host-admission) for the Core/PythonRe W+R recipe.

### Core caller-funded operations and memory cursors

Core's funded Compile overload returns `Utf8RegexProgramOwner`. Close it explicitly;
compilation remains funded through any active calls. The optional working allowance
lets a host use different children of one shared reservation for program and work:

```csharp
using var program = Utf8Regex.Compile(pattern, RegexOptions.None,
    matchTimeout, resourcePolicy, constructionPolicy, constructionTimeout,
    compileToken, programMemoryAllowance);
int count = program.Count(subject.Span, callToken, workingMemoryAllowance);
```

Boolean/count calls refund temporary loans synchronously, retain numeric limits
and never retry a refusal under another allowance. For repeated captures across
host calls or awaits, use the separate memory cursor:

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

Each advance's token ends with that call; the creation token and absolute deadline
still apply across waits. Dispose abandoned cursors. Closing the program rejects
new operations but lets its existing cursors finish. Result receipts may outlive
both; copies stay valid after receipt disposal, with further admission owned by
the caller. `MoveNext(out Utf8RangeMatch)` omits retained funding when the host takes
over retention directly. Both paths avoid captured strings and whole-subject
UTF-16 maps, retaining Core capture histories and sparse slot identities. Existing
span cursors keep their independent-copy contract. PCRE2 offers its own
`Utf8Pcre2RegexProgramOwner` through the funded Compile overload, with byte starts,
match flags and separate working admission. Its `CreateMatchCursor` and
`MoveNextRetained` follow the same explicit lifetime rule with PCRE2 last-capture
ranges, duplicate names and consumed-range progression. Opted-in `\C` values may
be scalar fragments; inspect the range's `IsUtf8SliceWellFormed` before decoding.
PCRE2 byte pattern constructors and funded Compile now own and parse UTF-8 source
directly; `Pattern` is an explicit text projection. Selected names/properties can
still use internal text. PCRE2 scalar-coordinate queries, native template parsing
and funded replacement remain unfinished.
The [resource contract](SPEC.md#cancellation-deadlines-and-resource-limits)
distinguishes modeled live admission from cumulative work/result traffic.

## Respect lifetime and default-result rules

Borrowed subjects/views require unchanged bytes and a live owner. Owned snapshots
survive later moves, failure and disposal. Core value-cursor copies have independent
positions and shared allowance/deadline; copying creates no new budget.
PythonRe memory-cursor aliases share position/lifetime; dispose when stopping
early. EOF/failure/disposal release scratch synchronously.

Default Core views expose empty values/zero coordinates; a default view has no
groups, while an unsuccessful operation retains group zero. Successful manually
constructed Utf8ValueMatch values reject negative active coordinates.
PythonRe default match/group text and default/missed detailed capture/name arrays
are empty. Supplied successful arrays retain identity.

Evaluators must be non-null even on misses; pre-cancellation still wins.
Core/PCRE2 scoped writer appends accept local byte/char stack spans and copy/encode
before return. Keep writers inside the callback. String replacement fragments
repair unpaired surrogates independently rather than joining fragments.

## Choose the profile deliberately

| Profile | Scope | Key exclusions/differences |
| --- | --- | --- |
| Core | .NET-style scalar grammar, captures/history, RTL, supported NonBacktracking subset | No UTF-16 parity or POSIX selection |
| PythonRe | CPython 3.13.2 str profile; Unicode 15.1 data; opening-order captures and last-closed metadata | No bytes-pattern semantics, LOCALE, Unicode-name lookup or endpos |
| PCRE2 | Selected managed 10.47 standard profile; final capture slots and explicit partial Probe | No DFA, callouts, invalid-UTF mode, split or full capture history; partial decisions are curated |

PCRE2 Hard decisions and generic marked partial misses can throw
NotSupportedException. Treat that as unsupported, not NoMatch. None-mode matching
and proven Soft full matches retain their documented scope.

Public Utf8CountDiagnostics/Utf8IsMatchDiagnostics records were removed; their
producers were internal diagnostics, not matching APIs.
Compiled falls back to existing non-emitted execution when dynamic code is
disabled; NativeAOT/trimming are not qualified.

Specifications: [Core](SPEC.md),
[PythonRe](SPEC-PYTHONRE.md),
[PCRE2](SPEC-PCRE2.md).
Historical benchmark rows retain their own versions and do not qualify this
development source or promise complete flavor equivalence.
