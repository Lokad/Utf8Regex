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
byte[] replaced = regex.Replace("foo-foo x foo-foo"u8, "<\\g<word>>"u8);
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

`Match` defaults to one anchored result. Use `Utf8PythonMatchMode.Search` to seek
from the specified start, or `Utf8PythonMatchMode.FullMatch` to match the complete
suffix. An optional scalar-aligned `endOffsetInBytes` limits the subject prefix
while preserving absolute byte coordinates and lookbehind context.
Dispose after consuming the desired result; snapshots remain valid. The former
SearchDetailed/MatchDetailed/FullMatchDetailed APIs and context/group view types
are removed. `LastIndex` retains Python's last-capture-to-close semantics.
A cursor owns persistent discovery state. Use `Match` with caller-owned capture
slots for one result's byte ranges, or omit slots for a whole-match value.
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

Snapshot SHA-256: `2D13330E0BFFE0CCC0777AAB537D7A677457F18118CDA966E4185772A0784143`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `60` managed faster, `0` equivalent, `6` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `a4067cf75ac9`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `atomic/search-miss` | `Search` | 0.334 us | 0.293 us | CPython faster | 1.15x |
| `reluctant/search-hit` | `Search` | 0.452 us | 0.426 us | CPython faster | 1.06x |
| `common/ip-match` | `IsMatch` | 0.330 us | 0.318 us | CPython faster | 1.04x |
| `common/date-miss` | `IsMatch` | 0.725 us | 0.846 us | Managed faster | 0.86x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.317 us | 0.379 us | Managed faster | 0.84x |
| `multiline/search-miss` | `Search` | 0.242 us | 0.290 us | Managed faster | 0.83x |
| `atomic/search-hit` | `Search` | 0.295 us | 0.406 us | Managed faster | 0.73x |
| `possessive/search-hit` | `Search` | 0.276 us | 0.388 us | Managed faster | 0.71x |
| `leading-inline/search-hit` | `Search` | 0.263 us | 0.421 us | Managed faster | 0.63x |
| `scoped-inline/search-hit` | `Search` | 0.226 us | 0.369 us | Managed faster | 0.61x |
| `lookahead/search-miss` | `Search` | 0.134 us | 0.221 us | Managed faster | 0.61x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.220 us | 0.369 us | Managed faster | 0.60x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.230 us | 0.388 us | Managed faster | 0.59x |
| `alternation/search-miss` | `Search` | 0.153 us | 0.261 us | Managed faster | 0.59x |
| `lookbehind/search-hit` | `Search` | 0.224 us | 0.414 us | Managed faster | 0.54x |
| `prefix/match` | `Match` | 0.188 us | 0.353 us | Managed faster | 0.53x |
| `unicode-category/search-miss` | `Search` | 0.121 us | 0.236 us | Managed faster | 0.51x |
| `ignorecase/search-hit` | `Search` | 0.213 us | 0.433 us | Managed faster | 0.49x |
| `multiline/search-hit` | `Search` | 0.194 us | 0.408 us | Managed faster | 0.48x |
| `ignorecase/search-miss` | `Search` | 0.141 us | 0.311 us | Managed faster | 0.45x |
| `ascii-option/search-hit` | `Search` | 0.154 us | 0.389 us | Managed faster | 0.39x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.117 us | 0.303 us | Managed faster | 0.39x |
| `greedy/search-miss` | `Search` | 0.088 us | 0.250 us | Managed faster | 0.35x |
| `unicode-option/search-hit` | `Search` | 0.229 us | 0.647 us | Managed faster | 0.35x |
| `possessive/search-miss` | `Search` | 0.078 us | 0.235 us | Managed faster | 0.33x |
| `lookahead/search-hit` | `Search` | 0.120 us | 0.382 us | Managed faster | 0.31x |
| `unicode-literal/search-miss` | `Search` | 0.075 us | 0.315 us | Managed faster | 0.24x |
| `literal/search-early` | `Search` | 0.069 us | 0.295 us | Managed faster | 0.23x |
| `unicode/fullmatch` | `FullMatch` | 4.682 us | 26.378 us | Managed faster | 0.18x |
| `literal/fullmatch` | `FullMatch` | 0.053 us | 0.421 us | Managed faster | 0.12x |
| `unicode-literal/search-hit` | `Search` | 0.068 us | 0.630 us | Managed faster | 0.11x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.055 us | 0.597 us | Managed faster | 0.09x |
| `literal/ismatch` | `IsMatch` | 1.058 us | 21.788 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.074 us | 22.247 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.071 us | 22.201 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.192 us | 0.207 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.230 us | 0.224 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.242 us | 0.246 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.613 us | 1.670 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 86.161 us | 220.522 us | Managed faster | 0.39x |
| `zero-width/count` | `Count` | 395.261 us | 1,084.491 us | Managed faster | 0.36x |
| `count/empty-progression` | `Count` | 127.105 us | 360.018 us | Managed faster | 0.35x |
| `count/from-offset` | `CountFromOffset` | 16.897 us | 48.995 us | Managed faster | 0.34x |
| `family/count` | `Count` | 792.894 us | 2,482.563 us | Managed faster | 0.32x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 117.387 us | 638.225 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 47.888 us | 429.827 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 283.558 us | 2,639.645 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.175 us | 1.934 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.290 us | 89.553 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.267 us | 139.154 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.988 us | 0.471 us | Inconclusive | 2.07x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.007 us | 23.049 us | CPython faster | 1.13x |
| `replacement/unmatched-template-string` | `ReplaceString` | 42.333 us | 46.877 us | Managed faster | 0.90x |
| `replacement/named-template-string` | `ReplaceString` | 0.832 us | 0.937 us | Managed faster | 0.88x |
| `replacement/fixed-string` | `ReplaceString` | 143.030 us | 171.689 us | Managed faster | 0.83x |
| `replacement/subn-string` | `SubnString` | 72.650 us | 89.954 us | Managed faster | 0.81x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 135.902 us | 177.050 us | Managed faster | 0.77x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.112 us | 89.010 us | Managed faster | 0.75x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 46.349 us | 64.028 us | Managed faster | 0.73x |
| `replacement/no-match-string` | `ReplaceString` | 1.690 us | 2.381 us | Inconclusive | 0.71x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 183.091 us | 272.107 us | Managed faster | 0.67x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 181.533 us | 274.842 us | Managed faster | 0.66x |
| `replacement/one-template-string` | `ReplaceString` | 1.734 us | 16,258.969 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.059 us | 0.569 us | Inconclusive | 1.89x |
| `split/unicode` | `SplitStrings` | 51.807 us | 38.886 us | CPython faster | 1.35x |
| `split/captures` | `SplitStrings` | 294.679 us | 260.614 us | Inconclusive | 1.13x |
| `split/no-captures` | `SplitStrings` | 255.954 us | 230.221 us | CPython faster | 1.11x |
| `split/zero-width` | `SplitStrings` | 71.845 us | 76.060 us | Managed faster | 0.94x |
| `split/optional-capture` | `SplitStrings` | 55.106 us | 81.746 us | Managed faster | 0.68x |
| `split/detailed` | `SplitDetailed` | 65.728 us | 133.684 us | Managed faster | 0.49x |
| `split/one-separator` | `SplitStrings` | 0.804 us | 11.811 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.049 us | 24.530 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 68.702 us | 393.198 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 80.192 us | 624.625 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 51.411 us | 568.474 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 936.015 us | 354,739.580 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `anchor/fullmatch-miss`: Removable harness floors are 0.23 %/16.41 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.19 %/15.03 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.18 %/13.76 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 106.088 us / 48,657 B | 59.521 us | 104.119 us / 48,721 B | 59.579 us |
| `fallback` | 162.841 us / 56,041 B | 42.576 us | 134.403 us / 56,041 B | 40.772 us |
| `literal` | 100.875 us / 19,705 B | 25.310 us | 92.084 us / 19,705 B | 25.161 us |
| `unicode` | 77.225 us / 25,905 B | 36.956 us | 73.275 us / 25,905 B | 36.589 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
