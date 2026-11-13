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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev81-20261113/README.md).

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

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `9CC78121A6A5027228B03C0A65BE2F7FD8C2FB2660FA2ABD68B629483A377119`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `49` managed faster, `0` equivalent, `27` CPython faster, `9` inconclusive, `5` unqualified.

Measured source `877dc8367fd7`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.820 us | 0.859 us | CPython faster | 2.12x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.364 us | 0.187 us | CPython faster | 1.96x |
| `atomic/search-miss` | `Search` | 0.433 us | 0.252 us | CPython faster | 1.72x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.478 us | 0.287 us | CPython faster | 1.67x |
| `possessive/search-hit` | `Search` | 0.460 us | 0.301 us | CPython faster | 1.53x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.422 us | 0.284 us | CPython faster | 1.50x |
| `reluctant/search-hit` | `Search` | 0.511 us | 0.345 us | CPython faster | 1.47x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.248 us | 0.172 us | CPython faster | 1.44x |
| `atomic/search-hit` | `Search` | 0.447 us | 0.316 us | CPython faster | 1.42x |
| `lookbehind/search-miss` | `Search` | 0.292 us | 0.212 us | CPython faster | 1.38x |
| `scoped-inline/search-hit` | `Search` | 0.353 us | 0.290 us | CPython faster | 1.22x |
| `leading-inline/search-hit` | `Search` | 0.376 us | 0.327 us | CPython faster | 1.15x |
| `unicode-option/search-hit` | `Search` | 0.322 us | 0.291 us | CPython faster | 1.11x |
| `multiline/search-miss` | `Search` | 0.278 us | 0.254 us | CPython faster | 1.10x |
| `lookbehind/search-hit` | `Search` | 0.348 us | 0.328 us | CPython faster | 1.06x |
| `alternation/search-miss` | `Search` | 0.171 us | 0.224 us | Managed faster | 0.77x |
| `ascii-option/search-hit` | `Search` | 0.229 us | 0.303 us | Managed faster | 0.76x |
| `lookahead/search-hit` | `Search` | 0.202 us | 0.292 us | Managed faster | 0.69x |
| `ignorecase/search-miss` | `Search` | 0.186 us | 0.277 us | Managed faster | 0.68x |
| `greedy/search-miss` | `Search` | 0.126 us | 0.207 us | Managed faster | 0.61x |
| `possessive/search-miss` | `Search` | 0.112 us | 0.200 us | Managed faster | 0.56x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.120 us | 0.327 us | Managed faster | 0.37x |
| `unicode-literal/search-hit` | `Search` | 0.076 us | 0.229 us | Managed faster | 0.33x |
| `literal/search-early` | `Search` | 0.070 us | 0.229 us | Managed faster | 0.31x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.072 us | 0.253 us | Managed faster | 0.28x |
| `prefix/match` | `Match` | 0.076 us | 0.269 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.067 us | 0.281 us | Managed faster | 0.24x |
| `unicode/fullmatch` | `FullMatch` | 3.514 us | 18.601 us | Managed faster | 0.19x |
| `literal/fullmatch` | `FullMatch` | 0.057 us | 0.339 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.053 us | 0.331 us | Managed faster | 0.16x |
| `literal/search` | `Search` | 1.964 us | 21.895 us | Managed faster | 0.09x |
| `literal/ismatch` | `IsMatch` | 1.085 us | 21.754 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.061 us | 21.805 us | Managed faster | 0.05x |
| `ignorecase/search-hit` | `Search` | 0.347 us | 0.347 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.184 us | 0.184 us | Unqualified | — |
| `multiline/search-hit` | `Search` | 0.293 us | 0.332 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.203 us | 0.233 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.177 us | 0.201 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.860 us | 1.280 us | Managed faster | 0.67x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 645.553 us | 332.598 us | CPython faster | 1.95x |
| `findall/one-capture-strings` | `FindAllStrings` | 403.205 us | 207.562 us | CPython faster | 1.94x |
| `findall/named-capture-strings` | `FindAllStrings` | 137.842 us | 94.009 us | CPython faster | 1.47x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 83.252 us | 60.222 us | CPython faster | 1.38x |
| `findall/full-strings` | `FindAllStrings` | 430.618 us | 362.807 us | CPython faster | 1.19x |
| `findall/empty-progression-strings` | `FindAllStrings` | 48.446 us | 43.792 us | CPython faster | 1.10x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.139 us | 15.485 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.690 us | 53.494 us | Managed faster | 0.97x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 494.740 us | 597.574 us | Managed faster | 0.82x |
| `findall/full-utf8` | `FindAllUtf8` | 398.977 us | 498.557 us | Managed faster | 0.80x |
| `findall/structural-many` | `FindAllStructural` | 130.184 us | 188.111 us | Managed faster | 0.70x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.086 us | 26.552 us | Managed faster | 0.68x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 630.944 us | 1,204.630 us | Managed faster | 0.52x |
| `zero-width/count` | `Count` | 520.682 us | 1,084.453 us | Managed faster | 0.49x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 221.556 us | 524.727 us | Managed faster | 0.42x |
| `count/empty-progression` | `Count` | 144.390 us | 353.645 us | Managed faster | 0.41x |
| `count/from-offset` | `CountFromOffset` | 13.502 us | 49.028 us | Managed faster | 0.28x |
| `family/count` | `Count` | 532.885 us | 2,376.331 us | Managed faster | 0.22x |
| `count/one` | `Count` | 0.260 us | 2.009 us | Managed faster | 0.13x |
| `unicode/count` | `Count` | 47.713 us | 411.134 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 268.960 us | 2,514.772 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.715 us | 37.864 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.366 us | 88.874 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.349 us | 88.283 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.191 us | 139.211 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.393 us | 0.445 us | Inconclusive | 3.09x |
| `replacement/empty-progression-string` | `ReplaceString` | 27.324 us | 23.103 us | CPython faster | 1.18x |
| `replacement/named-template-string` | `ReplaceString` | 1.098 us | 0.938 us | Inconclusive | 1.18x |
| `replacement/fixed-string` | `ReplaceString` | 181.877 us | 173.428 us | CPython faster | 1.05x |
| `replacement/subn-string` | `SubnString` | 91.753 us | 89.043 us | Inconclusive | 1.03x |
| `replacement/subn-utf8` | `SubnUtf8` | 86.202 us | 88.822 us | Managed faster | 0.97x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 172.467 us | 179.461 us | Managed faster | 0.96x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.755 us | 45.658 us | Managed faster | 0.92x |
| `replacement/no-match-string` | `ReplaceString` | 1.441 us | 2.364 us | Inconclusive | 0.61x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 136.507 us | 4,230.649 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 542.481 us | 16,851.403 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 519.655 us | 16,540.983 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.480 us | 15,764.087 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.620 us | 0.541 us | Inconclusive | 2.99x |
| `split/zero-width` | `SplitStrings` | 115.867 us | 66.406 us | CPython faster | 1.73x |
| `split/captures` | `SplitStrings` | 388.041 us | 257.687 us | CPython faster | 1.51x |
| `split/unicode` | `SplitStrings` | 58.301 us | 40.189 us | CPython faster | 1.45x |
| `split/no-captures` | `SplitStrings` | 229.902 us | 225.204 us | Inconclusive | 1.02x |
| `split/optional-capture` | `SplitStrings` | 77.334 us | 82.136 us | Managed faster | 0.94x |
| `split/detailed` | `SplitDetailed` | 105.949 us | 136.193 us | Managed faster | 0.78x |
| `split/one-separator` | `SplitStrings` | 1.270 us | 11.754 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.278 us | 24.482 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 70.286 us | 397.975 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 83.024 us | 627.021 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 48.093 us | 578.381 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,129.056 us | 358,594.086 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.77 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.22 %/17.83 % of the lanes.
- `multiline/search-hit`: Removable harness floors are 0.15 %/9.94 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.12 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/16.54 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 153.122 us / 67,593 B | 60.974 us | 141.794 us / 67,617 B | 60.885 us |
| `fallback` | 269.769 us / 79,753 B | 42.590 us | 204.281 us / 79,721 B | 42.830 us |
| `literal` | 225.619 us / 50,377 B | 25.336 us | 205.041 us / 50,345 B | 25.288 us |
| `unicode` | 89.894 us / 48,321 B | 36.705 us | 81.487 us / 48,289 B | 36.372 us |

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
