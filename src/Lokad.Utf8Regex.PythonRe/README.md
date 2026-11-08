# Lokad.Utf8Regex.PythonRe

**0.5.0 development**, .NET 10 and .NET 11. A managed Python `re`-style adapter
over strict UTF-8 Unicode scalars, requiring the exact Core package version
matching the adapter. It can coexist with PCRE2 at the same version. No Python
runtime or BCL regex matcher is a shipped implementation dependency.

## Supported scope

The profile targets the documented CPython 3.13.2 `str` grammar with owned Unicode
15.1.0 identifier/class/case data. Patterns are parsed before scalar lowering.
Captures are numbered in opening order; references/conditionals, fixed-width
lookbehind, greedy/lazy repetition, scoped flags and Python empty-match retry use
the owned engine. `LastIndex`/`LastGroup` identify the last capture to close on the
successful path; an unnamed last capture has a null name. Unmatched captures keep
the documented null/empty shaping for each API.

Search, Match and FullMatch expose value, detailed, string and UTF-8 forms.
`FindIterDetailed` returns owned capture snapshots. `FindAllToStrings` and
`FindAllToUtf8` provide Python-shaped scalar/tuple outputs; `FindAll` is the
structural full-match collector. Split and template/callback replacement include
capture-aware output shaping and substitution counts. Primary ranges are byte
coordinates; derived UTF-16 coordinates do not define matching width.

`Replace`, `Subn` and funded `SubnRetained` accept UTF-8 template spans as well as
text templates. Native preparation/cache keys and literal/capture output remain
bytes. Warm queries borrow their span; cold keys are copied after admission.
Template validation still runs on misses, and template error positions retain
UTF-16 units. These overloads preserve the Unicode grammar, not Python bytes-regex
semantics. For example, `regex.Subn(input, "[\\g<word>]"u8)` returns owned bytes
and a substitution count.

Bytes-pattern semantics, LOCALE and `\N{...}` Unicode-name lookup are excluded.
There is no `endpos` parameter. Start offsets follow the documented Python `pos`
contract without resetting prefix/suffix assertion context. This is a selected
profile, not complete Python `re` equivalence. See the
[profile specification](../../docs/SPEC-PYTHONRE.md).

## Controls and lifetimes

`EffectiveOptions` exposes validated global flags, including leading inline
directives and implicit Unicode unless global ASCII is selected. Scoped flags
stay scoped. `Options` retains the flags originally supplied by the caller.

Instances accept positive nullable `Utf8ResourcePolicy.MaxWorkSteps`, `MaxDepth`,
`MaxWorkingBytes` and `MaxResultBytes`. Null is unlimited; zero is invalid.
**Defaults remain unlimited.** Work includes covered validation, matching and
required shaping cumulatively. Depth admits live frames before acquisition;
working/result controls admit modeled major capacities and growth overlap.
Result/staging charges do not refund and may conservatively count storage again.
Denials use `Utf8ResourceLimitExceededException` with the corresponding
`Execution*` resource kind. This is not a total heap/RSS or caller-retention bound.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

Strict full-subject validation, original synchronous cancellation and one absolute
deadline apply through required shaping and persistent cursor advances, including
waits between moves. Caller callbacks cannot be preempted; copying/encoding their
returned output remains covered. Timeouts use `RegexMatchTimeoutException`; its
Input is empty when working or result bytes are bounded to avoid decoding the
subject for diagnostics after expiry.

Explicit `Compile` factories accept separate construction limits, timeout and
token, and eagerly prepare FullMatch. Constructors keep fixed construction
backstops and lazy FullMatch preparation. Cold FullMatch/replacement preparation,
cache retention, incidental owners, standalone getters, callback allocations and
host containers retain separate documented scope. The execution token must be
passed on each operation. See [resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits)
and [MIGRATION.md](../../docs/MIGRATION.md).

`Utf8PythonMatchCursor.MoveNext(token)` accepts a token for one advance without
retaining it for later advances. The creation token, original deadline and
cumulative budgets still apply. Cancellation faults the cursor and releases its
scratch; previously returned owned snapshots remain valid.

