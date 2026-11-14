# Lokad.Utf8Regex.PythonRe

**0.5.0 development**, .NET 10 and .NET 11. A managed Python `re`-style adapter
over strict UTF-8 Unicode scalars, requiring the exact Core package version
matching the adapter. It can coexist with PCRE2 at the same version. No Python
runtime or BCL regex matcher is a shipped implementation dependency.

## Subject text contract

This profile models Python `re` over `str`. Pass already-valid UTF-8 text and keep
borrowed bytes unchanged during use. Validate external bytes where they become
text. Regex operations do not validate the whole subject, and there is no
`inputValidation` option or `InputValidation` property in this adapter.

Pattern, template and capture-name validation remain separate. Managed bounds,
scalar/byte semantics, captures, original cooperative cancellation/deadlines and
resource admission remain. Requested coordinates and output sizing may inspect
text. See the [profile contract](../../docs/SPEC-PYTHONRE.md#subject-text-contract).

The tables below compare existing valid UTF-8 text with CPython str, with equal
requested output materialization. Compilation, input decoding and worker startup
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev89-20261114/README.md).

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
**Defaults remain unlimited.** Work includes matching and
required shaping cumulatively. Depth admits live frames before acquisition;
working/result controls admit modeled major capacities and growth overlap.
Result/staging charges do not refund and may conservatively count storage again.
Unknown short string lengths can charge a UTF-8-byte-derived capacity bound before
decoding; Unicode results can therefore reach a finite byte limit earlier.
Denials use `Utf8ResourceLimitExceededException` with the corresponding
`Execution*` resource kind. This is not a total heap/RSS or caller-retention bound.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

Original synchronous cancellation and one absolute
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
Detailed results and scalar queries share a lazy coordinate index through requested
captures, including lookarounds. They do not prepare an unused suffix. The index
uses admitted working storage and is refunded synchronously; published arrays and
strings retain their separate result charges. Distant scalar-only queries now store
both scalar and UTF-16 prefix counts, so their working requirements can increase;
short and ASCII-only projections need no index allocation.
`SplitRanges` provides ordinary segments and captured separators as byte ranges,
including unmatched captures, without decoding or copying their text. These paths
retain finite limits and synchronous cancellation.

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

Use `--measure-pythonre-paired-case findall/many-capture-strings 9` for the
canonical preexisting-text comparison. The command prints labeled paired evidence
without changing the recorded tables. There is no validation-policy matrix.

The recorded callback replacement rows time returning a prepared constant
replacement, including each API's required argument/output construction. Rich
callback capture checks and digests run in a separate untimed verification pass.
See the [timing correction](../../bench/Experiments/PythonCallbackTiming-20261113/README.md);
earlier instrumented callback ratios are not bare replacement-throughput claims.

The canonical comparison uses preexisting valid text on both sides. Value rows
include byte/UTF-16 range projection; detailed and structural rows include their
requested ranges, captures and text. CPython performs that conversion within
each timed operation, sharing work across bulk results. These are public-output
costs, rather than bare `re.Match` discovery timings. Boolean, count, string,
callback and split rows keep their existing timing contract. See the
[comparison correction](../../bench/Experiments/PythonCoordinateFairness-20261114/README.md).

Scaling rows now use the same timed coordinate projection as the canonical
comparison. The complete refresh records their current source and protocol;
older receipts retain their original preparation rules.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `E972150D8359C36ED1CC56EEBE13469726EEDD0C2BEC3AE6FD0F20CE75F1F52C`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `51` managed faster, `2` equivalent, `28` CPython faster, `7` inconclusive, `2` unqualified.

