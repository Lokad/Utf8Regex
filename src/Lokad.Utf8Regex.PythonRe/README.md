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

Snapshot SHA-256: `6123F557D5F9A3DCC1CB437D926F919CBAD8C9FA02916CFF6E03B71A261F0A38`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `42` managed faster, `2` equivalent, `32` CPython faster, `9` inconclusive, `5` unqualified.

Measured source `5519a6da9ac4`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.678 us | 0.235 us | CPython faster | 2.89x |
| `common/date-miss` | `IsMatch` | 1.812 us | 0.872 us | CPython faster | 2.08x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.377 us | 0.199 us | CPython faster | 1.89x |
| `leading-inline/search-hit` | `Search` | 0.605 us | 0.337 us | CPython faster | 1.79x |
| `atomic/search-miss` | `Search` | 0.459 us | 0.262 us | CPython faster | 1.75x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.501 us | 0.294 us | CPython faster | 1.70x |
| `possessive/search-hit` | `Search` | 0.455 us | 0.310 us | CPython faster | 1.47x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.401 us | 0.283 us | CPython faster | 1.42x |
| `lookbehind/search-miss` | `Search` | 0.312 us | 0.221 us | CPython faster | 1.41x |
| `reluctant/search-hit` | `Search` | 0.488 us | 0.352 us | CPython faster | 1.39x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.252 us | 0.183 us | CPython faster | 1.37x |
| `atomic/search-hit` | `Search` | 0.431 us | 0.325 us | CPython faster | 1.32x |
| `prefix/match` | `Match` | 0.317 us | 0.274 us | CPython faster | 1.16x |
| `scoped-inline/search-hit` | `Search` | 0.325 us | 0.293 us | CPython faster | 1.11x |
| `lookahead/search-miss` | `Search` | 0.202 us | 0.197 us | CPython faster | 1.03x |
| `multiline/search-miss` | `Search` | 0.275 us | 0.267 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.297 us | 19.788 us | Managed faster | 0.87x |
| `ignorecase/search-hit` | `Search` | 0.307 us | 0.355 us | Managed faster | 0.86x |
| `ascii-option/search-hit` | `Search` | 0.244 us | 0.307 us | Managed faster | 0.79x |
| `multiline/search-hit` | `Search` | 0.250 us | 0.336 us | Managed faster | 0.74x |
| `ignorecase/search-miss` | `Search` | 0.205 us | 0.288 us | Managed faster | 0.71x |
| `lookahead/search-hit` | `Search` | 0.189 us | 0.302 us | Managed faster | 0.63x |
| `greedy/search-miss` | `Search` | 0.134 us | 0.220 us | Managed faster | 0.61x |
| `unicode-literal/search-hit` | `Search` | 0.140 us | 0.238 us | Managed faster | 0.59x |
| `possessive/search-miss` | `Search` | 0.121 us | 0.213 us | Managed faster | 0.57x |
| `unicode-literal/search-miss` | `Search` | 0.145 us | 0.292 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.145 us | 0.335 us | Managed faster | 0.43x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.088 us | 0.258 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.070 us | 0.349 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.060 us | 0.322 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.674 us | 22.737 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.471 us | 22.416 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.668 us | 22.289 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.209 us | 0.238 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.325 us | 0.333 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.202 us | 0.238 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.182 us | 0.215 us | Unqualified | — |
| `unicode-option/search-hit` | `Search` | 0.306 us | 0.300 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.052 us | 1.333 us | Managed faster | 0.79x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 475.160 us | 213.807 us | CPython faster | 2.22x |
| `findall/many-capture-strings` | `FindAllStrings` | 776.555 us | 350.824 us | CPython faster | 2.21x |
| `findall/named-capture-strings` | `FindAllStrings` | 164.855 us | 95.264 us | CPython faster | 1.73x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 102.539 us | 62.255 us | CPython faster | 1.65x |
| `findall/empty-progression-strings` | `FindAllStrings` | 66.843 us | 43.926 us | CPython faster | 1.52x |
| `findall/full-strings` | `FindAllStrings` | 515.677 us | 368.045 us | CPython faster | 1.40x |
| `findall/unicode-full-strings` | `FindAllStrings` | 37.304 us | 27.604 us | CPython faster | 1.35x |
| `findall/optional-capture-strings` | `FindAllStrings` | 60.136 us | 55.591 us | CPython faster | 1.08x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.834 us | 16.394 us | CPython faster | 1.03x |
| `findall/full-utf8` | `FindAllUtf8` | 524.012 us | 519.864 us | Equivalent | 1.01x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 549.376 us | 618.727 us | Managed faster | 0.89x |
| `findall/structural-many` | `FindAllStructural` | 171.611 us | 202.870 us | Managed faster | 0.85x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 835.142 us | 1,247.207 us | Managed faster | 0.67x |
| `zero-width/count` | `Count` | 610.288 us | 1,119.035 us | Managed faster | 0.55x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 280.035 us | 544.172 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 180.730 us | 380.664 us | Managed faster | 0.47x |
| `count/from-offset` | `CountFromOffset` | 14.399 us | 50.183 us | Managed faster | 0.29x |
| `family/count` | `Count` | 648.997 us | 2,464.365 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.282 us | 2.015 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 56.057 us | 429.615 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 269.900 us | 2,587.590 us | Managed faster | 0.10x |
| `findall/one-strings` | `FindAllStrings` | 0.764 us | 38.935 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.515 us | 90.773 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.389 us | 91.376 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.256 us | 149.438 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.413 us | 0.453 us | Inconclusive | 3.11x |
| `replacement/empty-progression-string` | `ReplaceString` | 36.170 us | 23.924 us | CPython faster | 1.51x |
| `replacement/named-template-string` | `ReplaceString` | 1.294 us | 0.915 us | CPython faster | 1.42x |
| `replacement/fixed-string` | `ReplaceString` | 187.682 us | 177.206 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 94.529 us | 91.190 us | CPython faster | 1.03x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 179.439 us | 180.143 us | Equivalent | 1.00x |
| `replacement/unmatched-template-string` | `ReplaceString` | 49.874 us | 51.384 us | Managed faster | 0.97x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.971 us | 92.787 us | Managed faster | 0.97x |
| `replacement/no-match-string` | `ReplaceString` | 1.580 us | 2.417 us | Inconclusive | 0.65x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 160.024 us | 4,314.502 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 620.027 us | 17,430.043 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 608.947 us | 17,656.068 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.463 us | 16,418.381 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.719 us | 0.537 us | Inconclusive | 3.23x |
| `split/zero-width` | `SplitStrings` | 119.031 us | 66.906 us | CPython faster | 1.78x |
| `split/captures` | `SplitStrings` | 422.723 us | 256.219 us | Inconclusive | 1.65x |
| `split/unicode` | `SplitStrings` | 71.781 us | 45.128 us | CPython faster | 1.59x |
| `split/no-captures` | `SplitStrings` | 280.134 us | 224.384 us | CPython faster | 1.25x |
| `split/optional-capture` | `SplitStrings` | 74.381 us | 82.006 us | Inconclusive | 0.91x |
| `split/detailed` | `SplitDetailed` | 123.160 us | 135.863 us | Inconclusive | 0.91x |
| `split/one-separator` | `SplitStrings` | 1.309 us | 12.071 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.423 us | 25.060 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 181.601 us | 402.291 us | Managed faster | 0.45x |
| `corpus/code-declarations` | `Count` | 83.822 us | 629.586 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.127 us | 582.280 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,084.152 us | 359,667.363 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.23 %/14.31 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.22 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.39 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/15.91 % of the lanes.
- `unicode-option/search-hit`: Removable harness floors are 0.14 %/11.39 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 198.925 us / 71,617 B | 60.274 us | 185.906 us / 71,641 B | 61.119 us |
| `fallback` | 302.384 us / 83,561 B | 42.582 us | 265.400 us / 83,529 B | 42.453 us |
| `literal` | 349.731 us / 50,369 B | 25.217 us | 319.581 us / 50,337 B | 25.236 us |
| `unicode` | 55.791 us / 48,313 B | 36.081 us | 59.409 us / 48,281 B | 36.105 us |

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