Use `cursor.MoveNext(out Utf8PythonRangeMatch match)` for capture byte ranges
without captured strings or a whole-subject coordinate map. Range metadata stays
valid after advances/disposal; byte memory remains borrowed and must stay unchanged
with its manager alive. A range advance clears the text-based `Current`.
`SplitRanges` provides ordinary segments and captured separators as byte ranges,
including unmatched captures, without decoding or copying their text. These paths
retain finite limits and synchronous cancellation; shared memory funding remains
separate unfinished work.

The four-argument `CreateMatchCursor` overload accepts a caller-owned
`IUtf8MemoryAllowance` for shared working admission. Concurrent cursors use that
same allowance directly; refusal throws `Utf8MemoryAdmissionException` before
covered acquisition, and EOF/failure/disposal refund scratch synchronously.
Numeric limits still apply. Compilation, caches and published caller-retained
results need separate funding; this overload does not imply a total memory cap.

Named captures remain distinct when Unicode identifiers and names such as `g1`
are mixed. Public names and Python opening-order numbering remain unchanged.
Fixed finite policies omit optional unmetered Core runtime/emission storage and
native prefix buffers those instances cannot execute. Useful search/scalar
preparation remains; lower construction charges can admit earlier-denied patterns.

Optional Core preparations support native iteration, canonical literal FindAll
string counting, literal values/results and scalar cursor start hints. The current
grammar-level iteration flag can also retain Core for patterns whose operations
decline it. Tightening that flag is deferred after a bounded cost experiment;
backend presence alone does not establish execution. A Core preparation may still
be built transiently to discover eligibility, under the original construction
owner. Discarding a root reduces retention, not all construction traffic.

Default and unsuccessful detailed results expose empty capture/name arrays and
empty value text. Default match/group data also expose empty text. Successful
snapshots keep their supplied arrays and retain their values across later calls.

## Public API

```csharp
using System;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

var regex = new Utf8PythonRegex(
    pattern: @"(?P<word>foo)-(?P=word)",
    options: PythonReCompileOptions.None,
    matchTimeout: TimeSpan.FromSeconds(1),
    resourcePolicy: new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = 1024,
        MaxWorkingBytes = 16 * 1024 * 1024,
        MaxResultBytes = 16 * 1024 * 1024
    });

bool isMatch = regex.IsMatch("foo-foo"u8);
var first = regex.Search("xx foo-foo yy"u8);
int count = regex.Count("foo-foo x foo-foo"u8);
byte[] replaced = regex.Replace("foo x foo"u8, "<\\g<word>>");
Utf8PythonMatchData[] matches = regex.FindAll("foo-foo xx foo-foo"u8);
var iter = regex.FindIterDetailed("foo-foo xx foo-foo"u8);
string?[] parts = regex.SplitToStrings("foo-foo xx foo-foo"u8);
```

String-returning helpers such as `SearchToString(...)`, `ReplaceToString(...)`, and `FindAllToStrings(...)`
are also available as host-friendly conveniences when a runtime needs scalar string results.

`CreateMatchCursor(ReadOnlyMemory<byte>)` discovers one detailed snapshot per
`MoveNext` instead of materializing the complete `FindIterDetailed` array:

```csharp
ReadOnlyMemory<byte> subject = System.Text.Encoding.UTF8.GetBytes("é😀");
using var cursor = new Utf8PythonRegex("(?P<scalar>.)").CreateMatchCursor(subject);
while (cursor.MoveNext())
{
    Utf8PythonDetailedMatchData match = cursor.Current;
    // The supplementary scalar occupies four bytes and is one match.
    Console.WriteLine(match.Value.ValueText);
}
```

Keep the subject unchanged and its memory manager alive until exhaustion or
disposal. Dispose when stopping early. The cursor has one shared position and
lifetime across reference aliases, rejects concurrent/reentrant access, and has
no finalizer. EOF and failure release scratch and subject references immediately.
Returned snapshots own their arrays and text and remain valid after advance or
disposal. Sequential advances may cross host calls or asynchronous waits; those
waits count against the original absolute match deadline. The offset/token
overloads preserve scalar-aligned byte offsets and the original invocation token.
Existing span APIs retain their existing contracts.


