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

Snapshot SHA-256: `AEB62E873250F2CD8659A48C8EE726B08D51AECBFC149915159477F0D61B895D`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `47` managed faster, `2` equivalent, `29` CPython faster, `6` inconclusive, `6` unqualified.

Measured source `25dff5982919`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.694 us | 0.236 us | CPython faster | 2.94x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.397 us | 0.198 us | CPython faster | 2.01x |
| `common/date-miss` | `IsMatch` | 1.702 us | 0.867 us | CPython faster | 1.96x |
| `atomic/search-miss` | `Search` | 0.454 us | 0.262 us | CPython faster | 1.73x |
| `leading-inline/search-hit` | `Search` | 0.533 us | 0.332 us | CPython faster | 1.60x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.470 us | 0.295 us | CPython faster | 1.59x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.279 us | 0.185 us | CPython faster | 1.50x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.417 us | 0.282 us | CPython faster | 1.48x |
| `possessive/search-hit` | `Search` | 0.446 us | 0.308 us | CPython faster | 1.45x |
| `lookbehind/search-miss` | `Search` | 0.313 us | 0.219 us | CPython faster | 1.43x |
| `atomic/search-hit` | `Search` | 0.442 us | 0.325 us | CPython faster | 1.36x |
| `reluctant/search-hit` | `Search` | 0.464 us | 0.352 us | CPython faster | 1.32x |
| `scoped-inline/search-hit` | `Search` | 0.337 us | 0.293 us | CPython faster | 1.15x |
| `unicode-option/search-hit` | `Search` | 0.340 us | 0.300 us | CPython faster | 1.14x |
| `lookahead/search-miss` | `Search` | 0.216 us | 0.198 us | CPython faster | 1.09x |
| `multiline/search-miss` | `Search` | 0.274 us | 0.266 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 17.592 us | 19.634 us | Managed faster | 0.90x |
| `multiline/search-hit` | `Search` | 0.255 us | 0.334 us | Managed faster | 0.76x |
| `ignorecase/search-miss` | `Search` | 0.216 us | 0.285 us | Managed faster | 0.76x |
| `prefix/match` | `Match` | 0.191 us | 0.274 us | Managed faster | 0.70x |
| `greedy/search-miss` | `Search` | 0.153 us | 0.218 us | Managed faster | 0.70x |
| `lookahead/search-hit` | `Search` | 0.205 us | 0.301 us | Managed faster | 0.68x |
| `possessive/search-miss` | `Search` | 0.139 us | 0.211 us | Managed faster | 0.66x |
| `unicode-literal/search-hit` | `Search` | 0.143 us | 0.238 us | Managed faster | 0.60x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.159 us | 0.331 us | Managed faster | 0.48x |
| `unicode-literal/search-miss` | `Search` | 0.138 us | 0.291 us | Managed faster | 0.47x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.097 us | 0.258 us | Managed faster | 0.38x |
| `common/ip-match` | `IsMatch` | 0.063 us | 0.326 us | Managed faster | 0.19x |
| `literal/fullmatch` | `FullMatch` | 0.067 us | 0.348 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.669 us | 22.662 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.456 us | 22.371 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.673 us | 22.277 us | Managed faster | 0.08x |
| `alternation/search-miss` | `Search` | 0.204 us | 0.237 us | Unqualified | — |
| `ascii-option/search-hit` | `Search` | 0.275 us | 0.309 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.316 us | 0.352 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.333 us | 0.335 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.204 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.191 us | 0.214 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.015 us | 1.334 us | Managed faster | 0.76x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 480.411 us | 214.343 us | CPython faster | 2.24x |
| `findall/many-capture-strings` | `FindAllStrings` | 770.902 us | 349.534 us | CPython faster | 2.21x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 94.301 us | 61.918 us | CPython faster | 1.53x |
| `findall/named-capture-strings` | `FindAllStrings` | 136.588 us | 93.298 us | CPython faster | 1.46x |
| `findall/empty-progression-strings` | `FindAllStrings` | 63.448 us | 43.524 us | CPython faster | 1.46x |
| `findall/full-strings` | `FindAllStrings` | 467.849 us | 370.337 us | CPython faster | 1.27x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.761 us | 16.267 us | CPython faster | 1.09x |
| `findall/full-utf8` | `FindAllUtf8` | 472.985 us | 520.104 us | Managed faster | 0.91x |
| `findall/optional-capture-strings` | `FindAllStrings` | 49.778 us | 55.287 us | Managed faster | 0.90x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 504.141 us | 617.159 us | Managed faster | 0.82x |
| `findall/structural-many` | `FindAllStructural` | 161.237 us | 202.130 us | Managed faster | 0.80x |
| `findall/unicode-full-strings` | `FindAllStrings` | 19.037 us | 27.512 us | Managed faster | 0.69x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 759.080 us | 1,247.349 us | Managed faster | 0.61x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 271.962 us | 539.102 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 178.694 us | 380.531 us | Managed faster | 0.47x |
| `zero-width/count` | `Count` | 506.680 us | 1,112.994 us | Managed faster | 0.46x |
| `count/from-offset` | `CountFromOffset` | 14.665 us | 49.524 us | Managed faster | 0.30x |
| `family/count` | `Count` | 560.416 us | 2,456.611 us | Managed faster | 0.23x |
| `unicode/count` | `Count` | 56.599 us | 436.739 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.242 us | 2.023 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 276.367 us | 2,597.379 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.871 us | 39.389 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.545 us | 90.130 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.423 us | 90.968 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.278 us | 143.876 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.149 us | 0.452 us | Inconclusive | 2.53x |
| `replacement/empty-progression-string` | `ReplaceString` | 33.606 us | 23.584 us | CPython faster | 1.43x |
| `replacement/named-template-string` | `ReplaceString` | 1.212 us | 0.917 us | Inconclusive | 1.32x |
| `replacement/fixed-string` | `ReplaceString` | 182.578 us | 175.963 us | CPython faster | 1.04x |
| `replacement/subn-string` | `SubnString` | 91.594 us | 91.303 us | Equivalent | 1.00x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 173.835 us | 180.449 us | Managed faster | 0.96x |
| `replacement/subn-utf8` | `SubnUtf8` | 86.678 us | 90.762 us | Managed faster | 0.96x |
| `replacement/unmatched-template-string` | `ReplaceString` | 39.902 us | 50.687 us | Managed faster | 0.79x |
| `replacement/no-match-string` | `ReplaceString` | 1.198 us | 2.417 us | Inconclusive | 0.49x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 148.803 us | 4,337.533 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 591.931 us | 17,374.967 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 577.381 us | 17,159.353 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.042 us | 16,521.946 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.644 us | 0.535 us | CPython faster | 2.97x |
| `split/zero-width` | `SplitStrings` | 102.992 us | 66.559 us | CPython faster | 1.55x |
| `split/captures` | `SplitStrings` | 385.099 us | 255.124 us | CPython faster | 1.51x |
| `split/unicode` | `SplitStrings` | 62.068 us | 44.374 us | CPython faster | 1.39x |
| `split/no-captures` | `SplitStrings` | 219.873 us | 223.068 us | Equivalent | 0.99x |
| `split/optional-capture` | `SplitStrings` | 66.842 us | 81.895 us | Managed faster | 0.82x |
| `split/detailed` | `SplitDetailed` | 103.717 us | 136.018 us | Managed faster | 0.76x |
| `split/one-separator` | `SplitStrings` | 1.347 us | 12.018 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.422 us | 24.984 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 181.704 us | 399.978 us | Managed faster | 0.45x |
| `corpus/code-declarations` | `Count` | 75.753 us | 632.199 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 48.991 us | 580.081 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,041.892 us | 358,693.130 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.30 % of the lanes.
- `ascii-option/search-hit`: Removable harness floors are 0.16 %/11.00 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.18 %/9.61 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.15 %/10.07 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.22 %/14.20 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.21 %/16.24 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 162.988 us / 67,601 B | 60.161 us | 144.838 us / 67,625 B | 60.814 us |
| `fallback` | 285.706 us / 79,761 B | 42.493 us | 217.147 us / 79,729 B | 42.007 us |
| `literal` | 238.213 us / 50,385 B | 25.266 us | 214.669 us / 50,353 B | 25.377 us |
| `unicode` | 93.016 us / 48,329 B | 36.583 us | 86.997 us / 48,297 B | 36.430 us |

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
