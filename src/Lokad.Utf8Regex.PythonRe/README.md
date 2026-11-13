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
are excluded. See the [current delivery](../../bench/Experiments/DeliveryDev80-20261113/README.md).

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

Use `--measure-pythonre-paired-case findall/many-capture-strings 9` for the
canonical preexisting-text comparison. The command prints labeled paired evidence
without changing the recorded tables. There is no validation-policy matrix.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [summary and raw-evidence reference](../../bench/Snapshots/PythonRe.json).

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

Snapshot SHA-256: `FA175D7278D6FD8F4515F312483A91EFBD4A9F80716863565A46207F5C27C47E`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `50` managed faster, `0` equivalent, `29` CPython faster, `7` inconclusive, `4` unqualified.

Measured source `15b248dc3f74`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 1.790 us | 0.848 us | CPython faster | 2.10x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.373 us | 0.194 us | CPython faster | 1.92x |
| `atomic/search-miss` | `Search` | 0.443 us | 0.257 us | CPython faster | 1.73x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.495 us | 0.296 us | CPython faster | 1.68x |
| `possessive/search-hit` | `Search` | 0.470 us | 0.309 us | CPython faster | 1.52x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.419 us | 0.286 us | CPython faster | 1.46x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.258 us | 0.177 us | CPython faster | 1.46x |
| `reluctant/search-hit` | `Search` | 0.514 us | 0.353 us | CPython faster | 1.46x |
| `atomic/search-hit` | `Search` | 0.464 us | 0.326 us | CPython faster | 1.42x |
| `lookbehind/search-miss` | `Search` | 0.300 us | 0.214 us | CPython faster | 1.40x |
| `scoped-inline/search-hit` | `Search` | 0.346 us | 0.294 us | CPython faster | 1.18x |
| `leading-inline/search-hit` | `Search` | 0.391 us | 0.337 us | CPython faster | 1.16x |
| `unicode-option/search-hit` | `Search` | 0.347 us | 0.301 us | CPython faster | 1.15x |
| `multiline/search-miss` | `Search` | 0.286 us | 0.260 us | CPython faster | 1.10x |
| `lookbehind/search-hit` | `Search` | 0.346 us | 0.336 us | CPython faster | 1.03x |
| `multiline/search-hit` | `Search` | 0.294 us | 0.338 us | Managed faster | 0.87x |
| `alternation/search-miss` | `Search` | 0.187 us | 0.232 us | Managed faster | 0.81x |
| `ascii-option/search-hit` | `Search` | 0.240 us | 0.309 us | Managed faster | 0.78x |
| `ignorecase/search-miss` | `Search` | 0.203 us | 0.280 us | Managed faster | 0.73x |
| `lookahead/search-hit` | `Search` | 0.210 us | 0.306 us | Managed faster | 0.69x |
| `greedy/search-miss` | `Search` | 0.136 us | 0.214 us | Managed faster | 0.64x |
| `possessive/search-miss` | `Search` | 0.121 us | 0.205 us | Managed faster | 0.59x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.125 us | 0.337 us | Managed faster | 0.37x |
| `unicode-literal/search-hit` | `Search` | 0.081 us | 0.240 us | Managed faster | 0.34x |
| `literal/search-early` | `Search` | 0.072 us | 0.232 us | Managed faster | 0.31x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.065 us | 0.262 us | Managed faster | 0.25x |
| `unicode-literal/search-miss` | `Search` | 0.071 us | 0.289 us | Managed faster | 0.24x |
| `prefix/match` | `Match` | 0.067 us | 0.279 us | Managed faster | 0.24x |
| `unicode/fullmatch` | `FullMatch` | 3.748 us | 18.978 us | Managed faster | 0.20x |
| `literal/fullmatch` | `FullMatch` | 0.058 us | 0.346 us | Managed faster | 0.17x |
| `common/ip-match` | `IsMatch` | 0.053 us | 0.333 us | Managed faster | 0.16x |
| `literal/search` | `Search` | 2.026 us | 22.571 us | Managed faster | 0.09x |
| `literal/ismatch` | `IsMatch` | 1.124 us | 22.375 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.097 us | 22.425 us | Managed faster | 0.05x |
| `ignorecase/search-hit` | `Search` | 0.344 us | 0.358 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.193 us | 0.189 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.217 us | 0.240 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.190 us | 0.208 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.901 us | 1.316 us | Managed faster | 0.69x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/one-capture-strings` | `FindAllStrings` | 442.147 us | 218.111 us | CPython faster | 2.03x |
| `findall/many-capture-strings` | `FindAllStrings` | 674.591 us | 344.907 us | CPython faster | 1.96x |
| `findall/named-capture-strings` | `FindAllStrings` | 144.097 us | 96.973 us | CPython faster | 1.48x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 84.734 us | 62.309 us | CPython faster | 1.36x |
| `findall/full-strings` | `FindAllStrings` | 456.068 us | 377.198 us | CPython faster | 1.21x |
| `findall/empty-progression-strings` | `FindAllStrings` | 49.990 us | 45.264 us | CPython faster | 1.11x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 16.373 us | 15.932 us | CPython faster | 1.03x |
| `findall/optional-capture-strings` | `FindAllStrings` | 53.551 us | 54.734 us | Managed faster | 0.98x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 511.402 us | 623.251 us | Managed faster | 0.82x |
| `findall/full-utf8` | `FindAllUtf8` | 409.168 us | 518.371 us | Managed faster | 0.79x |
| `findall/structural-many` | `FindAllStructural` | 134.377 us | 194.049 us | Managed faster | 0.69x |
| `findall/unicode-full-strings` | `FindAllStrings` | 18.231 us | 27.796 us | Managed faster | 0.66x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 660.962 us | 1,252.426 us | Managed faster | 0.53x |
| `zero-width/count` | `Count` | 570.265 us | 1,102.603 us | Managed faster | 0.52x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 225.008 us | 533.931 us | Managed faster | 0.42x |
| `count/empty-progression` | `Count` | 126.157 us | 362.643 us | Managed faster | 0.35x |
| `count/from-offset` | `CountFromOffset` | 14.307 us | 50.361 us | Managed faster | 0.28x |
| `family/count` | `Count` | 542.826 us | 2,454.972 us | Managed faster | 0.22x |
| `count/one` | `Count` | 0.282 us | 2.009 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 49.524 us | 435.825 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 280.685 us | 2,582.718 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.775 us | 38.924 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.389 us | 90.303 us | Managed faster | 0.00x |
| `count/zero` | `Count` | 0.364 us | 91.279 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.181 us | 140.499 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.105 us | 0.456 us | Inconclusive | 2.43x |
| `replacement/named-template-string` | `ReplaceString` | 1.102 us | 0.907 us | Inconclusive | 1.21x |
| `replacement/empty-progression-string` | `ReplaceString` | 25.596 us | 23.567 us | CPython faster | 1.08x |
| `replacement/fixed-string` | `ReplaceString` | 186.472 us | 175.992 us | CPython faster | 1.06x |
| `replacement/subn-string` | `SubnString` | 93.510 us | 90.930 us | CPython faster | 1.03x |
| `replacement/subn-utf8` | `SubnUtf8` | 88.468 us | 91.263 us | Managed faster | 0.97x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 176.139 us | 185.967 us | Managed faster | 0.94x |
| `replacement/unmatched-template-string` | `ReplaceString` | 42.468 us | 47.001 us | Managed faster | 0.91x |
| `replacement/no-match-string` | `ReplaceString` | 1.169 us | 2.416 us | Inconclusive | 0.48x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 139.485 us | 4,286.243 us | Managed faster | 0.03x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 554.566 us | 17,311.538 us | Managed faster | 0.03x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 542.197 us | 17,137.354 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 1.923 us | 16,487.230 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.660 us | 0.546 us | Inconclusive | 3.02x |
| `split/zero-width` | `SplitStrings` | 118.181 us | 66.941 us | CPython faster | 1.77x |
| `split/captures` | `SplitStrings` | 397.202 us | 264.061 us | CPython faster | 1.50x |
| `split/unicode` | `SplitStrings` | 59.073 us | 39.444 us | CPython faster | 1.49x |
| `split/no-captures` | `SplitStrings` | 237.337 us | 231.592 us | CPython faster | 1.03x |
| `split/optional-capture` | `SplitStrings` | 77.003 us | 82.790 us | Managed faster | 0.93x |
| `split/detailed` | `SplitDetailed` | 106.685 us | 136.054 us | Managed faster | 0.78x |
| `split/one-separator` | `SplitStrings` | 1.282 us | 11.943 us | Inconclusive | 0.11x |
| `split/no-separator` | `SplitStrings` | 1.285 us | 24.937 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 70.035 us | 401.622 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 85.374 us | 628.782 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 48.727 us | 583.160 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 1,115.820 us | 358,565.950 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `ignorecase/search-hit`: Removable harness floors are 0.13 %/9.39 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.21 %/18.41 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.19 %/14.20 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.21 %/16.54 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 147.175 us / 67,593 B | 60.549 us | 139.506 us / 67,617 B | 60.414 us |
| `fallback` | 284.569 us / 79,753 B | 42.100 us | 193.078 us / 79,721 B | 42.275 us |
| `literal` | 237.131 us / 50,377 B | 24.719 us | 211.669 us / 50,345 B | 25.202 us |
| `unicode` | 89.513 us / 48,321 B | 35.597 us | 82.963 us / 48,289 B | 35.640 us |

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
