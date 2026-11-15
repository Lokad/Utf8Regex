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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev96-20261115/README.md).

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

Snapshot SHA-256: `350DA96243417837564AEA7EEE19555218C12032B594638741A5B2F0B89DB4ED`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `52` managed faster, `1` equivalent, `26` CPython faster, `7` inconclusive, `4` unqualified.

Measured source `4d9234147bcd`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.790 us | 0.823 us | CPython faster | 2.18x |
| `atomic/search-miss` | `Search` | 0.454 us | 0.274 us | CPython faster | 1.66x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.332 us | 0.211 us | CPython faster | 1.57x |
| `lookbehind/search-miss` | `Search` | 0.298 us | 0.233 us | CPython faster | 1.28x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.240 us | 0.197 us | CPython faster | 1.21x |
| `possessive/search-hit` | `Search` | 0.455 us | 0.403 us | CPython faster | 1.13x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.438 us | 0.395 us | CPython faster | 1.11x |
| `atomic/search-hit` | `Search` | 0.444 us | 0.414 us | CPython faster | 1.07x |
| `reluctant/search-hit` | `Search` | 0.466 us | 0.443 us | CPython faster | 1.06x |
| `leading-inline/search-hit` | `Search` | 0.369 us | 0.426 us | Managed faster | 0.85x |
| `scoped-inline/search-hit` | `Search` | 0.323 us | 0.383 us | Managed faster | 0.84x |
| `lookbehind/search-hit` | `Search` | 0.334 us | 0.429 us | Managed faster | 0.78x |
| `alternation/search-miss` | `Search` | 0.186 us | 0.245 us | Managed faster | 0.75x |
| `ignorecase/search-hit` | `Search` | 0.318 us | 0.446 us | Managed faster | 0.71x |
| `ignorecase/search-miss` | `Search` | 0.205 us | 0.302 us | Managed faster | 0.68x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.205 us | 0.321 us | Managed faster | 0.64x |
| `multiline/search-hit` | `Search` | 0.268 us | 0.424 us | Managed faster | 0.63x |
| `greedy/search-miss` | `Search` | 0.138 us | 0.234 us | Managed faster | 0.59x |
| `ascii-option/search-hit` | `Search` | 0.231 us | 0.401 us | Managed faster | 0.58x |
| `possessive/search-miss` | `Search` | 0.123 us | 0.223 us | Managed faster | 0.55x |
| `lookahead/search-hit` | `Search` | 0.203 us | 0.392 us | Managed faster | 0.52x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.123 us | 0.421 us | Managed faster | 0.29x |
| `unicode-option/search-hit` | `Search` | 0.333 us | 1.155 us | Managed faster | 0.29x |
| `prefix/match` | `Match` | 0.083 us | 0.365 us | Managed faster | 0.23x |
| `unicode-literal/search-miss` | `Search` | 0.069 us | 0.310 us | Managed faster | 0.22x |
| `literal/search-early` | `Search` | 0.069 us | 0.320 us | Managed faster | 0.22x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.311 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.055 us | 0.454 us | Managed faster | 0.12x |
| `unicode/fullmatch` | `FullMatch` | 3.336 us | 29.093 us | Managed faster | 0.11x |
| `literal/search` | `Search` | 1.955 us | 21.839 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.077 us | 1.203 us | Managed faster | 0.06x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.064 us | 1.101 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.077 us | 21.510 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.037 us | 21.501 us | Managed faster | 0.05x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.370 us | 0.380 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.189 us | 0.208 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.280 us | 0.279 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.193 us | 0.225 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.725 us | 2.008 us | Managed faster | 0.36x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 305.504 us | 210.494 us | CPython faster | 1.45x |
| `findall/many-capture-strings` | `FindAllStrings` | 467.615 us | 334.113 us | CPython faster | 1.41x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 81.640 us | 59.822 us | CPython faster | 1.36x |
| `findall/empty-progression-strings` | `FindAllStrings` | 53.944 us | 43.765 us | CPython faster | 1.23x |
| `findall/named-capture-strings` | `FindAllStrings` | 91.974 us | 85.069 us | CPython faster | 1.08x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.689 us | 15.518 us | CPython faster | 1.08x |
| `findall/full-strings` | `FindAllStrings` | 341.485 us | 331.357 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 45.111 us | 53.152 us | Managed faster | 0.85x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 452.474 us | 591.047 us | Managed faster | 0.77x |
| `findall/unicode-full-strings` | `FindAllStrings` | 17.503 us | 26.332 us | Managed faster | 0.67x |
| `findall/full-utf8` | `FindAllUtf8` | 301.064 us | 498.578 us | Managed faster | 0.60x |
| `zero-width/count` | `Count` | 390.232 us | 1,014.716 us | Managed faster | 0.39x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 447.824 us | 1,189.197 us | Managed faster | 0.37x |
| `count/empty-progression` | `Count` | 116.523 us | 317.892 us | Managed faster | 0.37x |
| `findall/structural-many` | `FindAllStructural` | 85.275 us | 268.378 us | Managed faster | 0.32x |
| `count/from-offset` | `CountFromOffset` | 13.797 us | 47.070 us | Inconclusive | 0.29x |
| `family/count` | `Count` | 524.327 us | 2,151.532 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 157.502 us | 792.932 us | Managed faster | 0.20x |
| `count/one` | `Count` | 0.272 us | 1.907 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 47.722 us | 407.674 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 255.486 us | 2,287.406 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.701 us | 37.382 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.370 us | 86.561 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.352 us | 89.142 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.187 us | 136.803 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.006 us | 0.446 us | Inconclusive | 2.25x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 86.719 us | 62.687 us | CPython faster | 1.39x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 340.314 us | 269.424 us | CPython faster | 1.26x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 332.031 us | 266.083 us | CPython faster | 1.25x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.672 us | 22.844 us | CPython faster | 1.13x |
| `replacement/fixed-string` | `ReplaceString` | 181.511 us | 171.927 us | CPython faster | 1.06x |
| `replacement/named-template-string` | `ReplaceString` | 0.932 us | 0.894 us | Inconclusive | 1.05x |
| `replacement/subn-string` | `SubnString` | 90.806 us | 86.735 us | CPython faster | 1.05x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 171.942 us | 172.632 us | Equivalent | 0.99x |
| `replacement/subn-utf8` | `SubnUtf8` | 85.536 us | 88.294 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 37.348 us | 46.261 us | Managed faster | 0.81x |
| `replacement/no-match-string` | `ReplaceString` | 1.064 us | 2.334 us | Inconclusive | 0.46x |
| `replacement/one-template-string` | `ReplaceString` | 1.687 us | 15,769.007 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.263 us | 0.519 us | Inconclusive | 2.45x |
| `split/unicode` | `SplitStrings` | 54.603 us | 37.590 us | CPython faster | 1.45x |
| `split/zero-width` | `SplitStrings` | 88.467 us | 64.550 us | CPython faster | 1.37x |
| `split/captures` | `SplitStrings` | 312.635 us | 247.892 us | CPython faster | 1.25x |
| `split/no-captures` | `SplitStrings` | 233.875 us | 218.513 us | CPython faster | 1.06x |
| `split/optional-capture` | `SplitStrings` | 63.488 us | 85.607 us | Managed faster | 0.74x |
| `split/detailed` | `SplitDetailed` | 85.833 us | 129.953 us | Managed faster | 0.66x |
| `split/one-separator` | `SplitStrings` | 0.988 us | 11.528 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.207 us | 23.840 us | Managed faster | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 66.420 us | 378.682 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 79.734 us | 601.116 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 47.002 us | 555.831 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,074.468 us | 345,860.017 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `backreference/fullmatch-hit`: Removable harness floors are 0.12 %/8.96 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/15.84 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.15 %/11.44 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.20 %/14.54 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 171.928 us / 67,609 B | 59.000 us | 170.147 us / 67,633 B | 58.690 us |
| `fallback` | 264.462 us / 79,769 B | 39.311 us | 241.925 us / 79,737 B | 40.469 us |
| `literal` | 292.438 us / 50,393 B | 23.345 us | 301.613 us / 50,361 B | 23.645 us |
| `unicode` | 51.791 us / 48,337 B | 35.061 us | 55.700 us / 48,305 B | 35.809 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
