# Core contract

`Lokad.Utf8Regex` matches Unicode scalars over UTF-8 on .NET 10 and 11.
Its coordinates are byte offsets. [API snapshot](../tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt) ·
[Release notes](../CHANGELOG.md) · [Upgrading](../CHANGELOG.md#upgrading)

## Matching and coordinates

Dot consumes one scalar subject to newline options. Quantifiers repeat atoms.
Combining marks remain separate scalars. Captures, references, lookarounds and
advancement respect scalar boundaries. Discovery preserves branch order and
greedy or lazy preference. Right-to-left mode changes discovery direction.

| Operation | Result |
| --- | --- |
| Dot on `😀` | One four-byte match |
| Dot on `a😀b` | Byte offsets and lengths `(0, 1)`, `(1, 4)` and `(5, 1)` |
| Empty pattern on `😀` | Empty matches at byte offsets 0 and 4 |
| `^(.)\1$` on `😀😀` | Success, with the first scalar in group 1 |
| `a\|aa` on `aa` | `a`, following alternative order |

`IsMatch` and `Count` start at the default edge. Use `Match` to check for a
match from a byte offset. Use `EnumerateMatches` to count from one. In RTL,
an explicit start is the initial search ceiling. `Match` accepts an optional
end bound. Starts and ends must be scalar boundaries between zero and the
subject length, with start at or below end. An omitted start selects zero
forward or the selected end in RTL. Anchors, boundaries and lookbehind retain
prefix context. End anchors and lookahead observe the bounded end. Invalid
bounds throw `ArgumentOutOfRangeException`.

### Caller-owned capture slots

`Match` returns a `Utf8CaptureRange` and can write each group's last capture
into a caller span. Compact slot zero is the whole match.
`GroupNumberFromCaptureSlot` maps slots to group numbers. Short spans preserve
match and capture selection. Extra entries remain untouched. A nonparticipating
group has a default range. A participating empty group has equal byte endpoints
and `Success = true`.

Pre-cancellation or a closed funded expression leaves the span unchanged.
A miss or failure after entry clears the defined window. Scratch needs
admission and refunds synchronously. The caller span has no result-byte
charge. Backtracking programs without capture readers record only the
requested groups. Copied ranges survive later operations and disposal.

For history, use `CreateMatchCursor` over stable memory. `Utf8RangeMatch.Value`
is the whole range. `GroupNumbers` lists group numbers. `TryGetGroup` gives
the last capture and `GetCaptures` gives the closing-order history.
Resolve names using the compiled expression. Results borrow text and own
immutable range metadata.

## Input and pattern language

### Subject input contract

Supply already-valid UTF-8 and keep borrowed bytes unchanged during operations
and while using borrowed results. Validate external bytes where they become
text. Matching trusts this input guarantee. Pattern, capture-name, template
and assembled-output checks are independent.

Core supports .NET-style literals, classes and subtraction, escapes, inline
flags, greedy and lazy repetition, captures and balancing groups, references,
conditionals, lookarounds, contiguous anchors, RTL and supported `ECMAScript`.
Compile options use `RegexOptions`. Unsupported syntax and option combinations
fail during construction. `AnyNewLine` is unsupported. Vertical tab stays
literal in verbose mode. Legacy XML classes retain their .NET 10 memberships.

Adjacent raw or escaped surrogate halves join within a literal run or class
member. A group, assertion or class boundary separates them. Unpaired
surrogates are impossible atoms. Zero repetitions remain possible and negation
uses the scalar complement. Class ranges use scalar order through U+10FFFF
and exclude the surrogate gap. `[\u0000-\uffff]` covers BMP scalars.
Singleline dot covers every plane. Finite repeat maximum 2147483647 is distinct
from infinity. Impossible classes and zero-width repetitions retain their
syntax and capture declarations with bounded construction.

Byte compilation validates strict UTF-8. Construction borrows the pattern,
then admits and copies retained source and literal bytes before publication.
The caller can reuse its buffer after return. Reading `Pattern` decodes a byte
source on each access. Syntax errors use `ArgumentException` with a reason and
source offset. Text errors identify `pattern` in UTF-16 units and byte errors
identify `patternUtf8` in bytes. Interruption or admission can preempt checks
of a malformed tail. Representation and resource failures remain distinct.

`NonBacktracking` provides linear discovery for its admitted regular subset.
It preserves branch priority, greedy and lazy preference, scalar empty
progress and the last completed capture in each group. Unsupported syntax
fails during construction. Discovery and capture replay share the invocation's
limits and interruption checks. Replay keeps byte offsets and boundary context
in the original subject. A later refusal leaves prior cursor results valid.

### Native pattern metadata

`CaptureGroupCount` counts defined slots excluding zero. Shared declarations
count once, and sparse numbers keep their identity. Compact ordinals run from
zero through `CaptureGroupCount`. Use `GroupNumberFromCaptureSlot` to read
the group number and `TryCopyGroupNameUtf8` to export its name. Unnamed
slots use their actual decimal number.

`TryCopyPatternUtf8` copies the original source. The copy APIs report the
required byte count. `Done` writes the complete value. `DestinationTooSmall`
leaves the buffer unchanged. Unpaired surrogates in a text pattern yield
`InvalidData` with zero required bytes and an unchanged buffer. Cancellation
can leave a partial copy.

`GroupNumberFromUtf8Name` validates all bytes and returns the exact group
number or −1. Dense numeric-only catalogs accept leading zeros. Named and
sparse catalogs use exact keys without normalization. A malformed key throws
`ArgumentException` with a byte offset.

Metadata uses its own token independently of match timeouts and budgets.
Entry cancellation precedes range and funded-open checks. Each call on a
funded expression holds admission through completion. Copied bytes survive
disposal. Separate calls can straddle program close.

## Case and culture

`CultureInvariant` is the primary supported/performance-tested mode.
Culture-sensitive folding captures construction culture, including inline scopes
and cache identity. The expression keeps that culture across ambient changes.

| I-cluster mode | Equivalent members |
| --- | --- |
| Invariant | I/i pair. İ and ı remain distinct. |
| Non-Turkic culture | I/i/İ are equivalent. ı remains distinct. |
| Turkic culture | I/ı and İ/i |

Case-equivalence mappings use owned Unicode 16.0 data for BMP and supplementary
scalars. General categories/shorthands use runtime scalar properties.
[PythonRe](SPEC-PYTHONRE.md) owns its
separate Unicode 15.1 rules.

## Results, splitting and replacement

`Utf8CaptureRange` stores byte coordinates. The default has `Success = false`
and zero endpoints. A successful empty range has equal endpoints. The same
type represents whole matches and captures. Borrowed views require unchanged
input. Scalar matching performs no normalization.

`EnumerateSplits` yields scalar-aligned borrowed segments with byte starts
and lengths. Count defaults to unlimited, including explicit zero. Count
one yields the whole input. Delimiter captures stay out of the split
sequence. Scalar empty progress and RTL follow the selected mode. Borrowed
split values acquire no result payload.

`Replace` returns owned bytes and `TryReplace` fills a caller destination.
Templates are strict UTF-8 and support .NET-style `$1`, `${name}`, `$&`,
prefix and suffix, `$+`, `$_` and `$$`. Capture references use the last
capture. RTL assembles output in subject order. Use capture cursors for
host-defined substitutions. Host copying and decoding belong to the caller.

`TryReplace` returns `Done` or `DestinationTooSmall`. Shortage writes zero
bytes and leaves the destination unchanged. Overlap is supported and
overlapping input requires staging. Other failures throw. Interruption or
work exhaustion during final copying can leave a partial destination.
Staging and output capacities count toward the applicable working and result
limits.

## Cancellation, deadlines and resource limits

A pre-canceled token wins before invocation validation and program access.
Cooperative checks cover bounded input inspection, search, matching, copying
and publication, including a failed advance before reporting EOF.
`OperationCanceledException` carries the token that triggered it. Invocation
tokens never become part of the compiled program or a cached plan.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8Regex("cat|horse");
regex.IsMatch("cat horse"u8, canceled.Token); // throws with canceled.Token
```

One absolute deadline covers a call, its cold replacement preparation and
waits between cursor advances. It is enforced cooperatively, with no
preemption of scheduling, GC or JIT work. Timeout throws
`RegexMatchTimeoutException`. Its `Input` is empty with a finite working
or result byte limit, on host-funded matching paths, and for memory cursors.
Other Core calls can include the decoded input in the diagnostic.

### Numeric limits

`Utf8ResourcePolicy` fields accept positive values. Null leaves that field
unlimited and zero is invalid. All fields default to unlimited.

| Policy | Covered scope | Denial kind |
| --- | --- | --- |
| `MaxWorkSteps` | Cumulative modeled engine work, including bounded scans and publication | `ExecutionWorkSteps` |
| `MaxDepth` | Live backtracking, continuation or assertion state, including logical depth | `ExecutionDepth` |
| `MaxWorkingBytes` | Live major scratch capacities, including growth overlap | `ExecutionWorkingBytes` |
| `MaxResultBytes` | Cumulative modeled result and staging acquisitions | `ExecutionResultBytes` |

Denial throws `Utf8ResourceLimitExceededException` with the resource kind,
limit and requested total. Totals saturate and the first denial remains in
effect across cursor copies. A denied acquisition never publishes a partial
successful result. Search, retries, and direct matches retain bounded work
and interruption checks. Depth includes logical assertion state even when
the engine allocates no continuation. Charges vary with the execution route
and library version. Recheck finite policies when upgrading.
If NonBacktracking needs a scratch array beyond the runtime's maximum array
length, it reports the capacity failure as `InvalidOperationException` before
allocating that array.

Exact backreferences compare captured bytes in the search direction. Folded
backreferences compare scalar equivalents. Both use bounded execution checks.

The engine admits covered storage before acquisition. It covers capture
and projection arrays, cursor tables, output staging and finalized result
storage. A shared or temporary allocation may count more than once.
Fixed scalar values, borrowed inputs and programs, caller destinations,
incidental helpers, standalone getters and host retention fall outside these
limits. Caps bound modeled capacities rather than process heap or RSS.
PythonRe uses the shared policy with its [profile-specific scopes](SPEC-PYTHONRE.md#construction-and-execution-controls).
See the [PCRE2 vocabulary and mapping](SPEC-PCRE2.md#interruption-and-resource-controls)
for its distinct failures.

### Program and working allowances

Set `ProgramMemoryAllowance` on `Utf8RegexPolicy` for shared admission of
covered compiled program storage. Core reserves conservative capacity for its
bounded replacement cache before publishing a funded program (4 MiB payload
and 64 KiB metadata). This reservation does not eagerly allocate the cache.
Disposal closes a funded program to new calls and refunds storage after
admitted calls and persistent cursors finish. Unfunded disposal is a no-op.
Pattern, options and policy properties remain readable after disposal.
Program-dependent metadata operations require an open funded expression.

`IsMatch`, `Match`, `Count`, `CreateMatchCursor` and `TryReplace` accept a
working allowance. Null inherits the program account if present. An explicit
account overrides working admission only. Span enumerators and owned-byte
`Replace` retain numeric limits and hold program admission while executing.
The numeric working limit is checked before shared working admission. Covered
storage requires both. A shared refusal throws `Utf8MemoryAdmissionException`
and the operation does not retry without funding. Temporary loans refund
synchronously on completion, failure or interruption.

Cold `TryReplace` preparation uses the same working allowance and absolute
deadline as matching and output. Covered key and parser capacity stays funded
through output, including for an uncached plan. The program account covers
retained cache capacity. Warm reuse can need less working storage. The existing
[replacement contract](#results-splitting-and-replacement) governs caller buffers
and interruption during the final copy.

### Cursor and result lifetimes

Copies of span match and split enumerators have independent positions and share
numeric limits and the original deadline. Concurrent advances are unsupported.
Creation and each advance borrow an open funded regex with no shared working
loan between calls. Original cancellation precedes the closed-program check.
Close rejects later advances on every copy. Returned ranges and views remain
usable while the borrowed subject lives unchanged. Exclusive scratch is refunded
synchronously. Numeric reservations for shared published state persist for the
invocation without keeping abandoned payloads alive.

A memory cursor can cross calls or awaits. It retains admitted program use
through regex close, and useful working state counts against its working limit
until released. Aliases share position and disposal. Concurrent or reentrant
access is rejected. The creation token, deadline and cumulative work/result
traffic continue to apply. An optional advance token applies to that advance
only, with the creation token winning when both are canceled. EOF, failure and
explicit disposal release exclusive scratch and program use synchronously.
Dispose an abandoned cursor.

`MoveNext` returns immutable byte ranges and group metadata that survive cursor
advance, failure and closure. The subject and its memory manager stay borrowed.

Ordinary publication ends working admission for the range result.
`MoveNextRetained` requires shared working funding and returns a
`Utf8RetainedRangeMatch`. Captures, group numbers and the receipt are admitted.
By default the staging loan transfers to the receipt. If a separate result
account is supplied, it reserves before staging is refunded. Refusal is
terminal. Closing the receipt refunds once. Result copies remain valid under
the caller's retention responsibility, and cumulative numeric result charges
persist even when a live loan refunds.

### Host admission

For Core or PythonRe with finite working cap W and result cap R, a host can
reserve `checked(W + R)` before an operation. Refuse entry if a cap is unlimited,
the sum overflows, or the reservation fails. W bounds live covered working
capacity and R bounds cumulative covered result capacity. The sum conservatively
covers their union even if the model counts a buffer twice.

Hold the reservation while covered results or cursor aliases survive, or
transfer their admission to another host owner. Fund programs, pools, shared
caches, subjects and host output separately. Admission before engine entry
needs a host deadline. This recipe describes modeled capacities. PCRE2 uses
the [profile's own limit scopes](SPEC-PCRE2.md#interruption-and-resource-controls).

## Construction, preparation and caches

The host owns each compiled expression. `Utf8RegexPolicy` sets construction
limits and timeout independently of match limits and timeout. Null construction
timeout means infinite. Null match timeout uses `DefaultMatchTimeout`.
`Compile` accepts a separate construction token and observes pre-cancellation
before examining the policy or pattern. Nested construction shares its scope.
Construction authority ends at publication. Text construction and text
`Compile` default to `RegexOptions.CultureInvariant`. UTF-8 `Compile` defaults
to `RegexOptions.None`.

| Fixed construction backstop | Ceiling |
| --- | ---: |
| Instruction slots | 1,048,576 |
| Covered bytes | 64 MiB |
| Work units | 67,108,864 |
| Live frames | 65,536 |

Positive construction policy values tighten the backstops and null keeps
the defaults. A program allowance handles byte admission for funded creation,
including an optional `MaxBytes`. Denials use `ConstructionWorkSteps`,
`ConstructionDepth`, `ConstructionInstructionCapacity` or `ConstructionBytes`.
Construction timeout has empty `Input` and uses the supplied text `Pattern`
or an empty pattern for byte source diagnostics.

Admission precedes major buffers, instruction expansion and growth overlap.
Construction work limits and interruption cover parsing, lowering and emission.
Covered work and byte charges can change with compiler algorithms and storage layouts.
Unpublished storage is released on failure. Replacement preparation uses
its own construction backstops within the original invocation deadline.
Finite working bytes also cover cold preparation and can cause
`ConstructionBytes` before matching. Interrupted preparation does not
publish a partial plan. Bounded caches retain immutable keys and lowered plans
under conservative charges. Active users keep their admitted plans through
eviction. See the [PythonRe contract](SPEC-PYTHONRE.md#construction-and-execution-controls)
and [PCRE2 contract](SPEC-PCRE2.md#interruption-and-resource-controls) for their
profile scopes.


## Implementation and qualification

Managed code performs parsing, preparation and execution. An immutable program
drives each invocation. Shipped matching and result publication are owned by the
library and use managed bounds checks. Actual `Unsafe.SizeOf<T>()` is permitted.
`Compiled` affects strategy only. General patterns use the scalar interpreter.
Eligible literal families can use emitted code when dynamic code is enabled.
NativeAOT and trimming remain outside qualification.

Byte search uses the same scalar semantics, including options, captures, branch
order, widths, assertions, case equivalences and match progress.
See [architecture](../src/Lokad.Utf8Regex/ARCHITECTURE.md) and
[search complexity](../src/Lokad.Utf8Regex/Internal/Search/COMPLEXITY.md).

Qualify scalar boundaries, grammar, captures, RTL/NonBacktracking, pattern and
replacement validation, interruption, admission and lifetimes through public
fixtures. Benchmark equal-result workloads with warm throughput against both
strict-decode and predecoded controls. See the current
[benchmark summary](../bench/Snapshots/Core.json).
