# Lokad.Utf8Regex.PythonRe

**0.5.0 development**, .NET 10 and .NET 11. A managed Python `re`-style adapter
over strict UTF-8 Unicode scalars, requiring the exact Core package version
matching the adapter. It can coexist with PCRE2 at the same version. No Python
runtime or BCL regex matcher is a shipped implementation dependency.

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

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `6301861507EEAA599C5ABD4CF60C9B19DF2BA56D85B1062ACC92E4F9AB9A24EB`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `45` managed faster, `2` equivalent, `33` CPython faster, `6` inconclusive, `4` unqualified.

Measured source `03f6839ec23c`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.680 us | 0.234 us | CPython faster | 2.89x |
| `common/date-miss` | `IsMatch` | 1.780 us | 0.868 us | CPython faster | 2.05x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.357 us | 0.198 us | CPython faster | 1.80x |
| `leading-inline/search-hit` | `Search` | 0.593 us | 0.336 us | CPython faster | 1.76x |
| `atomic/search-miss` | `Search` | 0.441 us | 0.262 us | CPython faster | 1.68x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.484 us | 0.296 us | CPython faster | 1.64x |
| `possessive/search-hit` | `Search` | 0.434 us | 0.309 us | CPython faster | 1.40x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.387 us | 0.283 us | CPython faster | 1.37x |
| `lookbehind/search-miss` | `Search` | 0.299 us | 0.220 us | CPython faster | 1.37x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.250 us | 0.185 us | CPython faster | 1.35x |
| `atomic/search-hit` | `Search` | 0.427 us | 0.325 us | CPython faster | 1.31x |
| `reluctant/search-hit` | `Search` | 0.460 us | 0.353 us | CPython faster | 1.30x |
| `prefix/match` | `Match` | 0.320 us | 0.275 us | CPython faster | 1.16x |
| `scoped-inline/search-hit` | `Search` | 0.322 us | 0.294 us | CPython faster | 1.10x |
| `unicode-option/search-hit` | `Search` | 0.310 us | 0.300 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.588 us | 19.704 us | Managed faster | 0.90x |
| `ignorecase/search-hit` | `Search` | 0.308 us | 0.358 us | Managed faster | 0.86x |
| `alternation/search-miss` | `Search` | 0.195 us | 0.239 us | Managed faster | 0.82x |
| `unicode-category/search-miss` | `Search` | 0.170 us | 0.213 us | Managed faster | 0.79x |
| `ascii-option/search-hit` | `Search` | 0.239 us | 0.306 us | Managed faster | 0.78x |
| `multiline/search-hit` | `Search` | 0.248 us | 0.337 us | Managed faster | 0.74x |
| `ignorecase/search-miss` | `Search` | 0.201 us | 0.288 us | Managed faster | 0.70x |
| `unicode-literal/search-hit` | `Search` | 0.145 us | 0.236 us | Managed faster | 0.61x |
| `greedy/search-miss` | `Search` | 0.131 us | 0.220 us | Managed faster | 0.59x |
| `lookahead/search-hit` | `Search` | 0.179 us | 0.302 us | Managed faster | 0.59x |
| `possessive/search-miss` | `Search` | 0.114 us | 0.214 us | Managed faster | 0.53x |
| `unicode-literal/search-miss` | `Search` | 0.140 us | 0.292 us | Managed faster | 0.48x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.144 us | 0.337 us | Managed faster | 0.43x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.087 us | 0.258 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.069 us | 0.352 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.059 us | 0.319 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.727 us | 22.721 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.489 us | 22.423 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.673 us | 22.295 us | Managed faster | 0.07x |
| `lookahead/search-miss` | `Search` | 0.191 us | 0.198 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.322 us | 0.334 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.263 us | 0.267 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.202 us | 0.238 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.885 us | 1.307 us | Managed faster | 0.68x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 911.139 us | 351.451 us | CPython faster | 2.59x |
| `findall/one-capture-strings` | `FindAllStrings` | 447.848 us | 214.294 us | CPython faster | 2.09x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 104.979 us | 62.395 us | CPython faster | 1.68x |
| `findall/named-capture-strings` | `FindAllStrings` | 156.518 us | 94.699 us | CPython faster | 1.66x |
| `findall/empty-progression-strings` | `FindAllStrings` | 62.491 us | 43.707 us | CPython faster | 1.43x |
| `findall/unicode-full-strings` | `FindAllStrings` | 39.113 us | 27.370 us | CPython faster | 1.43x |
| `findall/full-strings` | `FindAllStrings` | 516.678 us | 369.449 us | CPython faster | 1.40x |
| `findall/full-utf8` | `FindAllUtf8` | 535.421 us | 519.326 us | CPython faster | 1.03x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.405 us | 16.130 us | Equivalent | 1.02x |
| `findall/optional-capture-strings` | `FindAllStrings` | 54.905 us | 55.383 us | Equivalent | 0.99x |
| `findall/structural-many` | `FindAllStructural` | 190.857 us | 201.555 us | Managed faster | 0.95x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 539.645 us | 620.124 us | Managed faster | 0.87x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 938.261 us | 1,248.881 us | Managed faster | 0.75x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 294.649 us | 547.741 us | Managed faster | 0.54x |
| `zero-width/count` | `Count` | 539.840 us | 1,111.428 us | Managed faster | 0.49x |
| `count/empty-progression` | `Count` | 157.833 us | 381.365 us | Managed faster | 0.41x |
| `count/from-offset` | `CountFromOffset` | 14.742 us | 50.006 us | Managed faster | 0.29x |
| `family/count` | `Count` | 644.944 us | 2,458.447 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.276 us | 2.042 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 57.268 us | 430.089 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 284.204 us | 2,570.048 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.730 us | 38.977 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.525 us | 91.253 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.502 us | 91.363 us | Managed faster | 0.01x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.251 us | 142.661 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.129 us | 0.459 us | Inconclusive | 2.46x |
| `replacement/empty-progression-string` | `ReplaceString` | 32.817 us | 23.698 us | CPython faster | 1.39x |
| `replacement/named-template-string` | `ReplaceString` | 1.233 us | 0.932 us | CPython faster | 1.32x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 212.692 us | 178.923 us | CPython faster | 1.19x |
| `replacement/subn-utf8` | `SubnUtf8` | 105.473 us | 91.017 us | CPython faster | 1.16x |
| `replacement/fixed-string` | `ReplaceString` | 187.479 us | 178.158 us | CPython faster | 1.05x |
| `replacement/subn-string` | `SubnString` | 93.908 us | 90.647 us | CPython faster | 1.04x |
| `replacement/unmatched-template-string` | `ReplaceString` | 47.111 us | 50.862 us | Managed faster | 0.93x |
| `replacement/no-match-string` | `ReplaceString` | 1.257 us | 2.421 us | Inconclusive | 0.52x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 154.179 us | 4,325.097 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 605.675 us | 17,340.975 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 594.854 us | 17,468.247 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.112 us | 16,519.523 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.427 us | 0.536 us | Inconclusive | 2.60x |
| `split/captures` | `SplitStrings` | 419.211 us | 254.800 us | CPython faster | 1.65x |
| `split/zero-width` | `SplitStrings` | 108.343 us | 66.697 us | CPython faster | 1.63x |
| `split/unicode` | `SplitStrings` | 70.862 us | 45.101 us | CPython faster | 1.57x |
| `split/no-captures` | `SplitStrings` | 274.952 us | 224.624 us | CPython faster | 1.22x |
| `split/optional-capture` | `SplitStrings` | 71.385 us | 81.895 us | Managed faster | 0.87x |
| `split/detailed` | `SplitDetailed` | 108.400 us | 136.133 us | Managed faster | 0.80x |
| `split/one-separator` | `SplitStrings` | 1.177 us | 12.052 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.246 us | 25.009 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 182.850 us | 402.088 us | Managed faster | 0.45x |
| `corpus/code-declarations` | `Count` | 86.499 us | 630.635 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 48.733 us | 582.256 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,061.300 us | 359,142.658 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.21 %/17.22 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.27 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.20 %/12.81 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.26 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 199.922 us / 71,617 B | 60.686 us | 184.494 us / 71,641 B | 60.802 us |
| `fallback` | 312.425 us / 83,561 B | 42.397 us | 267.941 us / 83,529 B | 42.086 us |
| `literal` | 314.028 us / 50,369 B | 25.116 us | 314.691 us / 50,337 B | 25.162 us |
| `unicode` | 55.472 us / 48,313 B | 36.398 us | 58.834 us / 48,281 B | 36.602 us |

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
