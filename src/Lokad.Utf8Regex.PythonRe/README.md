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

Snapshot SHA-256: `2F2B31EBE230CCF275B90CC964B6FCA9996EF31AC8690C36E779207DF6A40B7E`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `46` managed faster, `2` equivalent, `29` CPython faster, `7` inconclusive, `6` unqualified.

Measured source `faa48f5730df`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.668 us | 0.226 us | CPython faster | 2.94x |
| `common/date-miss` | `IsMatch` | 1.754 us | 0.832 us | CPython faster | 2.09x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.376 us | 0.190 us | CPython faster | 1.98x |
| `leading-inline/search-hit` | `Search` | 0.582 us | 0.327 us | CPython faster | 1.78x |
| `atomic/search-miss` | `Search` | 0.451 us | 0.256 us | CPython faster | 1.78x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.476 us | 0.290 us | CPython faster | 1.64x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.414 us | 0.274 us | CPython faster | 1.52x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.264 us | 0.175 us | CPython faster | 1.50x |
| `possessive/search-hit` | `Search` | 0.440 us | 0.301 us | CPython faster | 1.47x |
| `lookbehind/search-miss` | `Search` | 0.316 us | 0.224 us | CPython faster | 1.41x |
| `reluctant/search-hit` | `Search` | 0.470 us | 0.339 us | CPython faster | 1.38x |
| `atomic/search-hit` | `Search` | 0.428 us | 0.317 us | CPython faster | 1.34x |
| `scoped-inline/search-hit` | `Search` | 0.336 us | 0.286 us | CPython faster | 1.18x |
| `unicode-option/search-hit` | `Search` | 0.324 us | 0.290 us | CPython faster | 1.11x |
| `multiline/search-miss` | `Search` | 0.284 us | 0.261 us | CPython faster | 1.09x |
| `lookahead/search-miss` | `Search` | 0.202 us | 0.189 us | CPython faster | 1.07x |
| `unicode/fullmatch` | `FullMatch` | 17.028 us | 19.045 us | Managed faster | 0.89x |
| `multiline/search-hit` | `Search` | 0.269 us | 0.330 us | Managed faster | 0.81x |
| `ignorecase/search-miss` | `Search` | 0.207 us | 0.277 us | Managed faster | 0.75x |
| `prefix/match` | `Match` | 0.184 us | 0.266 us | Managed faster | 0.69x |
| `lookahead/search-hit` | `Search` | 0.201 us | 0.292 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.139 us | 0.214 us | Managed faster | 0.65x |
| `possessive/search-miss` | `Search` | 0.126 us | 0.206 us | Managed faster | 0.61x |
| `unicode-literal/search-hit` | `Search` | 0.136 us | 0.230 us | Managed faster | 0.59x |
| `unicode-literal/search-miss` | `Search` | 0.139 us | 0.283 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.154 us | 0.326 us | Managed faster | 0.47x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.096 us | 0.251 us | Managed faster | 0.38x |
| `literal/fullmatch` | `FullMatch` | 0.066 us | 0.338 us | Managed faster | 0.19x |
| `common/ip-match` | `IsMatch` | 0.055 us | 0.311 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.607 us | 21.841 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.388 us | 21.792 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.620 us | 21.685 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.193 us | 0.228 us | Unqualified | — |
| `ascii-option/search-hit` | `Search` | 0.264 us | 0.301 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.325 us | 0.347 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.329 us | 0.322 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.202 us | 0.232 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.174 us | 0.207 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.895 us | 1.318 us | Inconclusive | 0.68x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 384.514 us | 208.560 us | CPython faster | 1.83x |
| `findall/many-capture-strings` | `FindAllStrings` | 600.485 us | 335.604 us | CPython faster | 1.79x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 94.196 us | 60.010 us | CPython faster | 1.57x |
| `findall/empty-progression-strings` | `FindAllStrings` | 60.230 us | 41.626 us | CPython faster | 1.45x |
| `findall/full-strings` | `FindAllStrings` | 478.538 us | 362.782 us | CPython faster | 1.32x |
| `findall/named-capture-strings` | `FindAllStrings` | 120.110 us | 91.399 us | CPython faster | 1.31x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.168 us | 15.497 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.303 us | 53.963 us | Managed faster | 0.95x |
| `findall/full-utf8` | `FindAllUtf8` | 478.814 us | 504.539 us | Managed faster | 0.95x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 513.523 us | 604.858 us | Managed faster | 0.85x |
| `findall/unicode-full-strings` | `FindAllStrings` | 19.355 us | 26.741 us | Managed faster | 0.73x |
| `findall/structural-many` | `FindAllStructural` | 127.062 us | 193.501 us | Managed faster | 0.65x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 627.417 us | 1,220.356 us | Managed faster | 0.51x |
| `zero-width/count` | `Count` | 522.035 us | 1,089.049 us | Managed faster | 0.48x |
| `count/empty-progression` | `Count` | 166.584 us | 363.619 us | Managed faster | 0.46x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 222.076 us | 531.712 us | Managed faster | 0.42x |
| `count/from-offset` | `CountFromOffset` | 14.139 us | 49.084 us | Managed faster | 0.29x |
| `family/count` | `Count` | 529.374 us | 2,388.097 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 55.600 us | 424.527 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.236 us | 1.936 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 271.536 us | 2,487.736 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.845 us | 37.633 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.554 us | 88.349 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.413 us | 88.579 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.261 us | 135.793 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.082 us | 0.440 us | Inconclusive | 2.47x |
| `replacement/empty-progression-string` | `ReplaceString` | 32.072 us | 23.087 us | CPython faster | 1.39x |
| `replacement/named-template-string` | `ReplaceString` | 1.116 us | 0.896 us | CPython faster | 1.25x |
| `replacement/fixed-string` | `ReplaceString` | 180.566 us | 175.040 us | CPython faster | 1.04x |
| `replacement/subn-string` | `SubnString` | 89.650 us | 88.976 us | Equivalent | 1.00x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 169.035 us | 174.883 us | Managed faster | 0.96x |
| `replacement/subn-utf8` | `SubnUtf8` | 84.679 us | 89.882 us | Managed faster | 0.94x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.374 us | 50.045 us | Managed faster | 0.83x |
| `replacement/no-match-string` | `ReplaceString` | 1.083 us | 2.312 us | Inconclusive | 0.47x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 135.948 us | 4,212.674 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 536.658 us | 16,942.676 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 529.280 us | 16,890.240 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.859 us | 15,834.268 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.558 us | 0.539 us | Inconclusive | 2.88x |
| `split/zero-width` | `SplitStrings` | 103.102 us | 64.102 us | CPython faster | 1.61x |
| `split/unicode` | `SplitStrings` | 61.081 us | 43.398 us | CPython faster | 1.41x |
| `split/captures` | `SplitStrings` | 349.174 us | 251.294 us | CPython faster | 1.39x |
| `split/no-captures` | `SplitStrings` | 216.266 us | 214.793 us | Equivalent | 1.00x |
| `split/optional-capture` | `SplitStrings` | 66.217 us | 78.910 us | Managed faster | 0.84x |
| `split/detailed` | `SplitDetailed` | 89.865 us | 131.854 us | Managed faster | 0.69x |
| `split/one-separator` | `SplitStrings` | 1.275 us | 11.648 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.330 us | 24.194 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 185.203 us | 396.935 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 73.829 us | 605.499 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 45.966 us | 554.114 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,877.393 us | 348,336.125 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.22 %/14.73 % of the lanes.
- `ascii-option/search-hit`: Removable harness floors are 0.20 %/11.17 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.70 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.44 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.55 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/16.28 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 141.091 us / 67,601 B | 59.892 us | 140.088 us / 67,625 B | 61.182 us |
| `fallback` | 271.791 us / 79,761 B | 43.549 us | 185.147 us / 79,729 B | 41.612 us |
| `literal` | 237.012 us / 50,385 B | 25.410 us | 215.728 us / 50,353 B | 26.667 us |
| `unicode` | 91.316 us / 48,329 B | 37.636 us | 86.078 us / 48,297 B | 36.852 us |

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
