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

Snapshot SHA-256: `33E49C3E6450F7F488C798C1AF76DB6E3D53CD84E5A7390E828E8EFDCD3B9834`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `41` managed faster, `2` equivalent, `34` CPython faster, `7` inconclusive, `6` unqualified.

Measured source `d6120197de6b`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.679 us | 0.236 us | CPython faster | 2.88x |
| `common/date-miss` | `IsMatch` | 1.836 us | 0.865 us | CPython faster | 2.12x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.365 us | 0.198 us | CPython faster | 1.84x |
| `leading-inline/search-hit` | `Search` | 0.617 us | 0.335 us | CPython faster | 1.84x |
| `atomic/search-miss` | `Search` | 0.475 us | 0.261 us | CPython faster | 1.82x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.477 us | 0.294 us | CPython faster | 1.62x |
| `possessive/search-hit` | `Search` | 0.466 us | 0.308 us | CPython faster | 1.51x |
| `lookbehind/search-miss` | `Search` | 0.320 us | 0.221 us | CPython faster | 1.46x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.406 us | 0.283 us | CPython faster | 1.43x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.261 us | 0.185 us | CPython faster | 1.41x |
| `reluctant/search-hit` | `Search` | 0.491 us | 0.352 us | CPython faster | 1.40x |
| `atomic/search-hit` | `Search` | 0.441 us | 0.325 us | CPython faster | 1.36x |
| `scoped-inline/search-hit` | `Search` | 0.344 us | 0.292 us | CPython faster | 1.18x |
| `prefix/match` | `Match` | 0.319 us | 0.275 us | CPython faster | 1.16x |
| `unicode-option/search-hit` | `Search` | 0.338 us | 0.299 us | CPython faster | 1.13x |
| `lookahead/search-miss` | `Search` | 0.202 us | 0.194 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 17.359 us | 19.722 us | Managed faster | 0.88x |
| `ascii-option/search-hit` | `Search` | 0.248 us | 0.308 us | Managed faster | 0.81x |
| `multiline/search-hit` | `Search` | 0.256 us | 0.336 us | Managed faster | 0.76x |
| `ignorecase/search-miss` | `Search` | 0.200 us | 0.290 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.144 us | 0.221 us | Managed faster | 0.65x |
| `lookahead/search-hit` | `Search` | 0.188 us | 0.301 us | Managed faster | 0.63x |
| `possessive/search-miss` | `Search` | 0.123 us | 0.213 us | Managed faster | 0.58x |
| `unicode-literal/search-hit` | `Search` | 0.126 us | 0.235 us | Managed faster | 0.54x |
| `unicode-literal/search-miss` | `Search` | 0.142 us | 0.292 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.146 us | 0.335 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.088 us | 0.259 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.070 us | 0.349 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.060 us | 0.319 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.649 us | 22.610 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.472 us | 22.415 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.655 us | 22.208 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.206 us | 0.237 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.325 us | 0.357 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.331 us | 0.334 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.270 us | 0.268 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.201 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.183 us | 0.214 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.887 us | 1.331 us | Managed faster | 0.67x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 878.160 us | 349.775 us | CPython faster | 2.51x |
| `findall/one-capture-strings` | `FindAllStrings` | 464.158 us | 213.209 us | CPython faster | 2.17x |
| `findall/named-capture-strings` | `FindAllStrings` | 161.476 us | 95.221 us | CPython faster | 1.70x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 102.083 us | 62.157 us | CPython faster | 1.65x |
| `findall/empty-progression-strings` | `FindAllStrings` | 65.718 us | 43.348 us | CPython faster | 1.52x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.334 us | 27.606 us | CPython faster | 1.39x |
| `findall/full-strings` | `FindAllStrings` | 508.447 us | 369.678 us | CPython faster | 1.38x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.746 us | 16.214 us | CPython faster | 1.09x |
| `findall/optional-capture-strings` | `FindAllStrings` | 59.823 us | 55.217 us | CPython faster | 1.08x |
| `findall/full-utf8` | `FindAllUtf8` | 542.495 us | 518.349 us | CPython faster | 1.05x |
| `findall/structural-many` | `FindAllStructural` | 200.559 us | 202.549 us | Equivalent | 0.99x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 555.041 us | 622.144 us | Managed faster | 0.89x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 952.594 us | 1,251.073 us | Managed faster | 0.76x |
| `zero-width/count` | `Count` | 623.407 us | 1,117.923 us | Managed faster | 0.56x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 299.465 us | 542.799 us | Managed faster | 0.55x |
| `count/empty-progression` | `Count` | 181.217 us | 381.167 us | Managed faster | 0.47x |
| `count/from-offset` | `CountFromOffset` | 13.981 us | 50.138 us | Managed faster | 0.28x |
| `family/count` | `Count` | 667.296 us | 2,452.759 us | Managed faster | 0.27x |
| `unicode/count` | `Count` | 66.596 us | 432.514 us | Managed faster | 0.15x |
| `count/one` | `Count` | 0.275 us | 2.014 us | Managed faster | 0.14x |
| `class-run/count` | `Count` | 264.904 us | 2,556.223 us | Managed faster | 0.10x |
| `findall/one-strings` | `FindAllStrings` | 0.792 us | 38.960 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.539 us | 90.793 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.387 us | 91.294 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.255 us | 140.279 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.147 us | 0.459 us | Inconclusive | 2.43x |
| `replacement/empty-progression-string` | `ReplaceString` | 35.650 us | 23.656 us | CPython faster | 1.51x |
| `replacement/named-template-string` | `ReplaceString` | 1.269 us | 0.922 us | CPython faster | 1.38x |
| `replacement/fixed-string` | `ReplaceString` | 188.530 us | 176.025 us | CPython faster | 1.07x |
| `replacement/subn-string` | `SubnString` | 93.903 us | 91.424 us | CPython faster | 1.03x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 177.885 us | 179.346 us | Equivalent | 0.99x |
| `replacement/unmatched-template-string` | `ReplaceString` | 50.179 us | 52.826 us | Inconclusive | 0.98x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.196 us | 91.834 us | Managed faster | 0.97x |
| `replacement/no-match-string` | `ReplaceString` | 1.260 us | 2.409 us | Inconclusive | 0.52x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 154.606 us | 4,318.358 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 605.807 us | 17,307.410 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 602.223 us | 17,520.187 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.095 us | 16,623.791 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.473 us | 0.534 us | Inconclusive | 2.70x |
| `split/zero-width` | `SplitStrings` | 116.629 us | 67.007 us | CPython faster | 1.74x |
| `split/captures` | `SplitStrings` | 440.023 us | 254.935 us | CPython faster | 1.73x |
| `split/unicode` | `SplitStrings` | 70.029 us | 44.724 us | CPython faster | 1.56x |
| `split/no-captures` | `SplitStrings` | 279.374 us | 224.282 us | CPython faster | 1.25x |
| `split/optional-capture` | `SplitStrings` | 75.222 us | 82.202 us | Managed faster | 0.92x |
| `split/detailed` | `SplitDetailed` | 122.811 us | 136.637 us | Managed faster | 0.90x |
| `split/one-separator` | `SplitStrings` | 1.186 us | 12.058 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.229 us | 25.035 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 181.749 us | 401.793 us | Managed faster | 0.45x |
| `corpus/code-declarations` | `Count` | 83.151 us | 630.669 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.113 us | 579.523 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,090.212 us | 357,965.850 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.44 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.15 %/9.50 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.11 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/12.68 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.33 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/15.95 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 174.581 us / 71,617 B | 59.768 us | 173.150 us / 71,641 B | 59.944 us |
| `fallback` | 302.859 us / 83,561 B | 42.816 us | 238.019 us / 83,529 B | 41.990 us |
| `literal` | 235.834 us / 50,369 B | 25.013 us | 214.553 us / 50,337 B | 24.832 us |
| `unicode` | 94.013 us / 48,313 B | 36.311 us | 88.059 us / 48,281 B | 36.395 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
