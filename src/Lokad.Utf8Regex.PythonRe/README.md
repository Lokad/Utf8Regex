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

Snapshot SHA-256: `3E27058C95EC877B4AB4D4E08204491C6675588FBD0A6B2DB401A6C4496EDE5C`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `48` managed faster, `2` equivalent, `29` CPython faster, `6` inconclusive, `5` unqualified.

Measured source `891cabbf23c9`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.676 us | 0.231 us | CPython faster | 2.92x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.407 us | 0.190 us | CPython faster | 2.15x |
| `common/date-miss` | `IsMatch` | 1.716 us | 0.833 us | CPython faster | 2.05x |
| `atomic/search-miss` | `Search` | 0.471 us | 0.253 us | CPython faster | 1.86x |
| `leading-inline/search-hit` | `Search` | 0.603 us | 0.331 us | CPython faster | 1.83x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.495 us | 0.295 us | CPython faster | 1.68x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.450 us | 0.282 us | CPython faster | 1.59x |
| `possessive/search-hit` | `Search` | 0.471 us | 0.306 us | CPython faster | 1.56x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.268 us | 0.174 us | CPython faster | 1.55x |
| `lookbehind/search-miss` | `Search` | 0.328 us | 0.212 us | CPython faster | 1.54x |
| `reluctant/search-hit` | `Search` | 0.515 us | 0.345 us | CPython faster | 1.49x |
| `atomic/search-hit` | `Search` | 0.461 us | 0.318 us | CPython faster | 1.45x |
| `scoped-inline/search-hit` | `Search` | 0.363 us | 0.289 us | CPython faster | 1.26x |
| `multiline/search-miss` | `Search` | 0.295 us | 0.259 us | CPython faster | 1.14x |
| `lookahead/search-miss` | `Search` | 0.214 us | 0.188 us | CPython faster | 1.14x |
| `lookbehind/search-hit` | `Search` | 0.350 us | 0.331 us | CPython faster | 1.07x |
| `unicode/fullmatch` | `FullMatch` | 17.311 us | 18.788 us | Managed faster | 0.92x |
| `multiline/search-hit` | `Search` | 0.272 us | 0.332 us | Managed faster | 0.82x |
| `ignorecase/search-miss` | `Search` | 0.215 us | 0.279 us | Managed faster | 0.77x |
| `ascii-option/search-hit` | `Search` | 0.237 us | 0.308 us | Managed faster | 0.77x |
| `greedy/search-miss` | `Search` | 0.149 us | 0.209 us | Managed faster | 0.71x |
| `lookahead/search-hit` | `Search` | 0.210 us | 0.299 us | Managed faster | 0.70x |
| `prefix/match` | `Match` | 0.188 us | 0.273 us | Managed faster | 0.69x |
| `possessive/search-miss` | `Search` | 0.136 us | 0.206 us | Managed faster | 0.66x |
| `unicode-literal/search-hit` | `Search` | 0.134 us | 0.232 us | Managed faster | 0.58x |
| `unicode-literal/search-miss` | `Search` | 0.140 us | 0.284 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.158 us | 0.340 us | Managed faster | 0.47x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.090 us | 0.254 us | Managed faster | 0.35x |
| `literal/fullmatch` | `FullMatch` | 0.066 us | 0.338 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.063 us | 0.328 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.633 us | 22.409 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.422 us | 22.217 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.644 us | 21.852 us | Managed faster | 0.08x |
| `alternation/search-miss` | `Search` | 0.208 us | 0.229 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.330 us | 0.354 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.205 us | 0.234 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.186 us | 0.205 us | Unqualified | — |
| `unicode-option/search-hit` | `Search` | 0.301 us | 0.302 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.927 us | 1.326 us | Managed faster | 0.70x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 392.556 us | 213.451 us | CPython faster | 1.84x |
| `findall/many-capture-strings` | `FindAllStrings` | 623.877 us | 342.658 us | CPython faster | 1.82x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 95.373 us | 61.486 us | CPython faster | 1.55x |
| `findall/named-capture-strings` | `FindAllStrings` | 125.832 us | 96.121 us | CPython faster | 1.31x |
| `findall/empty-progression-strings` | `FindAllStrings` | 48.630 us | 44.944 us | CPython faster | 1.08x |
| `findall/full-strings` | `FindAllStrings` | 390.428 us | 367.740 us | CPython faster | 1.06x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.905 us | 15.952 us | CPython faster | 1.06x |
| `findall/optional-capture-strings` | `FindAllStrings` | 54.389 us | 54.738 us | Equivalent | 0.99x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 547.032 us | 613.780 us | Managed faster | 0.89x |
| `findall/full-utf8` | `FindAllUtf8` | 375.033 us | 510.809 us | Managed faster | 0.74x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.388 us | 27.134 us | Managed faster | 0.68x |
| `findall/structural-many` | `FindAllStructural` | 126.261 us | 190.666 us | Managed faster | 0.67x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 635.895 us | 1,236.873 us | Managed faster | 0.51x |
| `zero-width/count` | `Count` | 536.508 us | 1,091.452 us | Managed faster | 0.49x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 221.263 us | 533.878 us | Managed faster | 0.41x |
| `count/empty-progression` | `Count` | 120.710 us | 355.657 us | Managed faster | 0.34x |
| `count/from-offset` | `CountFromOffset` | 14.417 us | 48.803 us | Managed faster | 0.30x |
| `family/count` | `Count` | 536.283 us | 2,421.973 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 57.188 us | 422.952 us | Managed faster | 0.14x |
| `count/one` | `Count` | 0.240 us | 1.959 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 284.781 us | 2,537.338 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.816 us | 38.242 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.557 us | 89.727 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.411 us | 89.319 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.276 us | 140.667 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.053 us | 0.453 us | Inconclusive | 2.33x |
| `replacement/named-template-string` | `ReplaceString` | 1.148 us | 0.911 us | CPython faster | 1.27x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.701 us | 23.559 us | CPython faster | 1.09x |
| `replacement/fixed-string` | `ReplaceString` | 178.441 us | 172.594 us | CPython faster | 1.04x |
| `replacement/subn-string` | `SubnString` | 90.572 us | 89.982 us | Equivalent | 1.01x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 171.351 us | 176.711 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 84.940 us | 89.441 us | Managed faster | 0.95x |
| `replacement/unmatched-template-string` | `ReplaceString` | 43.388 us | 46.489 us | Managed faster | 0.94x |
| `replacement/no-match-string` | `ReplaceString` | 1.388 us | 2.363 us | Inconclusive | 0.58x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 136.768 us | 4,246.856 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 544.786 us | 17,012.722 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 527.209 us | 16,817.265 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.222 us | 16,075.773 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.561 us | 0.531 us | Inconclusive | 2.92x |
| `split/unicode` | `SplitStrings` | 65.428 us | 39.281 us | CPython faster | 1.67x |
| `split/zero-width` | `SplitStrings` | 104.062 us | 66.111 us | CPython faster | 1.57x |
| `split/captures` | `SplitStrings` | 353.163 us | 261.474 us | CPython faster | 1.35x |
| `split/no-captures` | `SplitStrings` | 222.090 us | 229.318 us | Managed faster | 0.97x |
| `split/optional-capture` | `SplitStrings` | 69.462 us | 83.177 us | Managed faster | 0.84x |
| `split/detailed` | `SplitDetailed` | 93.036 us | 134.076 us | Managed faster | 0.69x |
| `split/one-separator` | `SplitStrings` | 1.294 us | 12.045 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.283 us | 24.136 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 184.546 us | 393.667 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 76.521 us | 614.931 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 48.371 us | 566.964 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 1,830.046 us | 347,358.556 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.22 %/14.92 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.15 %/9.58 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.42 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/16.60 % of the lanes.
- `unicode-option/search-hit`: Removable harness floors are 0.16 %/11.16 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 330.766 us / 67,601 B | 61.296 us | 312.331 us / 67,625 B | 61.027 us |
| `fallback` | 569.769 us / 79,761 B | 43.089 us | 432.784 us / 79,729 B | 41.251 us |
| `literal` | 235.438 us / 50,385 B | 24.692 us | 210.106 us / 50,353 B | 24.163 us |
| `unicode` | 164.381 us / 48,329 B | 36.004 us | 166.656 us / 48,297 B | 35.938 us |

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
