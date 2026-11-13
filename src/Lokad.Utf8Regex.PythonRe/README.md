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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev83-20261113/README.md).

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

Snapshot SHA-256: `EBBE8EEB197E8AC7366E81829979B71643BB8993BEFDE6E55480E394DEAFDA39`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `43` managed faster, `2` equivalent, `34` CPython faster, `6` inconclusive, `5` unqualified.

Measured source `7b8c22d0f082`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.775 us | 0.835 us | CPython faster | 2.13x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.362 us | 0.187 us | CPython faster | 1.95x |
| `atomic/search-miss` | `Search` | 0.436 us | 0.246 us | CPython faster | 1.78x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.490 us | 0.290 us | CPython faster | 1.69x |
| `possessive/search-hit` | `Search` | 0.461 us | 0.301 us | CPython faster | 1.52x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.253 us | 0.172 us | CPython faster | 1.47x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.409 us | 0.278 us | CPython faster | 1.47x |
| `reluctant/search-hit` | `Search` | 0.506 us | 0.346 us | CPython faster | 1.46x |
| `lookbehind/search-miss` | `Search` | 0.298 us | 0.207 us | CPython faster | 1.45x |
| `atomic/search-hit` | `Search` | 0.461 us | 0.321 us | CPython faster | 1.43x |
| `scoped-inline/search-hit` | `Search` | 0.353 us | 0.289 us | CPython faster | 1.22x |
| `leading-inline/search-hit` | `Search` | 0.375 us | 0.328 us | CPython faster | 1.14x |
| `multiline/search-miss` | `Search` | 0.288 us | 0.252 us | CPython faster | 1.14x |
| `unicode-option/search-hit` | `Search` | 0.320 us | 0.296 us | CPython faster | 1.08x |
| `lookahead/search-miss` | `Search` | 0.193 us | 0.179 us | CPython faster | 1.08x |
| `alternation/search-miss` | `Search` | 0.187 us | 0.227 us | Managed faster | 0.83x |
| `ascii-option/search-hit` | `Search` | 0.235 us | 0.302 us | Managed faster | 0.78x |
| `ignorecase/search-miss` | `Search` | 0.203 us | 0.275 us | Managed faster | 0.74x |
| `lookahead/search-hit` | `Search` | 0.206 us | 0.296 us | Managed faster | 0.70x |
| `greedy/search-miss` | `Search` | 0.143 us | 0.209 us | Managed faster | 0.68x |
| `possessive/search-miss` | `Search` | 0.123 us | 0.198 us | Managed faster | 0.63x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.124 us | 0.331 us | Managed faster | 0.37x |
| `unicode-literal/search-hit` | `Search` | 0.075 us | 0.230 us | Managed faster | 0.33x |
| `literal/search-early` | `Search` | 0.067 us | 0.231 us | Managed faster | 0.29x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.068 us | 0.254 us | Managed faster | 0.27x |
| `prefix/match` | `Match` | 0.070 us | 0.266 us | Managed faster | 0.26x |
| `unicode-literal/search-miss` | `Search` | 0.072 us | 0.283 us | Managed faster | 0.26x |
| `unicode/fullmatch` | `FullMatch` | 3.762 us | 18.996 us | Managed faster | 0.20x |
| `literal/fullmatch` | `FullMatch` | 0.065 us | 0.337 us | Managed faster | 0.19x |
| `common/ip-match` | `IsMatch` | 0.051 us | 0.315 us | Managed faster | 0.16x |
| `literal/search` | `Search` | 1.970 us | 21.782 us | Managed faster | 0.09x |
| `literal/ismatch` | `IsMatch` | 1.079 us | 21.752 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.034 us | 21.421 us | Managed faster | 0.05x |
| `ignorecase/search-hit` | `Search` | 0.341 us | 0.353 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.331 us | 0.325 us | Unqualified | — |
| `multiline/search-hit` | `Search` | 0.294 us | 0.331 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.207 us | 0.228 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.194 us | 0.201 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.880 us | 1.306 us | Managed faster | 0.67x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 428.361 us | 211.168 us | CPython faster | 2.04x |
| `findall/many-capture-strings` | `FindAllStrings` | 656.553 us | 364.684 us | CPython faster | 1.80x |
| `findall/named-capture-strings` | `FindAllStrings` | 135.936 us | 85.830 us | CPython faster | 1.58x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 82.849 us | 59.428 us | CPython faster | 1.38x |
| `findall/full-strings` | `FindAllStrings` | 440.318 us | 353.505 us | CPython faster | 1.25x |
| `findall/empty-progression-strings` | `FindAllStrings` | 53.817 us | 43.725 us | CPython faster | 1.23x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.966 us | 47.059 us | CPython faster | 1.10x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.010 us | 15.229 us | CPython faster | 1.05x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 506.891 us | 590.747 us | Managed faster | 0.86x |
| `findall/full-utf8` | `FindAllUtf8` | 382.561 us | 510.280 us | Managed faster | 0.75x |
| `findall/structural-many` | `FindAllStructural` | 131.357 us | 188.321 us | Managed faster | 0.70x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.153 us | 26.303 us | Managed faster | 0.69x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 626.052 us | 1,203.167 us | Managed faster | 0.52x |
| `zero-width/count` | `Count` | 534.988 us | 1,087.663 us | Managed faster | 0.49x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 219.923 us | 509.653 us | Managed faster | 0.43x |
| `count/empty-progression` | `Count` | 117.593 us | 352.690 us | Managed faster | 0.33x |
| `count/from-offset` | `CountFromOffset` | 13.271 us | 48.279 us | Managed faster | 0.28x |
| `family/count` | `Count` | 530.098 us | 2,436.681 us | Managed faster | 0.22x |
| `count/one` | `Count` | 0.258 us | 1.941 us | Managed faster | 0.13x |
| `unicode/count` | `Count` | 48.982 us | 425.390 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 269.417 us | 2,519.630 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.737 us | 37.810 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.382 us | 88.123 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.351 us | 88.009 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.193 us | 136.096 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.076 us | 0.443 us | Inconclusive | 2.41x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 125.914 us | 63.754 us | CPython faster | 1.99x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 494.782 us | 267.446 us | CPython faster | 1.86x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 467.110 us | 267.956 us | CPython faster | 1.75x |
| `replacement/named-template-string` | `ReplaceString` | 1.088 us | 0.902 us | CPython faster | 1.20x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.747 us | 23.073 us | CPython faster | 1.11x |
| `replacement/fixed-string` | `ReplaceString` | 181.910 us | 169.785 us | CPython faster | 1.07x |
| `replacement/subn-string` | `SubnString` | 91.265 us | 87.382 us | CPython faster | 1.05x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 174.714 us | 174.524 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 87.054 us | 87.186 us | Equivalent | 1.00x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.079 us | 46.927 us | Managed faster | 0.86x |
| `replacement/no-match-string` | `ReplaceString` | 1.107 us | 2.351 us | Inconclusive | 0.47x |
| `replacement/one-template-string` | `ReplaceString` | 1.691 us | 15,850.358 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.495 us | 0.519 us | Inconclusive | 2.86x |
| `split/zero-width` | `SplitStrings` | 112.403 us | 64.369 us | CPython faster | 1.75x |
| `split/unicode` | `SplitStrings` | 58.240 us | 38.356 us | CPython faster | 1.51x |
| `split/captures` | `SplitStrings` | 371.690 us | 251.691 us | CPython faster | 1.48x |
| `split/no-captures` | `SplitStrings` | 238.196 us | 222.705 us | CPython faster | 1.07x |
| `split/optional-capture` | `SplitStrings` | 74.058 us | 80.738 us | Managed faster | 0.92x |
| `split/detailed` | `SplitDetailed` | 103.105 us | 134.189 us | Managed faster | 0.77x |
| `split/one-separator` | `SplitStrings` | 1.221 us | 11.681 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.236 us | 24.243 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.110 us | 390.161 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 79.268 us | 611.338 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 46.373 us | 560.719 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,097.220 us | 348,181.382 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `ignorecase/search-hit`: Removable harness floors are 0.15 %/9.36 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.20 % of the lanes.
- `multiline/search-hit`: Removable harness floors are 0.17 %/10.20 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.44 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.20 %/16.47 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 161.566 us / 67,593 B | 60.136 us | 164.725 us / 67,617 B | 59.056 us |
| `fallback` | 277.016 us / 79,753 B | 42.983 us | 219.228 us / 79,721 B | 43.022 us |
| `literal` | 230.434 us / 50,377 B | 23.930 us | 210.766 us / 50,345 B | 24.703 us |
| `unicode` | 89.909 us / 48,321 B | 36.522 us | 80.766 us / 48,289 B | 35.667 us |

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
