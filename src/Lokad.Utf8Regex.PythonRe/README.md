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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev89-20261114/README.md).

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

Snapshot SHA-256: `C5F3069D18098C5501540C39E81081D5FD4C1924E8EB865F3A34CE5907CAF3C1`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `52` managed faster, `2` equivalent, `26` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `cd8eb5c7f1a7`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.802 us | 0.831 us | CPython faster | 2.17x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.357 us | 0.214 us | CPython faster | 1.67x |
| `atomic/search-miss` | `Search` | 0.456 us | 0.278 us | CPython faster | 1.64x |
| `lookbehind/search-miss` | `Search` | 0.313 us | 0.236 us | CPython faster | 1.31x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.242 us | 0.200 us | CPython faster | 1.21x |
| `possessive/search-hit` | `Search` | 0.472 us | 0.400 us | CPython faster | 1.17x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.460 us | 0.399 us | CPython faster | 1.15x |
| `atomic/search-hit` | `Search` | 0.471 us | 0.425 us | CPython faster | 1.11x |
| `reluctant/search-hit` | `Search` | 0.486 us | 0.447 us | CPython faster | 1.09x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.411 us | 0.389 us | CPython faster | 1.06x |
| `scoped-inline/search-hit` | `Search` | 0.343 us | 0.390 us | Managed faster | 0.88x |
| `leading-inline/search-hit` | `Search` | 0.375 us | 0.431 us | Managed faster | 0.87x |
| `lookbehind/search-hit` | `Search` | 0.353 us | 0.433 us | Managed faster | 0.82x |
| `alternation/search-miss` | `Search` | 0.189 us | 0.253 us | Managed faster | 0.74x |
| `ignorecase/search-hit` | `Search` | 0.324 us | 0.451 us | Managed faster | 0.72x |
| `ignorecase/search-miss` | `Search` | 0.204 us | 0.300 us | Managed faster | 0.68x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.209 us | 0.328 us | Managed faster | 0.63x |
| `multiline/search-hit` | `Search` | 0.272 us | 0.432 us | Managed faster | 0.63x |
| `greedy/search-miss` | `Search` | 0.147 us | 0.238 us | Managed faster | 0.61x |
| `ascii-option/search-hit` | `Search` | 0.237 us | 0.405 us | Managed faster | 0.58x |
| `possessive/search-miss` | `Search` | 0.128 us | 0.226 us | Managed faster | 0.56x |
| `lookahead/search-hit` | `Search` | 0.204 us | 0.397 us | Managed faster | 0.52x |
| `unicode-option/search-hit` | `Search` | 0.329 us | 1.154 us | Managed faster | 0.29x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.117 us | 0.419 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.067 us | 0.310 us | Managed faster | 0.22x |
| `literal/search-early` | `Search` | 0.071 us | 0.330 us | Managed faster | 0.21x |
| `prefix/match` | `Match` | 0.066 us | 0.377 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.311 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.057 us | 0.452 us | Managed faster | 0.13x |
| `unicode/fullmatch` | `FullMatch` | 3.603 us | 28.853 us | Managed faster | 0.12x |
| `literal/search` | `Search` | 1.966 us | 22.120 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.077 us | 1.214 us | Managed faster | 0.06x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.063 us | 1.135 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.085 us | 21.670 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.054 us | 21.875 us | Managed faster | 0.05x |
| `lookahead/search-miss` | `Search` | 0.199 us | 0.213 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.271 us | 0.277 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.197 us | 0.232 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.837 us | 2.027 us | Managed faster | 0.41x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 396.322 us | 212.423 us | CPython faster | 1.87x |
| `findall/many-capture-strings` | `FindAllStrings` | 591.324 us | 340.187 us | CPython faster | 1.74x |
| `findall/named-capture-strings` | `FindAllStrings` | 118.531 us | 87.412 us | CPython faster | 1.36x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 80.562 us | 60.225 us | CPython faster | 1.34x |
| `findall/empty-progression-strings` | `FindAllStrings` | 53.178 us | 44.577 us | CPython faster | 1.20x |
| `findall/full-strings` | `FindAllStrings` | 394.977 us | 341.866 us | CPython faster | 1.15x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 15.976 us | 15.387 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 47.941 us | 54.592 us | Managed faster | 0.88x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 461.403 us | 599.909 us | Managed faster | 0.77x |
| `findall/unicode-full-strings` | `FindAllStrings` | 17.557 us | 26.902 us | Managed faster | 0.65x |
| `findall/full-utf8` | `FindAllUtf8` | 318.943 us | 501.297 us | Managed faster | 0.64x |
| `zero-width/count` | `Count` | 480.158 us | 1,032.555 us | Managed faster | 0.46x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 548.317 us | 1,223.952 us | Managed faster | 0.45x |
| `findall/structural-many` | `FindAllStructural` | 116.497 us | 268.548 us | Managed faster | 0.44x |
| `count/empty-progression` | `Count` | 115.852 us | 330.702 us | Managed faster | 0.35x |
| `count/from-offset` | `CountFromOffset` | 14.277 us | 47.657 us | Managed faster | 0.30x |
| `family/count` | `Count` | 528.464 us | 2,172.631 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 191.205 us | 813.789 us | Managed faster | 0.24x |
| `count/one` | `Count` | 0.279 us | 1.954 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 48.282 us | 422.920 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 264.759 us | 2,336.431 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.704 us | 37.275 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.385 us | 88.209 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.347 us | 88.997 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.189 us | 137.323 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.036 us | 0.441 us | Inconclusive | 2.33x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 99.258 us | 62.005 us | CPython faster | 1.60x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 386.929 us | 271.265 us | CPython faster | 1.43x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 375.038 us | 265.419 us | CPython faster | 1.41x |
| `replacement/named-template-string` | `ReplaceString` | 0.979 us | 0.903 us | Inconclusive | 1.08x |
| `replacement/empty-progression-string` | `ReplaceString` | 24.819 us | 23.146 us | CPython faster | 1.07x |
| `replacement/subn-string` | `SubnString` | 92.123 us | 88.206 us | CPython faster | 1.05x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 174.704 us | 174.232 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 88.475 us | 88.973 us | Equivalent | 0.99x |
| `replacement/unmatched-template-string` | `ReplaceString` | 38.140 us | 46.246 us | Managed faster | 0.82x |
| `replacement/fixed-string` | `ReplaceString` | 182.437 us | 250.970 us | Managed faster | 0.73x |
| `replacement/no-match-string` | `ReplaceString` | 1.094 us | 2.340 us | Inconclusive | 0.46x |
| `replacement/one-template-string` | `ReplaceString` | 1.686 us | 15,960.246 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.583 us | 0.524 us | Inconclusive | 3.00x |
| `split/zero-width` | `SplitStrings` | 115.214 us | 65.809 us | CPython faster | 1.75x |
| `split/unicode` | `SplitStrings` | 58.418 us | 38.824 us | CPython faster | 1.51x |
| `split/captures` | `SplitStrings` | 338.972 us | 251.731 us | CPython faster | 1.35x |
| `split/no-captures` | `SplitStrings` | 249.425 us | 221.373 us | CPython faster | 1.13x |
| `split/optional-capture` | `SplitStrings` | 69.362 us | 89.063 us | Managed faster | 0.78x |
| `split/detailed` | `SplitDetailed` | 90.519 us | 132.139 us | Managed faster | 0.68x |
| `split/one-separator` | `SplitStrings` | 1.251 us | 11.818 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.277 us | 24.285 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.820 us | 384.116 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 78.668 us | 612.907 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 46.235 us | 562.279 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,093.650 us | 347,944.473 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.20 %/15.82 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/12.13 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.21 %/14.29 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 188.291 us / 67,593 B | 59.800 us | 173.075 us / 67,617 B | 60.342 us |
| `fallback` | 273.141 us / 79,753 B | 41.097 us | 247.072 us / 79,721 B | 41.220 us |
| `literal` | 337.062 us / 50,377 B | 25.014 us | 308.309 us / 50,345 B | 24.203 us |
| `unicode` | 55.319 us / 48,321 B | 35.422 us | 54.650 us / 48,289 B | 35.615 us |

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