A cursor has cumulative work/result allowance across all moves. Scratch remains
charged while positioned and releases at EOF/failure/Dispose. Published snapshots
never borrow reusable scratch. Faults preserve earlier snapshots; a new call gets
a new allowance and needs new host admission while earlier results survive.
Core value-cursor copies have independent positions/shared allowance; PythonRe
reference aliases share position/disposal. No finalizer-based lease is required.

Read [release notes](../../CHANGELOG.md). Benchmark observations below retain their
recorded source/runtime and qualification scope.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `E91D1274A34DEFCC322A805574C9A27909E496E4FFE397EC54C9C0A45279C25D`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `45` managed faster, `3` equivalent, `32` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `1c1cb97de771`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.678 us | 0.234 us | CPython faster | 2.90x |
| `common/date-miss` | `IsMatch` | 1.811 us | 0.877 us | CPython faster | 2.07x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.357 us | 0.197 us | CPython faster | 1.81x |
| `leading-inline/search-hit` | `Search` | 0.579 us | 0.335 us | CPython faster | 1.73x |
| `atomic/search-miss` | `Search` | 0.439 us | 0.261 us | CPython faster | 1.68x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.481 us | 0.294 us | CPython faster | 1.64x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.266 us | 0.186 us | CPython faster | 1.43x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.403 us | 0.287 us | CPython faster | 1.40x |
| `possessive/search-hit` | `Search` | 0.427 us | 0.310 us | CPython faster | 1.38x |
| `reluctant/search-hit` | `Search` | 0.458 us | 0.351 us | CPython faster | 1.31x |
| `atomic/search-hit` | `Search` | 0.417 us | 0.323 us | CPython faster | 1.30x |
| `lookbehind/search-miss` | `Search` | 0.297 us | 0.242 us | CPython faster | 1.22x |
| `prefix/match` | `Match` | 0.320 us | 0.276 us | CPython faster | 1.16x |
| `scoped-inline/search-hit` | `Search` | 0.329 us | 0.292 us | CPython faster | 1.13x |
| `unicode-option/search-hit` | `Search` | 0.313 us | 0.300 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 17.468 us | 19.736 us | Managed faster | 0.89x |
| `ignorecase/search-hit` | `Search` | 0.300 us | 0.357 us | Managed faster | 0.84x |
| `lookbehind/search-hit` | `Search` | 0.308 us | 0.366 us | Managed faster | 0.84x |
| `alternation/search-miss` | `Search` | 0.198 us | 0.237 us | Managed faster | 0.83x |
| `unicode-category/search-miss` | `Search` | 0.171 us | 0.213 us | Managed faster | 0.81x |
| `ascii-option/search-hit` | `Search` | 0.247 us | 0.308 us | Managed faster | 0.80x |
| `multiline/search-hit` | `Search` | 0.243 us | 0.334 us | Managed faster | 0.73x |
| `ignorecase/search-miss` | `Search` | 0.197 us | 0.287 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.133 us | 0.221 us | Managed faster | 0.60x |
| `lookahead/search-hit` | `Search` | 0.178 us | 0.301 us | Managed faster | 0.59x |
| `unicode-literal/search-hit` | `Search` | 0.138 us | 0.237 us | Managed faster | 0.58x |
| `possessive/search-miss` | `Search` | 0.118 us | 0.212 us | Managed faster | 0.55x |
| `unicode-literal/search-miss` | `Search` | 0.138 us | 0.291 us | Managed faster | 0.47x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.145 us | 0.334 us | Managed faster | 0.43x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.089 us | 0.256 us | Managed faster | 0.35x |
| `literal/fullmatch` | `FullMatch` | 0.070 us | 0.347 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.056 us | 0.322 us | Managed faster | 0.17x |
| `literal/search` | `Search` | 2.661 us | 22.630 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.476 us | 22.346 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.665 us | 22.237 us | Managed faster | 0.07x |
| `lookahead/search-miss` | `Search` | 0.194 us | 0.197 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.270 us | 0.268 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.220 us | 0.237 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.858 us | 1.342 us | Managed faster | 0.64x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 883.686 us | 347.940 us | CPython faster | 2.54x |
| `findall/one-capture-strings` | `FindAllStrings` | 435.393 us | 211.655 us | CPython faster | 2.05x |
| `findall/named-capture-strings` | `FindAllStrings` | 157.569 us | 94.335 us | CPython faster | 1.67x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 101.607 us | 62.359 us | CPython faster | 1.63x |
| `findall/empty-progression-strings` | `FindAllStrings` | 63.223 us | 43.824 us | CPython faster | 1.44x |
| `findall/unicode-full-strings` | `FindAllStrings` | 37.884 us | 27.322 us | CPython faster | 1.39x |
| `findall/full-strings` | `FindAllStrings` | 509.493 us | 368.145 us | CPython faster | 1.38x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.743 us | 16.033 us | CPython faster | 1.11x |
| `findall/full-utf8` | `FindAllUtf8` | 549.304 us | 517.847 us | CPython faster | 1.06x |
| `findall/optional-capture-strings` | `FindAllStrings` | 55.372 us | 55.648 us | Equivalent | 1.00x |
| `findall/structural-many` | `FindAllStructural` | 192.879 us | 201.945 us | Managed faster | 0.96x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 546.933 us | 621.483 us | Managed faster | 0.88x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 942.283 us | 1,243.232 us | Managed faster | 0.76x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 299.105 us | 536.608 us | Managed faster | 0.55x |
| `zero-width/count` | `Count` | 540.624 us | 1,106.109 us | Managed faster | 0.49x |
| `count/empty-progression` | `Count` | 167.461 us | 379.069 us | Managed faster | 0.44x |
| `count/from-offset` | `CountFromOffset` | 13.940 us | 50.898 us | Managed faster | 0.27x |
| `family/count` | `Count` | 647.851 us | 2,470.963 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.277 us | 2.007 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 57.047 us | 432.112 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 272.391 us | 2,577.441 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.744 us | 38.957 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.541 us | 90.497 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.482 us | 90.857 us | Managed faster | 0.01x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.233 us | 142.202 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.385 us | 0.460 us | Inconclusive | 3.06x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.417 us | 23.753 us | CPython faster | 1.45x |
| `replacement/named-template-string` | `ReplaceString` | 1.213 us | 0.918 us | CPython faster | 1.32x |
| `replacement/fixed-string` | `ReplaceString` | 187.518 us | 177.341 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 94.106 us | 90.211 us | CPython faster | 1.04x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 178.320 us | 178.028 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.764 us | 91.135 us | Equivalent | 0.98x |
| `replacement/unmatched-template-string` | `ReplaceString` | 46.792 us | 51.006 us | Managed faster | 0.92x |
| `replacement/no-match-string` | `ReplaceString` | 1.510 us | 2.402 us | Inconclusive | 0.63x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 153.712 us | 4,306.559 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 588.439 us | 16,907.119 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 576.782 us | 17,510.074 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.249 us | 16,272.892 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.659 us | 0.545 us | Inconclusive | 3.05x |
| `split/unicode` | `SplitStrings` | 73.966 us | 44.520 us | CPython faster | 1.66x |
| `split/zero-width` | `SplitStrings` | 109.884 us | 66.591 us | CPython faster | 1.65x |
| `split/captures` | `SplitStrings` | 418.595 us | 254.566 us | CPython faster | 1.64x |
| `split/no-captures` | `SplitStrings` | 276.656 us | 222.744 us | CPython faster | 1.24x |
| `split/detailed` | `SplitDetailed` | 124.810 us | 134.788 us | Inconclusive | 0.93x |
| `split/optional-capture` | `SplitStrings` | 73.702 us | 81.839 us | Managed faster | 0.90x |
| `split/one-separator` | `SplitStrings` | 1.240 us | 12.046 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.400 us | 25.012 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 177.152 us | 401.692 us | Managed faster | 0.44x |
| `corpus/code-declarations` | `Count` | 83.908 us | 630.350 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 48.855 us | 575.657 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,052.144 us | 356,622.199 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.21 %/17.42 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/12.89 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.18 %/14.33 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 220.572 us / 71,617 B | 61.083 us | 188.000 us / 71,641 B | 60.530 us |
| `fallback` | 300.641 us / 83,561 B | 42.491 us | 262.941 us / 83,529 B | 42.259 us |
| `literal` | 358.197 us / 50,369 B | 24.962 us | 319.216 us / 50,337 B | 25.087 us |
| `unicode` | 55.194 us / 48,313 B | 36.558 us | 58.416 us / 48,281 B | 36.204 us |

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
