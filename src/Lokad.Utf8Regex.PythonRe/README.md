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

Snapshot SHA-256: `140F855FF4FB30079C61DCFA4DEFCE142A20E6E50A182B01DD5D220AC6F16C75`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `44` managed faster, `1` equivalent, `35` CPython faster, `6` inconclusive, `4` unqualified.

Measured source `9bbf54de7597`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.681 us | 0.234 us | CPython faster | 2.91x |
| `common/date-miss` | `IsMatch` | 1.806 us | 0.867 us | CPython faster | 2.08x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.363 us | 0.198 us | CPython faster | 1.83x |
| `leading-inline/search-hit` | `Search` | 0.605 us | 0.336 us | CPython faster | 1.80x |
| `atomic/search-miss` | `Search` | 0.441 us | 0.262 us | CPython faster | 1.68x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.483 us | 0.296 us | CPython faster | 1.63x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.395 us | 0.283 us | CPython faster | 1.40x |
| `lookbehind/search-miss` | `Search` | 0.306 us | 0.220 us | CPython faster | 1.39x |
| `possessive/search-hit` | `Search` | 0.427 us | 0.309 us | CPython faster | 1.38x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.250 us | 0.182 us | CPython faster | 1.38x |
| `reluctant/search-hit` | `Search` | 0.483 us | 0.351 us | CPython faster | 1.37x |
| `atomic/search-hit` | `Search` | 0.424 us | 0.324 us | CPython faster | 1.31x |
| `scoped-inline/search-hit` | `Search` | 0.352 us | 0.292 us | CPython faster | 1.20x |
| `prefix/match` | `Match` | 0.320 us | 0.274 us | CPython faster | 1.16x |
| `unicode-option/search-hit` | `Search` | 0.319 us | 0.300 us | CPython faster | 1.07x |
| `multiline/search-miss` | `Search` | 0.284 us | 0.268 us | CPython faster | 1.06x |
| `lookahead/search-miss` | `Search` | 0.208 us | 0.197 us | CPython faster | 1.06x |
| `ignorecase/search-hit` | `Search` | 0.315 us | 0.359 us | Managed faster | 0.88x |
| `unicode/fullmatch` | `FullMatch` | 17.224 us | 19.703 us | Managed faster | 0.88x |
| `ascii-option/search-hit` | `Search` | 0.248 us | 0.308 us | Managed faster | 0.81x |
| `multiline/search-hit` | `Search` | 0.262 us | 0.336 us | Managed faster | 0.78x |
| `ignorecase/search-miss` | `Search` | 0.205 us | 0.320 us | Managed faster | 0.64x |
| `greedy/search-miss` | `Search` | 0.139 us | 0.221 us | Managed faster | 0.62x |
| `lookahead/search-hit` | `Search` | 0.188 us | 0.301 us | Managed faster | 0.62x |
| `possessive/search-miss` | `Search` | 0.124 us | 0.213 us | Managed faster | 0.58x |
| `unicode-literal/search-hit` | `Search` | 0.126 us | 0.237 us | Managed faster | 0.53x |
| `unicode-literal/search-miss` | `Search` | 0.141 us | 0.291 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.146 us | 0.335 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.089 us | 0.258 us | Managed faster | 0.34x |
| `literal/fullmatch` | `FullMatch` | 0.070 us | 0.348 us | Managed faster | 0.20x |
| `common/ip-match` | `IsMatch` | 0.056 us | 0.321 us | Managed faster | 0.17x |
| `literal/search` | `Search` | 2.663 us | 22.613 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.478 us | 22.362 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.663 us | 22.254 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.204 us | 0.237 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.319 us | 0.335 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.202 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.179 us | 0.214 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.055 us | 1.314 us | Managed faster | 0.80x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 793.327 us | 350.826 us | CPython faster | 2.26x |
| `findall/one-capture-strings` | `FindAllStrings` | 463.822 us | 213.238 us | CPython faster | 2.17x |
| `findall/named-capture-strings` | `FindAllStrings` | 163.569 us | 95.171 us | CPython faster | 1.72x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 102.740 us | 62.204 us | CPython faster | 1.65x |
| `findall/empty-progression-strings` | `FindAllStrings` | 67.418 us | 43.451 us | CPython faster | 1.55x |
| `findall/full-strings` | `FindAllStrings` | 542.174 us | 370.886 us | CPython faster | 1.46x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.004 us | 27.519 us | CPython faster | 1.39x |
| `findall/full-utf8` | `FindAllUtf8` | 586.303 us | 519.817 us | CPython faster | 1.13x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.787 us | 16.046 us | CPython faster | 1.11x |
| `findall/optional-capture-strings` | `FindAllStrings` | 57.590 us | 55.815 us | CPython faster | 1.03x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 564.291 us | 618.914 us | Managed faster | 0.91x |
| `findall/structural-many` | `FindAllStructural` | 172.948 us | 201.968 us | Managed faster | 0.86x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 872.984 us | 1,252.484 us | Managed faster | 0.70x |
| `zero-width/count` | `Count` | 583.813 us | 1,118.835 us | Managed faster | 0.52x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 274.544 us | 544.365 us | Managed faster | 0.50x |
| `count/empty-progression` | `Count` | 186.915 us | 379.617 us | Managed faster | 0.49x |
| `family/count` | `Count` | 790.110 us | 2,520.887 us | Managed faster | 0.31x |
| `count/from-offset` | `CountFromOffset` | 13.920 us | 49.905 us | Managed faster | 0.28x |
| `count/one` | `Count` | 0.276 us | 2.022 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 58.242 us | 435.678 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 279.381 us | 2,579.102 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.760 us | 38.924 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.539 us | 90.879 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.386 us | 91.252 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.258 us | 140.365 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.069 us | 0.459 us | Inconclusive | 2.31x |
| `replacement/empty-progression-string` | `ReplaceString` | 36.461 us | 23.686 us | CPython faster | 1.54x |
| `replacement/named-template-string` | `ReplaceString` | 1.238 us | 0.945 us | CPython faster | 1.31x |
| `replacement/fixed-string` | `ReplaceString` | 187.212 us | 176.139 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 93.711 us | 90.461 us | CPython faster | 1.04x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 177.910 us | 179.336 us | Equivalent | 0.99x |
| `replacement/subn-utf8` | `SubnUtf8` | 88.975 us | 91.810 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 48.489 us | 51.083 us | Managed faster | 0.95x |
| `replacement/no-match-string` | `ReplaceString` | 1.176 us | 2.408 us | Inconclusive | 0.49x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 154.809 us | 4,298.620 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 602.865 us | 17,204.141 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 589.707 us | 17,148.398 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.011 us | 16,390.308 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.552 us | 0.540 us | Inconclusive | 2.89x |
| `split/unicode` | `SplitStrings` | 78.439 us | 44.701 us | CPython faster | 1.75x |
| `split/zero-width` | `SplitStrings` | 117.071 us | 66.840 us | CPython faster | 1.75x |
| `split/captures` | `SplitStrings` | 425.241 us | 255.689 us | CPython faster | 1.66x |
| `split/no-captures` | `SplitStrings` | 305.943 us | 224.053 us | CPython faster | 1.36x |
| `split/optional-capture` | `SplitStrings` | 74.512 us | 82.880 us | Managed faster | 0.90x |
| `split/detailed` | `SplitDetailed` | 113.786 us | 136.252 us | Managed faster | 0.83x |
| `split/one-separator` | `SplitStrings` | 1.230 us | 12.064 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.188 us | 25.037 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 189.979 us | 403.292 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 83.674 us | 630.841 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 48.610 us | 582.086 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,093.280 us | 359,240.709 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.44 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.17 %/10.36 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.29 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/15.84 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 164.469 us / 71,617 B | 60.385 us | 146.566 us / 71,641 B | 61.327 us |
| `fallback` | 294.259 us / 83,561 B | 42.707 us | 223.409 us / 83,529 B | 42.676 us |
| `literal` | 233.787 us / 50,369 B | 25.019 us | 212.028 us / 50,337 B | 24.468 us |
| `unicode` | 89.597 us / 48,313 B | 36.760 us | 89.359 us / 48,281 B | 36.139 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
