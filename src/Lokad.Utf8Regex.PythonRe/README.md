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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev92-20261114/README.md).

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
**Defaults remain unlimited.** Work includes matching and
required shaping cumulatively. Depth admits live frames before acquisition;
working/result controls admit modeled major capacities and growth overlap.
Result/staging charges do not refund and may conservatively count storage again.
Unknown short string lengths can charge a UTF-8-byte-derived capacity bound before
decoding; Unicode results can therefore reach a finite byte limit earlier.
Denials use `Utf8ResourceLimitExceededException` with the corresponding
`Execution*` resource kind. This is not a total heap/RSS or caller-retention bound.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

Original synchronous cancellation and one absolute
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
Detailed results and scalar queries share a lazy coordinate index through requested
captures, including lookarounds. They do not prepare an unused suffix. The index
uses admitted working storage and is refunded synchronously; published arrays and
strings retain their separate result charges. Distant scalar-only queries now store
both scalar and UTF-16 prefix counts, so their working requirements can increase;
short and ASCII-only projections need no index allocation.
`SplitRanges` provides ordinary segments and captured separators as byte ranges,
including unmatched captures, without decoding or copying their text. These paths
retain finite limits and synchronous cancellation.

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

Use `--measure-pythonre-paired-case findall/many-capture-strings 9` for the
canonical preexisting-text comparison. The command prints labeled paired evidence
without changing the recorded tables. There is no validation-policy matrix.

The recorded callback replacement rows time returning a prepared constant
replacement, including each API's required argument/output construction. Rich
callback capture checks and digests run in a separate untimed verification pass.
See the [timing correction](../../bench/Experiments/PythonCallbackTiming-20261113/README.md);
earlier instrumented callback ratios are not bare replacement-throughput claims.

The canonical comparison uses preexisting valid text on both sides. Value rows
include byte/UTF-16 range projection; detailed and structural rows include their
requested ranges, captures and text. CPython performs that conversion within
each timed operation, sharing work across bulk results. These are public-output
costs, rather than bare `re.Match` discovery timings. Boolean, count, string,
callback and split rows keep their existing timing contract. See the
[comparison correction](../../bench/Experiments/PythonCoordinateFairness-20261114/README.md).

Scaling rows now use the same timed coordinate projection as the canonical
comparison. The complete refresh records their current source and protocol;
older receipts retain their original preparation rules.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `51A69BBD8436CA780D64F35AB359126B621D8651964076BCF379944693323758`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `51` managed faster, `1` equivalent, `28` CPython faster, `6` inconclusive, `4` unqualified.