Measured source `e8b20aa51f5c`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.884 us | 0.855 us | CPython faster | 2.20x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.366 us | 0.221 us | CPython faster | 1.66x |
| `atomic/search-miss` | `Search` | 0.460 us | 0.285 us | CPython faster | 1.61x |
| `lookbehind/search-miss` | `Search` | 0.321 us | 0.243 us | CPython faster | 1.32x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.250 us | 0.204 us | CPython faster | 1.23x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.478 us | 0.407 us | CPython faster | 1.17x |
| `possessive/search-hit` | `Search` | 0.475 us | 0.418 us | CPython faster | 1.14x |
| `atomic/search-hit` | `Search` | 0.467 us | 0.433 us | CPython faster | 1.08x |
| `reluctant/search-hit` | `Search` | 0.484 us | 0.460 us | CPython faster | 1.05x |
| `multiline/search-miss` | `Search` | 0.300 us | 0.288 us | CPython faster | 1.04x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.413 us | 0.398 us | CPython faster | 1.04x |
| `leading-inline/search-hit` | `Search` | 0.383 us | 0.442 us | Managed faster | 0.87x |
| `scoped-inline/search-hit` | `Search` | 0.346 us | 0.401 us | Managed faster | 0.86x |
| `lookbehind/search-hit` | `Search` | 0.359 us | 0.443 us | Managed faster | 0.81x |
| `alternation/search-miss` | `Search` | 0.195 us | 0.260 us | Managed faster | 0.75x |
| `ignorecase/search-hit` | `Search` | 0.336 us | 0.466 us | Managed faster | 0.72x |
| `ignorecase/search-miss` | `Search` | 0.217 us | 0.308 us | Managed faster | 0.70x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.216 us | 0.337 us | Managed faster | 0.64x |
| `multiline/search-hit` | `Search` | 0.282 us | 0.442 us | Managed faster | 0.64x |
| `ascii-option/search-hit` | `Search` | 0.247 us | 0.421 us | Managed faster | 0.59x |
| `greedy/search-miss` | `Search` | 0.142 us | 0.242 us | Managed faster | 0.59x |
| `possessive/search-miss` | `Search` | 0.130 us | 0.234 us | Managed faster | 0.55x |
| `lookahead/search-hit` | `Search` | 0.215 us | 0.407 us | Managed faster | 0.53x |
| `unicode-option/search-hit` | `Search` | 0.343 us | 1.175 us | Managed faster | 0.29x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.123 us | 0.434 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.071 us | 0.319 us | Managed faster | 0.22x |
| `literal/search-early` | `Search` | 0.072 us | 0.335 us | Managed faster | 0.22x |
| `prefix/match` | `Match` | 0.067 us | 0.384 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.055 us | 0.333 us | Managed faster | 0.16x |
| `unicode/fullmatch` | `FullMatch` | 4.812 us | 30.040 us | Managed faster | 0.16x |
| `literal/fullmatch` | `FullMatch` | 0.059 us | 0.459 us | Managed faster | 0.13x |
| `literal/search` | `Search` | 2.016 us | 22.714 us | Managed faster | 0.09x |
| `unicode-literal/search-hit` | `Search` | 0.081 us | 1.246 us | Managed faster | 0.06x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.065 us | 1.152 us | Managed faster | 0.06x |
| `literal/ismatch` | `IsMatch` | 1.129 us | 22.389 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.089 us | 22.497 us | Managed faster | 0.05x |
| `lookahead/search-miss` | `Search` | 0.201 us | 0.218 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.202 us | 0.239 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.897 us | 2.052 us | Managed faster | 0.44x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 402.027 us | 218.011 us | CPython faster | 1.84x |
| `findall/many-capture-strings` | `FindAllStrings` | 631.082 us | 346.840 us | CPython faster | 1.82x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 88.432 us | 62.104 us | CPython faster | 1.42x |
| `findall/named-capture-strings` | `FindAllStrings` | 125.134 us | 88.745 us | CPython faster | 1.41x |
| `findall/empty-progression-strings` | `FindAllStrings` | 57.268 us | 45.423 us | CPython faster | 1.26x |
| `findall/full-strings` | `FindAllStrings` | 405.265 us | 350.084 us | CPython faster | 1.16x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.203 us | 15.975 us | CPython faster | 1.08x |
| `findall/optional-capture-strings` | `FindAllStrings` | 49.538 us | 55.736 us | Managed faster | 0.89x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 511.783 us | 619.171 us | Managed faster | 0.82x |
| `findall/full-utf8` | `FindAllUtf8` | 379.711 us | 523.390 us | Managed faster | 0.73x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.413 us | 27.493 us | Managed faster | 0.67x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 632.837 us | 1,247.338 us | Managed faster | 0.51x |
| `zero-width/count` | `Count` | 508.354 us | 1,048.884 us | Managed faster | 0.48x |
| `findall/structural-many` | `FindAllStructural` | 122.246 us | 277.140 us | Managed faster | 0.44x |
| `count/empty-progression` | `Count` | 120.463 us | 339.429 us | Managed faster | 0.36x |
| `count/from-offset` | `CountFromOffset` | 14.459 us | 49.615 us | Managed faster | 0.29x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 216.435 us | 832.920 us | Managed faster | 0.26x |
| `family/count` | `Count` | 541.629 us | 2,255.892 us | Managed faster | 0.24x |
| `count/one` | `Count` | 0.266 us | 1.989 us | Managed faster | 0.13x |
| `unicode/count` | `Count` | 50.139 us | 424.870 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 280.174 us | 2,424.005 us | Managed faster | 0.12x |
| `findall/one-strings` | `FindAllStrings` | 0.770 us | 38.909 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.396 us | 90.689 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.361 us | 91.295 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.192 us | 143.573 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.157 us | 0.455 us | Inconclusive | 2.53x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 120.069 us | 64.635 us | CPython faster | 1.86x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 472.733 us | 279.148 us | CPython faster | 1.70x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 467.766 us | 277.786 us | CPython faster | 1.69x |
| `replacement/named-template-string` | `ReplaceString` | 1.094 us | 0.923 us | Inconclusive | 1.19x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.324 us | 23.877 us | CPython faster | 1.06x |
| `replacement/fixed-string` | `ReplaceString` | 188.752 us | 178.294 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 94.580 us | 90.447 us | CPython faster | 1.04x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.690 us | 90.501 us | Equivalent | 0.99x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 179.090 us | 180.725 us | Equivalent | 0.99x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.596 us | 47.764 us | Managed faster | 0.87x |
| `replacement/no-match-string` | `ReplaceString` | 1.196 us | 2.424 us | Inconclusive | 0.49x |
| `replacement/one-template-string` | `ReplaceString` | 1.889 us | 16,317.313 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.572 us | 0.547 us | Inconclusive | 2.82x |
| `split/zero-width` | `SplitStrings` | 107.349 us | 66.649 us | CPython faster | 1.61x |
| `split/unicode` | `SplitStrings` | 59.796 us | 39.660 us | CPython faster | 1.51x |
| `split/captures` | `SplitStrings` | 350.438 us | 257.638 us | CPython faster | 1.37x |
| `split/no-captures` | `SplitStrings` | 253.492 us | 225.917 us | CPython faster | 1.12x |
| `split/optional-capture` | `SplitStrings` | 71.281 us | 89.598 us | Managed faster | 0.80x |
| `split/detailed` | `SplitDetailed` | 95.185 us | 136.670 us | Managed faster | 0.70x |
| `split/one-separator` | `SplitStrings` | 1.346 us | 12.060 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.430 us | 25.028 us | Inconclusive | 0.06x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 70.368 us | 395.910 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 83.857 us | 627.791 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 47.768 us | 581.687 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,084.026 us | 359,421.010 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `lookahead/search-miss`: Removable harness floors are 0.20 %/15.73 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.20 %/14.30 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 139.803 us / 67,593 B | 60.291 us | 138.762 us / 67,617 B | 61.028 us |
| `fallback` | 274.000 us / 79,753 B | 42.595 us | 182.353 us / 79,721 B | 42.727 us |
| `literal` | 232.056 us / 50,377 B | 25.300 us | 208.297 us / 50,345 B | 25.265 us |
| `unicode` | 90.662 us / 48,321 B | 36.563 us | 82.128 us / 48,289 B | 36.565 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
