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

Snapshot SHA-256: `A2CCC28E3A1BF7AA855C80988518182F779588D6070843BCAFE98B5E25F3F4F1`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `46` managed faster, `2` equivalent, `29` CPython faster, `7` inconclusive, `6` unqualified.

Measured source `b1867c72547a`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.695 us | 0.238 us | CPython faster | 2.93x |
| `common/date-miss` | `IsMatch` | 1.815 us | 0.854 us | CPython faster | 2.13x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.392 us | 0.198 us | CPython faster | 1.99x |
| `atomic/search-miss` | `Search` | 0.465 us | 0.263 us | CPython faster | 1.77x |
| `leading-inline/search-hit` | `Search` | 0.578 us | 0.334 us | CPython faster | 1.73x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.495 us | 0.296 us | CPython faster | 1.67x |
| `possessive/search-hit` | `Search` | 0.462 us | 0.309 us | CPython faster | 1.48x |
| `lookbehind/search-miss` | `Search` | 0.320 us | 0.220 us | CPython faster | 1.46x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.411 us | 0.283 us | CPython faster | 1.45x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.266 us | 0.184 us | CPython faster | 1.44x |
| `reluctant/search-hit` | `Search` | 0.506 us | 0.354 us | CPython faster | 1.43x |
| `atomic/search-hit` | `Search` | 0.458 us | 0.324 us | CPython faster | 1.41x |
| `scoped-inline/search-hit` | `Search` | 0.350 us | 0.293 us | CPython faster | 1.19x |
| `multiline/search-miss` | `Search` | 0.293 us | 0.264 us | CPython faster | 1.11x |
| `unicode-option/search-hit` | `Search` | 0.325 us | 0.301 us | CPython faster | 1.08x |
| `lookahead/search-miss` | `Search` | 0.207 us | 0.198 us | CPython faster | 1.05x |
| `unicode/fullmatch` | `FullMatch` | 17.155 us | 19.628 us | Managed faster | 0.87x |
| `multiline/search-hit` | `Search` | 0.271 us | 0.335 us | Managed faster | 0.81x |
| `ignorecase/search-miss` | `Search` | 0.214 us | 0.288 us | Managed faster | 0.74x |
| `prefix/match` | `Match` | 0.191 us | 0.274 us | Managed faster | 0.70x |
| `lookahead/search-hit` | `Search` | 0.206 us | 0.301 us | Managed faster | 0.68x |
| `greedy/search-miss` | `Search` | 0.148 us | 0.220 us | Managed faster | 0.68x |
| `possessive/search-miss` | `Search` | 0.134 us | 0.212 us | Managed faster | 0.63x |
| `unicode-literal/search-hit` | `Search` | 0.147 us | 0.238 us | Managed faster | 0.62x |
| `unicode-literal/search-miss` | `Search` | 0.143 us | 0.292 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.159 us | 0.333 us | Managed faster | 0.48x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.094 us | 0.258 us | Managed faster | 0.37x |
| `literal/fullmatch` | `FullMatch` | 0.067 us | 0.347 us | Managed faster | 0.19x |
| `common/ip-match` | `IsMatch` | 0.063 us | 0.328 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.712 us | 22.595 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.459 us | 22.296 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.671 us | 22.264 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.204 us | 0.237 us | Unqualified | — |
| `ascii-option/search-hit` | `Search` | 0.268 us | 0.305 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.329 us | 0.357 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.331 us | 0.332 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.206 us | 0.238 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.193 us | 0.216 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.930 us | 1.336 us | Inconclusive | 0.70x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 662.873 us | 350.224 us | CPython faster | 1.90x |
| `findall/one-capture-strings` | `FindAllStrings` | 368.106 us | 215.143 us | CPython faster | 1.71x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 95.722 us | 62.022 us | CPython faster | 1.54x |
| `findall/empty-progression-strings` | `FindAllStrings` | 64.074 us | 42.922 us | CPython faster | 1.50x |
| `findall/full-strings` | `FindAllStrings` | 505.734 us | 368.651 us | CPython faster | 1.37x |
| `findall/named-capture-strings` | `FindAllStrings` | 122.446 us | 94.435 us | CPython faster | 1.30x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.429 us | 15.838 us | CPython faster | 1.04x |
| `findall/full-utf8` | `FindAllUtf8` | 510.065 us | 517.326 us | Equivalent | 0.99x |
| `findall/optional-capture-strings` | `FindAllStrings` | 51.233 us | 55.384 us | Managed faster | 0.92x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 538.187 us | 620.441 us | Managed faster | 0.87x |
| `findall/unicode-full-strings` | `FindAllStrings` | 19.567 us | 27.612 us | Managed faster | 0.71x |
| `findall/structural-many` | `FindAllStructural` | 133.613 us | 201.136 us | Managed faster | 0.66x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 659.377 us | 1,240.475 us | Managed faster | 0.53x |
| `count/empty-progression` | `Count` | 178.305 us | 379.586 us | Managed faster | 0.47x |
| `zero-width/count` | `Count` | 513.541 us | 1,115.098 us | Managed faster | 0.46x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 236.913 us | 543.232 us | Managed faster | 0.44x |
| `count/from-offset` | `CountFromOffset` | 14.039 us | 49.417 us | Managed faster | 0.28x |
| `family/count` | `Count` | 541.514 us | 2,459.655 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 57.570 us | 436.690 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.251 us | 2.017 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 272.048 us | 2,586.795 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.861 us | 38.080 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.589 us | 89.730 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.414 us | 91.099 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.281 us | 142.015 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.103 us | 0.451 us | Inconclusive | 2.45x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.018 us | 23.451 us | CPython faster | 1.45x |
| `replacement/named-template-string` | `ReplaceString` | 1.123 us | 0.908 us | CPython faster | 1.24x |
| `replacement/fixed-string` | `ReplaceString` | 183.318 us | 176.482 us | CPython faster | 1.04x |
| `replacement/subn-string` | `SubnString` | 91.446 us | 91.040 us | Equivalent | 1.01x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 173.849 us | 179.245 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 87.817 us | 91.047 us | Managed faster | 0.96x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.393 us | 50.665 us | Managed faster | 0.80x |
| `replacement/no-match-string` | `ReplaceString` | 1.162 us | 2.372 us | Inconclusive | 0.50x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 138.943 us | 4,302.385 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 545.374 us | 17,141.889 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 535.131 us | 17,281.731 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.992 us | 16,490.468 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.575 us | 0.528 us | Inconclusive | 2.93x |
| `split/zero-width` | `SplitStrings` | 105.847 us | 66.215 us | CPython faster | 1.60x |
| `split/captures` | `SplitStrings` | 394.168 us | 253.638 us | CPython faster | 1.55x |
| `split/unicode` | `SplitStrings` | 62.898 us | 44.794 us | CPython faster | 1.40x |
| `split/no-captures` | `SplitStrings` | 218.666 us | 223.906 us | Managed faster | 0.98x |
| `split/optional-capture` | `SplitStrings` | 72.528 us | 81.781 us | Managed faster | 0.88x |
| `split/detailed` | `SplitDetailed` | 104.013 us | 134.309 us | Managed faster | 0.78x |
| `split/one-separator` | `SplitStrings` | 1.316 us | 11.982 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.413 us | 25.147 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 186.289 us | 395.358 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 76.674 us | 626.617 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 48.369 us | 573.364 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,892.864 us | 353,920.810 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.23 % of the lanes.
- `ascii-option/search-hit`: Removable harness floors are 0.16 %/11.17 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.14 %/9.60 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.35 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.18 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.21 %/16.11 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 145.506 us / 67,601 B | 60.878 us | 148.256 us / 67,625 B | 61.947 us |
| `fallback` | 304.028 us / 79,761 B | 43.480 us | 197.791 us / 79,729 B | 43.400 us |
| `literal` | 246.488 us / 50,385 B | 26.081 us | 221.278 us / 50,353 B | 25.334 us |
| `unicode` | 96.616 us / 48,329 B | 38.712 us | 90.019 us / 48,297 B | 37.027 us |

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
