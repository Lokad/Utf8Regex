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

Snapshot SHA-256: `DA22DED7FBAFD381E95F404EA6D7F1DBF9820117A3CA94D1A502B46665B8BBBA`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `43` managed faster, `2` equivalent, `34` CPython faster, `6` inconclusive, `5` unqualified.

Measured source `6f46f988899b`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.
Measured source `a7e985ed2c1b`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.681 us | 0.238 us | CPython faster | 2.87x |
| `common/date-miss` | `IsMatch` | 1.818 us | 0.862 us | CPython faster | 2.11x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.384 us | 0.198 us | CPython faster | 1.94x |
| `leading-inline/search-hit` | `Search` | 0.606 us | 0.334 us | CPython faster | 1.81x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.495 us | 0.295 us | CPython faster | 1.68x |
| `atomic/search-miss` | `Search` | 0.433 us | 0.263 us | CPython faster | 1.64x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.279 us | 0.185 us | CPython faster | 1.51x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.411 us | 0.282 us | CPython faster | 1.45x |
| `possessive/search-hit` | `Search` | 0.427 us | 0.309 us | CPython faster | 1.38x |
| `reluctant/search-hit` | `Search` | 0.473 us | 0.352 us | CPython faster | 1.34x |
| `atomic/search-hit` | `Search` | 0.423 us | 0.326 us | CPython faster | 1.30x |
| `lookbehind/search-miss` | `Search` | 0.295 us | 0.236 us | CPython faster | 1.25x |
| `prefix/match` | `Match` | 0.331 us | 0.275 us | CPython faster | 1.20x |
| `scoped-inline/search-hit` | `Search` | 0.336 us | 0.292 us | CPython faster | 1.15x |
| `unicode-option/search-hit` | `Search` | 0.317 us | 0.302 us | CPython faster | 1.05x |
| `multiline/search-miss` | `Search` | 0.277 us | 0.268 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.547 us | 19.703 us | Managed faster | 0.89x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.199 us | 0.239 us | Managed faster | 0.84x |
| `ascii-option/search-hit` | `Search` | 0.242 us | 0.307 us | Managed faster | 0.79x |
| `multiline/search-hit` | `Search` | 0.251 us | 0.334 us | Managed faster | 0.75x |
| `ignorecase/search-miss` | `Search` | 0.206 us | 0.287 us | Managed faster | 0.71x |
| `greedy/search-miss` | `Search` | 0.134 us | 0.222 us | Managed faster | 0.60x |
| `lookahead/search-hit` | `Search` | 0.181 us | 0.301 us | Managed faster | 0.60x |
| `unicode-literal/search-hit` | `Search` | 0.137 us | 0.238 us | Managed faster | 0.57x |
| `possessive/search-miss` | `Search` | 0.118 us | 0.212 us | Managed faster | 0.55x |
| `unicode-literal/search-miss` | `Search` | 0.152 us | 0.292 us | Managed faster | 0.52x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.148 us | 0.335 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.086 us | 0.257 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.077 us | 0.351 us | Managed faster | 0.22x |
| `common/ip-match` | `IsMatch` | 0.058 us | 0.324 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.687 us | 22.661 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.465 us | 22.382 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.674 us | 22.244 us | Managed faster | 0.08x |
| `alternation/search-miss` | `Search` | 0.201 us | 0.237 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.318 us | 0.359 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.195 us | 0.196 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.320 us | 0.334 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.177 us | 0.215 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.029 us | 1.338 us | Managed faster | 0.76x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 428.963 us | 217.061 us | CPython faster | 1.98x |
| `findall/many-capture-strings` | `FindAllStrings` | 649.661 us | 343.797 us | CPython faster | 1.89x |
| `findall/named-capture-strings` | `FindAllStrings` | 160.909 us | 95.819 us | CPython faster | 1.68x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 97.068 us | 62.480 us | CPython faster | 1.55x |
| `findall/empty-progression-strings` | `FindAllStrings` | 64.951 us | 43.507 us | CPython faster | 1.49x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.837 us | 27.779 us | CPython faster | 1.40x |
| `findall/full-strings` | `FindAllStrings` | 516.760 us | 371.624 us | CPython faster | 1.39x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.086 us | 16.068 us | CPython faster | 1.06x |
| `findall/optional-capture-strings` | `FindAllStrings` | 58.599 us | 55.570 us | CPython faster | 1.06x |
| `findall/full-utf8` | `FindAllUtf8` | 517.244 us | 519.880 us | Equivalent | 0.99x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 565.769 us | 621.199 us | Managed faster | 0.91x |
| `findall/structural-many` | `FindAllStructural` | 182.049 us | 203.480 us | Managed faster | 0.90x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 839.890 us | 1,247.768 us | Managed faster | 0.67x |
| `zero-width/count` | `Count` | 577.854 us | 1,117.747 us | Managed faster | 0.52x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 275.245 us | 543.605 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 175.791 us | 380.885 us | Managed faster | 0.46x |
| `count/from-offset` | `CountFromOffset` | 14.159 us | 50.752 us | Managed faster | 0.28x |
| `family/count` | `Count` | 649.823 us | 2,455.166 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.272 us | 2.023 us | Managed faster | 0.13x |
| `unicode/count` | `Count` | 57.959 us | 431.951 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 282.482 us | 2,621.827 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.853 us | 38.895 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.553 us | 90.701 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.386 us | 91.332 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.256 us | 143.214 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.110 us | 0.456 us | Inconclusive | 2.42x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.265 us | 23.740 us | CPython faster | 1.45x |
| `replacement/named-template-string` | `ReplaceString` | 1.292 us | 0.950 us | CPython faster | 1.36x |
| `replacement/fixed-string` | `ReplaceString` | 189.478 us | 175.891 us | CPython faster | 1.08x |
| `replacement/subn-string` | `SubnString` | 95.040 us | 90.654 us | CPython faster | 1.05x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 179.716 us | 181.058 us | Equivalent | 0.99x |
| `replacement/subn-utf8` | `SubnUtf8` | 90.047 us | 91.505 us | Inconclusive | 0.99x |
| `replacement/unmatched-template-string` | `ReplaceString` | 47.520 us | 50.865 us | Managed faster | 0.94x |
| `replacement/no-match-string` | `ReplaceString` | 1.142 us | 2.411 us | Inconclusive | 0.47x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 153.905 us | 4,312.575 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 601.682 us | 17,271.772 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 608.492 us | 17,498.399 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.963 us | 16,427.862 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.544 us | 0.534 us | CPython faster | 2.80x |
| `split/zero-width` | `SplitStrings` | 114.964 us | 66.653 us | CPython faster | 1.72x |
| `split/captures` | `SplitStrings` | 428.314 us | 259.369 us | CPython faster | 1.65x |
| `split/unicode` | `SplitStrings` | 70.358 us | 44.440 us | CPython faster | 1.58x |
| `split/no-captures` | `SplitStrings` | 278.674 us | 225.298 us | CPython faster | 1.24x |
| `split/optional-capture` | `SplitStrings` | 73.627 us | 82.047 us | Managed faster | 0.90x |
| `split/detailed` | `SplitDetailed` | 111.312 us | 135.606 us | Managed faster | 0.82x |
| `split/one-separator` | `SplitStrings` | 1.268 us | 12.061 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.290 us | 25.051 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 195.559 us | 401.790 us | Managed faster | 0.49x |
| `corpus/code-declarations` | `Count` | 86.246 us | 630.284 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 48.059 us | 580.125 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,132.850 us | 359,289.741 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.21 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.82 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/17.30 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.26 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/15.98 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 164.581 us / 67,601 B | 62.291 us | 152.681 us / 67,625 B | 62.678 us |
| `fallback` | 292.650 us / 79,761 B | 43.982 us | 220.178 us / 79,729 B | 43.580 us |
| `literal` | 237.247 us / 50,385 B | 26.258 us | 215.750 us / 50,353 B | 26.313 us |
| `unicode` | 91.362 us / 48,329 B | 37.267 us | 87.897 us / 48,297 B | 37.381 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
