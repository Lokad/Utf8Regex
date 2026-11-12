# Lokad.Utf8Regex.PythonRe

**0.5.0 development**, .NET 10 and .NET 11. A managed Python `re`-style adapter
over strict UTF-8 Unicode scalars, requiring the exact Core package version
matching the adapter. It can coexist with PCRE2 at the same version. No Python
runtime or BCL regex matcher is a shipped implementation dependency.

## Subject validation policy

Construct with `inputValidation: Utf8InputValidation.AssumeValid` only when the
host guarantees valid UTF-8 and unchanged borrowed storage. Read-only
`InputValidation` reports the choice; `Validate` is default and checks the whole
subject at most once per logical operation or cursor lifetime. Matching, counting,
replacement/splitting where supported and cursors inherit the engine policy.
Pattern/replacement validation, checked accesses, scalar/byte semantics,
cooperative cancellation/deadlines and admission remain. Required coordinate or
output work may still inspect input. See the [shared contract](../../docs/SPEC.md#subject-input-validation).
Existing benchmark tables use Validate unless explicitly labeled otherwise.

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

Use `--measure-pythonre-paired-case findall/many-capture-strings 9 AssumeValid`
for a focused trusted-input comparison. Omit the final argument for `Validate`.
The command prints labeled raw paired evidence without changing the recorded tables.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `B0202165F10DFD875E2C401D32989F8380A0B069BA3B1709BCE4B39258F17984`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `47` managed faster, `2` equivalent, `29` CPython faster, `7` inconclusive, `5` unqualified.

Measured source `7e6f00e6810b`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.685 us | 0.232 us | CPython faster | 2.94x |
| `common/date-miss` | `IsMatch` | 1.818 us | 0.861 us | CPython faster | 2.11x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.383 us | 0.199 us | CPython faster | 1.93x |
| `atomic/search-miss` | `Search` | 0.473 us | 0.262 us | CPython faster | 1.81x |
| `leading-inline/search-hit` | `Search` | 0.583 us | 0.336 us | CPython faster | 1.73x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.485 us | 0.294 us | CPython faster | 1.65x |
| `possessive/search-hit` | `Search` | 0.479 us | 0.308 us | CPython faster | 1.55x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.271 us | 0.180 us | CPython faster | 1.50x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.410 us | 0.282 us | CPython faster | 1.46x |
| `lookbehind/search-miss` | `Search` | 0.318 us | 0.219 us | CPython faster | 1.46x |
| `atomic/search-hit` | `Search` | 0.450 us | 0.324 us | CPython faster | 1.39x |
| `reluctant/search-hit` | `Search` | 0.487 us | 0.352 us | CPython faster | 1.38x |
| `scoped-inline/search-hit` | `Search` | 0.343 us | 0.293 us | CPython faster | 1.17x |
| `multiline/search-miss` | `Search` | 0.291 us | 0.268 us | CPython faster | 1.09x |
| `unicode-option/search-hit` | `Search` | 0.325 us | 0.299 us | CPython faster | 1.09x |
| `lookahead/search-miss` | `Search` | 0.212 us | 0.197 us | CPython faster | 1.08x |
| `unicode/fullmatch` | `FullMatch` | 17.477 us | 19.679 us | Managed faster | 0.89x |
| `ascii-option/search-hit` | `Search` | 0.264 us | 0.306 us | Managed faster | 0.86x |
| `multiline/search-hit` | `Search` | 0.267 us | 0.335 us | Managed faster | 0.80x |
| `ignorecase/search-miss` | `Search` | 0.216 us | 0.286 us | Managed faster | 0.75x |
| `lookahead/search-hit` | `Search` | 0.211 us | 0.300 us | Managed faster | 0.70x |
| `prefix/match` | `Match` | 0.190 us | 0.272 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.146 us | 0.222 us | Managed faster | 0.66x |
| `possessive/search-miss` | `Search` | 0.132 us | 0.212 us | Managed faster | 0.62x |
| `unicode-literal/search-hit` | `Search` | 0.134 us | 0.237 us | Managed faster | 0.57x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.162 us | 0.336 us | Managed faster | 0.48x |
| `unicode-literal/search-miss` | `Search` | 0.136 us | 0.291 us | Managed faster | 0.47x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.095 us | 0.256 us | Managed faster | 0.38x |
| `common/ip-match` | `IsMatch` | 0.064 us | 0.320 us | Managed faster | 0.20x |
| `literal/fullmatch` | `FullMatch` | 0.067 us | 0.349 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.705 us | 22.555 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.438 us | 22.204 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.651 us | 22.021 us | Managed faster | 0.08x |
| `alternation/search-miss` | `Search` | 0.202 us | 0.237 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.328 us | 0.357 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.336 us | 0.333 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.211 us | 0.237 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.184 us | 0.213 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.029 us | 1.319 us | Inconclusive | 0.78x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 497.491 us | 213.381 us | CPython faster | 2.33x |
| `findall/many-capture-strings` | `FindAllStrings` | 774.094 us | 349.007 us | CPython faster | 2.22x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 96.800 us | 62.135 us | CPython faster | 1.55x |
| `findall/named-capture-strings` | `FindAllStrings` | 143.642 us | 94.305 us | CPython faster | 1.52x |
| `findall/empty-progression-strings` | `FindAllStrings` | 61.267 us | 43.377 us | CPython faster | 1.41x |
| `findall/full-strings` | `FindAllStrings` | 472.119 us | 368.494 us | CPython faster | 1.28x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.110 us | 15.937 us | CPython faster | 1.07x |
| `findall/optional-capture-strings` | `FindAllStrings` | 53.047 us | 55.594 us | Managed faster | 0.95x |
| `findall/full-utf8` | `FindAllUtf8` | 474.022 us | 518.936 us | Managed faster | 0.91x |
| `findall/structural-many` | `FindAllStructural` | 168.009 us | 199.829 us | Managed faster | 0.84x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 527.265 us | 634.605 us | Managed faster | 0.83x |
| `findall/unicode-full-strings` | `FindAllStrings` | 19.746 us | 27.552 us | Managed faster | 0.72x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 787.994 us | 1,242.968 us | Managed faster | 0.63x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 268.497 us | 539.686 us | Managed faster | 0.50x |
| `zero-width/count` | `Count` | 532.212 us | 1,112.398 us | Managed faster | 0.48x |
| `count/empty-progression` | `Count` | 171.802 us | 378.223 us | Managed faster | 0.45x |
| `count/from-offset` | `CountFromOffset` | 13.637 us | 49.841 us | Managed faster | 0.27x |
| `family/count` | `Count` | 546.038 us | 2,465.372 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 56.429 us | 429.689 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.242 us | 1.989 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 279.356 us | 2,586.355 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.875 us | 38.725 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.557 us | 90.685 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.417 us | 90.941 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.265 us | 142.831 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.126 us | 0.459 us | Inconclusive | 2.47x |
| `replacement/empty-progression-string` | `ReplaceString` | 33.761 us | 23.302 us | CPython faster | 1.44x |
| `replacement/named-template-string` | `ReplaceString` | 1.227 us | 0.920 us | CPython faster | 1.33x |
| `replacement/fixed-string` | `ReplaceString` | 183.164 us | 177.548 us | CPython faster | 1.03x |
| `replacement/subn-string` | `SubnString` | 90.750 us | 90.909 us | Equivalent | 1.00x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 172.661 us | 177.834 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 85.875 us | 90.668 us | Managed faster | 0.94x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.839 us | 50.379 us | Managed faster | 0.81x |
| `replacement/no-match-string` | `ReplaceString` | 1.113 us | 2.404 us | Inconclusive | 0.46x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 150.423 us | 4,348.090 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 581.613 us | 17,046.090 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 580.188 us | 17,059.808 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.956 us | 16,244.087 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.585 us | 0.544 us | Inconclusive | 2.94x |
| `split/zero-width` | `SplitStrings` | 105.482 us | 66.580 us | CPython faster | 1.59x |
| `split/captures` | `SplitStrings` | 391.416 us | 254.811 us | CPython faster | 1.54x |
| `split/unicode` | `SplitStrings` | 62.284 us | 44.513 us | CPython faster | 1.40x |
| `split/no-captures` | `SplitStrings` | 219.923 us | 223.234 us | Equivalent | 0.99x |
| `split/optional-capture` | `SplitStrings` | 67.423 us | 82.461 us | Managed faster | 0.82x |
| `split/detailed` | `SplitDetailed` | 103.331 us | 136.136 us | Managed faster | 0.76x |
| `split/one-separator` | `SplitStrings` | 1.300 us | 12.014 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.316 us | 25.020 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 186.890 us | 399.853 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 76.530 us | 629.380 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 47.673 us | 575.139 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,067.396 us | 355,269.726 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.26 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.55 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.21 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.48 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/15.88 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 184.944 us / 67,601 B | 59.726 us | 168.844 us / 67,625 B | 58.816 us |
| `fallback` | 241.956 us / 79,761 B | 42.425 us | 253.047 us / 79,729 B | 40.748 us |
| `literal` | 336.613 us / 50,385 B | 25.474 us | 257.947 us / 50,353 B | 25.263 us |
| `unicode` | 54.978 us / 48,329 B | 36.113 us | 57.703 us / 48,297 B | 35.838 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
