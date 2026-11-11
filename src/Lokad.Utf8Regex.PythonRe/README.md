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

Snapshot SHA-256: `1CB2EFC0D5343F0D05B5C8F4B884C7CE97D70CABF875404C057F46055A5B13D6`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `48` managed faster, `2` equivalent, `30` CPython faster, `6` inconclusive, `4` unqualified.

Measured source `bd1a9e968011`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.700 us | 0.237 us | CPython faster | 2.96x |
| `common/date-miss` | `IsMatch` | 1.921 us | 0.861 us | CPython faster | 2.23x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.408 us | 0.197 us | CPython faster | 2.06x |
| `leading-inline/search-hit` | `Search` | 0.597 us | 0.335 us | CPython faster | 1.79x |
| `atomic/search-miss` | `Search` | 0.468 us | 0.261 us | CPython faster | 1.79x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.503 us | 0.296 us | CPython faster | 1.70x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.291 us | 0.184 us | CPython faster | 1.59x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.432 us | 0.281 us | CPython faster | 1.54x |
| `possessive/search-hit` | `Search` | 0.460 us | 0.309 us | CPython faster | 1.49x |
| `lookbehind/search-miss` | `Search` | 0.315 us | 0.220 us | CPython faster | 1.43x |
| `reluctant/search-hit` | `Search` | 0.482 us | 0.353 us | CPython faster | 1.37x |
| `atomic/search-hit` | `Search` | 0.439 us | 0.325 us | CPython faster | 1.35x |
| `scoped-inline/search-hit` | `Search` | 0.326 us | 0.293 us | CPython faster | 1.11x |
| `multiline/search-miss` | `Search` | 0.283 us | 0.266 us | CPython faster | 1.06x |
| `unicode-option/search-hit` | `Search` | 0.315 us | 0.300 us | CPython faster | 1.05x |
| `ignorecase/search-hit` | `Search` | 0.315 us | 0.358 us | Managed faster | 0.88x |
| `unicode/fullmatch` | `FullMatch` | 17.057 us | 19.666 us | Managed faster | 0.87x |
| `ascii-option/search-hit` | `Search` | 0.257 us | 0.305 us | Managed faster | 0.84x |
| `alternation/search-miss` | `Search` | 0.193 us | 0.237 us | Managed faster | 0.81x |
| `multiline/search-hit` | `Search` | 0.260 us | 0.335 us | Managed faster | 0.77x |
| `ignorecase/search-miss` | `Search` | 0.208 us | 0.288 us | Managed faster | 0.72x |
| `prefix/match` | `Match` | 0.191 us | 0.275 us | Managed faster | 0.70x |
| `lookahead/search-hit` | `Search` | 0.183 us | 0.301 us | Managed faster | 0.61x |
| `greedy/search-miss` | `Search` | 0.132 us | 0.221 us | Managed faster | 0.60x |
| `unicode-literal/search-hit` | `Search` | 0.138 us | 0.238 us | Managed faster | 0.58x |
| `possessive/search-miss` | `Search` | 0.121 us | 0.212 us | Managed faster | 0.57x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.160 us | 0.335 us | Managed faster | 0.48x |
| `unicode-literal/search-miss` | `Search` | 0.139 us | 0.292 us | Managed faster | 0.48x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.095 us | 0.257 us | Managed faster | 0.37x |
| `common/ip-match` | `IsMatch` | 0.072 us | 0.322 us | Managed faster | 0.22x |
| `literal/fullmatch` | `FullMatch` | 0.067 us | 0.350 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.705 us | 22.679 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.478 us | 22.407 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.677 us | 22.264 us | Managed faster | 0.08x |
| `lookahead/search-miss` | `Search` | 0.199 us | 0.196 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.326 us | 0.334 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.205 us | 0.238 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.180 us | 0.214 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.029 us | 1.358 us | Managed faster | 0.76x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 526.799 us | 214.846 us | CPython faster | 2.46x |
| `findall/many-capture-strings` | `FindAllStrings` | 817.094 us | 356.645 us | CPython faster | 2.29x |
| `findall/named-capture-strings` | `FindAllStrings` | 164.227 us | 94.951 us | CPython faster | 1.73x |
| `findall/empty-progression-strings` | `FindAllStrings` | 67.396 us | 43.284 us | CPython faster | 1.56x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 95.575 us | 62.025 us | CPython faster | 1.54x |
| `findall/full-strings` | `FindAllStrings` | 514.327 us | 368.810 us | CPython faster | 1.40x |
| `findall/optional-capture-strings` | `FindAllStrings` | 61.030 us | 55.331 us | CPython faster | 1.10x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.741 us | 16.258 us | CPython faster | 1.03x |
| `findall/full-utf8` | `FindAllUtf8` | 515.537 us | 519.866 us | Equivalent | 0.99x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 567.984 us | 622.801 us | Managed faster | 0.91x |
| `findall/structural-many` | `FindAllStructural` | 181.846 us | 201.866 us | Managed faster | 0.90x |
| `findall/unicode-full-strings` | `FindAllStrings` | 19.575 us | 28.179 us | Managed faster | 0.69x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 847.008 us | 1,251.370 us | Managed faster | 0.68x |
| `zero-width/count` | `Count` | 627.090 us | 1,111.685 us | Managed faster | 0.56x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 275.534 us | 542.082 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 181.447 us | 379.988 us | Managed faster | 0.48x |
| `count/from-offset` | `CountFromOffset` | 14.465 us | 49.869 us | Managed faster | 0.29x |
| `family/count` | `Count` | 545.509 us | 2,449.307 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 55.397 us | 429.131 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.241 us | 2.031 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 284.522 us | 2,600.464 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.873 us | 38.867 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.550 us | 90.747 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.398 us | 91.118 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.260 us | 140.499 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.128 us | 0.463 us | Inconclusive | 2.46x |
| `replacement/empty-progression-string` | `ReplaceString` | 35.921 us | 23.614 us | CPython faster | 1.53x |
| `replacement/named-template-string` | `ReplaceString` | 1.286 us | 0.904 us | CPython faster | 1.42x |
| `replacement/fixed-string` | `ReplaceString` | 183.431 us | 177.278 us | CPython faster | 1.04x |
| `replacement/subn-string` | `SubnString` | 91.361 us | 91.406 us | Equivalent | 1.00x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 173.965 us | 178.527 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 49.411 us | 50.938 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 86.961 us | 91.116 us | Managed faster | 0.95x |
| `replacement/no-match-string` | `ReplaceString` | 1.208 us | 2.420 us | Inconclusive | 0.50x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 160.633 us | 4,479.584 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 617.630 us | 17,198.469 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 614.409 us | 17,303.895 us | Managed faster | 0.04x |
| `replacement/one-template-string` | `ReplaceString` | 2.086 us | 16,473.727 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.576 us | 0.540 us | Inconclusive | 2.92x |
| `split/zero-width` | `SplitStrings` | 120.505 us | 66.903 us | CPython faster | 1.80x |
| `split/captures` | `SplitStrings` | 444.250 us | 254.092 us | CPython faster | 1.75x |
| `split/unicode` | `SplitStrings` | 65.744 us | 44.715 us | CPython faster | 1.47x |
| `split/no-captures` | `SplitStrings` | 245.673 us | 223.900 us | CPython faster | 1.10x |
| `split/optional-capture` | `SplitStrings` | 76.139 us | 82.380 us | Managed faster | 0.92x |
| `split/detailed` | `SplitDetailed` | 115.051 us | 136.059 us | Managed faster | 0.84x |
| `split/one-separator` | `SplitStrings` | 1.303 us | 12.034 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.370 us | 25.029 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 189.866 us | 401.597 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 76.542 us | 627.471 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 49.133 us | 579.466 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,089.424 us | 358,475.190 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.20 %/17.69 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.15 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.24 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/16.06 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 167.784 us / 67,601 B | 61.654 us | 145.466 us / 67,625 B | 61.721 us |
| `fallback` | 289.222 us / 79,761 B | 43.209 us | 222.012 us / 79,729 B | 43.677 us |
| `literal` | 234.809 us / 50,385 B | 26.318 us | 216.481 us / 50,353 B | 25.919 us |
| `unicode` | 91.763 us / 48,329 B | 37.227 us | 85.644 us / 48,297 B | 37.364 us |

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
