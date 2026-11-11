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

Snapshot SHA-256: `F007CCE3E179976F31BAEB7B5A46A14D59C1931A3ED27692BD8C94BD8E16AF3D`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `48` managed faster, `1` equivalent, `31` CPython faster, `5` inconclusive, `5` unqualified.

Measured source `2c5e341619f8`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.693 us | 0.235 us | CPython faster | 2.95x |
| `common/date-miss` | `IsMatch` | 1.888 us | 0.862 us | CPython faster | 2.19x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.388 us | 0.197 us | CPython faster | 1.97x |
| `leading-inline/search-hit` | `Search` | 0.580 us | 0.336 us | CPython faster | 1.73x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.507 us | 0.295 us | CPython faster | 1.72x |
| `atomic/search-miss` | `Search` | 0.438 us | 0.263 us | CPython faster | 1.67x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.284 us | 0.183 us | CPython faster | 1.55x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.418 us | 0.283 us | CPython faster | 1.48x |
| `lookbehind/search-miss` | `Search` | 0.311 us | 0.221 us | CPython faster | 1.40x |
| `possessive/search-hit` | `Search` | 0.431 us | 0.309 us | CPython faster | 1.40x |
| `reluctant/search-hit` | `Search` | 0.472 us | 0.352 us | CPython faster | 1.34x |
| `atomic/search-hit` | `Search` | 0.427 us | 0.325 us | CPython faster | 1.31x |
| `scoped-inline/search-hit` | `Search` | 0.316 us | 0.294 us | CPython faster | 1.07x |
| `multiline/search-miss` | `Search` | 0.277 us | 0.267 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 17.326 us | 19.699 us | Managed faster | 0.88x |
| `ignorecase/search-hit` | `Search` | 0.304 us | 0.358 us | Managed faster | 0.85x |
| `alternation/search-miss` | `Search` | 0.191 us | 0.237 us | Managed faster | 0.81x |
| `ascii-option/search-hit` | `Search` | 0.237 us | 0.306 us | Managed faster | 0.78x |
| `multiline/search-hit` | `Search` | 0.253 us | 0.338 us | Managed faster | 0.75x |
| `ignorecase/search-miss` | `Search` | 0.207 us | 0.289 us | Managed faster | 0.72x |
| `prefix/match` | `Match` | 0.191 us | 0.274 us | Managed faster | 0.70x |
| `lookahead/search-hit` | `Search` | 0.186 us | 0.301 us | Managed faster | 0.62x |
| `greedy/search-miss` | `Search` | 0.135 us | 0.221 us | Managed faster | 0.61x |
| `unicode-literal/search-hit` | `Search` | 0.143 us | 0.237 us | Managed faster | 0.60x |
| `possessive/search-miss` | `Search` | 0.122 us | 0.213 us | Managed faster | 0.57x |
| `unicode-literal/search-miss` | `Search` | 0.139 us | 0.292 us | Managed faster | 0.48x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.161 us | 0.346 us | Managed faster | 0.46x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.100 us | 0.257 us | Managed faster | 0.39x |
| `literal/fullmatch` | `FullMatch` | 0.068 us | 0.349 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.063 us | 0.324 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.805 us | 22.664 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.501 us | 22.663 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.677 us | 22.251 us | Managed faster | 0.08x |
| `lookahead/search-miss` | `Search` | 0.200 us | 0.197 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.312 us | 0.336 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.206 us | 0.240 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.177 us | 0.214 us | Unqualified | — |
| `unicode-option/search-hit` | `Search` | 0.304 us | 0.302 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.046 us | 1.317 us | Managed faster | 0.80x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 525.872 us | 214.574 us | CPython faster | 2.45x |
| `findall/many-capture-strings` | `FindAllStrings` | 800.508 us | 352.545 us | CPython faster | 2.27x |
| `findall/named-capture-strings` | `FindAllStrings` | 162.411 us | 95.160 us | CPython faster | 1.71x |
| `findall/empty-progression-strings` | `FindAllStrings` | 69.555 us | 43.478 us | CPython faster | 1.60x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 96.593 us | 62.444 us | CPython faster | 1.55x |
| `findall/full-strings` | `FindAllStrings` | 545.200 us | 373.496 us | CPython faster | 1.47x |
| `findall/full-utf8` | `FindAllUtf8` | 542.381 us | 520.245 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 57.679 us | 55.536 us | CPython faster | 1.04x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.533 us | 16.087 us | CPython faster | 1.03x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 557.423 us | 621.988 us | Managed faster | 0.90x |
| `findall/structural-many` | `FindAllStructural` | 179.644 us | 201.677 us | Managed faster | 0.89x |
| `findall/unicode-full-strings` | `FindAllStrings` | 19.377 us | 27.639 us | Managed faster | 0.70x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 829.962 us | 1,256.937 us | Managed faster | 0.66x |
| `zero-width/count` | `Count` | 581.275 us | 1,118.287 us | Managed faster | 0.52x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 281.030 us | 545.102 us | Managed faster | 0.52x |
| `count/empty-progression` | `Count` | 196.528 us | 381.598 us | Managed faster | 0.52x |
| `count/from-offset` | `CountFromOffset` | 13.926 us | 50.158 us | Managed faster | 0.28x |
| `family/count` | `Count` | 545.971 us | 2,464.601 us | Managed faster | 0.22x |
| `unicode/count` | `Count` | 56.813 us | 429.764 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.259 us | 2.022 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 281.336 us | 2,580.119 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.903 us | 38.891 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.583 us | 90.866 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.386 us | 94.052 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.258 us | 142.975 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.222 us | 0.639 us | Inconclusive | 1.91x |
| `replacement/empty-progression-string` | `ReplaceString` | 36.527 us | 23.745 us | CPython faster | 1.54x |
| `replacement/named-template-string` | `ReplaceString` | 1.309 us | 0.923 us | CPython faster | 1.42x |
| `replacement/fixed-string` | `ReplaceString` | 183.218 us | 177.227 us | CPython faster | 1.03x |
| `replacement/subn-string` | `SubnString` | 91.676 us | 90.917 us | Equivalent | 1.01x |
| `replacement/unmatched-template-string` | `ReplaceString` | 49.590 us | 50.981 us | Managed faster | 0.97x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 174.228 us | 179.525 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 87.244 us | 92.004 us | Managed faster | 0.95x |
| `replacement/no-match-string` | `ReplaceString` | 1.258 us | 2.421 us | Inconclusive | 0.52x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 157.744 us | 4,317.387 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 617.649 us | 17,324.242 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 596.632 us | 17,273.073 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.151 us | 16,365.985 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.657 us | 0.530 us | CPython faster | 3.04x |
| `split/zero-width` | `SplitStrings` | 115.854 us | 67.205 us | CPython faster | 1.73x |
| `split/captures` | `SplitStrings` | 426.227 us | 255.890 us | CPython faster | 1.67x |
| `split/unicode` | `SplitStrings` | 66.980 us | 45.064 us | CPython faster | 1.49x |
| `split/no-captures` | `SplitStrings` | 243.085 us | 224.708 us | CPython faster | 1.08x |
| `split/optional-capture` | `SplitStrings` | 73.461 us | 82.218 us | Managed faster | 0.89x |
| `split/detailed` | `SplitDetailed` | 110.656 us | 136.319 us | Managed faster | 0.81x |
| `split/one-separator` | `SplitStrings` | 1.387 us | 12.067 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.555 us | 25.041 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 186.493 us | 406.971 us | Managed faster | 0.46x |
| `corpus/code-declarations` | `Count` | 76.657 us | 629.358 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 49.264 us | 579.746 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 2,069.672 us | 358,852.774 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.20 %/17.33 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.48 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.24 %/14.30 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/15.89 % of the lanes.
- `unicode-option/search-hit`: Removable harness floors are 0.15 %/11.32 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 166.287 us / 67,601 B | 60.756 us | 145.378 us / 67,625 B | 61.296 us |
| `fallback` | 288.413 us / 79,761 B | 42.348 us | 222.431 us / 79,729 B | 42.316 us |
| `literal` | 235.684 us / 50,385 B | 24.871 us | 213.522 us / 50,353 B | 24.800 us |
| `unicode` | 90.744 us / 48,329 B | 36.312 us | 90.519 us / 48,297 B | 36.242 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
