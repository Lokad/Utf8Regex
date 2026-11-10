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

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `7B4645FA7CAED43C64D7B4E8D0D7326C3D5EAD5C3A7326DA99A7E0873CEC0B5E`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `43` managed faster, `2` equivalent, `34` CPython faster, `6` inconclusive, `5` unqualified.

Measured source `998c630f3601`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.682 us | 0.235 us | CPython faster | 2.91x |
| `common/date-miss` | `IsMatch` | 1.851 us | 0.870 us | CPython faster | 2.13x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.364 us | 0.198 us | CPython faster | 1.84x |
| `leading-inline/search-hit` | `Search` | 0.618 us | 0.337 us | CPython faster | 1.83x |
| `atomic/search-miss` | `Search` | 0.456 us | 0.263 us | CPython faster | 1.74x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.495 us | 0.295 us | CPython faster | 1.68x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.405 us | 0.282 us | CPython faster | 1.43x |
| `possessive/search-hit` | `Search` | 0.442 us | 0.309 us | CPython faster | 1.42x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.262 us | 0.184 us | CPython faster | 1.42x |
| `reluctant/search-hit` | `Search` | 0.491 us | 0.353 us | CPython faster | 1.39x |
| `lookbehind/search-miss` | `Search` | 0.308 us | 0.222 us | CPython faster | 1.38x |
| `atomic/search-hit` | `Search` | 0.437 us | 0.326 us | CPython faster | 1.34x |
| `prefix/match` | `Match` | 0.321 us | 0.274 us | CPython faster | 1.17x |
| `scoped-inline/search-hit` | `Search` | 0.323 us | 0.294 us | CPython faster | 1.10x |
| `lookahead/search-miss` | `Search` | 0.204 us | 0.197 us | CPython faster | 1.03x |
| `unicode-option/search-hit` | `Search` | 0.311 us | 0.301 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.323 us | 19.726 us | Managed faster | 0.88x |
| `ignorecase/search-hit` | `Search` | 0.310 us | 0.358 us | Managed faster | 0.87x |
| `ascii-option/search-hit` | `Search` | 0.245 us | 0.308 us | Managed faster | 0.80x |
| `multiline/search-hit` | `Search` | 0.257 us | 0.336 us | Managed faster | 0.77x |
| `ignorecase/search-miss` | `Search` | 0.209 us | 0.291 us | Managed faster | 0.72x |
| `greedy/search-miss` | `Search` | 0.135 us | 0.221 us | Managed faster | 0.61x |
| `unicode-literal/search-hit` | `Search` | 0.143 us | 0.238 us | Managed faster | 0.60x |
| `lookahead/search-hit` | `Search` | 0.181 us | 0.302 us | Managed faster | 0.60x |
| `possessive/search-miss` | `Search` | 0.122 us | 0.212 us | Managed faster | 0.57x |
| `unicode-literal/search-miss` | `Search` | 0.149 us | 0.292 us | Managed faster | 0.51x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.145 us | 0.335 us | Managed faster | 0.43x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.088 us | 0.257 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.069 us | 0.348 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.057 us | 0.320 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.740 us | 22.602 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.465 us | 22.412 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.666 us | 22.279 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.202 us | 0.237 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.319 us | 0.335 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.274 us | 0.267 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.204 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.186 us | 0.213 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.050 us | 1.329 us | Managed faster | 0.79x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 794.189 us | 349.912 us | CPython faster | 2.27x |
| `findall/one-capture-strings` | `FindAllStrings` | 476.764 us | 215.060 us | CPython faster | 2.22x |
| `findall/named-capture-strings` | `FindAllStrings` | 167.648 us | 94.685 us | CPython faster | 1.77x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 102.157 us | 62.318 us | CPython faster | 1.64x |
| `findall/empty-progression-strings` | `FindAllStrings` | 67.884 us | 43.863 us | CPython faster | 1.55x |
| `findall/unicode-full-strings` | `FindAllStrings` | 39.040 us | 27.449 us | CPython faster | 1.42x |
| `findall/full-strings` | `FindAllStrings` | 512.974 us | 369.855 us | CPython faster | 1.39x |
| `findall/optional-capture-strings` | `FindAllStrings` | 61.042 us | 56.595 us | CPython faster | 1.08x |
| `findall/full-utf8` | `FindAllUtf8` | 558.603 us | 521.400 us | CPython faster | 1.07x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.548 us | 16.060 us | CPython faster | 1.03x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 573.977 us | 619.960 us | Managed faster | 0.93x |
| `findall/structural-many` | `FindAllStructural` | 177.395 us | 202.139 us | Managed faster | 0.88x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 865.120 us | 1,258.840 us | Managed faster | 0.69x |
| `zero-width/count` | `Count` | 623.213 us | 1,118.082 us | Managed faster | 0.56x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 275.074 us | 541.708 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 187.967 us | 378.741 us | Managed faster | 0.50x |
| `family/count` | `Count` | 719.122 us | 2,465.438 us | Managed faster | 0.29x |
| `count/from-offset` | `CountFromOffset` | 14.745 us | 50.515 us | Managed faster | 0.29x |
| `count/one` | `Count` | 0.282 us | 2.015 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 58.121 us | 434.894 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 287.286 us | 2,579.485 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.762 us | 38.896 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.508 us | 90.865 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.386 us | 91.960 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.262 us | 142.655 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.150 us | 0.452 us | Inconclusive | 2.51x |
| `replacement/empty-progression-string` | `ReplaceString` | 36.795 us | 23.729 us | CPython faster | 1.55x |
| `replacement/named-template-string` | `ReplaceString` | 1.271 us | 0.923 us | CPython faster | 1.38x |
| `replacement/fixed-string` | `ReplaceString` | 187.732 us | 178.570 us | CPython faster | 1.05x |
| `replacement/subn-string` | `SubnString` | 94.091 us | 91.570 us | CPython faster | 1.03x |
| `replacement/unmatched-template-string` | `ReplaceString` | 50.733 us | 51.040 us | Equivalent | 0.99x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 178.573 us | 180.277 us | Equivalent | 0.99x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.391 us | 92.204 us | Managed faster | 0.97x |
| `replacement/no-match-string` | `ReplaceString` | 1.324 us | 2.419 us | Inconclusive | 0.55x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 158.848 us | 4,296.068 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 620.433 us | 17,263.040 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 618.816 us | 17,330.964 us | Managed faster | 0.04x |
| `replacement/one-template-string` | `ReplaceString` | 1.980 us | 16,496.497 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.407 us | 0.546 us | Inconclusive | 2.53x |
| `split/zero-width` | `SplitStrings` | 121.989 us | 67.084 us | CPython faster | 1.82x |
| `split/captures` | `SplitStrings` | 436.987 us | 254.863 us | CPython faster | 1.71x |
| `split/unicode` | `SplitStrings` | 72.907 us | 44.646 us | CPython faster | 1.63x |
| `split/no-captures` | `SplitStrings` | 277.774 us | 224.343 us | CPython faster | 1.24x |
| `split/optional-capture` | `SplitStrings` | 76.333 us | 82.801 us | Managed faster | 0.92x |
| `split/detailed` | `SplitDetailed` | 115.610 us | 136.367 us | Managed faster | 0.85x |
| `split/one-separator` | `SplitStrings` | 1.112 us | 12.071 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.203 us | 25.051 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 194.858 us | 403.789 us | Managed faster | 0.48x |
| `corpus/code-declarations` | `Count` | 85.320 us | 630.995 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 49.621 us | 582.873 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 2,134.700 us | 358,975.268 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.24 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.23 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/12.66 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.31 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/15.89 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 191.044 us / 71,617 B | 60.201 us | 190.153 us / 71,641 B | 60.555 us |
| `fallback` | 248.175 us / 83,561 B | 42.442 us | 267.669 us / 83,529 B | 42.632 us |
| `literal` | 475.666 us / 50,369 B | 24.813 us | 241.812 us / 50,337 B | 24.747 us |
| `unicode` | 54.913 us / 48,313 B | 36.150 us | 58.916 us / 48,281 B | 36.146 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
