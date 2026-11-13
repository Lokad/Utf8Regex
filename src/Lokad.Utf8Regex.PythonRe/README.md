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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev82-20261113/README.md).

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

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `821BFE1E70C7D0F2944EECCF64847896674A90652C3242FEA434D6D4707F3552`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `43` managed faster, `1` equivalent, `31` CPython faster, `10` inconclusive, `5` unqualified.

Measured source `150d2f5b128c`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.
Measured source `b537a1885e3f`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.814 us | 0.856 us | CPython faster | 2.12x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.360 us | 0.187 us | CPython faster | 1.93x |
| `atomic/search-miss` | `Search` | 0.432 us | 0.245 us | CPython faster | 1.76x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.467 us | 0.289 us | CPython faster | 1.62x |
| `possessive/search-hit` | `Search` | 0.446 us | 0.295 us | CPython faster | 1.52x |
| `lookbehind/search-miss` | `Search` | 0.309 us | 0.204 us | CPython faster | 1.52x |
| `reluctant/search-hit` | `Search` | 0.496 us | 0.336 us | CPython faster | 1.47x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.401 us | 0.275 us | CPython faster | 1.46x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.245 us | 0.169 us | CPython faster | 1.45x |
| `atomic/search-hit` | `Search` | 0.447 us | 0.313 us | CPython faster | 1.43x |
| `scoped-inline/search-hit` | `Search` | 0.345 us | 0.280 us | CPython faster | 1.23x |
| `leading-inline/search-hit` | `Search` | 0.372 us | 0.323 us | CPython faster | 1.15x |
| `multiline/search-miss` | `Search` | 0.282 us | 0.253 us | CPython faster | 1.11x |
| `unicode-option/search-hit` | `Search` | 0.315 us | 0.289 us | CPython faster | 1.09x |
| `lookbehind/search-hit` | `Search` | 0.339 us | 0.320 us | CPython faster | 1.06x |
| `alternation/search-miss` | `Search` | 0.175 us | 0.221 us | Managed faster | 0.79x |
| `ascii-option/search-hit` | `Search` | 0.229 us | 0.295 us | Managed faster | 0.77x |
| `ignorecase/search-miss` | `Search` | 0.193 us | 0.270 us | Managed faster | 0.71x |
| `lookahead/search-hit` | `Search` | 0.202 us | 0.291 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.132 us | 0.205 us | Managed faster | 0.65x |
| `possessive/search-miss` | `Search` | 0.118 us | 0.200 us | Managed faster | 0.59x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.119 us | 0.324 us | Managed faster | 0.37x |
| `unicode-literal/search-hit` | `Search` | 0.074 us | 0.225 us | Managed faster | 0.33x |
| `literal/search-early` | `Search` | 0.068 us | 0.223 us | Managed faster | 0.31x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.062 us | 0.251 us | Managed faster | 0.25x |
| `prefix/match` | `Match` | 0.066 us | 0.268 us | Managed faster | 0.24x |
| `unicode-literal/search-miss` | `Search` | 0.067 us | 0.279 us | Managed faster | 0.24x |
| `unicode/fullmatch` | `FullMatch` | 3.561 us | 18.168 us | Managed faster | 0.20x |
| `literal/fullmatch` | `FullMatch` | 0.055 us | 0.332 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.053 us | 0.328 us | Managed faster | 0.16x |
| `literal/search` | `Search` | 1.926 us | 21.856 us | Managed faster | 0.09x |
| `literal/ismatch` | `IsMatch` | 1.073 us | 21.374 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.039 us | 21.394 us | Managed faster | 0.05x |
| `ignorecase/search-hit` | `Search` | 0.336 us | 0.346 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.186 us | 0.183 us | Unqualified | — |
| `multiline/search-hit` | `Search` | 0.285 us | 0.323 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.207 us | 0.229 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.180 us | 0.201 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.857 us | 1.249 us | Managed faster | 0.69x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 650.978 us | 328.069 us | CPython faster | 1.97x |
| `findall/one-capture-strings` | `FindAllStrings` | 404.868 us | 208.797 us | CPython faster | 1.93x |
| `findall/named-capture-strings` | `FindAllStrings` | 141.710 us | 96.782 us | CPython faster | 1.46x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 81.706 us | 60.432 us | CPython faster | 1.35x |
| `findall/empty-progression-strings` | `FindAllStrings` | 59.906 us | 45.239 us | CPython faster | 1.33x |
| `findall/full-strings` | `FindAllStrings` | 441.773 us | 355.975 us | CPython faster | 1.24x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.401 us | 16.127 us | Inconclusive | 1.02x |
| `findall/optional-capture-strings` | `FindAllStrings` | 53.743 us | 54.865 us | Inconclusive | 0.98x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 500.868 us | 598.169 us | Managed faster | 0.84x |
| `findall/full-utf8` | `FindAllUtf8` | 407.106 us | 505.325 us | Managed faster | 0.80x |
| `findall/structural-many` | `FindAllStructural` | 136.075 us | 189.750 us | Managed faster | 0.72x |
| `findall/unicode-full-strings` | `FindAllStrings` | 17.387 us | 26.722 us | Managed faster | 0.64x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 632.090 us | 1,195.673 us | Managed faster | 0.53x |
| `zero-width/count` | `Count` | 529.663 us | 1,065.678 us | Managed faster | 0.50x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 226.964 us | 512.057 us | Managed faster | 0.44x |
| `count/empty-progression` | `Count` | 123.810 us | 363.895 us | Managed faster | 0.34x |
| `count/from-offset` | `CountFromOffset` | 14.855 us | 50.100 us | Managed faster | 0.30x |
| `family/count` | `Count` | 533.460 us | 2,361.889 us | Managed faster | 0.23x |
| `count/one` | `Count` | 0.280 us | 1.966 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 47.125 us | 410.663 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 273.318 us | 2,470.502 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.757 us | 39.268 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.374 us | 90.216 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.363 us | 89.430 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.188 us | 138.875 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.074 us | 0.444 us | Inconclusive | 2.43x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 103.599 us | 69.808 us | CPython faster | 1.48x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 401.077 us | 272.763 us | CPython faster | 1.47x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 380.196 us | 282.723 us | Inconclusive | 1.33x |
| `replacement/empty-progression-string` | `ReplaceString` | 28.086 us | 23.629 us | CPython faster | 1.19x |
| `replacement/named-template-string` | `ReplaceString` | 1.106 us | 0.939 us | CPython faster | 1.18x |
| `replacement/fixed-string` | `ReplaceString` | 180.940 us | 170.367 us | CPython faster | 1.07x |
| `replacement/subn-string` | `SubnString` | 90.629 us | 85.781 us | CPython faster | 1.05x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 170.588 us | 169.323 us | Equivalent | 1.01x |
| `replacement/subn-utf8` | `SubnUtf8` | 86.314 us | 88.031 us | Inconclusive | 0.98x |
| `replacement/unmatched-template-string` | `ReplaceString` | 42.624 us | 46.913 us | Managed faster | 0.91x |
| `replacement/no-match-string` | `ReplaceString` | 1.114 us | 2.389 us | Inconclusive | 0.47x |
| `replacement/one-template-string` | `ReplaceString` | 1.777 us | 16,449.467 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.514 us | 0.533 us | Inconclusive | 2.85x |
| `split/zero-width` | `SplitStrings` | 117.732 us | 65.887 us | CPython faster | 1.79x |
| `split/captures` | `SplitStrings` | 398.769 us | 254.654 us | CPython faster | 1.57x |
| `split/unicode` | `SplitStrings` | 58.426 us | 39.360 us | CPython faster | 1.48x |
| `split/no-captures` | `SplitStrings` | 245.130 us | 224.894 us | CPython faster | 1.09x |
| `split/optional-capture` | `SplitStrings` | 75.924 us | 81.474 us | Managed faster | 0.94x |
| `split/detailed` | `SplitDetailed` | 108.173 us | 135.650 us | Managed faster | 0.80x |
| `split/one-separator` | `SplitStrings` | 1.289 us | 11.842 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.293 us | 24.925 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.091 us | 398.062 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 85.748 us | 625.178 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 47.614 us | 577.140 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,139.843 us | 352,522.831 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.34 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.22 %/18.20 % of the lanes.
- `multiline/search-hit`: Removable harness floors are 0.15 %/10.08 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.18 %/14.34 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/16.60 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 184.222 us / 67,593 B | 60.179 us | 176.869 us / 67,617 B | 60.563 us |
| `fallback` | 270.800 us / 79,753 B | 41.887 us | 250.922 us / 79,721 B | 42.096 us |
| `literal` | 331.103 us / 50,377 B | 24.862 us | 282.309 us / 50,345 B | 24.838 us |
| `unicode` | 55.834 us / 48,321 B | 36.078 us | 57.047 us / 48,289 B | 35.878 us |

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
