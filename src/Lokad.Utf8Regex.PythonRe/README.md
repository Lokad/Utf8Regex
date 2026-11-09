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

Snapshot SHA-256: `80CA3F41186E355B9BB3E761E94B4569920A63483CE9F9846A65219403C5DD9B`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `41` managed faster, `3` equivalent, `34` CPython faster, `7` inconclusive, `5` unqualified.

Measured source `848dbb295d31`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.679 us | 0.233 us | CPython faster | 2.90x |
| `common/date-miss` | `IsMatch` | 1.826 us | 0.866 us | CPython faster | 2.11x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.367 us | 0.198 us | CPython faster | 1.85x |
| `leading-inline/search-hit` | `Search` | 0.595 us | 0.335 us | CPython faster | 1.77x |
| `atomic/search-miss` | `Search` | 0.446 us | 0.262 us | CPython faster | 1.70x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.491 us | 0.295 us | CPython faster | 1.67x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.286 us | 0.184 us | CPython faster | 1.55x |
| `possessive/search-hit` | `Search` | 0.449 us | 0.308 us | CPython faster | 1.46x |
| `lookbehind/search-miss` | `Search` | 0.322 us | 0.221 us | CPython faster | 1.46x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.397 us | 0.283 us | CPython faster | 1.40x |
| `atomic/search-hit` | `Search` | 0.443 us | 0.325 us | CPython faster | 1.36x |
| `reluctant/search-hit` | `Search` | 0.470 us | 0.353 us | CPython faster | 1.33x |
| `prefix/match` | `Match` | 0.320 us | 0.274 us | CPython faster | 1.17x |
| `scoped-inline/search-hit` | `Search` | 0.327 us | 0.292 us | CPython faster | 1.12x |
| `unicode-option/search-hit` | `Search` | 0.323 us | 0.300 us | CPython faster | 1.07x |
| `lookahead/search-miss` | `Search` | 0.209 us | 0.196 us | CPython faster | 1.06x |
| `unicode/fullmatch` | `FullMatch` | 17.045 us | 19.683 us | Managed faster | 0.87x |
| `ignorecase/search-hit` | `Search` | 0.309 us | 0.359 us | Managed faster | 0.86x |
| `ascii-option/search-hit` | `Search` | 0.249 us | 0.307 us | Managed faster | 0.81x |
| `multiline/search-hit` | `Search` | 0.255 us | 0.337 us | Managed faster | 0.76x |
| `ignorecase/search-miss` | `Search` | 0.205 us | 0.288 us | Managed faster | 0.71x |
| `lookahead/search-hit` | `Search` | 0.195 us | 0.303 us | Managed faster | 0.64x |
| `greedy/search-miss` | `Search` | 0.138 us | 0.221 us | Managed faster | 0.63x |
| `possessive/search-miss` | `Search` | 0.126 us | 0.214 us | Managed faster | 0.59x |
| `unicode-literal/search-hit` | `Search` | 0.135 us | 0.237 us | Managed faster | 0.57x |
| `unicode-literal/search-miss` | `Search` | 0.141 us | 0.292 us | Managed faster | 0.48x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.150 us | 0.342 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.096 us | 0.257 us | Managed faster | 0.37x |
| `literal/fullmatch` | `FullMatch` | 0.069 us | 0.347 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.061 us | 0.321 us | Managed faster | 0.19x |
| `literal/search` | `Search` | 2.651 us | 22.657 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.484 us | 22.444 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.670 us | 22.306 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.208 us | 0.238 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.332 us | 0.335 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.268 us | 0.267 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.212 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.182 us | 0.214 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.873 us | 1.311 us | Inconclusive | 0.66x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 877.271 us | 349.572 us | CPython faster | 2.50x |
| `findall/one-capture-strings` | `FindAllStrings` | 448.554 us | 216.202 us | CPython faster | 2.07x |
| `findall/named-capture-strings` | `FindAllStrings` | 160.475 us | 95.031 us | CPython faster | 1.69x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 101.569 us | 62.177 us | CPython faster | 1.63x |
| `findall/empty-progression-strings` | `FindAllStrings` | 61.862 us | 43.427 us | CPython faster | 1.42x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.900 us | 27.464 us | CPython faster | 1.42x |
| `findall/full-strings` | `FindAllStrings` | 500.990 us | 367.922 us | CPython faster | 1.36x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.680 us | 16.025 us | CPython faster | 1.04x |
| `findall/optional-capture-strings` | `FindAllStrings` | 56.132 us | 55.371 us | Equivalent | 1.01x |
| `findall/structural-many` | `FindAllStructural` | 199.262 us | 201.830 us | Equivalent | 0.99x |
| `findall/full-utf8` | `FindAllUtf8` | 513.138 us | 520.181 us | Equivalent | 0.99x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 537.370 us | 629.720 us | Managed faster | 0.85x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 946.744 us | 1,248.780 us | Managed faster | 0.76x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 300.379 us | 541.713 us | Managed faster | 0.55x |
| `zero-width/count` | `Count` | 552.942 us | 1,110.562 us | Managed faster | 0.50x |
| `count/empty-progression` | `Count` | 163.219 us | 379.308 us | Managed faster | 0.43x |
| `count/from-offset` | `CountFromOffset` | 15.447 us | 50.529 us | Managed faster | 0.31x |
| `family/count` | `Count` | 698.933 us | 2,481.658 us | Managed faster | 0.28x |
| `unicode/count` | `Count` | 57.331 us | 434.427 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.257 us | 2.023 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 273.117 us | 2,576.579 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.779 us | 38.934 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.535 us | 91.450 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.382 us | 91.320 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.268 us | 144.509 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.223 us | 0.463 us | Inconclusive | 2.65x |
| `replacement/empty-progression-string` | `ReplaceString` | 33.748 us | 23.677 us | CPython faster | 1.42x |
| `replacement/named-template-string` | `ReplaceString` | 1.231 us | 0.917 us | CPython faster | 1.34x |
| `replacement/fixed-string` | `ReplaceString` | 201.061 us | 176.712 us | CPython faster | 1.14x |
| `replacement/subn-string` | `SubnString` | 102.659 us | 91.242 us | CPython faster | 1.12x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 190.050 us | 179.073 us | CPython faster | 1.06x |
| `replacement/subn-utf8` | `SubnUtf8` | 95.626 us | 90.604 us | CPython faster | 1.06x |
| `replacement/unmatched-template-string` | `ReplaceString` | 47.877 us | 51.207 us | Managed faster | 0.94x |
| `replacement/no-match-string` | `ReplaceString` | 1.614 us | 2.409 us | Inconclusive | 0.67x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 154.598 us | 4,343.902 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 600.042 us | 17,437.527 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 585.143 us | 17,552.747 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.435 us | 16,260.524 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.554 us | 0.544 us | Inconclusive | 2.85x |
| `split/zero-width` | `SplitStrings` | 111.695 us | 66.835 us | CPython faster | 1.67x |
| `split/captures` | `SplitStrings` | 416.388 us | 255.165 us | CPython faster | 1.63x |
| `split/unicode` | `SplitStrings` | 70.326 us | 44.892 us | CPython faster | 1.57x |
| `split/no-captures` | `SplitStrings` | 279.740 us | 224.093 us | CPython faster | 1.25x |
| `split/optional-capture` | `SplitStrings` | 72.254 us | 82.426 us | Managed faster | 0.88x |
| `split/detailed` | `SplitDetailed` | 109.119 us | 136.051 us | Managed faster | 0.80x |
| `split/one-separator` | `SplitStrings` | 1.187 us | 12.080 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.301 us | 24.974 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 183.465 us | 400.117 us | Managed faster | 0.46x |
| `corpus/code-declarations` | `Count` | 83.778 us | 629.263 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 48.235 us | 582.163 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,070.664 us | 358,083.074 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.19 %/14.29 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.12 %/10.14 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.18 %/12.66 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.21 %/14.16 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.23 %/15.91 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 191.169 us / 71,617 B | 60.429 us | 190.166 us / 71,641 B | 60.952 us |
| `fallback` | 256.238 us / 83,561 B | 42.303 us | 268.663 us / 83,529 B | 42.808 us |
| `literal` | 372.712 us / 50,369 B | 24.872 us | 304.400 us / 50,337 B | 25.021 us |
| `unicode` | 55.303 us / 48,313 B | 36.432 us | 58.631 us / 48,281 B | 36.543 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
