# Lokad.Utf8Regex.PythonRe

**0.5.0 development**, .NET 10 and .NET 11. A managed Python `re`-style adapter
over strict UTF-8 Unicode scalars, requiring the exact Core package version
matching the adapter. It can coexist with PCRE2 at the same version. No Python
runtime or BCL regex matcher is a shipped implementation dependency.

## Subject text contract

This profile models Python `re` over `str`. Pass already-valid UTF-8 text and keep
borrowed bytes unchanged during use. Validate external bytes where they become
text. Regex operations do not validate the whole subject, and there is no
`inputValidation` option or `InputValidation` property in this adapter.

Pattern, template and capture-name validation remain separate. Managed bounds,
scalar/byte semantics, captures, original cooperative cancellation/deadlines and
resource admission remain. Requested coordinates and output sizing may inspect
text. See the [profile contract](../../docs/SPEC-PYTHONRE.md#subject-text-contract).

The tables below compare existing valid UTF-8 text with CPython str, with equal
requested output materialization. Compilation, input decoding and worker startup
are excluded.

## Supported scope

The profile targets the documented CPython 3.13.2 `str` grammar with owned Unicode
15.1.0 identifier/class/case data. Patterns are parsed before scalar lowering.
Captures are numbered in opening order; references/conditionals, fixed-width
lookbehind, greedy/lazy repetition, scoped flags and Python empty-match retry use
the owned engine. `LastIndex` identifies the last capture to close on the
successful path. The compiled regex exports UTF-8 capture names. Unmatched captures keep
the documented null/empty shaping for each API.

Match exposes a borrowed whole-match byte value and optional caller-owned capture
slots. Utf8PythonMatchMode selects anchored Match (default), Search or FullMatch.
The range cursor supplies incremental captures with immutable byte metadata.
Whole-match ranges and capture memories come from `CreateMatchCursor`; hosts
collect, copy or decode them when needed, outside regex resource controls.
Cursor discovery and snapshots retain cumulative admission and interruption.
Split ranges and byte-template replacement include
capture-aware output shaping and substitution counts. Primary ranges are byte
coordinates. Results have no UTF-16 offsets or byte-alignment flags; scalar
positions are derived by the host when needed.
`TryCopyGroupNameUtf8` exports slot names in Python opening order, including
numeric labels for unnamed groups. It replaces `GetGroupNames` without exposing
program storage. Short buffers report the complete required size; funded copies
require an open regex and observe the original optional token.

`Replace`, `Subn` and funded `SubnRetained` accept UTF-8 template spans.
Encode dynamic text templates in the host or use C# `u8` literals. Cache keys,
literal fragments and capture output remain
bytes. Warm queries borrow their span; cold keys are copied after admission.
Template validation still runs on misses, and template error positions retain
UTF-16 units. The templates use Python's Unicode grammar.
`regex.Subn(input, "[\\g<word>]"u8)` returns owned bytes
and a substitution count.

Bytes-pattern semantics, LOCALE and `\N{...}` Unicode-name lookup are excluded.
The optional scalar-aligned byte endOffsetInBytes bounds the subject prefix; it
does not implement Python character endpos clamping. Start/end retain absolute
prefix/lookbehind context. This is a selected
profile, not complete Python `re` equivalence. See the
[profile specification](../../docs/SPEC-PYTHONRE.md).

## Controls and lifetimes

`EffectiveOptions` exposes validated global flags, including leading inline
directives and implicit Unicode unless global ASCII is selected. Scoped flags
stay scoped. `Options` retains the flags originally supplied by the caller.

Instances accept positive nullable `Utf8ResourcePolicy.MaxWorkSteps`, `MaxDepth`,
`MaxWorkingBytes` and `MaxResultBytes`. Null is unlimited; zero is invalid.
**Defaults remain unlimited.** Work includes matching and
required shaping cumulatively. Depth admits live frames before acquisition;
working/result controls admit modeled major capacities and growth overlap.
Result/staging charges do not refund and may conservatively count storage again.
Caller-owned Match slots require no result-array allocation or result-byte charge;
short buffers request a prefix of the numbered slots and excess entries stay
untouched. Pre-cancellation preserves caller output; failure after capture entry
clears the requested prefix. Returned whole-match bytes survive slot reuse.
Denials use `Utf8ResourceLimitExceededException` with the corresponding
`Execution*` resource kind. This is not a total heap/RSS or caller-retention bound.

Callback replacement and evaluator delegates are removed. Custom output uses
the range cursor and host-owned buffers. Template replacement retains its output
admission, strict template checks and original interruption controls.

Original synchronous cancellation and one absolute
deadline apply through required shaping and persistent cursor advances, including
waits between moves. Host composition and its final output/cancellation checks
belong to the host. Timeouts use `RegexMatchTimeoutException`; its
Input is empty when working or result bytes are bounded to avoid decoding the
subject for diagnostics after expiry.

Constructors and Compile accept one immutable Utf8RegexPolicy. Its ResourcePolicy
and MatchTimeout govern later invocations; ConstructionPolicy and ConstructionTimeout
govern eager creation. Null timeouts use DefaultMatchTimeout for matching and
infinite construction time. Compile accepts a separate synchronous construction
token. Search, Match and FullMatch share one scalar program. A ProgramMemoryAllowance
funds construction before publication and supplies default working admission.
Dispose funded expressions after use. Active calls and cursors keep
their funding until they finish. Refusal never retries without admission.

With no explicit construction controls/funding, constructors can borrow an active
internal construction owner. Explicit Compile and construction/funding controls
start one owner and reject replacing an active one. Invalid policy values report
the policy parameter. Settings are copied; the construction token does not become
execution authority. Host retention/decoding and incidental objects keep their
separate scope. See the [resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits)
and [release notes](../../CHANGELOG.md#upgrading).

Old timeout/resource/construction/funding argument lists are removed. Recompile
callers. Matching/count/output retain their optional offset/count/token signatures;
Match's explicit mode selects search, anchored or full behavior.

`cursor.MoveNext(out Utf8PythonRangeMatch match, token = default)` discovers
capture byte ranges without captured strings or a whole-subject coordinate map.
Keep the returned value; the cursor has no text `Current` or no-output advance.
Its optional token applies to one advance. The creation token, original deadline
and cumulative budgets still apply. Cancellation faults the cursor and releases
scratch; earlier range metadata remains valid.

Range metadata survives advances/disposal. Byte memory remains borrowed and must
stay unchanged with its manager alive while any returned bytes are read. Results
expose byte coordinates; hosts derive character positions and decode or copy
bytes under their own resource and interruption policy. Regex operations prepare
no scalar-coordinate index. Published arrays retain their result charges.
`SplitRanges` provides ordinary segments and captured separators as byte ranges,
including unmatched captures, without decoding or copying their text. These paths
retain finite limits and synchronous cancellation.

`CreateMatchCursor` accepts an optional caller-owned
`IUtf8MemoryAllowance` for shared working admission. Concurrent cursors use that
same allowance directly; refusal throws `Utf8MemoryAdmissionException` before
covered acquisition, and EOF/failure/disposal refund scratch synchronously.
Numeric limits still apply. Compilation, caches and published caller-retained
results need separate funding; this parameter does not imply a total memory cap. Null inherits program funding
or numeric engine limits; non-null refusal never retries without admission.

Named captures remain distinct when Unicode identifiers and names such as `g1`
are mixed. Public names and Python opening-order numbering remain unchanged.
Fixed finite policies omit optional unmetered Core runtime/emission storage and
native prefix buffers those instances cannot execute. Useful search/scalar
preparation remains; lower construction charges can admit earlier-denied patterns.

Optional Core preparations support native iteration,
literal values/results and scalar cursor start hints. The current
grammar-level iteration flag can also retain Core for patterns whose operations
decline it. Tightening that flag is deferred after a bounded cost experiment;
backend presence alone does not establish execution. A Core preparation may still
be built transiently to discover eligibility, under the original construction
owner. Discarding a root reduces retention, not all construction traffic.

Default and unsuccessful range results expose empty capture arrays. Successful
snapshots retain their numbered ranges across later calls.

## Public API

```csharp
using System;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

var regex = new Utf8PythonRegex(
    pattern: @"(?P<word>foo)-(?P=word)",
    options: PythonReCompileOptions.None,
    policy: new Utf8RegexPolicy
    {
        MatchTimeout = TimeSpan.FromSeconds(1),
        ResourcePolicy = new()
        {
            MaxWorkSteps = 1_000_000,
            MaxDepth = 1024,
            MaxWorkingBytes = 16 * 1024 * 1024,
            MaxResultBytes = 16 * 1024 * 1024
        }
    });

bool isMatch = regex.IsMatch("foo-foo"u8);
var first = regex.Match("xx foo-foo yy"u8, mode: Utf8PythonMatchMode.Search);
int count = regex.Count("foo-foo x foo-foo"u8);
byte[] replaced = regex.Replace("foo x foo"u8, "<\\g<word>>"u8);
using var matches = regex.CreateMatchCursor("foo-foo xx foo-foo"u8.ToArray());
while (matches.MoveNext(out var match))
{
    Console.WriteLine($"{match.Value.StartOffsetInBytes}:{match.Value.EndOffsetInBytes}");
}
Utf8PythonCaptureRange[] parts = regex.SplitRanges("foo-foo xx foo-foo"u8);
```

`Match` returns borrowed byte values in all three modes. The former
`ToString` operation variants are removed. Check `Success`, then decode
`GetValueBytes()` in the host if text is needed; caller decoding is outside the
regex invocation's resource and cancellation scope. `FindAllToStrings`,
`FindAllToUtf8`, their shaped result carriers and the legacy structural
`FindAll`/`Utf8PythonMatchData` collector are removed. Cursor ranges borrow
stable, unchanged subject memory; the host owns collected arrays/strings and
their final admission/cancellation checks. `ReplaceToString`
and `SubnToString` are removed too; decode the owned byte-template output when needed.

Use mode: Utf8PythonMatchMode.Match for one anchored capture result or
Utf8PythonMatchMode.FullMatch for one complete-suffix result; Search is the default
and advances successively. An optional scalar-aligned endOffsetInBytes limits the
subject prefix without rebasing byte coordinates or losing lookbehind context.
Dispose after consuming the desired result; snapshots remain valid. The former
SearchDetailed/MatchDetailed/FullMatchDetailed APIs and context/group view types
are removed. `LastIndex` retains Python's last-capture-to-close semantics.
A cursor owns persistent discovery state, so one-shot capture materialization now
pays cursor setup and allocation too. Use Match with caller-owned capture slots
when only byte ranges are required, or omit slots for a whole-match value.
Resolve names on the compiled regex before using the resulting numbers on a snapshot.

`CreateMatchCursor(ReadOnlyMemory<byte>)` discovers one range result per
advance without materializing a complete collection:

```csharp
ReadOnlyMemory<byte> subject = System.Text.Encoding.UTF8.GetBytes("é😀");
using var cursor = new Utf8PythonRegex("(?P<scalar>.)").CreateMatchCursor(subject);
while (cursor.MoveNext(out var match))
{
    // The supplementary scalar occupies four bytes and is one match.
    Console.WriteLine(System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
}
```

Keep the subject unchanged and its memory manager alive while reading borrowed
bytes, including after the cursor closes. Dispose when stopping early. The cursor has one shared position and
lifetime across reference aliases, rejects concurrent/reentrant access, and has
no finalizer. EOF and failure release scratch and subject references immediately.
Returned ranges own immutable metadata and borrow subject bytes; metadata remains
valid after advance or disposal. Sequential advances may cross host calls or asynchronous waits; those
waits count against the original absolute match deadline. The offset/token
overloads preserve scalar-aligned byte offsets and the original invocation token.
Existing span APIs retain their existing contracts.


A cursor has cumulative work/result allowance across all moves. Scratch remains
charged while positioned and releases at EOF/failure/Dispose. Published snapshots
never borrow reusable scratch. Faults preserve earlier snapshots; a new call gets
a new allowance and needs new host admission while earlier results survive.
Core value-cursor copies have independent positions/shared allowance; PythonRe
reference aliases share position/disposal. No finalizer-based lease is required.

Read [release notes](../../CHANGELOG.md). The [PythonRe summary](../../bench/Snapshots/PythonRe.json)
records the source, runtime and qualification of each row.

Use `--measure-pythonre-paired-case iteration/finditer-detailed 9` for a focused
paired comparison over already-valid text. Timing includes equivalent requested
output. The benchmark host collects and decodes byte ranges for detailed, structural
and string split results. It composes constant callback replacement over the range
cursor and decodes byte-template replacement when string output is requested.
CPython includes the corresponding coordinate and output conversion. Capture replay
and digest verification run outside those measurements.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over caller-valid UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation. The paired pass includes required result materialization and measures after startup and pattern compilation. CPython uses a long-lived worker. Use `--measure-pythonre-case` for focused decode-inclusive and .NET controls.

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

FindAllStrings, FindAllUtf8 and FindAllStringsFromOffset rows are historical measurements of the removed FindAllToUtf8 API. They are no longer refreshed and do not qualify current cursor materialization. Structural rows compose the retained cursor in benchmark host code.

Snapshot SHA-256: `881CC0B2E79E32F76273AF3B8F375D542065EC23074EB6D90A33C3585062FC9E`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `58` managed faster, `0` equivalent, `8` CPython faster, `9` inconclusive, `1` unqualified.

Measured source `d657d7e61696`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `anchor/fullmatch-miss` | `FullMatch` | 0.281 us | 0.206 us | CPython faster | 1.36x |
| `atomic/search-miss` | `Search` | 0.385 us | 0.290 us | CPython faster | 1.33x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.298 us | 0.226 us | CPython faster | 1.32x |
| `lookbehind/search-miss` | `Search` | 0.293 us | 0.247 us | CPython faster | 1.18x |
| `reluctant/search-hit` | `Search` | 0.507 us | 0.442 us | CPython faster | 1.15x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.406 us | 0.380 us | CPython faster | 1.07x |
| `common/date-miss` | `IsMatch` | 0.787 us | 0.852 us | Managed faster | 0.92x |
| `atomic/search-hit` | `Search` | 0.356 us | 0.407 us | Managed faster | 0.88x |
| `possessive/search-hit` | `Search` | 0.341 us | 0.391 us | Managed faster | 0.87x |
| `common/ip-match` | `IsMatch` | 0.273 us | 0.320 us | Managed faster | 0.85x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.311 us | 0.386 us | Managed faster | 0.81x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.298 us | 0.373 us | Managed faster | 0.80x |
| `lookahead/search-miss` | `Search` | 0.178 us | 0.224 us | Managed faster | 0.79x |
| `leading-inline/search-hit` | `Search` | 0.321 us | 0.412 us | Managed faster | 0.78x |
| `scoped-inline/search-hit` | `Search` | 0.286 us | 0.371 us | Managed faster | 0.77x |
| `prefix/match` | `Match` | 0.254 us | 0.357 us | Managed faster | 0.71x |
| `alternation/search-miss` | `Search` | 0.176 us | 0.263 us | Managed faster | 0.67x |
| `lookbehind/search-hit` | `Search` | 0.274 us | 0.412 us | Managed faster | 0.67x |
| `unicode-category/search-miss` | `Search` | 0.159 us | 0.242 us | Managed faster | 0.65x |
| `ignorecase/search-hit` | `Search` | 0.269 us | 0.434 us | Managed faster | 0.62x |
| `multiline/search-hit` | `Search` | 0.243 us | 0.412 us | Managed faster | 0.59x |
| `ignorecase/search-miss` | `Search` | 0.182 us | 0.312 us | Managed faster | 0.59x |
| `ascii-option/search-hit` | `Search` | 0.203 us | 0.394 us | Managed faster | 0.52x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.154 us | 0.307 us | Managed faster | 0.50x |
| `greedy/search-miss` | `Search` | 0.116 us | 0.245 us | Managed faster | 0.47x |
| `possessive/search-miss` | `Search` | 0.106 us | 0.240 us | Managed faster | 0.45x |
| `unicode-option/search-hit` | `Search` | 0.265 us | 0.635 us | Managed faster | 0.42x |
| `lookahead/search-hit` | `Search` | 0.157 us | 0.378 us | Managed faster | 0.41x |
| `literal/search-early` | `Search` | 0.094 us | 0.299 us | Managed faster | 0.32x |
| `unicode-literal/search-miss` | `Search` | 0.088 us | 0.315 us | Managed faster | 0.28x |
| `unicode/fullmatch` | `FullMatch` | 3.976 us | 26.559 us | Managed faster | 0.15x |
| `literal/fullmatch` | `FullMatch` | 0.059 us | 0.425 us | Managed faster | 0.14x |
| `unicode-literal/search-hit` | `Search` | 0.077 us | 0.630 us | Managed faster | 0.12x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.070 us | 0.593 us | Managed faster | 0.12x |
| `literal/search-miss` | `Search` | 1.084 us | 22.090 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.097 us | 22.317 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.066 us | 22.355 us | Managed faster | 0.05x |
| `multiline/search-miss` | `Search` | 0.293 us | 0.292 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.777 us | 1.683 us | Managed faster | 0.46x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 97.916 us | 219.675 us | Managed faster | 0.45x |
| `count/empty-progression` | `Count` | 149.141 us | 366.141 us | Managed faster | 0.41x |
| `count/from-offset` | `CountFromOffset` | 17.320 us | 49.295 us | Managed faster | 0.35x |
| `zero-width/count` | `Count` | 386.440 us | 1,100.561 us | Managed faster | 0.35x |
| `family/count` | `Count` | 552.535 us | 2,460.621 us | Managed faster | 0.22x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 133.065 us | 644.978 us | Managed faster | 0.21x |
| `unicode/count` | `Count` | 47.960 us | 428.611 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 267.164 us | 2,663.262 us | Managed faster | 0.10x |
| `count/one` | `Count` | 0.180 us | 1.954 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.301 us | 90.467 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.269 us | 142.640 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.190 us | 0.476 us | Inconclusive | 2.49x |
| `replacement/empty-progression-string` | `ReplaceString` | 29.451 us | 23.757 us | CPython faster | 1.24x |
| `replacement/named-template-string` | `ReplaceString` | 0.915 us | 0.954 us | Managed faster | 0.95x |
| `replacement/unmatched-template-string` | `ReplaceString` | 44.440 us | 47.440 us | Managed faster | 0.94x |
| `replacement/fixed-string` | `ReplaceString` | 149.788 us | 174.633 us | Inconclusive | 0.85x |
| `replacement/subn-string` | `SubnString` | 72.562 us | 90.233 us | Inconclusive | 0.81x |
| `replacement/no-match-string` | `ReplaceString` | 1.910 us | 2.382 us | Inconclusive | 0.80x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 50.644 us | 64.126 us | Managed faster | 0.79x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 133.893 us | 175.598 us | Managed faster | 0.76x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.500 us | 89.808 us | Managed faster | 0.76x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 197.358 us | 275.491 us | Managed faster | 0.72x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 195.117 us | 278.150 us | Managed faster | 0.70x |
| `replacement/one-template-string` | `ReplaceString` | 2.249 us | 16,403.751 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.233 us | 0.568 us | Inconclusive | 2.18x |
| `split/unicode` | `SplitStrings` | 50.699 us | 39.233 us | CPython faster | 1.29x |
| `split/captures` | `SplitStrings` | 276.535 us | 264.338 us | Inconclusive | 1.04x |
| `split/no-captures` | `SplitStrings` | 222.702 us | 229.601 us | Managed faster | 0.97x |
| `split/zero-width` | `SplitStrings` | 71.656 us | 76.839 us | Managed faster | 0.93x |
| `split/optional-capture` | `SplitStrings` | 62.191 us | 82.613 us | Managed faster | 0.75x |
| `split/detailed` | `SplitDetailed` | 63.714 us | 135.576 us | Managed faster | 0.47x |
| `split/one-separator` | `SplitStrings` | 0.986 us | 11.889 us | Inconclusive | 0.08x |
| `split/no-separator` | `SplitStrings` | 1.306 us | 24.669 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 71.436 us | 393.350 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 79.733 us | 619.083 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 51.357 us | 575.876 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 967.837 us | 352,233.899 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `multiline/search-miss`: Removable harness floors are 0.18 %/11.58 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 103.888 us / 49,785 B | 59.770 us | 106.519 us / 49,849 B | 59.862 us |
| `fallback` | 161.247 us / 57,081 B | 41.563 us | 143.503 us / 57,081 B | 40.418 us |
| `literal` | 105.447 us / 20,337 B | 24.540 us | 95.356 us / 20,337 B | 24.747 us |
| `unicode` | 78.353 us / 26,537 B | 36.515 us | 77.366 us / 26,537 B | 36.060 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
