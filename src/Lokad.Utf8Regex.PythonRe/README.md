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

Snapshot SHA-256: `D5C634459EC7BC951292145505DC0D7F16265048933BE6FE5644D5A21488C5AD`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `44` managed faster, `1` equivalent, `32` CPython faster, `8` inconclusive, `5` unqualified.

Measured source `d8eb14a52b2c`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.650 us | 0.224 us | CPython faster | 2.91x |
| `common/date-miss` | `IsMatch` | 1.833 us | 0.833 us | CPython faster | 2.18x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.380 us | 0.192 us | CPython faster | 1.98x |
| `leading-inline/search-hit` | `Search` | 0.593 us | 0.326 us | CPython faster | 1.82x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.490 us | 0.283 us | CPython faster | 1.73x |
| `atomic/search-miss` | `Search` | 0.433 us | 0.257 us | CPython faster | 1.70x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.275 us | 0.176 us | CPython faster | 1.56x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.423 us | 0.274 us | CPython faster | 1.54x |
| `possessive/search-hit` | `Search` | 0.431 us | 0.305 us | CPython faster | 1.40x |
| `reluctant/search-hit` | `Search` | 0.465 us | 0.338 us | CPython faster | 1.38x |
| `atomic/search-hit` | `Search` | 0.422 us | 0.310 us | CPython faster | 1.36x |
| `lookbehind/search-miss` | `Search` | 0.289 us | 0.221 us | CPython faster | 1.30x |
| `scoped-inline/search-hit` | `Search` | 0.322 us | 0.285 us | CPython faster | 1.13x |
| `prefix/match` | `Match` | 0.316 us | 0.279 us | CPython faster | 1.13x |
| `multiline/search-miss` | `Search` | 0.270 us | 0.257 us | CPython faster | 1.05x |
| `unicode-option/search-hit` | `Search` | 0.298 us | 0.288 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 16.549 us | 18.987 us | Managed faster | 0.87x |
| `unicode-category/search-miss` | `Search` | 0.167 us | 0.207 us | Managed faster | 0.81x |
| `ascii-option/search-hit` | `Search` | 0.233 us | 0.295 us | Managed faster | 0.79x |
| `multiline/search-hit` | `Search` | 0.249 us | 0.324 us | Managed faster | 0.77x |
| `ignorecase/search-miss` | `Search` | 0.195 us | 0.280 us | Managed faster | 0.70x |
| `greedy/search-miss` | `Search` | 0.134 us | 0.215 us | Managed faster | 0.62x |
| `lookahead/search-hit` | `Search` | 0.175 us | 0.288 us | Managed faster | 0.60x |
| `possessive/search-miss` | `Search` | 0.117 us | 0.208 us | Managed faster | 0.56x |
| `unicode-literal/search-hit` | `Search` | 0.130 us | 0.232 us | Managed faster | 0.56x |
| `unicode-literal/search-miss` | `Search` | 0.150 us | 0.279 us | Managed faster | 0.53x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.145 us | 0.327 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.084 us | 0.249 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.076 us | 0.335 us | Managed faster | 0.22x |
| `common/ip-match` | `IsMatch` | 0.056 us | 0.315 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.542 us | 21.890 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.421 us | 21.594 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.610 us | 21.438 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.193 us | 0.227 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.305 us | 0.347 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.192 us | 0.188 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.299 us | 0.321 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.199 us | 0.231 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.021 us | 1.280 us | Managed faster | 0.80x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 499.312 us | 209.151 us | CPython faster | 2.39x |
| `findall/many-capture-strings` | `FindAllStrings` | 801.318 us | 342.819 us | CPython faster | 2.33x |
| `findall/named-capture-strings` | `FindAllStrings` | 160.601 us | 91.362 us | CPython faster | 1.76x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 95.049 us | 60.102 us | CPython faster | 1.58x |
| `findall/empty-progression-strings` | `FindAllStrings` | 65.820 us | 41.979 us | CPython faster | 1.57x |
| `findall/full-strings` | `FindAllStrings` | 498.242 us | 353.290 us | CPython faster | 1.41x |
| `findall/unicode-full-strings` | `FindAllStrings` | 37.590 us | 26.675 us | CPython faster | 1.41x |
| `findall/optional-capture-strings` | `FindAllStrings` | 57.778 us | 52.823 us | CPython faster | 1.09x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.338 us | 15.461 us | CPython faster | 1.06x |
| `findall/full-utf8` | `FindAllUtf8` | 492.019 us | 501.013 us | Inconclusive | 0.98x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 546.099 us | 596.655 us | Managed faster | 0.91x |
| `findall/structural-many` | `FindAllStructural` | 175.829 us | 192.989 us | Managed faster | 0.91x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 821.765 us | 1,208.369 us | Managed faster | 0.68x |
| `zero-width/count` | `Count` | 597.540 us | 1,078.622 us | Managed faster | 0.55x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 271.203 us | 530.978 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 175.704 us | 370.357 us | Managed faster | 0.47x |
| `count/from-offset` | `CountFromOffset` | 13.482 us | 48.728 us | Managed faster | 0.28x |
| `family/count` | `Count` | 633.127 us | 2,385.212 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.273 us | 1.942 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 55.697 us | 417.465 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 262.627 us | 2,449.585 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.797 us | 36.998 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.507 us | 88.152 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.382 us | 88.685 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.242 us | 137.324 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.081 us | 0.442 us | Inconclusive | 2.44x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.697 us | 22.962 us | CPython faster | 1.51x |
| `replacement/named-template-string` | `ReplaceString` | 1.266 us | 0.907 us | Inconclusive | 1.40x |
| `replacement/fixed-string` | `ReplaceString` | 183.408 us | 169.724 us | CPython faster | 1.08x |
| `replacement/subn-string` | `SubnString` | 91.744 us | 89.015 us | CPython faster | 1.03x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 174.912 us | 174.950 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 86.492 us | 89.116 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 47.425 us | 49.791 us | Managed faster | 0.95x |
| `replacement/no-match-string` | `ReplaceString` | 1.194 us | 2.336 us | Inconclusive | 0.51x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 155.657 us | 4,169.999 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 598.827 us | 16,664.118 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 597.642 us | 16,698.910 us | Managed faster | 0.04x |
| `replacement/one-template-string` | `ReplaceString` | 1.876 us | 15,779.061 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.543 us | 0.525 us | Inconclusive | 2.92x |
| `split/zero-width` | `SplitStrings` | 119.122 us | 65.299 us | CPython faster | 1.82x |
| `split/captures` | `SplitStrings` | 430.831 us | 246.845 us | CPython faster | 1.74x |
| `split/unicode` | `SplitStrings` | 69.563 us | 43.029 us | CPython faster | 1.62x |
| `split/no-captures` | `SplitStrings` | 271.334 us | 217.327 us | CPython faster | 1.25x |
| `split/optional-capture` | `SplitStrings` | 75.371 us | 80.083 us | Managed faster | 0.94x |
| `split/detailed` | `SplitDetailed` | 112.641 us | 131.110 us | Managed faster | 0.86x |
| `split/one-separator` | `SplitStrings` | 1.265 us | 11.532 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.271 us | 24.081 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 190.391 us | 389.742 us | Managed faster | 0.49x |
| `corpus/code-declarations` | `Count` | 84.045 us | 610.686 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 47.662 us | 556.880 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 2,023.058 us | 344,717.019 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.21 %/14.80 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.15 %/9.69 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.20 %/17.42 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.63 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.19 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 201.566 us / 67,601 B | 58.713 us | 183.581 us / 67,625 B | 58.961 us |
| `fallback` | 310.887 us / 79,761 B | 39.646 us | 257.150 us / 79,729 B | 40.899 us |
| `literal` | 283.856 us / 50,385 B | 23.835 us | 311.347 us / 50,353 B | 24.009 us |
| `unicode` | 53.206 us / 48,329 B | 36.049 us | 59.075 us / 48,297 B | 35.208 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