Measured source `528fabba5f5d`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.913 us | 0.844 us | CPython faster | 2.28x |
| `atomic/search-miss` | `Search` | 0.457 us | 0.277 us | CPython faster | 1.66x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.347 us | 0.217 us | CPython faster | 1.59x |
| `lookbehind/search-miss` | `Search` | 0.312 us | 0.238 us | CPython faster | 1.31x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.242 us | 0.200 us | CPython faster | 1.21x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.467 us | 0.402 us | CPython faster | 1.16x |
| `possessive/search-hit` | `Search` | 0.462 us | 0.404 us | CPython faster | 1.14x |
| `atomic/search-hit` | `Search` | 0.464 us | 0.427 us | CPython faster | 1.09x |
| `reluctant/search-hit` | `Search` | 0.491 us | 0.453 us | CPython faster | 1.08x |
| `leading-inline/search-hit` | `Search` | 0.375 us | 0.431 us | Managed faster | 0.87x |
| `scoped-inline/search-hit` | `Search` | 0.336 us | 0.395 us | Managed faster | 0.85x |
| `lookbehind/search-hit` | `Search` | 0.346 us | 0.437 us | Managed faster | 0.80x |
| `alternation/search-miss` | `Search` | 0.191 us | 0.256 us | Managed faster | 0.75x |
| `ignorecase/search-hit` | `Search` | 0.331 us | 0.457 us | Managed faster | 0.73x |
| `ignorecase/search-miss` | `Search` | 0.205 us | 0.302 us | Managed faster | 0.68x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.208 us | 0.328 us | Managed faster | 0.64x |
| `multiline/search-hit` | `Search` | 0.269 us | 0.429 us | Managed faster | 0.63x |
| `greedy/search-miss` | `Search` | 0.139 us | 0.236 us | Managed faster | 0.59x |
| `ascii-option/search-hit` | `Search` | 0.240 us | 0.409 us | Managed faster | 0.59x |
| `possessive/search-miss` | `Search` | 0.126 us | 0.231 us | Managed faster | 0.55x |
| `lookahead/search-hit` | `Search` | 0.209 us | 0.400 us | Managed faster | 0.52x |
| `unicode-option/search-hit` | `Search` | 0.339 us | 1.160 us | Managed faster | 0.29x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.120 us | 0.429 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.086 us | 0.316 us | Managed faster | 0.27x |
| `literal/search-early` | `Search` | 0.088 us | 0.331 us | Managed faster | 0.26x |
| `prefix/match` | `Match` | 0.066 us | 0.375 us | Managed faster | 0.18x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.320 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.058 us | 0.449 us | Managed faster | 0.13x |
| `unicode/fullmatch` | `FullMatch` | 3.256 us | 29.560 us | Managed faster | 0.11x |
| `literal/search` | `Search` | 2.111 us | 22.389 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.093 us | 1.199 us | Managed faster | 0.08x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.065 us | 1.133 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.107 us | 22.159 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.077 us | 22.036 us | Managed faster | 0.05x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.393 us | 0.391 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.199 us | 0.212 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.277 us | 0.282 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.199 us | 0.233 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.788 us | 2.032 us | Managed faster | 0.39x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 340.292 us | 216.098 us | CPython faster | 1.58x |
| `findall/many-capture-strings` | `FindAllStrings` | 514.719 us | 343.279 us | CPython faster | 1.50x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 83.073 us | 60.241 us | CPython faster | 1.37x |
| `findall/empty-progression-strings` | `FindAllStrings` | 56.364 us | 44.581 us | CPython faster | 1.27x |
| `findall/named-capture-strings` | `FindAllStrings` | 105.376 us | 87.665 us | CPython faster | 1.20x |
| `findall/full-strings` | `FindAllStrings` | 370.704 us | 344.318 us | CPython faster | 1.08x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.960 us | 15.977 us | CPython faster | 1.06x |
| `findall/optional-capture-strings` | `FindAllStrings` | 46.203 us | 55.063 us | Managed faster | 0.84x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 472.314 us | 611.277 us | Managed faster | 0.77x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.028 us | 27.284 us | Managed faster | 0.66x |
| `findall/full-utf8` | `FindAllUtf8` | 325.436 us | 509.437 us | Managed faster | 0.64x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 506.978 us | 1,234.458 us | Managed faster | 0.41x |
| `zero-width/count` | `Count` | 417.967 us | 1,032.013 us | Managed faster | 0.40x |
| `findall/structural-many` | `FindAllStructural` | 108.483 us | 275.967 us | Managed faster | 0.40x |
| `count/empty-progression` | `Count` | 126.088 us | 335.023 us | Managed faster | 0.38x |
| `count/from-offset` | `CountFromOffset` | 14.207 us | 47.878 us | Managed faster | 0.30x |
| `family/count` | `Count` | 535.750 us | 2,169.650 us | Managed faster | 0.25x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 176.804 us | 829.713 us | Managed faster | 0.21x |
| `count/one` | `Count` | 0.264 us | 1.954 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 275.212 us | 2,307.201 us | Managed faster | 0.12x |
| `unicode/count` | `Count` | 48.063 us | 410.597 us | Managed faster | 0.12x |
| `findall/one-strings` | `FindAllStrings` | 0.743 us | 38.521 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.427 us | 89.665 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.353 us | 90.123 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.183 us | 142.863 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.051 us | 0.449 us | Inconclusive | 2.33x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 97.846 us | 62.874 us | CPython faster | 1.56x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 380.772 us | 275.685 us | CPython faster | 1.38x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 368.770 us | 274.992 us | CPython faster | 1.34x |
| `replacement/fixed-string` | `ReplaceString` | 192.488 us | 175.624 us | CPython faster | 1.09x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.188 us | 23.353 us | CPython faster | 1.08x |
| `replacement/subn-string` | `SubnString` | 96.340 us | 89.353 us | CPython faster | 1.08x |
| `replacement/named-template-string` | `ReplaceString` | 0.977 us | 0.944 us | CPython faster | 1.04x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 182.255 us | 177.151 us | CPython faster | 1.03x |
| `replacement/subn-utf8` | `SubnUtf8` | 91.080 us | 90.366 us | Equivalent | 1.01x |
| `replacement/unmatched-template-string` | `ReplaceString` | 36.780 us | 46.617 us | Managed faster | 0.79x |
| `replacement/no-match-string` | `ReplaceString` | 1.112 us | 2.388 us | Inconclusive | 0.47x |
| `replacement/one-template-string` | `ReplaceString` | 1.697 us | 16,237.601 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.618 us | 0.537 us | Inconclusive | 3.00x |
| `split/unicode` | `SplitStrings` | 58.826 us | 39.274 us | CPython faster | 1.50x |
| `split/zero-width` | `SplitStrings` | 92.880 us | 65.863 us | CPython faster | 1.41x |
| `split/captures` | `SplitStrings` | 335.348 us | 250.868 us | CPython faster | 1.33x |
| `split/no-captures` | `SplitStrings` | 247.446 us | 223.550 us | CPython faster | 1.10x |
| `split/optional-capture` | `SplitStrings` | 67.867 us | 87.877 us | Managed faster | 0.77x |
| `split/detailed` | `SplitDetailed` | 88.695 us | 135.155 us | Managed faster | 0.65x |
| `split/one-separator` | `SplitStrings` | 1.261 us | 11.869 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.227 us | 24.602 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 70.265 us | 392.828 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 83.109 us | 616.852 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 47.418 us | 572.724 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,088.685 us | 352,204.967 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `backreference/fullmatch-hit`: Removable harness floors are 0.12 %/8.76 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.20 %/15.80 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.19 %/12.50 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.21 %/14.50 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 328.328 us / 67,609 B | 59.913 us | 308.741 us / 67,633 B | 60.414 us |
| `fallback` | 592.175 us / 79,769 B | 42.533 us | 439.591 us / 79,737 B | 42.551 us |
| `literal` | 232.622 us / 50,393 B | 25.432 us | 209.641 us / 50,361 B | 25.096 us |
| `unicode` | 154.069 us / 48,337 B | 36.421 us | 157.438 us / 48,305 B | 36.522 us |

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
