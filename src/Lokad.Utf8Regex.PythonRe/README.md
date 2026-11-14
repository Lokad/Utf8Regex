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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev87-20261114/README.md).

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

Scaling rows retain their original source/protocol. `candidate-position`,
`capture-count` and `unicode-coordinate-density` still use the earlier CPython
coordinate preparation; treat their relative costs as historical diagnostics
until the next coherent refresh. Their observations have not been remeasured.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `47E6DB2F768FEB5629C21342AC4537FE908F395E9882590CC0E4AD5F598B5931`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `47` managed faster, `1` equivalent, `27` CPython faster, `8` inconclusive, `7` unqualified.

Measured source `cc59d1fbe554`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.
Measured source `d7e28bd3e731`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.926 us | 0.844 us | CPython faster | 2.28x |
| `atomic/search-miss` | `Search` | 0.404 us | 0.281 us | CPython faster | 1.44x |
| `possessive/search-hit` | `Search` | 0.572 us | 0.417 us | Inconclusive | 1.39x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.302 us | 0.220 us | CPython faster | 1.36x |
| `atomic/search-hit` | `Search` | 0.560 us | 0.432 us | CPython faster | 1.29x |
| `reluctant/search-hit` | `Search` | 0.589 us | 0.456 us | CPython faster | 1.29x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.253 us | 0.201 us | CPython faster | 1.26x |
| `lookbehind/search-miss` | `Search` | 0.279 us | 0.242 us | CPython faster | 1.15x |
| `leading-inline/search-hit` | `Search` | 0.468 us | 0.440 us | CPython faster | 1.06x |
| `ascii-option/search-hit` | `Search` | 0.352 us | 0.413 us | Managed faster | 0.85x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.327 us | 0.391 us | Managed faster | 0.83x |
| `lookahead/search-miss` | `Search` | 0.177 us | 0.217 us | Managed faster | 0.81x |
| `scoped-inline/search-hit` | `Search` | 0.309 us | 0.400 us | Managed faster | 0.77x |
| `alternation/search-miss` | `Search` | 0.179 us | 0.259 us | Managed faster | 0.70x |
| `ignorecase/search-hit` | `Search` | 0.280 us | 0.465 us | Managed faster | 0.60x |
| `multiline/search-hit` | `Search` | 0.253 us | 0.442 us | Managed faster | 0.57x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.189 us | 0.335 us | Managed faster | 0.57x |
| `ignorecase/search-miss` | `Search` | 0.182 us | 0.343 us | Managed faster | 0.53x |
| `lookahead/search-hit` | `Search` | 0.176 us | 0.406 us | Managed faster | 0.44x |
| `unicode-option/search-hit` | `Search` | 0.450 us | 1.216 us | Inconclusive | 0.37x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.105 us | 0.433 us | Managed faster | 0.24x |
| `literal/search-early` | `Search` | 0.069 us | 0.333 us | Managed faster | 0.21x |
| `unicode-literal/search-miss` | `Search` | 0.066 us | 0.321 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.052 us | 0.311 us | Managed faster | 0.16x |
| `prefix/match` | `Match` | 0.052 us | 0.386 us | Managed faster | 0.14x |
| `unicode/fullmatch` | `FullMatch` | 3.083 us | 30.095 us | Managed faster | 0.10x |
| `literal/search` | `Search` | 1.945 us | 22.614 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.074 us | 1.231 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.081 us | 21.795 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.066 us | 22.294 us | Managed faster | 0.05x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.040 us | 1.135 us | Managed faster | 0.04x |
| `greedy/search-miss` | `Search` | 0.137 us | 0.240 us | Unqualified | — |
| `literal/fullmatch` | `FullMatch` | 0.035 us | 0.455 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.447 us | 0.439 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.274 us | 0.285 us | Unqualified | — |
| `possessive/search-miss` | `Search` | 0.128 us | 0.232 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.216 us | 0.236 us | Unqualified | — |
| `verbose/fullmatch-hit` | `FullMatch` | 0.399 us | 0.406 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.781 us | 2.056 us | Managed faster | 0.38x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 670.652 us | 337.848 us | CPython faster | 2.00x |
| `findall/one-capture-strings` | `FindAllStrings` | 327.509 us | 185.935 us | CPython faster | 1.76x |
| `findall/named-capture-strings` | `FindAllStrings` | 139.632 us | 94.547 us | CPython faster | 1.48x |
| `findall/empty-progression-strings` | `FindAllStrings` | 57.854 us | 42.546 us | CPython faster | 1.36x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 83.195 us | 62.182 us | CPython faster | 1.34x |
| `findall/full-strings` | `FindAllStrings` | 450.427 us | 364.070 us | CPython faster | 1.24x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.855 us | 15.908 us | CPython faster | 1.06x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.405 us | 53.958 us | Managed faster | 0.95x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 504.230 us | 598.295 us | Managed faster | 0.85x |
| `findall/full-utf8` | `FindAllUtf8` | 388.153 us | 499.946 us | Managed faster | 0.78x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.402 us | 27.473 us | Managed faster | 0.67x |
| `zero-width/count` | `Count` | 596.683 us | 1,075.022 us | Managed faster | 0.55x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 665.336 us | 1,209.841 us | Managed faster | 0.55x |
| `count/empty-progression` | `Count` | 123.091 us | 359.574 us | Managed faster | 0.34x |
| `findall/structural-many` | `FindAllStructural` | 86.114 us | 277.937 us | Managed faster | 0.31x |
| `count/from-offset` | `CountFromOffset` | 14.061 us | 49.145 us | Managed faster | 0.29x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 180.436 us | 822.211 us | Managed faster | 0.22x |
| `family/count` | `Count` | 521.605 us | 2,444.782 us | Managed faster | 0.21x |
| `count/one` | `Count` | 0.275 us | 1.945 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 48.696 us | 418.535 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 267.670 us | 2,482.463 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.781 us | 37.847 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.394 us | 87.766 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.354 us | 88.790 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.181 us | 141.006 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.028 us | 0.441 us | Inconclusive | 2.31x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 124.753 us | 69.439 us | CPython faster | 1.80x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 488.958 us | 274.031 us | CPython faster | 1.79x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 476.400 us | 271.775 us | CPython faster | 1.76x |
| `replacement/fixed-string` | `ReplaceString` | 281.421 us | 174.411 us | CPython faster | 1.61x |
| `replacement/named-template-string` | `ReplaceString` | 1.163 us | 0.904 us | CPython faster | 1.29x |
| `replacement/subn-string` | `SubnString` | 112.078 us | 87.490 us | CPython faster | 1.28x |
| `replacement/subn-utf8` | `SubnUtf8` | 107.293 us | 88.945 us | CPython faster | 1.21x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.674 us | 23.192 us | CPython faster | 1.16x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 178.443 us | 176.532 us | Equivalent | 1.01x |
| `replacement/unmatched-template-string` | `ReplaceString` | 42.788 us | 46.836 us | Managed faster | 0.92x |
| `replacement/no-match-string` | `ReplaceString` | 1.044 us | 2.351 us | Inconclusive | 0.44x |
| `replacement/one-template-string` | `ReplaceString` | 1.800 us | 16,157.206 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.486 us | 0.522 us | Inconclusive | 2.88x |
| `split/zero-width` | `SplitStrings` | 117.721 us | 64.850 us | CPython faster | 1.81x |
| `split/unicode` | `SplitStrings` | 60.524 us | 38.061 us | CPython faster | 1.59x |
| `split/captures` | `SplitStrings` | 393.015 us | 252.752 us | CPython faster | 1.55x |
| `split/no-captures` | `SplitStrings` | 260.649 us | 225.864 us | CPython faster | 1.14x |
| `split/optional-capture` | `SplitStrings` | 75.259 us | 85.727 us | Managed faster | 0.87x |
| `split/detailed` | `SplitDetailed` | 102.655 us | 133.295 us | Managed faster | 0.77x |
| `split/one-separator` | `SplitStrings` | 1.243 us | 11.830 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.268 us | 24.348 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 67.572 us | 388.354 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 80.513 us | 600.217 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 48.496 us | 564.626 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 1,096.664 us | 347,940.812 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `greedy/search-miss`: At least one lane sample was shorter than 20 ms.
- `literal/fullmatch`: At least one lane sample was shorter than 20 ms.
- `lookbehind/search-hit`: Removable harness floors are 0.14 %/7.76 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.20 %/11.93 % of the lanes.
- `possessive/search-miss`: At least one lane sample was shorter than 20 ms.
- `unicode-category/search-miss`: Removable harness floors are 0.24 %/14.59 % of the lanes.
- `verbose/fullmatch-hit`: Removable harness floors are 0.15 %/8.25 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 186.469 us / 67,593 B | 60.705 us | 169.444 us / 67,617 B | 60.400 us |
| `fallback` | 267.672 us / 79,753 B | 41.639 us | 240.756 us / 79,721 B | 41.095 us |
| `literal` | 301.247 us / 50,377 B | 24.222 us | 271.562 us / 50,345 B | 23.774 us |
| `unicode` | 53.475 us / 48,321 B | 34.654 us | 53.513 us / 48,289 B | 35.022 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
