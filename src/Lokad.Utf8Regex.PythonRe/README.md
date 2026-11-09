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

Snapshot SHA-256: `628EF86D7BA7E404985409A47632532FA8B653F3D135CE75A238039BA384644B`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `42` managed faster, `2` equivalent, `35` CPython faster, `6` inconclusive, `5` unqualified.

Measured source `41e05e7655e4`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.679 us | 0.236 us | CPython faster | 2.87x |
| `common/date-miss` | `IsMatch` | 1.808 us | 0.868 us | CPython faster | 2.08x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.353 us | 0.199 us | CPython faster | 1.78x |
| `leading-inline/search-hit` | `Search` | 0.589 us | 0.335 us | CPython faster | 1.76x |
| `atomic/search-miss` | `Search` | 0.456 us | 0.262 us | CPython faster | 1.74x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.477 us | 0.296 us | CPython faster | 1.61x |
| `possessive/search-hit` | `Search` | 0.452 us | 0.310 us | CPython faster | 1.46x |
| `lookbehind/search-miss` | `Search` | 0.310 us | 0.222 us | CPython faster | 1.40x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.251 us | 0.184 us | CPython faster | 1.36x |
| `reluctant/search-hit` | `Search` | 0.480 us | 0.353 us | CPython faster | 1.36x |
| `atomic/search-hit` | `Search` | 0.441 us | 0.325 us | CPython faster | 1.36x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.378 us | 0.283 us | CPython faster | 1.34x |
| `prefix/match` | `Match` | 0.318 us | 0.273 us | CPython faster | 1.16x |
| `scoped-inline/search-hit` | `Search` | 0.327 us | 0.293 us | CPython faster | 1.12x |
| `unicode-option/search-hit` | `Search` | 0.317 us | 0.301 us | CPython faster | 1.06x |
| `lookahead/search-miss` | `Search` | 0.203 us | 0.196 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 17.092 us | 19.750 us | Managed faster | 0.86x |
| `unicode-category/search-miss` | `Search` | 0.172 us | 0.213 us | Managed faster | 0.80x |
| `ascii-option/search-hit` | `Search` | 0.238 us | 0.306 us | Managed faster | 0.78x |
| `multiline/search-hit` | `Search` | 0.253 us | 0.337 us | Managed faster | 0.75x |
| `ignorecase/search-miss` | `Search` | 0.206 us | 0.290 us | Managed faster | 0.71x |
| `lookahead/search-hit` | `Search` | 0.185 us | 0.301 us | Managed faster | 0.62x |
| `greedy/search-miss` | `Search` | 0.133 us | 0.219 us | Managed faster | 0.61x |
| `possessive/search-miss` | `Search` | 0.120 us | 0.212 us | Managed faster | 0.57x |
| `unicode-literal/search-hit` | `Search` | 0.131 us | 0.238 us | Managed faster | 0.55x |
| `unicode-literal/search-miss` | `Search` | 0.144 us | 0.292 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.143 us | 0.336 us | Managed faster | 0.43x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.086 us | 0.257 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.070 us | 0.348 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.057 us | 0.325 us | Managed faster | 0.17x |
| `literal/search` | `Search` | 2.727 us | 22.682 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.478 us | 22.409 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.663 us | 22.264 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.204 us | 0.240 us | Unqualified | — |
| `ignorecase/search-hit` | `Search` | 0.324 us | 0.359 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.317 us | 0.334 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.272 us | 0.267 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.205 us | 0.239 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.870 us | 1.309 us | Managed faster | 0.66x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 872.145 us | 351.211 us | CPython faster | 2.48x |
| `findall/one-capture-strings` | `FindAllStrings` | 441.961 us | 214.990 us | CPython faster | 2.06x |
| `findall/named-capture-strings` | `FindAllStrings` | 158.209 us | 95.423 us | CPython faster | 1.66x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 101.039 us | 62.056 us | CPython faster | 1.63x |
| `findall/empty-progression-strings` | `FindAllStrings` | 63.138 us | 43.850 us | CPython faster | 1.45x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.706 us | 27.593 us | CPython faster | 1.41x |
| `findall/full-strings` | `FindAllStrings` | 514.128 us | 369.359 us | CPython faster | 1.39x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 18.119 us | 16.033 us | CPython faster | 1.13x |
| `findall/optional-capture-strings` | `FindAllStrings` | 57.989 us | 55.986 us | CPython faster | 1.03x |
| `findall/structural-many` | `FindAllStructural` | 200.556 us | 201.147 us | Equivalent | 0.99x |
| `findall/full-utf8` | `FindAllUtf8` | 512.538 us | 520.387 us | Equivalent | 0.99x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 548.165 us | 623.366 us | Managed faster | 0.88x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 945.855 us | 1,253.608 us | Managed faster | 0.75x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 304.756 us | 542.782 us | Managed faster | 0.56x |
| `zero-width/count` | `Count` | 586.520 us | 1,114.830 us | Managed faster | 0.53x |
| `count/empty-progression` | `Count` | 195.446 us | 381.022 us | Managed faster | 0.51x |
| `count/from-offset` | `CountFromOffset` | 14.385 us | 50.560 us | Managed faster | 0.28x |
| `family/count` | `Count` | 687.281 us | 2,458.941 us | Managed faster | 0.28x |
| `count/one` | `Count` | 0.283 us | 2.025 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 57.348 us | 428.517 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 281.414 us | 2,572.197 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.761 us | 38.878 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.546 us | 90.827 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.500 us | 91.291 us | Managed faster | 0.01x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.250 us | 140.403 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.134 us | 0.462 us | Inconclusive | 2.44x |
| `replacement/subn-string` | `SubnString` | 141.874 us | 90.702 us | CPython faster | 1.56x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.750 us | 23.745 us | CPython faster | 1.47x |
| `replacement/named-template-string` | `ReplaceString` | 1.243 us | 0.915 us | CPython faster | 1.36x |
| `replacement/fixed-string` | `ReplaceString` | 198.529 us | 176.982 us | CPython faster | 1.12x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 189.526 us | 180.489 us | CPython faster | 1.05x |
| `replacement/subn-utf8` | `SubnUtf8` | 95.746 us | 91.419 us | CPython faster | 1.05x |
| `replacement/unmatched-template-string` | `ReplaceString` | 48.912 us | 51.491 us | Managed faster | 0.95x |
| `replacement/no-match-string` | `ReplaceString` | 1.344 us | 2.420 us | Inconclusive | 0.55x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 156.275 us | 4,328.929 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 605.673 us | 17,401.938 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 599.719 us | 17,239.665 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.023 us | 16,353.501 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.480 us | 0.540 us | Inconclusive | 2.79x |
| `split/zero-width` | `SplitStrings` | 116.425 us | 66.336 us | CPython faster | 1.76x |
| `split/captures` | `SplitStrings` | 428.008 us | 254.614 us | CPython faster | 1.68x |
| `split/unicode` | `SplitStrings` | 70.140 us | 44.580 us | CPython faster | 1.57x |
| `split/no-captures` | `SplitStrings` | 278.115 us | 224.836 us | CPython faster | 1.24x |
| `split/optional-capture` | `SplitStrings` | 73.167 us | 82.750 us | Managed faster | 0.88x |
| `split/detailed` | `SplitDetailed` | 112.702 us | 136.945 us | Managed faster | 0.82x |
| `split/one-separator` | `SplitStrings` | 1.166 us | 12.059 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.233 us | 25.018 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 189.254 us | 403.005 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 86.588 us | 628.261 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 47.816 us | 580.226 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,071.036 us | 358,368.813 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.22 % of the lanes.
- `ignorecase/search-hit`: Removable harness floors are 0.15 %/9.62 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.13 %/10.17 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/12.87 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.19 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 197.606 us / 71,617 B | 60.402 us | 185.713 us / 71,641 B | 60.559 us |
| `fallback` | 294.384 us / 83,561 B | 42.647 us | 264.153 us / 83,529 B | 42.857 us |
| `literal` | 344.056 us / 50,369 B | 25.413 us | 325.325 us / 50,337 B | 25.177 us |
| `unicode` | 55.903 us / 48,313 B | 36.116 us | 59.741 us / 48,281 B | 36.417 us |

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
