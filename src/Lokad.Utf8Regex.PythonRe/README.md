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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev84-20261114/README.md).

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

Snapshot SHA-256: `2B224A7D31F7F61D8E644B41C4993B11A92E9C4044EBED3F4E942A51CC517F08`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `44` managed faster, `1` equivalent, `34` CPython faster, `7` inconclusive, `4` unqualified.

Measured source `834162dffe2d`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.
Measured source `bf5c18ec092a`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.890 us | 0.828 us | CPython faster | 2.29x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.348 us | 0.185 us | CPython faster | 1.88x |
| `atomic/search-miss` | `Search` | 0.427 us | 0.246 us | CPython faster | 1.74x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.459 us | 0.284 us | CPython faster | 1.62x |
| `possessive/search-hit` | `Search` | 0.452 us | 0.302 us | CPython faster | 1.50x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.251 us | 0.169 us | CPython faster | 1.49x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.404 us | 0.274 us | CPython faster | 1.47x |
| `lookbehind/search-miss` | `Search` | 0.292 us | 0.202 us | CPython faster | 1.44x |
| `atomic/search-hit` | `Search` | 0.444 us | 0.314 us | CPython faster | 1.42x |
| `reluctant/search-hit` | `Search` | 0.483 us | 0.358 us | CPython faster | 1.36x |
| `scoped-inline/search-hit` | `Search` | 0.335 us | 0.282 us | CPython faster | 1.19x |
| `leading-inline/search-hit` | `Search` | 0.371 us | 0.328 us | CPython faster | 1.14x |
| `multiline/search-miss` | `Search` | 0.279 us | 0.249 us | CPython faster | 1.12x |
| `unicode-option/search-hit` | `Search` | 0.324 us | 0.293 us | CPython faster | 1.11x |
| `lookahead/search-miss` | `Search` | 0.191 us | 0.182 us | CPython faster | 1.05x |
| `lookbehind/search-hit` | `Search` | 0.340 us | 0.327 us | CPython faster | 1.04x |
| `alternation/search-miss` | `Search` | 0.177 us | 0.220 us | Managed faster | 0.81x |
| `ascii-option/search-hit` | `Search` | 0.233 us | 0.302 us | Managed faster | 0.77x |
| `ignorecase/search-miss` | `Search` | 0.193 us | 0.268 us | Managed faster | 0.72x |
| `lookahead/search-hit` | `Search` | 0.212 us | 0.295 us | Managed faster | 0.72x |
| `greedy/search-miss` | `Search` | 0.128 us | 0.203 us | Managed faster | 0.63x |
| `possessive/search-miss` | `Search` | 0.116 us | 0.196 us | Managed faster | 0.59x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.116 us | 0.320 us | Managed faster | 0.36x |
| `unicode-literal/search-hit` | `Search` | 0.072 us | 0.228 us | Managed faster | 0.32x |
| `literal/search-early` | `Search` | 0.067 us | 0.228 us | Managed faster | 0.30x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.063 us | 0.249 us | Managed faster | 0.25x |
| `prefix/match` | `Match` | 0.065 us | 0.262 us | Managed faster | 0.25x |
| `unicode-literal/search-miss` | `Search` | 0.064 us | 0.279 us | Managed faster | 0.23x |
| `unicode/fullmatch` | `FullMatch` | 3.443 us | 18.728 us | Managed faster | 0.18x |
| `literal/fullmatch` | `FullMatch` | 0.056 us | 0.329 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.050 us | 0.310 us | Managed faster | 0.16x |
| `literal/search` | `Search` | 1.897 us | 21.522 us | Managed faster | 0.09x |
| `literal/ismatch` | `IsMatch` | 1.076 us | 21.488 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.030 us | 21.419 us | Managed faster | 0.05x |
| `ignorecase/search-hit` | `Search` | 0.334 us | 0.346 us | Unqualified | — |
| `multiline/search-hit` | `Search` | 0.284 us | 0.324 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.210 us | 0.228 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.183 us | 0.201 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.880 us | 1.469 us | Managed faster | 0.60x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/named-capture-strings` | `FindAllStrings` | 136.867 us | 84.310 us | CPython faster | 1.62x |
| `findall/one-capture-strings` | `FindAllStrings` | 294.570 us | 210.894 us | CPython faster | 1.39x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 81.567 us | 59.195 us | CPython faster | 1.38x |
| `findall/empty-progression-strings` | `FindAllStrings` | 55.689 us | 43.241 us | CPython faster | 1.28x |
| `findall/many-capture-strings` | `FindAllStrings` | 440.958 us | 347.628 us | CPython faster | 1.27x |
| `findall/full-strings` | `FindAllStrings` | 441.376 us | 348.062 us | CPython faster | 1.27x |
| `findall/optional-capture-strings` | `FindAllStrings` | 53.248 us | 46.947 us | CPython faster | 1.13x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 15.743 us | 15.373 us | Inconclusive | 1.02x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 484.716 us | 588.636 us | Managed faster | 0.83x |
| `findall/full-utf8` | `FindAllUtf8` | 398.445 us | 501.308 us | Managed faster | 0.79x |
| `findall/structural-many` | `FindAllStructural` | 132.248 us | 184.567 us | Managed faster | 0.71x |
| `findall/unicode-full-strings` | `FindAllStrings` | 17.495 us | 25.934 us | Managed faster | 0.67x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 641.611 us | 1,180.635 us | Managed faster | 0.54x |
| `zero-width/count` | `Count` | 534.726 us | 1,083.190 us | Managed faster | 0.49x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 224.165 us | 513.990 us | Managed faster | 0.44x |
| `count/empty-progression` | `Count` | 120.441 us | 349.371 us | Managed faster | 0.34x |
| `count/from-offset` | `CountFromOffset` | 13.697 us | 48.098 us | Managed faster | 0.28x |
| `family/count` | `Count` | 527.996 us | 2,416.083 us | Managed faster | 0.22x |
| `count/one` | `Count` | 0.270 us | 1.970 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 47.893 us | 416.534 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 269.702 us | 2,508.804 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.745 us | 37.298 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.374 us | 87.826 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.351 us | 86.812 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.197 us | 136.036 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.005 us | 0.431 us | Inconclusive | 2.34x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 103.421 us | 69.345 us | CPython faster | 1.49x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 403.477 us | 272.527 us | CPython faster | 1.48x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 390.834 us | 275.761 us | CPython faster | 1.41x |
| `replacement/named-template-string` | `ReplaceString` | 1.082 us | 0.894 us | CPython faster | 1.21x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.204 us | 22.446 us | CPython faster | 1.17x |
| `replacement/fixed-string` | `ReplaceString` | 179.221 us | 166.867 us | CPython faster | 1.08x |
| `replacement/subn-string` | `SubnString` | 89.277 us | 85.601 us | CPython faster | 1.04x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 170.687 us | 169.659 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 85.130 us | 87.462 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.038 us | 46.176 us | Managed faster | 0.87x |
| `replacement/no-match-string` | `ReplaceString` | 1.049 us | 2.301 us | Inconclusive | 0.46x |
| `replacement/one-template-string` | `ReplaceString` | 1.668 us | 15,667.997 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.563 us | 0.513 us | Inconclusive | 3.08x |
| `split/zero-width` | `SplitStrings` | 114.770 us | 64.241 us | CPython faster | 1.78x |
| `split/captures` | `SplitStrings` | 396.689 us | 246.031 us | CPython faster | 1.61x |
| `split/unicode` | `SplitStrings` | 55.528 us | 38.346 us | CPython faster | 1.45x |
| `split/no-captures` | `SplitStrings` | 236.521 us | 219.354 us | CPython faster | 1.08x |
| `split/optional-capture` | `SplitStrings` | 73.533 us | 79.158 us | Managed faster | 0.93x |
| `split/detailed` | `SplitDetailed` | 103.816 us | 129.391 us | Managed faster | 0.80x |
| `split/one-separator` | `SplitStrings` | 1.242 us | 11.365 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.333 us | 23.753 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.607 us | 383.918 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 82.968 us | 602.913 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 44.998 us | 554.145 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,075.019 us | 342,800.431 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.53 % of the lanes.
- `multiline/search-hit`: Removable harness floors are 0.18 %/9.96 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.18 %/14.47 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/16.34 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 172.159 us / 67,593 B | 57.122 us | 164.916 us / 67,617 B | 58.845 us |
| `fallback` | 272.562 us / 79,753 B | 40.764 us | 235.037 us / 79,721 B | 40.781 us |
| `literal` | 322.225 us / 50,377 B | 24.093 us | 299.462 us / 50,345 B | 24.318 us |
| `unicode` | 52.737 us / 48,321 B | 36.840 us | 54.103 us / 48,289 B | 34.562 us |

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
