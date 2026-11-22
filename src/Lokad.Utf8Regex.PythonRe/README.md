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

Snapshot SHA-256: `3294BAB3AEFE778ECD520A5C0672D2DAEB80C523D8CEC6801AFE5E305B3E7642`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `59` managed faster, `0` equivalent, `9` CPython faster, `7` inconclusive, `1` unqualified.

Measured source `8e6de36759da`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.284 us | 0.217 us | CPython faster | 1.32x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.267 us | 0.205 us | CPython faster | 1.30x |
| `atomic/search-miss` | `Search` | 0.353 us | 0.280 us | CPython faster | 1.26x |
| `lookbehind/search-miss` | `Search` | 0.275 us | 0.238 us | CPython faster | 1.16x |
| `reluctant/search-hit` | `Search` | 0.462 us | 0.414 us | CPython faster | 1.12x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.403 us | 0.367 us | CPython faster | 1.10x |
| `common/date-miss` | `IsMatch` | 0.761 us | 0.817 us | Managed faster | 0.93x |
| `common/ip-match` | `IsMatch` | 0.259 us | 0.309 us | Managed faster | 0.84x |
| `lookahead/search-miss` | `Search` | 0.176 us | 0.215 us | Managed faster | 0.82x |
| `lookbehind/search-hit` | `Search` | 0.320 us | 0.398 us | Managed faster | 0.80x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.288 us | 0.361 us | Managed faster | 0.80x |
| `possessive/search-hit` | `Search` | 0.298 us | 0.379 us | Managed faster | 0.78x |
| `scoped-inline/search-hit` | `Search` | 0.278 us | 0.358 us | Managed faster | 0.77x |
| `leading-inline/search-hit` | `Search` | 0.304 us | 0.405 us | Managed faster | 0.76x |
| `atomic/search-hit` | `Search` | 0.318 us | 0.426 us | Managed faster | 0.75x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.264 us | 0.374 us | Managed faster | 0.71x |
| `prefix/match` | `Match` | 0.237 us | 0.343 us | Managed faster | 0.69x |
| `unicode-category/search-miss` | `Search` | 0.159 us | 0.232 us | Managed faster | 0.69x |
| `alternation/search-miss` | `Search` | 0.165 us | 0.254 us | Managed faster | 0.65x |
| `ignorecase/search-miss` | `Search` | 0.183 us | 0.303 us | Managed faster | 0.60x |
| `ignorecase/search-hit` | `Search` | 0.255 us | 0.423 us | Managed faster | 0.60x |
| `multiline/search-hit` | `Search` | 0.229 us | 0.401 us | Managed faster | 0.57x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.150 us | 0.294 us | Managed faster | 0.51x |
| `greedy/search-miss` | `Search` | 0.113 us | 0.239 us | Managed faster | 0.47x |
| `ascii-option/search-hit` | `Search` | 0.174 us | 0.374 us | Managed faster | 0.46x |
| `possessive/search-miss` | `Search` | 0.103 us | 0.229 us | Managed faster | 0.45x |
| `lookahead/search-hit` | `Search` | 0.155 us | 0.366 us | Managed faster | 0.42x |
| `unicode-option/search-hit` | `Search` | 0.262 us | 0.617 us | Managed faster | 0.42x |
| `unicode-literal/search-miss` | `Search` | 0.080 us | 0.306 us | Managed faster | 0.26x |
| `literal/search-early` | `Search` | 0.075 us | 0.290 us | Managed faster | 0.26x |
| `literal/fullmatch` | `FullMatch` | 0.059 us | 0.413 us | Managed faster | 0.14x |
| `unicode/fullmatch` | `FullMatch` | 3.651 us | 25.603 us | Managed faster | 0.14x |
| `unicode-literal/search-hit` | `Search` | 0.073 us | 0.630 us | Managed faster | 0.12x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.058 us | 0.582 us | Managed faster | 0.10x |
| `literal/search-miss` | `Search` | 1.036 us | 21.286 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.045 us | 21.589 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.008 us | 21.187 us | Managed faster | 0.05x |
| `multiline/search-miss` | `Search` | 0.276 us | 0.280 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.698 us | 1.636 us | Inconclusive | 0.43x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 89.932 us | 211.636 us | Managed faster | 0.43x |
| `count/from-offset` | `CountFromOffset` | 16.295 us | 47.109 us | Managed faster | 0.35x |
| `zero-width/count` | `Count` | 351.353 us | 1,048.907 us | Managed faster | 0.33x |
| `count/empty-progression` | `Count` | 113.074 us | 349.341 us | Managed faster | 0.33x |
| `family/count` | `Count` | 532.257 us | 2,407.971 us | Managed faster | 0.22x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 115.931 us | 613.526 us | Managed faster | 0.19x |
| `unicode/count` | `Count` | 45.896 us | 399.964 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 276.547 us | 2,475.804 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.171 us | 1.876 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.285 us | 86.276 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.257 us | 134.815 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.894 us | 0.448 us | Inconclusive | 1.99x |
| `replacement/empty-progression-string` | `ReplaceString` | 23.802 us | 22.560 us | CPython faster | 1.06x |
| `replacement/named-template-string` | `ReplaceString` | 0.836 us | 0.883 us | Managed faster | 0.95x |
| `replacement/unmatched-template-string` | `ReplaceString` | 39.489 us | 45.950 us | Managed faster | 0.86x |
| `replacement/subn-string` | `SubnString` | 69.693 us | 85.391 us | Managed faster | 0.82x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 130.033 us | 167.774 us | Managed faster | 0.77x |
| `replacement/subn-utf8` | `SubnUtf8` | 65.214 us | 85.632 us | Managed faster | 0.76x |
| `replacement/fixed-string` | `ReplaceString` | 141.353 us | 192.252 us | Managed faster | 0.73x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 44.887 us | 61.522 us | Managed faster | 0.73x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 174.001 us | 261.551 us | Managed faster | 0.67x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 171.465 us | 262.967 us | Managed faster | 0.65x |
| `replacement/no-match-string` | `ReplaceString` | 1.446 us | 2.294 us | Inconclusive | 0.63x |
| `replacement/one-template-string` | `ReplaceString` | 1.673 us | 15,404.194 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.971 us | 0.542 us | Inconclusive | 1.82x |
| `split/unicode` | `SplitStrings` | 46.121 us | 37.522 us | CPython faster | 1.23x |
| `split/captures` | `SplitStrings` | 268.089 us | 251.080 us | Inconclusive | 1.07x |
| `split/no-captures` | `SplitStrings` | 232.567 us | 223.043 us | CPython faster | 1.04x |
| `split/zero-width` | `SplitStrings` | 65.840 us | 73.020 us | Managed faster | 0.90x |
| `split/optional-capture` | `SplitStrings` | 53.698 us | 78.020 us | Managed faster | 0.69x |
| `split/detailed` | `SplitDetailed` | 61.552 us | 128.587 us | Managed faster | 0.47x |
| `split/one-separator` | `SplitStrings` | 0.722 us | 10.956 us | Managed faster | 0.07x |
| `split/no-separator` | `SplitStrings` | 0.963 us | 23.611 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 67.932 us | 377.013 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 77.891 us | 595.079 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.861 us | 551.430 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 937.789 us | 340,068.077 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `multiline/search-miss`: Removable harness floors are 0.16 %/11.83 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 105.606 us / 49,785 B | 56.382 us | 108.716 us / 49,849 B | 56.540 us |
| `fallback` | 161.266 us / 57,081 B | 39.758 us | 132.975 us / 57,081 B | 39.463 us |
| `literal` | 100.959 us / 20,337 B | 23.177 us | 92.912 us / 20,337 B | 23.110 us |
| `unicode` | 73.763 us / 26,537 B | 35.008 us | 71.497 us / 26,537 B | 34.873 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
