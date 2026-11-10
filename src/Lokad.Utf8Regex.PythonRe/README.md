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

Snapshot SHA-256: `780084DBE9BFB0F4ACE6F2816E2A5949DC027F9BBCA2ECEFA986315163636239`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `44` managed faster, `1` equivalent, `34` CPython faster, `6` inconclusive, `5` unqualified.

Measured source `9af7141f0ee7`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.675 us | 0.236 us | CPython faster | 2.85x |
| `common/date-miss` | `IsMatch` | 1.796 us | 0.867 us | CPython faster | 2.07x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.366 us | 0.199 us | CPython faster | 1.84x |
| `leading-inline/search-hit` | `Search` | 0.597 us | 0.335 us | CPython faster | 1.79x |
| `atomic/search-miss` | `Search` | 0.450 us | 0.263 us | CPython faster | 1.72x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.476 us | 0.296 us | CPython faster | 1.60x |
| `lookbehind/search-miss` | `Search` | 0.321 us | 0.221 us | CPython faster | 1.45x |
| `possessive/search-hit` | `Search` | 0.445 us | 0.308 us | CPython faster | 1.45x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.405 us | 0.282 us | CPython faster | 1.44x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.264 us | 0.184 us | CPython faster | 1.44x |
| `reluctant/search-hit` | `Search` | 0.485 us | 0.351 us | CPython faster | 1.38x |
| `atomic/search-hit` | `Search` | 0.440 us | 0.324 us | CPython faster | 1.36x |
| `prefix/match` | `Match` | 0.316 us | 0.273 us | CPython faster | 1.16x |
| `scoped-inline/search-hit` | `Search` | 0.328 us | 0.292 us | CPython faster | 1.12x |
| `unicode-option/search-hit` | `Search` | 0.316 us | 0.299 us | CPython faster | 1.06x |
| `multiline/search-miss` | `Search` | 0.274 us | 0.267 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.581 us | 19.719 us | Managed faster | 0.89x |
| `ignorecase/search-hit` | `Search` | 0.306 us | 0.358 us | Managed faster | 0.86x |
| `ascii-option/search-hit` | `Search` | 0.251 us | 0.308 us | Managed faster | 0.82x |
| `multiline/search-hit` | `Search` | 0.248 us | 0.335 us | Managed faster | 0.74x |
| `ignorecase/search-miss` | `Search` | 0.199 us | 0.287 us | Managed faster | 0.69x |
| `lookahead/search-hit` | `Search` | 0.184 us | 0.302 us | Managed faster | 0.62x |
| `greedy/search-miss` | `Search` | 0.132 us | 0.220 us | Managed faster | 0.60x |
| `unicode-literal/search-hit` | `Search` | 0.133 us | 0.238 us | Managed faster | 0.56x |
| `possessive/search-miss` | `Search` | 0.116 us | 0.212 us | Managed faster | 0.55x |
| `unicode-literal/search-miss` | `Search` | 0.137 us | 0.291 us | Managed faster | 0.47x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.145 us | 0.334 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.088 us | 0.258 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.069 us | 0.347 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.056 us | 0.320 us | Managed faster | 0.17x |
| `literal/search` | `Search` | 2.649 us | 22.626 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.464 us | 22.375 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.667 us | 22.295 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.200 us | 0.238 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.197 us | 0.198 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.335 us | 0.334 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.207 us | 0.238 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.177 us | 0.213 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.045 us | 1.297 us | Managed faster | 0.81x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 509.214 us | 214.143 us | CPython faster | 2.38x |
| `findall/many-capture-strings` | `FindAllStrings` | 817.706 us | 350.351 us | CPython faster | 2.34x |
| `findall/named-capture-strings` | `FindAllStrings` | 176.691 us | 95.403 us | CPython faster | 1.85x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 99.902 us | 62.523 us | CPython faster | 1.59x |
| `findall/empty-progression-strings` | `FindAllStrings` | 68.073 us | 43.731 us | CPython faster | 1.56x |
| `findall/full-strings` | `FindAllStrings` | 547.435 us | 369.650 us | CPython faster | 1.48x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.261 us | 27.485 us | CPython faster | 1.40x |
| `findall/optional-capture-strings` | `FindAllStrings` | 59.595 us | 56.324 us | CPython faster | 1.06x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.114 us | 16.191 us | CPython faster | 1.06x |
| `findall/full-utf8` | `FindAllUtf8` | 546.688 us | 518.614 us | CPython faster | 1.06x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 564.845 us | 621.839 us | Managed faster | 0.91x |
| `findall/structural-many` | `FindAllStructural` | 180.831 us | 202.595 us | Managed faster | 0.89x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 866.058 us | 1,248.053 us | Managed faster | 0.69x |
| `zero-width/count` | `Count` | 637.729 us | 1,113.511 us | Managed faster | 0.57x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 280.838 us | 544.110 us | Managed faster | 0.52x |
| `count/empty-progression` | `Count` | 177.834 us | 381.018 us | Managed faster | 0.47x |
| `count/from-offset` | `CountFromOffset` | 14.211 us | 50.372 us | Managed faster | 0.28x |
| `family/count` | `Count` | 640.804 us | 2,470.200 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.288 us | 2.006 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 57.899 us | 425.841 us | Managed faster | 0.14x |
| `class-run/count` | `Count` | 270.398 us | 2,568.437 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.756 us | 38.833 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.544 us | 90.684 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.379 us | 91.083 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.263 us | 142.714 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.103 us | 0.455 us | Inconclusive | 2.43x |
| `replacement/empty-progression-string` | `ReplaceString` | 36.495 us | 23.788 us | CPython faster | 1.53x |
| `replacement/named-template-string` | `ReplaceString` | 1.276 us | 0.916 us | CPython faster | 1.39x |
| `replacement/fixed-string` | `ReplaceString` | 187.599 us | 176.942 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 94.042 us | 90.605 us | CPython faster | 1.04x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 178.915 us | 180.185 us | Equivalent | 0.99x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.689 us | 92.282 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 49.340 us | 51.127 us | Managed faster | 0.97x |
| `replacement/no-match-string` | `ReplaceString` | 1.222 us | 2.417 us | Inconclusive | 0.51x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 158.739 us | 4,287.455 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 612.035 us | 17,369.759 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 612.088 us | 17,509.950 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.877 us | 16,522.756 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.452 us | 0.546 us | Inconclusive | 2.66x |
| `split/zero-width` | `SplitStrings` | 121.850 us | 66.781 us | CPython faster | 1.83x |
| `split/unicode` | `SplitStrings` | 77.229 us | 44.698 us | CPython faster | 1.73x |
| `split/captures` | `SplitStrings` | 434.746 us | 255.595 us | CPython faster | 1.70x |
| `split/no-captures` | `SplitStrings` | 312.724 us | 224.431 us | CPython faster | 1.39x |
| `split/optional-capture` | `SplitStrings` | 74.227 us | 83.585 us | Managed faster | 0.89x |
| `split/detailed` | `SplitDetailed` | 113.061 us | 136.377 us | Managed faster | 0.83x |
| `split/one-separator` | `SplitStrings` | 1.183 us | 12.052 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.218 us | 25.073 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 186.788 us | 400.138 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 82.068 us | 629.257 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.364 us | 581.089 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,066.728 us | 357,798.799 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.37 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.20 %/17.29 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.22 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.20 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/15.93 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 175.222 us / 71,617 B | 60.336 us | 171.534 us / 71,641 B | 60.685 us |
| `fallback` | 308.856 us / 83,561 B | 42.450 us | 231.916 us / 83,529 B | 42.214 us |
| `literal` | 237.525 us / 50,369 B | 25.140 us | 214.553 us / 50,337 B | 25.082 us |
| `unicode` | 93.156 us / 48,313 B | 35.901 us | 91.638 us / 48,281 B | 36.206 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
