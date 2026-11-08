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

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [full snapshot](../../PythonRe.Benchmarks.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `A0BF8F16E55705D5845B1A07839D2A636C0F39E64AA936CA871E48BDF510C673`. Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`.

Public Status: `44` managed faster, `2` equivalent, `30` CPython faster, `9` inconclusive, `5` unqualified.

Measured source `d60679252cf6`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.665 us | 0.232 us | CPython faster | 2.87x |
| `common/date-miss` | `IsMatch` | 1.777 us | 0.852 us | CPython faster | 2.09x |
| `leading-inline/search-hit` | `Search` | 0.588 us | 0.331 us | CPython faster | 1.78x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.350 us | 0.197 us | CPython faster | 1.78x |
| `atomic/search-miss` | `Search` | 0.456 us | 0.258 us | Inconclusive | 1.75x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.464 us | 0.293 us | CPython faster | 1.60x |
| `possessive/search-hit` | `Search` | 0.455 us | 0.304 us | CPython faster | 1.49x |
| `lookbehind/search-miss` | `Search` | 0.307 us | 0.218 us | CPython faster | 1.41x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.251 us | 0.182 us | CPython faster | 1.38x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.375 us | 0.278 us | CPython faster | 1.35x |
| `atomic/search-hit` | `Search` | 0.426 us | 0.317 us | CPython faster | 1.34x |
| `reluctant/search-hit` | `Search` | 0.451 us | 0.346 us | CPython faster | 1.31x |
| `prefix/match` | `Match` | 0.313 us | 0.268 us | CPython faster | 1.16x |
| `scoped-inline/search-hit` | `Search` | 0.320 us | 0.287 us | CPython faster | 1.12x |
| `unicode-option/search-hit` | `Search` | 0.316 us | 0.297 us | CPython faster | 1.06x |
| `multiline/search-miss` | `Search` | 0.272 us | 0.262 us | CPython faster | 1.04x |
| `unicode/fullmatch` | `FullMatch` | 18.292 us | 19.485 us | Managed faster | 0.94x |
| `ignorecase/search-hit` | `Search` | 0.307 us | 0.351 us | Managed faster | 0.88x |
| `unicode-category/search-miss` | `Search` | 0.171 us | 0.214 us | Managed faster | 0.80x |
| `multiline/search-hit` | `Search` | 0.250 us | 0.330 us | Managed faster | 0.76x |
| `ignorecase/search-miss` | `Search` | 0.208 us | 0.284 us | Managed faster | 0.73x |
| `lookahead/search-hit` | `Search` | 0.182 us | 0.294 us | Managed faster | 0.62x |
| `greedy/search-miss` | `Search` | 0.131 us | 0.217 us | Managed faster | 0.60x |
| `unicode-literal/search-hit` | `Search` | 0.135 us | 0.235 us | Managed faster | 0.58x |
| `possessive/search-miss` | `Search` | 0.120 us | 0.209 us | Managed faster | 0.57x |
| `unicode-literal/search-miss` | `Search` | 0.141 us | 0.289 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.143 us | 0.330 us | Managed faster | 0.43x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.087 us | 0.256 us | Managed faster | 0.34x |
| `common/ip-match` | `IsMatch` | 0.092 us | 0.314 us | Managed faster | 0.29x |
| `literal/fullmatch` | `FullMatch` | 0.068 us | 0.343 us | Managed faster | 0.20x |
| `literal/search` | `Search` | 2.600 us | 22.280 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.451 us | 22.070 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.632 us | 21.978 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.205 us | 0.231 us | Unqualified | — |
| `ascii-option/search-hit` | `Search` | 0.262 us | 0.301 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.199 us | 0.194 us | Unqualified | — |
| `lookbehind/search-hit` | `Search` | 0.310 us | 0.332 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.201 us | 0.234 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.848 us | 1.300 us | Managed faster | 0.66x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 901.988 us | 343.761 us | CPython faster | 2.63x |
| `findall/one-capture-strings` | `FindAllStrings` | 469.980 us | 211.277 us | CPython faster | 2.23x |
| `findall/named-capture-strings` | `FindAllStrings` | 162.645 us | 94.346 us | CPython faster | 1.74x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 99.568 us | 61.174 us | CPython faster | 1.63x |
| `findall/empty-progression-strings` | `FindAllStrings` | 64.384 us | 42.779 us | CPython faster | 1.50x |
| `findall/full-strings` | `FindAllStrings` | 518.264 us | 366.596 us | CPython faster | 1.41x |
| `findall/unicode-full-strings` | `FindAllStrings` | 37.213 us | 27.418 us | CPython faster | 1.37x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.811 us | 15.851 us | CPython faster | 1.06x |
| `findall/optional-capture-strings` | `FindAllStrings` | 56.304 us | 55.463 us | Inconclusive | 1.01x |
| `findall/full-utf8` | `FindAllUtf8` | 517.200 us | 513.839 us | Equivalent | 1.01x |
| `findall/structural-many` | `FindAllStructural` | 192.107 us | 201.842 us | Managed faster | 0.95x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 531.587 us | 616.665 us | Managed faster | 0.87x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 960.568 us | 1,252.363 us | Managed faster | 0.76x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 303.190 us | 538.697 us | Managed faster | 0.57x |
| `zero-width/count` | `Count` | 583.431 us | 1,102.484 us | Managed faster | 0.53x |
| `count/empty-progression` | `Count` | 168.278 us | 374.616 us | Managed faster | 0.45x |
| `count/from-offset` | `CountFromOffset` | 13.905 us | 49.125 us | Managed faster | 0.28x |
| `family/count` | `Count` | 638.363 us | 2,423.680 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.272 us | 1.988 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 57.080 us | 430.048 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 281.752 us | 2,547.554 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.734 us | 38.126 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.527 us | 88.926 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.489 us | 90.141 us | Managed faster | 0.01x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.251 us | 140.681 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.048 us | 0.452 us | Inconclusive | 2.31x |
| `replacement/empty-progression-string` | `ReplaceString` | 34.930 us | 23.331 us | CPython faster | 1.49x |
| `replacement/named-template-string` | `ReplaceString` | 1.239 us | 0.900 us | CPython faster | 1.38x |
| `replacement/fixed-string` | `ReplaceString` | 184.423 us | 173.669 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 92.251 us | 90.279 us | Inconclusive | 1.03x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 173.808 us | 175.676 us | Equivalent | 0.99x |
| `replacement/subn-utf8` | `SubnUtf8` | 87.515 us | 91.036 us | Managed faster | 0.96x |
| `replacement/unmatched-template-string` | `ReplaceString` | 47.447 us | 50.555 us | Managed faster | 0.94x |
| `replacement/no-match-string` | `ReplaceString` | 1.182 us | 2.364 us | Inconclusive | 0.50x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 149.131 us | 4,197.089 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 585.319 us | 16,601.309 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 595.972 us | 17,178.526 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.818 us | 19,472.275 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.407 us | 0.542 us | Inconclusive | 2.56x |
| `split/zero-width` | `SplitStrings` | 114.799 us | 66.643 us | CPython faster | 1.73x |
| `split/captures` | `SplitStrings` | 430.681 us | 254.069 us | CPython faster | 1.70x |
| `split/unicode` | `SplitStrings` | 70.198 us | 43.611 us | CPython faster | 1.61x |
| `split/no-captures` | `SplitStrings` | 279.435 us | 222.240 us | CPython faster | 1.27x |
| `split/optional-capture` | `SplitStrings` | 74.328 us | 81.520 us | Managed faster | 0.91x |
| `split/detailed` | `SplitDetailed` | 108.122 us | 134.090 us | Managed faster | 0.81x |
| `split/one-separator` | `SplitStrings` | 1.122 us | 11.763 us | Inconclusive | 0.09x |
| `split/no-separator` | `SplitStrings` | 1.154 us | 24.694 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 187.996 us | 391.899 us | Managed faster | 0.48x |
| `corpus/code-declarations` | `Count` | 79.964 us | 615.230 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.074 us | 573.472 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 2,062.812 us | 351,877.392 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.20 %/14.61 % of the lanes.
- `ascii-option/search-hit`: Removable harness floors are 0.16 %/11.34 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/17.78 % of the lanes.
- `lookbehind/search-hit`: Removable harness floors are 0.17 %/10.32 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.34 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 195.981 us / 71,617 B | 59.864 us | 184.703 us / 71,641 B | 58.491 us |
| `fallback` | 297.047 us / 83,561 B | 41.343 us | 266.384 us / 83,529 B | 42.232 us |
| `literal` | 238.912 us / 50,369 B | 25.197 us | 312.616 us / 50,337 B | 25.366 us |
| `unicode` | 55.487 us / 48,313 B | 36.357 us | 59.897 us / 48,281 B | 35.139 us |

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
