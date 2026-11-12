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

Snapshot SHA-256: `DFE493C533A01667EB4A24820F1506D32584A4F04D3F990EF6A9D0AA87CF611B`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `48` managed faster, `1` equivalent, `29` CPython faster, `7` inconclusive, `5` unqualified.

Measured source `ccf5210173b1`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.693 us | 0.234 us | CPython faster | 2.96x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.389 us | 0.198 us | CPython faster | 1.97x |
| `common/date-miss` | `IsMatch` | 1.670 us | 0.857 us | CPython faster | 1.95x |
| `atomic/search-miss` | `Search` | 0.440 us | 0.260 us | CPython faster | 1.69x |
| `leading-inline/search-hit` | `Search` | 0.559 us | 0.336 us | CPython faster | 1.67x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.492 us | 0.295 us | CPython faster | 1.67x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.427 us | 0.283 us | CPython faster | 1.51x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.270 us | 0.183 us | CPython faster | 1.48x |
| `possessive/search-hit` | `Search` | 0.452 us | 0.310 us | CPython faster | 1.46x |
| `lookbehind/search-miss` | `Search` | 0.303 us | 0.220 us | CPython faster | 1.37x |
| `atomic/search-hit` | `Search` | 0.439 us | 0.323 us | CPython faster | 1.36x |
| `reluctant/search-hit` | `Search` | 0.475 us | 0.352 us | CPython faster | 1.35x |
| `scoped-inline/search-hit` | `Search` | 0.344 us | 0.292 us | CPython faster | 1.18x |
| `lookahead/search-miss` | `Search` | 0.212 us | 0.197 us | CPython faster | 1.08x |
| `multiline/search-miss` | `Search` | 0.288 us | 0.268 us | CPython faster | 1.07x |
| `unicode-option/search-hit` | `Search` | 0.320 us | 0.300 us | CPython faster | 1.07x |
| `unicode/fullmatch` | `FullMatch` | 17.321 us | 19.639 us | Managed faster | 0.88x |
| `ascii-option/search-hit` | `Search` | 0.258 us | 0.306 us | Managed faster | 0.84x |
| `multiline/search-hit` | `Search` | 0.266 us | 0.336 us | Managed faster | 0.79x |
| `ignorecase/search-miss` | `Search` | 0.223 us | 0.287 us | Managed faster | 0.78x |
| `prefix/match` | `Match` | 0.192 us | 0.274 us | Managed faster | 0.70x |
| `lookahead/search-hit` | `Search` | 0.211 us | 0.303 us | Managed faster | 0.70x |
| `greedy/search-miss` | `Search` | 0.152 us | 0.219 us | Managed faster | 0.69x |
| `possessive/search-miss` | `Search` | 0.137 us | 0.211 us | Managed faster | 0.65x |
| `unicode-literal/search-hit` | `Search` | 0.148 us | 0.237 us | Managed faster | 0.62x |
| `unicode-literal/search-miss` | `Search` | 0.137 us | 0.291 us | Managed faster | 0.47x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.158 us | 0.336 us | Managed faster | 0.47x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.099 us | 0.257 us | Managed faster | 0.39x |
| `literal/fullmatch` | `FullMatch` | 0.067 us | 0.349 us | Managed faster | 0.19x |
| `common/ip-match` | `IsMatch` | 0.057 us | 0.322 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.720 us | 22.602 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.441 us | 22.423 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.676 us | 22.285 us | Managed faster | 0.08x |
| `alternation/search-miss` | `Search` | 0.208 us | 0.237 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.328 us | 0.357 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.331 us | 0.333 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.213 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.192 us | 0.214 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.031 us | 1.307 us | Managed faster | 0.79x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 749.085 us | 349.926 us | CPython faster | 2.14x |
| `findall/one-capture-strings` | `FindAllStrings` | 359.970 us | 213.798 us | CPython faster | 1.68x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 96.588 us | 62.342 us | CPython faster | 1.55x |
| `findall/empty-progression-strings` | `FindAllStrings` | 61.257 us | 43.914 us | CPython faster | 1.40x |
| `findall/full-strings` | `FindAllStrings` | 488.448 us | 369.496 us | CPython faster | 1.32x |
| `findall/named-capture-strings` | `FindAllStrings` | 109.811 us | 94.865 us | CPython faster | 1.16x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.114 us | 16.054 us | CPython faster | 1.07x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.303 us | 55.406 us | Managed faster | 0.93x |
| `findall/full-utf8` | `FindAllUtf8` | 473.733 us | 517.727 us | Managed faster | 0.92x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 533.048 us | 618.105 us | Managed faster | 0.86x |
| `findall/structural-many` | `FindAllStructural` | 162.747 us | 202.056 us | Managed faster | 0.81x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.552 us | 27.309 us | Managed faster | 0.68x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 782.120 us | 1,266.239 us | Managed faster | 0.62x |
| `count/empty-progression` | `Count` | 193.278 us | 380.634 us | Managed faster | 0.51x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 265.740 us | 547.112 us | Managed faster | 0.49x |
| `zero-width/count` | `Count` | 503.658 us | 1,116.004 us | Managed faster | 0.45x |
| `count/from-offset` | `CountFromOffset` | 14.858 us | 49.595 us | Managed faster | 0.30x |
| `family/count` | `Count` | 545.539 us | 2,461.008 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 56.469 us | 432.984 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.244 us | 2.031 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 269.232 us | 2,569.862 us | Managed faster | 0.10x |
| `findall/one-strings` | `FindAllStrings` | 0.855 us | 38.897 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.575 us | 90.526 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.420 us | 91.241 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.281 us | 140.303 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.500 us | 0.459 us | Inconclusive | 3.31x |
| `replacement/empty-progression-string` | `ReplaceString` | 32.751 us | 23.680 us | CPython faster | 1.39x |
| `replacement/named-template-string` | `ReplaceString` | 1.068 us | 0.932 us | CPython faster | 1.14x |
| `replacement/fixed-string` | `ReplaceString` | 183.644 us | 178.675 us | CPython faster | 1.03x |
| `replacement/subn-string` | `SubnString` | 91.783 us | 90.891 us | Equivalent | 1.01x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 174.042 us | 178.772 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 87.519 us | 92.153 us | Managed faster | 0.95x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.784 us | 50.721 us | Managed faster | 0.80x |
| `replacement/no-match-string` | `ReplaceString` | 1.497 us | 2.413 us | Inconclusive | 0.62x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 129.488 us | 4,299.575 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 509.028 us | 17,331.552 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 494.822 us | 17,230.272 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.604 us | 16,325.846 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.640 us | 0.540 us | Inconclusive | 3.07x |
| `split/zero-width` | `SplitStrings` | 100.367 us | 65.556 us | CPython faster | 1.53x |
| `split/captures` | `SplitStrings` | 390.930 us | 255.463 us | CPython faster | 1.53x |
| `split/unicode` | `SplitStrings` | 61.237 us | 45.034 us | CPython faster | 1.36x |
| `split/no-captures` | `SplitStrings` | 220.176 us | 224.651 us | Inconclusive | 0.98x |
| `split/optional-capture` | `SplitStrings` | 67.219 us | 83.092 us | Managed faster | 0.81x |
| `split/detailed` | `SplitDetailed` | 102.649 us | 136.168 us | Managed faster | 0.76x |
| `split/one-separator` | `SplitStrings` | 1.326 us | 11.961 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.604 us | 24.900 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 183.892 us | 400.811 us | Managed faster | 0.46x |
| `corpus/code-declarations` | `Count` | 76.460 us | 627.504 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 47.682 us | 576.174 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,054.056 us | 358,332.090 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.19 %/14.29 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.76 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.14 %/10.27 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.21 %/14.33 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.21 %/15.91 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 141.416 us / 67,601 B | 60.371 us | 136.753 us / 67,625 B | 61.115 us |
| `fallback` | 265.816 us / 79,761 B | 43.011 us | 194.863 us / 79,729 B | 42.728 us |
| `literal` | 231.169 us / 50,385 B | 25.414 us | 207.200 us / 50,353 B | 25.226 us |
| `unicode` | 91.094 us / 48,329 B | 38.224 us | 85.487 us / 48,297 B | 37.333 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
