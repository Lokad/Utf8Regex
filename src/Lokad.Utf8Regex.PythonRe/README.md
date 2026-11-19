# Lokad.Utf8Regex.PythonRe

**0.5.0 release candidate**, .NET 10 and .NET 11. A managed Python `re`-style adapter
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
token and completes FullMatch preparation. Unfunded constructors defer that program;
a ProgramMemoryAllowance funds it eagerly before publication and supplies default
working admission. Dispose funded expressions after use; active calls/cursors keep
their funding until they finish. Refusal never retries without admission.

With no explicit construction controls/funding, constructors can borrow an active
internal construction owner. Explicit Compile and construction/funding controls
start one owner and reject replacing an active one. Invalid policy values report
the policy parameter. Settings are copied; the construction token does not become
execution authority. Host retention/decoding and incidental objects keep their
separate scope. See [resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits)
and [migration guidance](../../docs/MIGRATION.md#pythonre-compilation-policy).

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

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

FindAllStrings, FindAllUtf8 and FindAllStringsFromOffset rows are historical measurements of the removed FindAllToUtf8 API. They are no longer refreshed and do not qualify current cursor materialization. Structural rows compose the retained cursor in benchmark host code.

Snapshot SHA-256: `ADF6B5F18D3BDE41CE8754B4EEE690CD0071BF8123216EECA4EC0C6CF354B0A1`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `44` managed faster, `1` equivalent, `21` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `c66180bb21e1`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 2.312 us | 0.866 us | CPython faster | 2.67x |
| `common/ip-match` | `IsMatch` | 0.755 us | 0.327 us | CPython faster | 2.30x |
| `atomic/search-miss` | `Search` | 0.642 us | 0.291 us | CPython faster | 2.19x |
| `lookbehind/search-miss` | `Search` | 0.469 us | 0.248 us | CPython faster | 1.88x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.378 us | 0.227 us | CPython faster | 1.67x |
| `multiline/search-miss` | `Search` | 0.482 us | 0.293 us | CPython faster | 1.64x |
| `reluctant/search-hit` | `Search` | 0.688 us | 0.431 us | CPython faster | 1.60x |
| `possessive/search-hit` | `Search` | 0.602 us | 0.392 us | CPython faster | 1.54x |
| `atomic/search-hit` | `Search` | 0.596 us | 0.409 us | CPython faster | 1.46x |
| `unicode-category/search-miss` | `Search` | 0.352 us | 0.243 us | CPython faster | 1.45x |
| `leading-inline/search-hit` | `Search` | 0.498 us | 0.418 us | CPython faster | 1.19x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.446 us | 0.381 us | CPython faster | 1.17x |
| `lookbehind/search-hit` | `Search` | 0.457 us | 0.413 us | CPython faster | 1.10x |
| `multiline/search-hit` | `Search` | 0.439 us | 0.420 us | CPython faster | 1.04x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.380 us | 0.368 us | CPython faster | 1.03x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.341 us | 0.388 us | Managed faster | 0.88x |
| `prefix/match` | `Match` | 0.284 us | 0.359 us | Managed faster | 0.79x |
| `alternation/search-miss` | `Search` | 0.208 us | 0.264 us | Managed faster | 0.79x |
| `ignorecase/search-miss` | `Search` | 0.228 us | 0.313 us | Managed faster | 0.73x |
| `ignorecase/search-hit` | `Search` | 0.314 us | 0.439 us | Managed faster | 0.71x |
| `unicode-option/search-hit` | `Search` | 0.462 us | 0.651 us | Managed faster | 0.71x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.148 us | 0.210 us | Managed faster | 0.70x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.197 us | 0.306 us | Managed faster | 0.64x |
| `greedy/search-miss` | `Search` | 0.157 us | 0.245 us | Managed faster | 0.64x |
| `possessive/search-miss` | `Search` | 0.143 us | 0.238 us | Managed faster | 0.60x |
| `lookahead/search-hit` | `Search` | 0.199 us | 0.380 us | Managed faster | 0.52x |
| `unicode-literal/search-miss` | `Search` | 0.076 us | 0.319 us | Managed faster | 0.24x |
| `literal/search-early` | `Search` | 0.070 us | 0.301 us | Managed faster | 0.23x |
| `unicode/fullmatch` | `FullMatch` | 3.479 us | 26.262 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.051 us | 0.423 us | Managed faster | 0.12x |
| `unicode-literal/search-hit` | `Search` | 0.069 us | 0.644 us | Managed faster | 0.11x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.052 us | 0.608 us | Managed faster | 0.09x |
| `literal/search-miss` | `Search` | 1.089 us | 22.318 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.088 us | 22.544 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.072 us | 22.293 us | Managed faster | 0.05x |
| `ascii-option/search-hit` | `Search` | 0.385 us | 0.388 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.218 us | 0.225 us | Unqualified | — |
| `scoped-inline/search-hit` | `Search` | 0.333 us | 0.375 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.775 us | 1.665 us | Managed faster | 0.47x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 102.891 us | 219.147 us | Managed faster | 0.47x |
| `zero-width/count` | `Count` | 376.081 us | 1,092.568 us | Managed faster | 0.34x |
| `count/empty-progression` | `Count` | 123.458 us | 361.671 us | Managed faster | 0.34x |
| `count/from-offset` | `CountFromOffset` | 13.786 us | 48.980 us | Managed faster | 0.28x |
| `family/count` | `Count` | 537.956 us | 2,480.911 us | Managed faster | 0.22x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 133.081 us | 643.832 us | Managed faster | 0.21x |
| `unicode/count` | `Count` | 48.681 us | 414.051 us | Managed faster | 0.12x |
| `count/one` | `Count` | 0.225 us | 1.952 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 280.869 us | 2,598.714 us | Managed faster | 0.11x |
| `count/zero` | `Count` | 0.388 us | 90.130 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.294 us | 140.858 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.108 us | 0.474 us | Inconclusive | 2.34x |
| `replacement/fixed-string` | `ReplaceString` | 192.702 us | 172.829 us | CPython faster | 1.10x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.424 us | 23.137 us | CPython faster | 1.10x |
| `replacement/named-template-string` | `ReplaceString` | 0.984 us | 0.932 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 94.911 us | 89.498 us | CPython faster | 1.06x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 181.552 us | 176.127 us | CPython faster | 1.03x |
| `replacement/subn-utf8` | `SubnUtf8` | 91.052 us | 90.983 us | Equivalent | 1.00x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.600 us | 47.332 us | Managed faster | 0.86x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 49.474 us | 63.881 us | Managed faster | 0.77x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 193.180 us | 275.810 us | Managed faster | 0.70x |
| `replacement/no-match-string` | `ReplaceString` | 1.633 us | 2.382 us | Inconclusive | 0.69x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 190.942 us | 275.385 us | Managed faster | 0.69x |
| `replacement/one-template-string` | `ReplaceString` | 2.061 us | 16,123.782 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.127 us | 0.564 us | Inconclusive | 2.02x |
| `split/unicode` | `SplitStrings` | 50.029 us | 39.563 us | CPython faster | 1.27x |
| `split/captures` | `SplitStrings` | 270.049 us | 265.284 us | Inconclusive | 1.02x |
| `split/zero-width` | `SplitStrings` | 72.235 us | 76.762 us | Managed faster | 0.94x |
| `split/no-captures` | `SplitStrings` | 215.370 us | 231.161 us | Managed faster | 0.93x |
| `split/optional-capture` | `SplitStrings` | 54.059 us | 82.092 us | Managed faster | 0.66x |
| `split/detailed` | `SplitDetailed` | 62.453 us | 135.733 us | Managed faster | 0.46x |
| `split/one-separator` | `SplitStrings` | 0.867 us | 11.904 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.136 us | 24.574 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 71.418 us | 398.688 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 83.912 us | 622.789 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.551 us | 574.559 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 946.970 us | 355,656.154 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `ascii-option/search-hit`: Removable harness floors are 0.14 %/8.65 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/15.12 % of the lanes.
- `scoped-inline/search-hit`: Removable harness floors are 0.12 %/9.02 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 126.509 us / 53,073 B | 60.172 us | 121.359 us / 53,137 B | 60.609 us |
| `fallback` | 194.466 us / 62,969 B | 42.820 us | 153.459 us / 62,969 B | 41.806 us |
| `literal` | 119.084 us / 26,241 B | 24.527 us | 107.581 us / 26,241 B | 24.358 us |
| `unicode` | 63.472 us / 32,441 B | 36.769 us | 59.581 us / 32,441 B | 36.437 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
