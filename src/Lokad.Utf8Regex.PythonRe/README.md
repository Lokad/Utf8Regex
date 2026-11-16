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
are excluded. See the [current delivery](../../bench/Experiments/Release050-20261115/README.md).

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
Unknown string lengths up to 4096 UTF-8 bytes, including one-shot and literal
replacement string results,
use a byte-derived capacity bound before decoding; Unicode results can therefore
reach a finite byte limit earlier. Larger results retain bounded exact sizing.
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

Snapshot SHA-256: `9992B19D3EA960F00F43E8F9599F9C7856C1D0C1A2F0D7B44C8A1372E14B820A`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `51` managed faster, `1` equivalent, `26` CPython faster, `9` inconclusive, `3` unqualified.

Measured source `0e10d86b6038`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.914 us | 0.855 us | CPython faster | 2.24x |
| `atomic/search-miss` | `Search` | 0.464 us | 0.282 us | CPython faster | 1.64x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.345 us | 0.220 us | CPython faster | 1.57x |
| `lookbehind/search-miss` | `Search` | 0.305 us | 0.241 us | CPython faster | 1.27x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.247 us | 0.203 us | CPython faster | 1.21x |
| `possessive/search-hit` | `Search` | 0.484 us | 0.418 us | CPython faster | 1.16x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.466 us | 0.403 us | CPython faster | 1.16x |
| `reluctant/search-hit` | `Search` | 0.515 us | 0.457 us | CPython faster | 1.13x |
| `atomic/search-hit` | `Search` | 0.479 us | 0.435 us | CPython faster | 1.10x |
| `scoped-inline/search-hit` | `Search` | 0.342 us | 0.398 us | Managed faster | 0.86x |
| `leading-inline/search-hit` | `Search` | 0.379 us | 0.442 us | Managed faster | 0.86x |
| `unicode-category/search-miss` | `Search` | 0.192 us | 0.235 us | Managed faster | 0.81x |
| `lookbehind/search-hit` | `Search` | 0.350 us | 0.442 us | Managed faster | 0.79x |
| `ignorecase/search-hit` | `Search` | 0.338 us | 0.465 us | Managed faster | 0.73x |
| `alternation/search-miss` | `Search` | 0.179 us | 0.257 us | Managed faster | 0.69x |
| `ignorecase/search-miss` | `Search` | 0.197 us | 0.311 us | Managed faster | 0.63x |
| `multiline/search-hit` | `Search` | 0.274 us | 0.437 us | Managed faster | 0.63x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.209 us | 0.335 us | Managed faster | 0.63x |
| `ascii-option/search-hit` | `Search` | 0.238 us | 0.416 us | Managed faster | 0.57x |
| `greedy/search-miss` | `Search` | 0.135 us | 0.241 us | Managed faster | 0.56x |
| `lookahead/search-hit` | `Search` | 0.209 us | 0.407 us | Managed faster | 0.51x |
| `possessive/search-miss` | `Search` | 0.117 us | 0.229 us | Managed faster | 0.51x |
| `unicode-option/search-hit` | `Search` | 0.352 us | 1.188 us | Managed faster | 0.30x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.122 us | 0.432 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.075 us | 0.318 us | Managed faster | 0.24x |
| `literal/search-early` | `Search` | 0.068 us | 0.330 us | Managed faster | 0.21x |
| `prefix/match` | `Match` | 0.066 us | 0.382 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.319 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.058 us | 0.456 us | Managed faster | 0.13x |
| `unicode/fullmatch` | `FullMatch` | 3.564 us | 29.884 us | Managed faster | 0.12x |
| `literal/search` | `Search` | 2.043 us | 22.723 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.079 us | 1.227 us | Managed faster | 0.06x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.066 us | 1.149 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.123 us | 22.401 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.074 us | 22.051 us | Managed faster | 0.05x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.394 us | 0.395 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.188 us | 0.216 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.271 us | 0.289 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.742 us | 2.071 us | Inconclusive | 0.36x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 298.581 us | 216.648 us | CPython faster | 1.39x |
| `findall/many-capture-strings` | `FindAllStrings` | 460.816 us | 346.250 us | CPython faster | 1.33x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 82.323 us | 61.766 us | CPython faster | 1.32x |
| `findall/empty-progression-strings` | `FindAllStrings` | 59.145 us | 45.594 us | CPython faster | 1.30x |
| `findall/named-capture-strings` | `FindAllStrings` | 93.255 us | 88.039 us | Inconclusive | 1.06x |
| `findall/full-strings` | `FindAllStrings` | 362.407 us | 345.609 us | CPython faster | 1.05x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.541 us | 15.883 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 46.542 us | 55.483 us | Managed faster | 0.84x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 480.497 us | 612.566 us | Managed faster | 0.79x |
| `findall/unicode-full-strings` | `FindAllStrings` | 21.088 us | 27.335 us | Managed faster | 0.77x |
| `findall/full-utf8` | `FindAllUtf8` | 328.417 us | 518.388 us | Managed faster | 0.63x |
| `zero-width/count` | `Count` | 404.206 us | 1,028.514 us | Managed faster | 0.40x |
| `count/empty-progression` | `Count` | 123.106 us | 334.574 us | Managed faster | 0.37x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 438.500 us | 1,232.619 us | Managed faster | 0.36x |
| `findall/structural-many` | `FindAllStructural` | 84.512 us | 273.895 us | Managed faster | 0.31x |
| `count/from-offset` | `CountFromOffset` | 13.872 us | 48.775 us | Managed faster | 0.28x |
| `family/count` | `Count` | 543.369 us | 2,217.407 us | Managed faster | 0.25x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 157.868 us | 821.523 us | Managed faster | 0.19x |
| `count/one` | `Count` | 0.286 us | 1.985 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 49.316 us | 416.385 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 269.915 us | 2,359.802 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.765 us | 38.491 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.394 us | 89.563 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.357 us | 89.954 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.192 us | 141.933 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.087 us | 0.454 us | Inconclusive | 2.40x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 85.406 us | 64.321 us | CPython faster | 1.33x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 335.992 us | 279.906 us | CPython faster | 1.20x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 330.430 us | 275.310 us | CPython faster | 1.20x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.602 us | 23.714 us | CPython faster | 1.12x |
| `replacement/named-template-string` | `ReplaceString` | 0.978 us | 0.911 us | CPython faster | 1.08x |
| `replacement/fixed-string` | `ReplaceString` | 187.135 us | 177.916 us | CPython faster | 1.05x |
| `replacement/subn-string` | `SubnString` | 93.335 us | 88.210 us | CPython faster | 1.05x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 177.102 us | 177.064 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 88.526 us | 90.309 us | Inconclusive | 0.98x |
| `replacement/unmatched-template-string` | `ReplaceString` | 38.402 us | 48.011 us | Managed faster | 0.80x |
| `replacement/no-match-string` | `ReplaceString` | 1.207 us | 2.398 us | Inconclusive | 0.50x |
| `replacement/one-template-string` | `ReplaceString` | 1.912 us | 16,120.249 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.381 us | 0.531 us | Inconclusive | 2.55x |
| `split/unicode` | `SplitStrings` | 57.550 us | 39.147 us | CPython faster | 1.47x |
| `split/zero-width` | `SplitStrings` | 91.510 us | 66.683 us | CPython faster | 1.37x |
| `split/captures` | `SplitStrings` | 316.785 us | 254.403 us | CPython faster | 1.25x |
| `split/no-captures` | `SplitStrings` | 239.356 us | 224.466 us | CPython faster | 1.07x |
| `split/optional-capture` | `SplitStrings` | 65.659 us | 88.318 us | Managed faster | 0.74x |
| `split/detailed` | `SplitDetailed` | 88.875 us | 136.405 us | Managed faster | 0.65x |
| `split/one-separator` | `SplitStrings` | 1.085 us | 11.962 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.284 us | 24.776 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.678 us | 389.764 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 82.704 us | 623.474 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 47.458 us | 576.154 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,105.291 us | 354,593.309 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `backreference/fullmatch-hit`: Removable harness floors are 0.15 %/8.68 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.23 %/15.86 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.19 %/11.66 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 143.050 us / 67,609 B | 62.764 us | 142.044 us / 67,633 B | 61.707 us |
| `fallback` | 267.428 us / 79,769 B | 43.962 us | 197.363 us / 79,737 B | 44.309 us |
| `literal` | 247.766 us / 50,393 B | 25.066 us | 216.391 us / 50,361 B | 24.990 us |
| `unicode` | 92.287 us / 48,337 B | 38.023 us | 84.097 us / 48,305 B | 37.829 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
