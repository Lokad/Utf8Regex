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

Snapshot SHA-256: `43E21DAC26383457B0D399AD2C486BA2DE2D1BCE8469955377F4AE801CDC89E4`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `45` managed faster, `2` equivalent, `29` CPython faster, `9` inconclusive, `5` unqualified.

Measured source `eae880c88f7a`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.676 us | 0.232 us | CPython faster | 2.93x |
| `common/date-miss` | `IsMatch` | 1.802 us | 0.855 us | CPython faster | 2.11x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.382 us | 0.192 us | CPython faster | 1.98x |
| `leading-inline/search-hit` | `Search` | 0.614 us | 0.332 us | CPython faster | 1.83x |
| `atomic/search-miss` | `Search` | 0.454 us | 0.258 us | CPython faster | 1.74x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.466 us | 0.288 us | CPython faster | 1.62x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.266 us | 0.181 us | CPython faster | 1.46x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.405 us | 0.280 us | CPython faster | 1.45x |
| `possessive/search-hit` | `Search` | 0.441 us | 0.308 us | CPython faster | 1.43x |
| `lookbehind/search-miss` | `Search` | 0.305 us | 0.215 us | CPython faster | 1.41x |
| `reluctant/search-hit` | `Search` | 0.474 us | 0.346 us | CPython faster | 1.37x |
| `atomic/search-hit` | `Search` | 0.433 us | 0.323 us | CPython faster | 1.34x |
| `scoped-inline/search-hit` | `Search` | 0.344 us | 0.289 us | CPython faster | 1.19x |
| `multiline/search-miss` | `Search` | 0.288 us | 0.259 us | CPython faster | 1.11x |
| `unicode-option/search-hit` | `Search` | 0.319 us | 0.294 us | CPython faster | 1.08x |
| `lookahead/search-miss` | `Search` | 0.204 us | 0.189 us | CPython faster | 1.07x |
| `unicode/fullmatch` | `FullMatch` | 17.550 us | 19.181 us | Managed faster | 0.92x |
| `ascii-option/search-hit` | `Search` | 0.258 us | 0.301 us | Managed faster | 0.86x |
| `multiline/search-hit` | `Search` | 0.265 us | 0.328 us | Managed faster | 0.81x |
| `ignorecase/search-miss` | `Search` | 0.208 us | 0.280 us | Managed faster | 0.74x |
| `lookahead/search-hit` | `Search` | 0.205 us | 0.300 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.147 us | 0.217 us | Managed faster | 0.68x |
| `prefix/match` | `Match` | 0.185 us | 0.282 us | Managed faster | 0.66x |
| `possessive/search-miss` | `Search` | 0.136 us | 0.210 us | Managed faster | 0.65x |
| `unicode-literal/search-hit` | `Search` | 0.135 us | 0.237 us | Managed faster | 0.58x |
| `unicode-literal/search-miss` | `Search` | 0.137 us | 0.289 us | Managed faster | 0.48x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.155 us | 0.328 us | Managed faster | 0.47x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.093 us | 0.255 us | Managed faster | 0.36x |
| `common/ip-match` | `IsMatch` | 0.063 us | 0.316 us | Managed faster | 0.20x |
| `literal/fullmatch` | `FullMatch` | 0.066 us | 0.337 us | Managed faster | 0.20x |
| `literal/search` | `Search` | 2.592 us | 21.925 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.368 us | 21.472 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.593 us | 21.074 us | Managed faster | 0.08x |
| `alternation/search-miss` | `Search` | 0.205 us | 0.238 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.316 us | 0.343 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.322 us | 0.329 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.201 us | 0.233 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.184 us | 0.209 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.043 us | 1.355 us | Managed faster | 0.77x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 499.578 us | 213.855 us | CPython faster | 2.34x |
| `findall/many-capture-strings` | `FindAllStrings` | 785.660 us | 351.918 us | CPython faster | 2.23x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 97.581 us | 62.669 us | CPython faster | 1.55x |
| `findall/named-capture-strings` | `FindAllStrings` | 144.269 us | 94.504 us | CPython faster | 1.53x |
| `findall/empty-progression-strings` | `FindAllStrings` | 61.204 us | 42.084 us | CPython faster | 1.47x |
| `findall/full-strings` | `FindAllStrings` | 464.359 us | 362.890 us | CPython faster | 1.27x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.686 us | 16.026 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 53.295 us | 55.369 us | Managed faster | 0.96x |
| `findall/full-utf8` | `FindAllUtf8` | 473.351 us | 513.135 us | Managed faster | 0.92x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 532.103 us | 617.066 us | Managed faster | 0.86x |
| `findall/structural-many` | `FindAllStructural` | 157.698 us | 198.240 us | Managed faster | 0.80x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.499 us | 27.447 us | Managed faster | 0.68x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 793.606 us | 1,237.795 us | Managed faster | 0.64x |
| `zero-width/count` | `Count` | 555.236 us | 1,099.389 us | Managed faster | 0.51x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 260.150 us | 544.093 us | Managed faster | 0.48x |
| `count/empty-progression` | `Count` | 174.407 us | 370.034 us | Managed faster | 0.47x |
| `count/from-offset` | `CountFromOffset` | 14.686 us | 49.197 us | Managed faster | 0.30x |
| `family/count` | `Count` | 540.247 us | 2,474.006 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 55.552 us | 425.622 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.250 us | 1.974 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 280.586 us | 2,561.474 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.837 us | 38.133 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.551 us | 88.672 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.414 us | 89.453 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.260 us | 139.753 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.359 us | 0.444 us | Inconclusive | 3.04x |
| `replacement/empty-progression-string` | `ReplaceString` | 33.478 us | 23.215 us | CPython faster | 1.44x |
| `replacement/named-template-string` | `ReplaceString` | 1.190 us | 0.904 us | Inconclusive | 1.33x |
| `replacement/fixed-string` | `ReplaceString` | 182.129 us | 176.681 us | CPython faster | 1.03x |
| `replacement/subn-string` | `SubnString` | 91.544 us | 91.190 us | Equivalent | 1.00x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 169.667 us | 176.723 us | Managed faster | 0.96x |
| `replacement/subn-utf8` | `SubnUtf8` | 84.673 us | 89.628 us | Managed faster | 0.95x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.753 us | 49.710 us | Managed faster | 0.82x |
| `replacement/no-match-string` | `ReplaceString` | 1.407 us | 2.343 us | Inconclusive | 0.60x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 149.196 us | 4,296.301 us | Inconclusive | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 585.884 us | 16,917.998 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 581.615 us | 16,973.459 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.294 us | 16,005.552 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.791 us | 0.525 us | CPython faster | 3.45x |
| `split/zero-width` | `SplitStrings` | 107.087 us | 65.023 us | CPython faster | 1.65x |
| `split/captures` | `SplitStrings` | 388.132 us | 250.276 us | CPython faster | 1.55x |
| `split/unicode` | `SplitStrings` | 60.438 us | 42.972 us | CPython faster | 1.40x |
| `split/no-captures` | `SplitStrings` | 216.944 us | 217.670 us | Equivalent | 0.99x |
| `split/detailed` | `SplitDetailed` | 114.411 us | 134.340 us | Inconclusive | 0.85x |
| `split/optional-capture` | `SplitStrings` | 67.496 us | 80.868 us | Inconclusive | 0.83x |
| `split/one-separator` | `SplitStrings` | 1.363 us | 11.824 us | Inconclusive | 0.12x |
| `split/no-separator` | `SplitStrings` | 1.499 us | 24.443 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 179.280 us | 397.300 us | Managed faster | 0.45x |
| `corpus/code-declarations` | `Count` | 73.730 us | 613.083 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 47.602 us | 569.126 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,046.532 us | 354,470.857 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.40 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.81 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.36 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.23 %/14.57 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/16.08 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 189.356 us / 67,601 B | 59.937 us | 174.647 us / 67,625 B | 60.123 us |
| `fallback` | 288.734 us / 79,761 B | 40.772 us | 258.103 us / 79,729 B | 42.352 us |
| `literal` | 292.087 us / 50,385 B | 23.593 us | 321.247 us / 50,353 B | 24.545 us |
| `unicode` | 55.528 us / 48,329 B | 36.253 us | 59.466 us / 48,297 B | 36.158 us |

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
