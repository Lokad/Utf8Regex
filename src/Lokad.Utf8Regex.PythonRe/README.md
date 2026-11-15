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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev95-20261115/README.md).

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

Snapshot SHA-256: `D8D5334E5650C974E5334AC6BD4692D5CF2EF4C4783E31DBDE2D197D9A33EE9B`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `51` managed faster, `2` equivalent, `27` CPython faster, `6` inconclusive, `4` unqualified.

Measured source `b50bb5e3f42e`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.834 us | 0.839 us | CPython faster | 2.20x |
| `atomic/search-miss` | `Search` | 0.446 us | 0.276 us | CPython faster | 1.61x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.345 us | 0.214 us | CPython faster | 1.60x |
| `lookbehind/search-miss` | `Search` | 0.313 us | 0.241 us | CPython faster | 1.30x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.248 us | 0.202 us | CPython faster | 1.23x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.456 us | 0.401 us | CPython faster | 1.14x |
| `possessive/search-hit` | `Search` | 0.455 us | 0.410 us | CPython faster | 1.12x |
| `reluctant/search-hit` | `Search` | 0.479 us | 0.447 us | CPython faster | 1.07x |
| `atomic/search-hit` | `Search` | 0.446 us | 0.421 us | CPython faster | 1.06x |
| `leading-inline/search-hit` | `Search` | 0.362 us | 0.430 us | Managed faster | 0.84x |
| `scoped-inline/search-hit` | `Search` | 0.329 us | 0.391 us | Managed faster | 0.84x |
| `lookbehind/search-hit` | `Search` | 0.347 us | 0.433 us | Managed faster | 0.80x |
| `alternation/search-miss` | `Search` | 0.183 us | 0.249 us | Managed faster | 0.74x |
| `ignorecase/search-hit` | `Search` | 0.323 us | 0.457 us | Managed faster | 0.71x |
| `ignorecase/search-miss` | `Search` | 0.203 us | 0.303 us | Managed faster | 0.66x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.209 us | 0.328 us | Managed faster | 0.64x |
| `multiline/search-hit` | `Search` | 0.271 us | 0.435 us | Managed faster | 0.62x |
| `greedy/search-miss` | `Search` | 0.141 us | 0.233 us | Managed faster | 0.60x |
| `ascii-option/search-hit` | `Search` | 0.241 us | 0.409 us | Managed faster | 0.59x |
| `possessive/search-miss` | `Search` | 0.124 us | 0.226 us | Managed faster | 0.55x |
| `lookahead/search-hit` | `Search` | 0.211 us | 0.403 us | Managed faster | 0.52x |
| `unicode-option/search-hit` | `Search` | 0.344 us | 1.156 us | Managed faster | 0.30x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.117 us | 0.424 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.069 us | 0.313 us | Managed faster | 0.22x |
| `literal/search-early` | `Search` | 0.068 us | 0.330 us | Managed faster | 0.21x |
| `prefix/match` | `Match` | 0.066 us | 0.376 us | Managed faster | 0.18x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.314 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.056 us | 0.444 us | Managed faster | 0.13x |
| `unicode/fullmatch` | `FullMatch` | 3.623 us | 29.445 us | Managed faster | 0.12x |
| `literal/search` | `Search` | 1.937 us | 22.319 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.077 us | 1.223 us | Managed faster | 0.06x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.063 us | 1.128 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.079 us | 21.770 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.061 us | 21.873 us | Managed faster | 0.05x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.389 us | 0.386 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.197 us | 0.213 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.279 us | 0.285 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.196 us | 0.231 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.738 us | 2.001 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 349.851 us | 216.223 us | CPython faster | 1.62x |
| `findall/many-capture-strings` | `FindAllStrings` | 509.863 us | 336.940 us | CPython faster | 1.50x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 82.173 us | 60.559 us | CPython faster | 1.35x |
| `findall/empty-progression-strings` | `FindAllStrings` | 55.654 us | 44.459 us | CPython faster | 1.26x |
| `findall/named-capture-strings` | `FindAllStrings` | 103.018 us | 86.572 us | Inconclusive | 1.19x |
| `findall/full-strings` | `FindAllStrings` | 378.845 us | 345.341 us | CPython faster | 1.10x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.061 us | 15.436 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 44.955 us | 53.973 us | Managed faster | 0.83x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 474.752 us | 610.307 us | Managed faster | 0.78x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.418 us | 26.644 us | Managed faster | 0.69x |
| `findall/full-utf8` | `FindAllUtf8` | 336.197 us | 506.695 us | Managed faster | 0.66x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 520.863 us | 1,216.776 us | Managed faster | 0.42x |
| `zero-width/count` | `Count` | 407.019 us | 1,031.463 us | Managed faster | 0.39x |
| `findall/structural-many` | `FindAllStructural` | 99.942 us | 272.192 us | Managed faster | 0.37x |
| `count/empty-progression` | `Count` | 117.456 us | 331.710 us | Managed faster | 0.35x |
| `count/from-offset` | `CountFromOffset` | 13.583 us | 48.027 us | Managed faster | 0.28x |
| `family/count` | `Count` | 522.004 us | 2,131.327 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 166.629 us | 807.100 us | Managed faster | 0.21x |
| `count/one` | `Count` | 0.278 us | 1.968 us | Managed faster | 0.14x |
| `class-run/count` | `Count` | 274.248 us | 2,334.616 us | Managed faster | 0.12x |
| `unicode/count` | `Count` | 48.022 us | 418.685 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.730 us | 37.782 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.384 us | 88.430 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.360 us | 89.317 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.193 us | 142.300 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.088 us | 0.444 us | Inconclusive | 2.42x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 93.320 us | 62.968 us | CPython faster | 1.48x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 369.845 us | 276.171 us | CPython faster | 1.34x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 363.986 us | 274.523 us | CPython faster | 1.33x |
| `replacement/named-template-string` | `ReplaceString` | 0.996 us | 0.903 us | CPython faster | 1.10x |
| `replacement/empty-progression-string` | `ReplaceString` | 24.968 us | 23.204 us | CPython faster | 1.08x |
| `replacement/fixed-string` | `ReplaceString` | 184.130 us | 176.338 us | CPython faster | 1.05x |
| `replacement/subn-string` | `SubnString` | 92.576 us | 88.489 us | CPython faster | 1.04x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 174.860 us | 174.831 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 87.616 us | 89.205 us | Equivalent | 0.98x |
| `replacement/unmatched-template-string` | `ReplaceString` | 37.246 us | 46.733 us | Managed faster | 0.79x |
| `replacement/no-match-string` | `ReplaceString` | 1.146 us | 2.380 us | Inconclusive | 0.48x |
| `replacement/one-template-string` | `ReplaceString` | 1.773 us | 15,963.220 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.328 us | 0.532 us | CPython faster | 2.50x |
| `split/zero-width` | `SplitStrings` | 94.976 us | 64.882 us | CPython faster | 1.46x |
| `split/unicode` | `SplitStrings` | 55.551 us | 38.280 us | CPython faster | 1.46x |
| `split/captures` | `SplitStrings` | 313.431 us | 253.234 us | CPython faster | 1.24x |
| `split/no-captures` | `SplitStrings` | 238.132 us | 220.147 us | CPython faster | 1.08x |
| `split/optional-capture` | `SplitStrings` | 64.225 us | 88.108 us | Managed faster | 0.73x |
| `split/detailed` | `SplitDetailed` | 84.730 us | 132.771 us | Managed faster | 0.64x |
| `split/one-separator` | `SplitStrings` | 1.052 us | 11.820 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.275 us | 24.499 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 67.209 us | 382.547 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 82.740 us | 617.746 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 46.476 us | 563.916 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,077.172 us | 348,007.717 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `backreference/fullmatch-hit`: Removable harness floors are 0.10 %/8.74 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/15.62 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/11.55 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.20 %/14.60 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 151.697 us / 67,609 B | 57.792 us | 155.756 us / 67,633 B | 61.159 us |
| `fallback` | 270.094 us / 79,769 B | 39.899 us | 208.950 us / 79,737 B | 41.695 us |
| `literal` | 233.141 us / 50,393 B | 25.116 us | 202.500 us / 50,361 B | 23.217 us |
| `unicode` | 89.759 us / 48,337 B | 36.420 us | 83.825 us / 48,305 B | 35.600 us |

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
