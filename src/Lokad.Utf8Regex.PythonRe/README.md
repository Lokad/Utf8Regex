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

Snapshot SHA-256: `35AB0F98F834278984241F07E61BA421D50663167D78FAB202572FE02D16ED7A`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `51` managed faster, `1` equivalent, `27` CPython faster, `8` inconclusive, `3` unqualified.

Measured source `70ea16b20f7d`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.978 us | 0.844 us | CPython faster | 2.34x |
| `atomic/search-miss` | `Search` | 0.462 us | 0.281 us | CPython faster | 1.64x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.346 us | 0.242 us | CPython faster | 1.43x |
| `lookbehind/search-miss` | `Search` | 0.308 us | 0.242 us | CPython faster | 1.27x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.246 us | 0.202 us | CPython faster | 1.22x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.473 us | 0.407 us | CPython faster | 1.16x |
| `possessive/search-hit` | `Search` | 0.465 us | 0.416 us | CPython faster | 1.12x |
| `reluctant/search-hit` | `Search` | 0.507 us | 0.452 us | CPython faster | 1.11x |
| `atomic/search-hit` | `Search` | 0.461 us | 0.431 us | CPython faster | 1.07x |
| `leading-inline/search-hit` | `Search` | 0.377 us | 0.438 us | Managed faster | 0.86x |
| `scoped-inline/search-hit` | `Search` | 0.335 us | 0.399 us | Managed faster | 0.84x |
| `lookbehind/search-hit` | `Search` | 0.355 us | 0.439 us | Managed faster | 0.81x |
| `unicode-category/search-miss` | `Search` | 0.188 us | 0.236 us | Managed faster | 0.80x |
| `ignorecase/search-hit` | `Search` | 0.338 us | 0.464 us | Managed faster | 0.73x |
| `alternation/search-miss` | `Search` | 0.181 us | 0.258 us | Managed faster | 0.70x |
| `ignorecase/search-miss` | `Search` | 0.198 us | 0.305 us | Managed faster | 0.65x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.210 us | 0.332 us | Managed faster | 0.63x |
| `multiline/search-hit` | `Search` | 0.287 us | 0.467 us | Managed faster | 0.61x |
| `ascii-option/search-hit` | `Search` | 0.242 us | 0.414 us | Managed faster | 0.58x |
| `greedy/search-miss` | `Search` | 0.133 us | 0.240 us | Managed faster | 0.55x |
| `lookahead/search-hit` | `Search` | 0.214 us | 0.405 us | Managed faster | 0.53x |
| `possessive/search-miss` | `Search` | 0.119 us | 0.232 us | Managed faster | 0.51x |
| `unicode-option/search-hit` | `Search` | 0.351 us | 1.181 us | Managed faster | 0.30x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.121 us | 0.434 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.071 us | 0.320 us | Managed faster | 0.22x |
| `literal/search-early` | `Search` | 0.073 us | 0.335 us | Managed faster | 0.22x |
| `prefix/match` | `Match` | 0.067 us | 0.381 us | Managed faster | 0.18x |
| `common/ip-match` | `IsMatch` | 0.052 us | 0.320 us | Managed faster | 0.16x |
| `unicode/fullmatch` | `FullMatch` | 4.277 us | 29.911 us | Managed faster | 0.14x |
| `literal/fullmatch` | `FullMatch` | 0.058 us | 0.456 us | Managed faster | 0.13x |
| `literal/search` | `Search` | 2.139 us | 22.708 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.078 us | 1.225 us | Managed faster | 0.06x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.064 us | 1.149 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.116 us | 22.100 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.073 us | 22.064 us | Managed faster | 0.05x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.395 us | 0.394 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.196 us | 0.218 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.283 us | 0.288 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.829 us | 2.040 us | Managed faster | 0.41x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 392.098 us | 218.101 us | CPython faster | 1.80x |
| `findall/many-capture-strings` | `FindAllStrings` | 616.716 us | 346.918 us | CPython faster | 1.78x |
| `findall/named-capture-strings` | `FindAllStrings` | 124.025 us | 88.864 us | CPython faster | 1.40x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 83.366 us | 61.576 us | CPython faster | 1.35x |
| `findall/empty-progression-strings` | `FindAllStrings` | 55.045 us | 45.066 us | CPython faster | 1.22x |
| `findall/full-strings` | `FindAllStrings` | 403.993 us | 346.708 us | CPython faster | 1.17x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.890 us | 16.233 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 48.934 us | 55.714 us | Managed faster | 0.88x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 502.981 us | 618.551 us | Managed faster | 0.82x |
| `findall/full-utf8` | `FindAllUtf8` | 369.231 us | 516.670 us | Managed faster | 0.71x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.770 us | 27.213 us | Managed faster | 0.69x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 590.231 us | 1,219.213 us | Inconclusive | 0.49x |
| `zero-width/count` | `Count` | 488.812 us | 1,043.735 us | Managed faster | 0.47x |
| `findall/structural-many` | `FindAllStructural` | 127.525 us | 277.727 us | Managed faster | 0.46x |
| `count/empty-progression` | `Count` | 121.870 us | 334.895 us | Managed faster | 0.37x |
| `count/from-offset` | `CountFromOffset` | 14.052 us | 49.070 us | Managed faster | 0.29x |
| `family/count` | `Count` | 542.796 us | 2,215.713 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 199.425 us | 827.618 us | Managed faster | 0.24x |
| `count/one` | `Count` | 0.284 us | 1.989 us | Managed faster | 0.14x |
| `class-run/count` | `Count` | 280.619 us | 2,360.137 us | Managed faster | 0.12x |
| `unicode/count` | `Count` | 49.976 us | 430.247 us | Managed faster | 0.12x |
| `findall/one-strings` | `FindAllStrings` | 0.785 us | 38.652 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.384 us | 89.450 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.354 us | 90.108 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.181 us | 142.143 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.046 us | 0.446 us | Inconclusive | 2.33x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 106.882 us | 64.296 us | CPython faster | 1.66x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 416.289 us | 275.618 us | CPython faster | 1.51x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 406.828 us | 279.754 us | CPython faster | 1.47x |
| `replacement/named-template-string` | `ReplaceString` | 1.078 us | 0.922 us | CPython faster | 1.17x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.026 us | 23.491 us | CPython faster | 1.11x |
| `replacement/subn-string` | `SubnString` | 95.209 us | 88.278 us | CPython faster | 1.08x |
| `replacement/fixed-string` | `ReplaceString` | 191.036 us | 177.664 us | CPython faster | 1.08x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 181.119 us | 177.390 us | Inconclusive | 1.02x |
| `replacement/subn-utf8` | `SubnUtf8` | 90.323 us | 89.761 us | Equivalent | 1.01x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.991 us | 47.799 us | Managed faster | 0.86x |
| `replacement/no-match-string` | `ReplaceString` | 1.149 us | 2.392 us | Inconclusive | 0.48x |
| `replacement/one-template-string` | `ReplaceString` | 1.810 us | 16,269.093 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.586 us | 0.543 us | Inconclusive | 2.95x |
| `split/zero-width` | `SplitStrings` | 111.188 us | 66.785 us | CPython faster | 1.66x |
| `split/unicode` | `SplitStrings` | 61.248 us | 39.007 us | CPython faster | 1.58x |
| `split/captures` | `SplitStrings` | 394.081 us | 255.446 us | CPython faster | 1.55x |
| `split/no-captures` | `SplitStrings` | 252.628 us | 225.449 us | CPython faster | 1.11x |
| `split/optional-capture` | `SplitStrings` | 71.374 us | 88.858 us | Managed faster | 0.80x |
| `split/detailed` | `SplitDetailed` | 103.339 us | 134.653 us | Managed faster | 0.77x |
| `split/one-separator` | `SplitStrings` | 1.284 us | 11.939 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.365 us | 24.786 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.028 us | 390.817 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 85.513 us | 623.589 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 47.580 us | 571.874 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,145.284 us | 358,390.452 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `backreference/fullmatch-hit`: Removable harness floors are 0.10 %/8.72 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/15.60 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.18 %/11.92 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 149.806 us / 67,593 B | 61.030 us | 136.403 us / 67,617 B | 60.917 us |
| `fallback` | 257.016 us / 79,753 B | 42.687 us | 196.797 us / 79,721 B | 42.318 us |
| `literal` | 235.053 us / 50,377 B | 23.188 us | 207.644 us / 50,345 B | 24.029 us |
| `unicode` | 87.575 us / 48,321 B | 37.011 us | 82.609 us / 48,289 B | 35.646 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
