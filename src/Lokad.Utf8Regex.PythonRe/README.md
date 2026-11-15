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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev94-20261115/README.md).

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

Snapshot SHA-256: `DB84593819FB6F8614F17D375F01016D19562C4AE7913BDF113DDD9996996EB7`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `52` managed faster, `2` equivalent, `25` CPython faster, `9` inconclusive, `2` unqualified.

Measured source `7f3895caa58c`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.843 us | 0.842 us | CPython faster | 2.19x |
| `atomic/search-miss` | `Search` | 0.451 us | 0.280 us | CPython faster | 1.60x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.330 us | 0.214 us | CPython faster | 1.55x |
| `lookbehind/search-miss` | `Search` | 0.313 us | 0.234 us | CPython faster | 1.34x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.242 us | 0.200 us | CPython faster | 1.21x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.456 us | 0.400 us | CPython faster | 1.15x |
| `possessive/search-hit` | `Search` | 0.452 us | 0.403 us | CPython faster | 1.12x |
| `reluctant/search-hit` | `Search` | 0.475 us | 0.442 us | CPython faster | 1.07x |
| `atomic/search-hit` | `Search` | 0.450 us | 0.427 us | CPython faster | 1.05x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.385 us | 0.385 us | Inconclusive | 1.00x |
| `leading-inline/search-hit` | `Search` | 0.375 us | 0.435 us | Managed faster | 0.86x |
| `scoped-inline/search-hit` | `Search` | 0.330 us | 0.389 us | Managed faster | 0.84x |
| `lookbehind/search-hit` | `Search` | 0.342 us | 0.432 us | Managed faster | 0.79x |
| `unicode-category/search-miss` | `Search` | 0.181 us | 0.230 us | Managed faster | 0.78x |
| `ignorecase/search-hit` | `Search` | 0.325 us | 0.457 us | Managed faster | 0.71x |
| `alternation/search-miss` | `Search` | 0.180 us | 0.257 us | Managed faster | 0.71x |
| `ignorecase/search-miss` | `Search` | 0.190 us | 0.301 us | Managed faster | 0.63x |
| `multiline/search-hit` | `Search` | 0.270 us | 0.430 us | Managed faster | 0.63x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.207 us | 0.331 us | Managed faster | 0.62x |
| `ascii-option/search-hit` | `Search` | 0.235 us | 0.410 us | Managed faster | 0.57x |
| `greedy/search-miss` | `Search` | 0.132 us | 0.237 us | Managed faster | 0.56x |
| `lookahead/search-hit` | `Search` | 0.206 us | 0.396 us | Managed faster | 0.52x |
| `possessive/search-miss` | `Search` | 0.116 us | 0.225 us | Managed faster | 0.51x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.122 us | 0.424 us | Managed faster | 0.29x |
| `unicode-option/search-hit` | `Search` | 0.328 us | 1.172 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.067 us | 0.312 us | Managed faster | 0.21x |
| `literal/search-early` | `Search` | 0.068 us | 0.325 us | Managed faster | 0.21x |
| `prefix/match` | `Match` | 0.065 us | 0.375 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.312 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.057 us | 0.454 us | Managed faster | 0.12x |
| `unicode/fullmatch` | `FullMatch` | 3.405 us | 29.357 us | Managed faster | 0.12x |
| `literal/search` | `Search` | 1.962 us | 22.231 us | Managed faster | 0.09x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.078 us | 1.129 us | Managed faster | 0.07x |
| `unicode-literal/search-hit` | `Search` | 0.074 us | 1.219 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.101 us | 21.929 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.059 us | 21.916 us | Managed faster | 0.05x |
| `lookahead/search-miss` | `Search` | 0.187 us | 0.210 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.271 us | 0.282 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.761 us | 1.990 us | Managed faster | 0.38x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 330.948 us | 215.416 us | CPython faster | 1.54x |
| `findall/many-capture-strings` | `FindAllStrings` | 518.869 us | 339.997 us | CPython faster | 1.52x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 83.818 us | 60.591 us | CPython faster | 1.38x |
| `findall/empty-progression-strings` | `FindAllStrings` | 54.904 us | 44.493 us | CPython faster | 1.23x |
| `findall/named-capture-strings` | `FindAllStrings` | 104.967 us | 87.480 us | CPython faster | 1.20x |
| `findall/full-strings` | `FindAllStrings` | 370.454 us | 340.772 us | CPython faster | 1.09x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.049 us | 15.759 us | Equivalent | 1.02x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.156 us | 54.956 us | Managed faster | 0.93x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 477.557 us | 606.607 us | Managed faster | 0.79x |
| `findall/full-utf8` | `FindAllUtf8` | 334.018 us | 503.462 us | Managed faster | 0.66x |
| `findall/unicode-full-strings` | `FindAllStrings` | 17.563 us | 27.060 us | Managed faster | 0.65x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 508.583 us | 1,231.382 us | Managed faster | 0.41x |
| `zero-width/count` | `Count` | 400.093 us | 1,009.651 us | Managed faster | 0.40x |
| `findall/structural-many` | `FindAllStructural` | 102.153 us | 270.220 us | Managed faster | 0.38x |
| `count/empty-progression` | `Count` | 120.203 us | 327.920 us | Managed faster | 0.37x |
| `count/from-offset` | `CountFromOffset` | 14.216 us | 47.925 us | Managed faster | 0.30x |
| `family/count` | `Count` | 533.332 us | 2,166.839 us | Managed faster | 0.25x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 174.633 us | 807.852 us | Managed faster | 0.22x |
| `count/one` | `Count` | 0.272 us | 2.006 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 48.003 us | 400.939 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 275.081 us | 2,345.875 us | Managed faster | 0.12x |
| `findall/one-strings` | `FindAllStrings` | 0.755 us | 38.360 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.399 us | 88.655 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.348 us | 89.637 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.180 us | 139.191 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.054 us | 0.446 us | Inconclusive | 2.35x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 94.236 us | 62.364 us | CPython faster | 1.51x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 373.312 us | 272.377 us | CPython faster | 1.37x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 369.568 us | 271.017 us | CPython faster | 1.36x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.463 us | 22.799 us | CPython faster | 1.16x |
| `replacement/fixed-string` | `ReplaceString` | 187.321 us | 171.702 us | CPython faster | 1.09x |
| `replacement/named-template-string` | `ReplaceString` | 0.997 us | 0.929 us | Inconclusive | 1.07x |
| `replacement/subn-string` | `SubnString` | 94.322 us | 88.226 us | CPython faster | 1.06x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 179.988 us | 175.566 us | Inconclusive | 1.03x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.458 us | 89.353 us | Equivalent | 1.00x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.268 us | 47.876 us | Managed faster | 0.84x |
| `replacement/no-match-string` | `ReplaceString` | 1.091 us | 2.343 us | Inconclusive | 0.46x |
| `replacement/one-template-string` | `ReplaceString` | 1.785 us | 15,921.888 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.324 us | 0.530 us | Inconclusive | 2.47x |
| `split/unicode` | `SplitStrings` | 55.012 us | 38.749 us | CPython faster | 1.42x |
| `split/zero-width` | `SplitStrings` | 90.762 us | 65.347 us | CPython faster | 1.40x |
| `split/captures` | `SplitStrings` | 311.693 us | 250.510 us | CPython faster | 1.24x |
| `split/no-captures` | `SplitStrings` | 242.140 us | 217.927 us | CPython faster | 1.11x |
| `split/optional-capture` | `SplitStrings` | 64.148 us | 87.203 us | Managed faster | 0.73x |
| `split/detailed` | `SplitDetailed` | 86.682 us | 135.437 us | Managed faster | 0.64x |
| `split/one-separator` | `SplitStrings` | 1.003 us | 11.664 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.281 us | 24.406 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 68.707 us | 386.748 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 82.587 us | 617.170 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 47.022 us | 572.938 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,083.279 us | 352,068.898 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.21 %/16.21 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.20 %/11.73 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 152.928 us / 67,609 B | 60.513 us | 153.106 us / 67,633 B | 58.870 us |
| `fallback` | 261.906 us / 79,769 B | 42.484 us | 209.800 us / 79,737 B | 42.410 us |
| `literal` | 224.403 us / 50,393 B | 25.262 us | 207.447 us / 50,361 B | 24.532 us |
| `unicode` | 91.091 us / 48,337 B | 35.253 us | 82.856 us / 48,305 B | 36.109 us |

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
