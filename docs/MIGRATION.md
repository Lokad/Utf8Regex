# Migration guide

## 0.5.0 development: .NET 10 and .NET 11

Core and all three adapters build `net10.0` and `net11.0` assets. Adapters still require
exactly the same Core version; NuGet selects the asset matching the application.
The stable .NET 11 build SDK is pinned in `global.json`. Scalar matching and
primary byte coordinates remain. The input and ownership contracts below define
subject checks and additional storage lifetimes.
Benchmark runs default to .NET 11; use `-Framework net10.0` with
the test/benchmark scripts for .NET 10 controls. Historical measurements retain
their original runtime labels. Published 0.4.0 packages remain .NET 10-only.

Core retains an explicit immutable subject-validation choice:

```csharp
var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
```

Core's default `Validate` preserves strict whole-input rejection. `AssumeValid` skips
that pass because the host guarantees valid UTF-8 and unchanged borrowed storage;
it does not guarantee rejection of malformed subjects. Choose it once at engine
construction or a `Compile`/`Create` factory and inspect `InputValidation` afterward.
Matching, replacement, splitting and cursor APIs inherit the engine policy.
Both modes retain managed bounds, scalar semantics, byte offsets and cooperative
interruption/admission. Patterns and replacements keep their separate validation.
Required coordinates/materialization may still scan input. Validation runs at most
once per logical operation/cursor lifetime; omitted/redundant passes can reduce
numeric work requirements. See the [input contract](SPEC.md#subject-input-validation).

PCRE2 retains the same immutable choice, but `Validate` follows native UTF-mode
checking: start offset minus the maximum scalar lookbehind through subject end.
It can ignore invalid bytes before that window. `AssumeValid` is the explicit
subject `PCRE2_NO_UTF_CHECK` equivalent. See its [input contract](SPEC-PCRE2.md#subject-input-validation).

PythonRe models Python `re` over `str`; PythonRe and POSIX instead require existing
valid UTF-8 text. Their regex operations omit whole-subject validation, and their
development-only `inputValidation` parameters and `InputValidation` properties
are removed. Validate/decode external bytes at the text boundary. Borrowed text
must remain unchanged while in use. Pattern/replacement validation, managed bounds,
scalar semantics, offsets and interruption/admission remain active; malformed-tail
rejection is not an additional compatibility guarantee. Recheck finite work limits
because removed preparation changes route-dependent work requirements.

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

PythonRe and PCRE2 likewise compile native source bytes directly. UTF-8 template
overloads across all three profiles parse/cache bytes and assemble byte output
without mandatory whole-template strings. Their replacement grammars and error
timing remain profile-specific. For example, Core can use both native inputs:

```csharp
byte[] output = emoji.Replace("a😀😀b"u8, "[${value}]"u8); // a[😀]b
```

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

Core NonBacktracking preparation now meters groups of at most 64 size-scan bytes,
64 scalar offsets or64 state cells, with token/deadline checks before every group.
Scanning prepared candidate starts similarly admits at most 64 scalar-boundary
lookups per group, with a final token/deadline check before publishing no match.
Executed atom work retains its direct charges; storage/depth admission is unchanged.
Packed NonBacktracking rows cover constant rejections of impossible case-sensitive
literals within their existing cell quantum. Those rejections no longer incur a
separate failed-predicate work charge; feasible/folded/complex atoms and wider
rows retain their charges. Token/deadline checks still precede every row quantum.
Preparation and candidate scanning can consume fewer MaxWorkSteps than earlier versions. Treat work units as route/version
dependent, and qualify finite policies against representative workloads when upgrading.

Core NonBacktracking Count can now scan a proved complete anchored-line shape
directly: optional Unicode whitespace, mandatory unfolded ASCII prefix, greedy
non-newline tail and end anchor. It retains strict full-subject validation and
bounded token/deadline/work checks, while avoiding the viability table and local
scratch. Numeric work and working/depth requirements can be lower. Matching,
captures and retained cursors keep their existing semantics and implementations.

Proven whole-expression Unicode-letter Count now shares the existing category
counter's scalar-ended 4096-byte admission, including caller-funded Count. This
reduces work units and can admit tighter storage policies while retaining strict
validation, original-token cancellation and deadlines.

Bounded greedy Dot repeats with a proved terminal ASCII literal can avoid rewind
storage. Captures and strict validation are unchanged; finite work, depth and
working requirements can be lower for these shapes.

Smaller capture-trail chunks reduce the first reservation for brief captures.
Long histories can require more headers and growth checkpoints; qualify finite
working policies on those workloads as well. Capture semantics are unchanged.

Count can reuse a prepared disjoint-class repeat such as
`(?:[A-Z][a-z]+\s*){10,100}` without VM scratch, including on mixed UTF-8.
Its original separator predicate preserves Unicode whitespace. Complete-input
validation, bounded work checks and original interruption authority remain;
route-dependent work/depth/working requirements can be lower. Captured roots
and unproved shapes retain their existing paths; returned captures/cursors are unchanged.

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
Span<byte> destination = stackalloc byte[256];
var status = program.TryReplace(subject.Span, "[$0]"u8, destination,
    out int bytesWritten, callToken, workingMemoryAllowance);
```

Boolean/count/destination calls refund temporary loans synchronously, retain numeric limits
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
still use internal text. Its funded owner also exposes byte-template `TryReplace`
with substitution/match options, byte starts and separate working admission.
PCRE2 optional scalar-coordinate projection remains a later improvement.

Core and PCRE2 funded compilation conservatively prepay their bounded replacement
cache maximum (currently 4MiB payload plus bounded metadata), even before first
use; this is a modeled loan, not an eager allocation. Cold template preparation
and output scratch share working admission through final copying. Caller-owned
destinations stay valid after program close. Short destinations remain unchanged;
PCRE2 can additionally report required length with SubstituteOverflowLength.
PythonRe's funded `SubnRetained` instead returns an explicit disposable output
receipt. Continued retention after releasing a receipt belongs to the host.
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
| POSIX (new in 0.5.0) | Case-sensitive ERE subset, leftmost-longest whole match and hierarchical subexpression priority | Fixed ASCII named classes; no locale collation, backreferences, lookarounds, replacement or capture history API |

Choose `Utf8PosixRegex` when longest selection is required: `(a|aa)(a*)` on
`aaa` captures `aa` then `a`. Ordered Core/PythonRe/PCRE2 alternatives can choose
different captures. POSIX returns existing byte/UTF-16 coordinate values, fills
caller-owned capture spans and offers a disposable memory cursor. These outputs
create no producer-owned result payload, so a positive result-byte cap admits
them; working limits cover the competing histories. Ambiguous patterns may
require substantial work: use finite work/depth/working limits for untrusted
patterns. No worst-case linear-time guarantee or complete GNU/BSD equivalence.

PCRE2 Hard decisions and generic marked partial misses can throw
NotSupportedException. Treat that as unsupported, not NoMatch. None-mode matching
and proven Soft full matches retain their documented scope.

Public Utf8CountDiagnostics/Utf8IsMatchDiagnostics records were removed; their
producers were internal diagnostics, not matching APIs.
Compiled falls back to existing non-emitted execution when dynamic code is
disabled; NativeAOT/trimming are not qualified.

Specifications: [Core](SPEC.md),
[PythonRe](SPEC-PYTHONRE.md),
[PCRE2](SPEC-PCRE2.md), [POSIX](SPEC-POSIX.md).
Historical benchmark rows retain their own versions and do not qualify this
development source or promise complete flavor equivalence.

PCRE2's default subject check now follows its native UTF-mode window: the start
offset minus the maximum lookbehind, through the subject end. Invalid bytes
before that window no longer reject a match. Byte ranges and well-formed matched
values remain available; exact UTF-16 positions are unavailable if an ignored
prefix cannot be decoded. `AssumeValid` retains the explicit trusted-input bypass.
Removing unnecessary preparation can lower route-specific work requirements;
finite work limits remain enforceable rather than promising fixed instruction counts.
