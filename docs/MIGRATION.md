# Migrating to the 0.4 scalar line

Development is **0.4.0-rc.3-dev**, with no new candidate staged. Frozen **RC2**
is unpublished; its correctness evidence retains its scope and its cumulative cost
gate is inconclusive. See [release notes](https://github.com/Lokad/Utf8Regex/blob/master/CHANGELOG.md). Both adapters require
exactly their own Core version: upgrade selected packages together rather than
overriding dependencies. All target .NET 10.

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
have separate scope. See [host integration](HOST-INTEGRATION.md) for phase/failure
mapping and the Core/PythonRe W+R modeled-payload recipe.

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

Specifications: [Core](https://github.com/Lokad/Utf8Regex/blob/master/docs/SPEC.md),
[PythonRe](https://github.com/Lokad/Utf8Regex/blob/master/docs/SPEC-PYTHONRE.md),
[PCRE2](https://github.com/Lokad/Utf8Regex/blob/master/docs/SPEC-PCRE2.md).
Historical benchmark rows retain their own versions and do not qualify this
development source or promise complete flavor equivalence.
