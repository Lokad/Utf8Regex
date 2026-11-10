# Lokad.Utf8Regex.PythonRe

**0.5.0 development**, .NET 10 and .NET 11. A managed Python `re`-style adapter
over strict UTF-8 Unicode scalars, requiring the exact Core package version
matching the adapter. It can coexist with PCRE2 at the same version. No Python
runtime or BCL regex matcher is a shipped implementation dependency.

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
**Defaults remain unlimited.** Work includes covered validation, matching and
required shaping cumulatively. Depth admits live frames before acquisition;
working/result controls admit modeled major capacities and growth overlap.
Result/staging charges do not refund and may conservatively count storage again.
Denials use `Utf8ResourceLimitExceededException` with the corresponding
`Execution*` resource kind. This is not a total heap/RSS or caller-retention bound.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

Strict full-subject validation, original synchronous cancellation and one absolute
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

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `152F05679AEDAA00A0553B53761BAF8D6AF8EA3CCF5DAEE197C6CC65AA0B9304`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `43` managed faster, `2` equivalent, `33` CPython faster, `7` inconclusive, `5` unqualified.

Measured source `f0211f28073d`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.678 us | 0.234 us | CPython faster | 2.89x |
| `common/date-miss` | `IsMatch` | 1.801 us | 0.870 us | CPython faster | 2.07x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.385 us | 0.200 us | CPython faster | 1.93x |
| `leading-inline/search-hit` | `Search` | 0.611 us | 0.337 us | CPython faster | 1.81x |
| `atomic/search-miss` | `Search` | 0.457 us | 0.262 us | CPython faster | 1.73x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.495 us | 0.294 us | CPython faster | 1.67x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.287 us | 0.184 us | CPython faster | 1.55x |
| `possessive/search-hit` | `Search` | 0.457 us | 0.309 us | CPython faster | 1.47x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.412 us | 0.282 us | CPython faster | 1.46x |
| `lookbehind/search-miss` | `Search` | 0.303 us | 0.219 us | CPython faster | 1.38x |
| `reluctant/search-hit` | `Search` | 0.485 us | 0.352 us | CPython faster | 1.38x |
| `atomic/search-hit` | `Search` | 0.433 us | 0.325 us | CPython faster | 1.33x |
| `scoped-inline/search-hit` | `Search` | 0.350 us | 0.292 us | CPython faster | 1.20x |
| `prefix/match` | `Match` | 0.320 us | 0.290 us | CPython faster | 1.10x |
| `unicode-option/search-hit` | `Search` | 0.323 us | 0.300 us | CPython faster | 1.08x |
| `multiline/search-miss` | `Search` | 0.275 us | 0.267 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.274 us | 19.681 us | Managed faster | 0.88x |
| `ignorecase/search-hit` | `Search` | 0.311 us | 0.356 us | Managed faster | 0.87x |
| `ascii-option/search-hit` | `Search` | 0.257 us | 0.308 us | Managed faster | 0.83x |
| `multiline/search-hit` | `Search` | 0.250 us | 0.335 us | Managed faster | 0.75x |
| `ignorecase/search-miss` | `Search` | 0.203 us | 0.286 us | Managed faster | 0.71x |
| `lookahead/search-hit` | `Search` | 0.185 us | 0.301 us | Managed faster | 0.61x |
| `greedy/search-miss` | `Search` | 0.134 us | 0.222 us | Managed faster | 0.60x |
| `possessive/search-miss` | `Search` | 0.120 us | 0.214 us | Managed faster | 0.56x |
| `unicode-literal/search-hit` | `Search` | 0.129 us | 0.238 us | Managed faster | 0.54x |
| `unicode-literal/search-miss` | `Search` | 0.143 us | 0.292 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.133 us | 0.337 us | Managed faster | 0.40x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.087 us | 0.259 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.069 us | 0.348 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.056 us | 0.321 us | Managed faster | 0.17x |
| `literal/search` | `Search` | 2.634 us | 22.674 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.459 us | 22.388 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.670 us | 22.364 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.211 us | 0.237 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.200 us | 0.199 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.314 us | 0.334 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.206 us | 0.240 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.179 us | 0.215 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.036 us | 1.336 us | Inconclusive | 0.78x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 763.415 us | 351.115 us | CPython faster | 2.17x |
| `findall/one-capture-strings` | `FindAllStrings` | 451.746 us | 214.930 us | CPython faster | 2.10x |
| `findall/named-capture-strings` | `FindAllStrings` | 159.806 us | 94.685 us | CPython faster | 1.69x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 102.431 us | 62.189 us | CPython faster | 1.65x |
| `findall/empty-progression-strings` | `FindAllStrings` | 64.083 us | 43.570 us | CPython faster | 1.47x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.923 us | 27.691 us | CPython faster | 1.40x |
| `findall/full-strings` | `FindAllStrings` | 504.116 us | 369.217 us | CPython faster | 1.36x |
| `findall/optional-capture-strings` | `FindAllStrings` | 59.942 us | 55.525 us | CPython faster | 1.08x |
| `findall/full-utf8` | `FindAllUtf8` | 550.230 us | 520.216 us | CPython faster | 1.06x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.711 us | 16.479 us | Equivalent | 1.01x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 578.031 us | 631.067 us | Managed faster | 0.92x |
| `findall/structural-many` | `FindAllStructural` | 167.094 us | 204.943 us | Managed faster | 0.82x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 883.337 us | 1,253.584 us | Managed faster | 0.70x |
| `zero-width/count` | `Count` | 609.418 us | 1,117.422 us | Managed faster | 0.55x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 268.940 us | 547.921 us | Managed faster | 0.49x |
| `count/empty-progression` | `Count` | 171.023 us | 380.016 us | Managed faster | 0.45x |
| `count/from-offset` | `CountFromOffset` | 14.782 us | 50.221 us | Managed faster | 0.29x |
| `family/count` | `Count` | 638.447 us | 2,465.469 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.283 us | 2.029 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 57.096 us | 432.897 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 284.236 us | 2,581.035 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.758 us | 38.950 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.542 us | 90.920 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.393 us | 91.346 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.260 us | 140.261 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.148 us | 0.455 us | Inconclusive | 2.50x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.235 us | 23.726 us | CPython faster | 1.44x |
| `replacement/named-template-string` | `ReplaceString` | 1.259 us | 0.954 us | CPython faster | 1.32x |
| `replacement/fixed-string` | `ReplaceString` | 187.774 us | 179.085 us | CPython faster | 1.05x |
| `replacement/subn-string` | `SubnString` | 93.968 us | 91.222 us | CPython faster | 1.03x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 178.309 us | 179.466 us | Equivalent | 0.99x |
| `replacement/unmatched-template-string` | `ReplaceString` | 49.543 us | 51.446 us | Managed faster | 0.96x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.071 us | 93.058 us | Managed faster | 0.96x |
| `replacement/no-match-string` | `ReplaceString` | 1.261 us | 2.420 us | Inconclusive | 0.51x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 159.905 us | 4,335.597 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 624.022 us | 17,328.560 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 593.854 us | 17,385.428 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.975 us | 16,598.589 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.392 us | 0.550 us | Inconclusive | 2.54x |
| `split/zero-width` | `SplitStrings` | 119.648 us | 66.678 us | CPython faster | 1.79x |
| `split/captures` | `SplitStrings` | 433.025 us | 255.556 us | CPython faster | 1.69x |
| `split/unicode` | `SplitStrings` | 70.027 us | 44.774 us | CPython faster | 1.57x |
| `split/no-captures` | `SplitStrings` | 275.953 us | 225.550 us | CPython faster | 1.23x |
| `split/optional-capture` | `SplitStrings` | 74.882 us | 82.832 us | Managed faster | 0.90x |
| `split/detailed` | `SplitDetailed` | 114.961 us | 136.152 us | Managed faster | 0.84x |
| `split/one-separator` | `SplitStrings` | 1.139 us | 12.069 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.172 us | 25.047 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 195.133 us | 401.165 us | Managed faster | 0.49x |
| `corpus/code-declarations` | `Count` | 87.263 us | 629.472 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 49.128 us | 580.027 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,070.356 us | 359,496.648 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.19 %/14.38 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/17.27 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.35 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.22 %/14.24 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/15.86 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 202.391 us / 71,617 B | 60.660 us | 188.325 us / 71,641 B | 61.029 us |
| `fallback` | 294.209 us / 83,561 B | 41.973 us | 268.203 us / 83,529 B | 42.285 us |
| `literal` | 377.366 us / 50,369 B | 25.650 us | 321.978 us / 50,337 B | 25.311 us |
| `unicode` | 55.859 us / 48,313 B | 36.227 us | 59.222 us / 48,281 B | 36.474 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
