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

Snapshot SHA-256: `E55FE20A183582CE6420AC2EC8345DD85C40CD6D5EC309A18ECA1738A861D54C`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `58` managed faster, `1` equivalent, `9` CPython faster, `6` inconclusive, `2` unqualified.

Measured source `698d2b68b6d5`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `anchor/fullmatch-miss` | `FullMatch` | 0.278 us | 0.208 us | CPython faster | 1.33x |
| `atomic/search-miss` | `Search` | 0.385 us | 0.290 us | CPython faster | 1.33x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.299 us | 0.227 us | CPython faster | 1.32x |
| `lookbehind/search-miss` | `Search` | 0.294 us | 0.247 us | CPython faster | 1.19x |
| `reluctant/search-hit` | `Search` | 0.512 us | 0.431 us | CPython faster | 1.18x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.395 us | 0.381 us | CPython faster | 1.04x |
| `common/date-miss` | `IsMatch` | 0.798 us | 0.856 us | Managed faster | 0.93x |
| `atomic/search-hit` | `Search` | 0.343 us | 0.405 us | Managed faster | 0.85x |
| `possessive/search-hit` | `Search` | 0.329 us | 0.393 us | Managed faster | 0.84x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.301 us | 0.374 us | Managed faster | 0.80x |
| `leading-inline/search-hit` | `Search` | 0.323 us | 0.414 us | Managed faster | 0.78x |
| `lookahead/search-miss` | `Search` | 0.171 us | 0.224 us | Managed faster | 0.77x |
| `scoped-inline/search-hit` | `Search` | 0.281 us | 0.374 us | Managed faster | 0.75x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.287 us | 0.385 us | Managed faster | 0.74x |
| `prefix/match` | `Match` | 0.247 us | 0.357 us | Managed faster | 0.69x |
| `lookbehind/search-hit` | `Search` | 0.274 us | 0.413 us | Managed faster | 0.66x |
| `unicode-category/search-miss` | `Search` | 0.153 us | 0.240 us | Managed faster | 0.64x |
| `alternation/search-miss` | `Search` | 0.166 us | 0.265 us | Managed faster | 0.63x |
| `ignorecase/search-hit` | `Search` | 0.272 us | 0.437 us | Managed faster | 0.62x |
| `ignorecase/search-miss` | `Search` | 0.182 us | 0.314 us | Managed faster | 0.58x |
| `multiline/search-hit` | `Search` | 0.235 us | 0.416 us | Managed faster | 0.57x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.154 us | 0.304 us | Managed faster | 0.51x |
| `ascii-option/search-hit` | `Search` | 0.190 us | 0.391 us | Managed faster | 0.49x |
| `greedy/search-miss` | `Search` | 0.117 us | 0.249 us | Managed faster | 0.47x |
| `possessive/search-miss` | `Search` | 0.107 us | 0.239 us | Managed faster | 0.45x |
| `unicode-option/search-hit` | `Search` | 0.271 us | 0.642 us | Managed faster | 0.42x |
| `lookahead/search-hit` | `Search` | 0.158 us | 0.379 us | Managed faster | 0.42x |
| `unicode-literal/search-miss` | `Search` | 0.083 us | 0.313 us | Managed faster | 0.26x |
| `literal/search-early` | `Search` | 0.077 us | 0.302 us | Managed faster | 0.26x |
| `unicode/fullmatch` | `FullMatch` | 4.671 us | 26.579 us | Managed faster | 0.18x |
| `literal/fullmatch` | `FullMatch` | 0.060 us | 0.422 us | Managed faster | 0.14x |
| `unicode-literal/search-hit` | `Search` | 0.076 us | 0.648 us | Managed faster | 0.12x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.060 us | 0.598 us | Managed faster | 0.10x |
| `literal/search-miss` | `Search` | 1.081 us | 22.017 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.090 us | 22.455 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.054 us | 22.063 us | Managed faster | 0.05x |
| `common/ip-match` | `IsMatch` | 0.282 us | 0.320 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.284 us | 0.291 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.733 us | 1.713 us | Managed faster | 0.43x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 100.224 us | 221.380 us | Managed faster | 0.45x |
| `count/empty-progression` | `Count` | 151.729 us | 364.386 us | Managed faster | 0.41x |
| `zero-width/count` | `Count` | 395.146 us | 1,094.566 us | Managed faster | 0.36x |
| `count/from-offset` | `CountFromOffset` | 17.469 us | 49.671 us | Managed faster | 0.35x |
| `family/count` | `Count` | 555.972 us | 2,500.429 us | Managed faster | 0.22x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 129.615 us | 646.612 us | Managed faster | 0.20x |
| `unicode/count` | `Count` | 49.057 us | 422.266 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 266.022 us | 2,573.364 us | Managed faster | 0.10x |
| `count/one` | `Count` | 0.142 us | 1.966 us | Managed faster | 0.07x |
| `count/zero` | `Count` | 0.296 us | 90.006 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.267 us | 142.292 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.021 us | 0.473 us | Inconclusive | 2.16x |
| `replacement/empty-progression-string` | `ReplaceString` | 30.860 us | 23.335 us | CPython faster | 1.32x |
| `replacement/named-template-string` | `ReplaceString` | 0.929 us | 0.932 us | Equivalent | 1.00x |
| `replacement/unmatched-template-string` | `ReplaceString` | 43.415 us | 47.321 us | Managed faster | 0.92x |
| `replacement/fixed-string` | `ReplaceString` | 145.508 us | 176.068 us | Managed faster | 0.83x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 51.576 us | 64.109 us | Managed faster | 0.81x |
| `replacement/subn-string` | `SubnString` | 72.534 us | 90.145 us | Managed faster | 0.80x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 135.111 us | 177.856 us | Managed faster | 0.76x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 202.038 us | 275.230 us | Managed faster | 0.73x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 201.810 us | 275.451 us | Managed faster | 0.73x |
| `replacement/no-match-string` | `ReplaceString` | 1.669 us | 2.381 us | Inconclusive | 0.71x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.745 us | 100.798 us | Managed faster | 0.67x |
| `replacement/one-template-string` | `ReplaceString` | 1.887 us | 16,201.226 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.013 us | 0.566 us | Inconclusive | 1.83x |
| `split/unicode` | `SplitStrings` | 47.442 us | 39.168 us | CPython faster | 1.21x |
| `split/captures` | `SplitStrings` | 277.776 us | 262.721 us | CPython faster | 1.05x |
| `split/no-captures` | `SplitStrings` | 222.531 us | 229.303 us | Managed faster | 0.97x |
| `split/zero-width` | `SplitStrings` | 72.435 us | 76.781 us | Managed faster | 0.94x |
| `split/optional-capture` | `SplitStrings` | 60.997 us | 81.538 us | Managed faster | 0.75x |
| `split/detailed` | `SplitDetailed` | 75.611 us | 134.683 us | Managed faster | 0.56x |
| `split/one-separator` | `SplitStrings` | 0.825 us | 11.999 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.086 us | 24.919 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 67.670 us | 394.948 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 77.066 us | 620.538 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 50.519 us | 571.095 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 978.673 us | 355,018.193 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `common/ip-match`: Removable harness floors are 0.14 %/10.46 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/12.16 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 118.481 us / 49,769 B | 58.462 us | 137.853 us / 49,833 B | 61.432 us |
| `fallback` | 170.262 us / 57,065 B | 42.891 us | 141.988 us / 57,065 B | 43.364 us |
| `literal` | 110.231 us / 20,321 B | 24.030 us | 96.922 us / 20,321 B | 24.509 us |
| `unicode` | 87.209 us / 26,521 B | 37.078 us | 80.662 us / 26,521 B | 35.104 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
