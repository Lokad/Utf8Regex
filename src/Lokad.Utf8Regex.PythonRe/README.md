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

Snapshot SHA-256: `9F2DEC5B1DC741569E7E83866B74ED4B629B5C3514151B63F16E7090E5AACFA2`. Catalog SHA-256: `1345C0F6C42558E3F64186353C2DCA0A4506DA24155E69336C7945C78D563450`.

Public Status: `44` managed faster, `1` equivalent, `35` CPython faster, `6` inconclusive, `4` unqualified.

Measured source `b478f853f722`: .NET 11.0.0-rc.3.26522.148; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `literal/search-early` | `Search` | 0.683 us | 0.235 us | CPython faster | 2.91x |
| `common/date-miss` | `IsMatch` | 1.864 us | 0.866 us | CPython faster | 2.16x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.368 us | 0.198 us | CPython faster | 1.85x |
| `leading-inline/search-hit` | `Search` | 0.606 us | 0.336 us | CPython faster | 1.80x |
| `atomic/search-miss` | `Search` | 0.452 us | 0.262 us | CPython faster | 1.72x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.484 us | 0.296 us | CPython faster | 1.64x |
| `lookbehind/search-miss` | `Search` | 0.321 us | 0.219 us | CPython faster | 1.46x |
| `possessive/search-hit` | `Search` | 0.450 us | 0.308 us | CPython faster | 1.46x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.263 us | 0.185 us | CPython faster | 1.42x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.401 us | 0.283 us | CPython faster | 1.42x |
| `reluctant/search-hit` | `Search` | 0.483 us | 0.352 us | CPython faster | 1.37x |
| `atomic/search-hit` | `Search` | 0.442 us | 0.326 us | CPython faster | 1.36x |
| `prefix/match` | `Match` | 0.327 us | 0.274 us | CPython faster | 1.19x |
| `scoped-inline/search-hit` | `Search` | 0.346 us | 0.293 us | CPython faster | 1.18x |
| `unicode-option/search-hit` | `Search` | 0.320 us | 0.302 us | CPython faster | 1.06x |
| `multiline/search-miss` | `Search` | 0.280 us | 0.267 us | CPython faster | 1.05x |
| `lookbehind/search-hit` | `Search` | 0.344 us | 0.333 us | CPython faster | 1.03x |
| `unicode/fullmatch` | `FullMatch` | 17.392 us | 19.787 us | Managed faster | 0.88x |
| `ignorecase/search-hit` | `Search` | 0.313 us | 0.358 us | Managed faster | 0.87x |
| `ascii-option/search-hit` | `Search` | 0.258 us | 0.307 us | Managed faster | 0.84x |
| `multiline/search-hit` | `Search` | 0.260 us | 0.335 us | Managed faster | 0.77x |
| `ignorecase/search-miss` | `Search` | 0.207 us | 0.288 us | Managed faster | 0.72x |
| `greedy/search-miss` | `Search` | 0.146 us | 0.221 us | Managed faster | 0.66x |
| `lookahead/search-hit` | `Search` | 0.186 us | 0.301 us | Managed faster | 0.62x |
| `possessive/search-miss` | `Search` | 0.128 us | 0.213 us | Managed faster | 0.60x |
| `unicode-literal/search-hit` | `Search` | 0.130 us | 0.237 us | Managed faster | 0.55x |
| `unicode-literal/search-miss` | `Search` | 0.144 us | 0.292 us | Managed faster | 0.49x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.148 us | 0.338 us | Managed faster | 0.44x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.099 us | 0.257 us | Managed faster | 0.38x |
| `literal/fullmatch` | `FullMatch` | 0.084 us | 0.350 us | Managed faster | 0.24x |
| `common/ip-match` | `IsMatch` | 0.058 us | 0.321 us | Managed faster | 0.18x |
| `literal/search` | `Search` | 2.732 us | 22.757 us | Managed faster | 0.12x |
| `literal/ismatch` | `IsMatch` | 2.484 us | 22.395 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.676 us | 22.340 us | Managed faster | 0.07x |
| `alternation/search-miss` | `Search` | 0.208 us | 0.238 us | Unqualified | — |
| `lookahead/search-miss` | `Search` | 0.202 us | 0.197 us | Unqualified | — |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.208 us | 0.239 us | Unqualified | — |
| `unicode-category/search-miss` | `Search` | 0.181 us | 0.213 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 1.042 us | 1.311 us | Managed faster | 0.80x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/many-capture-strings` | `FindAllStrings` | 778.585 us | 349.767 us | CPython faster | 2.23x |
| `findall/one-capture-strings` | `FindAllStrings` | 474.299 us | 213.981 us | CPython faster | 2.22x |
| `findall/named-capture-strings` | `FindAllStrings` | 169.995 us | 96.107 us | CPython faster | 1.77x |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 97.680 us | 62.455 us | CPython faster | 1.56x |
| `findall/empty-progression-strings` | `FindAllStrings` | 66.029 us | 43.741 us | CPython faster | 1.51x |
| `findall/full-strings` | `FindAllStrings` | 530.781 us | 370.538 us | CPython faster | 1.43x |
| `findall/unicode-full-strings` | `FindAllStrings` | 38.923 us | 27.515 us | CPython faster | 1.42x |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | 17.983 us | 16.172 us | CPython faster | 1.11x |
| `findall/optional-capture-strings` | `FindAllStrings` | 59.120 us | 55.455 us | CPython faster | 1.07x |
| `findall/full-utf8` | `FindAllUtf8` | 551.423 us | 519.708 us | CPython faster | 1.06x |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 549.989 us | 620.407 us | Managed faster | 0.89x |
| `findall/structural-many` | `FindAllStructural` | 177.846 us | 202.740 us | Managed faster | 0.88x |
| `findall/many-capture-utf8` | `FindAllUtf8` | 863.612 us | 1,249.218 us | Managed faster | 0.69x |
| `zero-width/count` | `Count` | 584.486 us | 1,118.633 us | Managed faster | 0.52x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 274.866 us | 541.495 us | Managed faster | 0.51x |
| `count/empty-progression` | `Count` | 179.155 us | 380.373 us | Managed faster | 0.47x |
| `count/from-offset` | `CountFromOffset` | 14.782 us | 50.230 us | Managed faster | 0.29x |
| `family/count` | `Count` | 651.949 us | 2,473.242 us | Managed faster | 0.26x |
| `count/one` | `Count` | 0.284 us | 2.018 us | Managed faster | 0.14x |
| `unicode/count` | `Count` | 58.042 us | 436.905 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 276.963 us | 2,577.560 us | Managed faster | 0.11x |
| `findall/one-strings` | `FindAllStrings` | 0.783 us | 38.894 us | Managed faster | 0.02x |
| `findall/zero-strings` | `FindAllStrings` | 0.549 us | 90.761 us | Managed faster | 0.01x |
| `count/zero` | `Count` | 0.398 us | 91.227 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.260 us | 142.470 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.178 us | 0.464 us | Inconclusive | 2.57x |
| `replacement/empty-progression-string` | `ReplaceString` | 37.486 us | 23.650 us | CPython faster | 1.58x |
| `replacement/named-template-string` | `ReplaceString` | 1.262 us | 0.908 us | CPython faster | 1.39x |
| `replacement/fixed-string` | `ReplaceString` | 189.588 us | 177.655 us | CPython faster | 1.07x |
| `replacement/subn-string` | `SubnString` | 94.159 us | 91.337 us | CPython faster | 1.03x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 179.631 us | 179.656 us | Equivalent | 1.00x |
| `replacement/subn-utf8` | `SubnUtf8` | 89.629 us | 92.213 us | Managed faster | 0.97x |
| `replacement/unmatched-template-string` | `ReplaceString` | 48.670 us | 51.225 us | Managed faster | 0.95x |
| `replacement/no-match-string` | `ReplaceString` | 1.229 us | 2.421 us | Inconclusive | 0.51x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 158.990 us | 4,343.207 us | Managed faster | 0.04x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 620.232 us | 17,333.859 us | Managed faster | 0.04x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 596.460 us | 17,240.424 us | Managed faster | 0.03x |
| `replacement/one-template-string` | `ReplaceString` | 2.103 us | 16,554.525 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.491 us | 0.547 us | Inconclusive | 2.72x |
| `split/zero-width` | `SplitStrings` | 116.909 us | 67.634 us | CPython faster | 1.74x |
| `split/captures` | `SplitStrings` | 440.259 us | 255.708 us | CPython faster | 1.72x |
| `split/unicode` | `SplitStrings` | 70.458 us | 44.696 us | CPython faster | 1.58x |
| `split/no-captures` | `SplitStrings` | 278.129 us | 225.171 us | CPython faster | 1.24x |
| `split/optional-capture` | `SplitStrings` | 75.479 us | 82.477 us | Managed faster | 0.92x |
| `split/detailed` | `SplitDetailed` | 113.302 us | 136.648 us | Managed faster | 0.83x |
| `split/one-separator` | `SplitStrings` | 1.220 us | 12.057 us | Inconclusive | 0.10x |
| `split/no-separator` | `SplitStrings` | 1.291 us | 25.059 us | Inconclusive | 0.05x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 190.242 us | 401.647 us | Managed faster | 0.47x |
| `corpus/code-declarations` | `Count` | 85.062 us | 630.171 us | Managed faster | 0.14x |
| `corpus/code-log-methods` | `Count` | 48.510 us | 581.600 us | Managed faster | 0.08x |
| `corpus/mail-email-count` | `Count` | 2,088.258 us | 359,250.660 us | Managed faster | 0.01x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `alternation/search-miss`: Removable harness floors are 0.23 %/14.31 % of the lanes.
- `lookahead/search-miss`: Removable harness floors are 0.20 %/17.12 % of the lanes.
- `search-offset/literal-hit`: Removable harness floors are 0.20 %/14.28 % of the lanes.
- `unicode-category/search-miss`: Removable harness floors are 0.22 %/16.06 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 196.200 us / 71,633 B | 59.990 us | 189.056 us / 71,657 B | 60.709 us |
| `fallback` | 275.872 us / 83,577 B | 42.471 us | 263.172 us / 83,545 B | 42.546 us |
| `literal` | 416.041 us / 50,385 B | 24.721 us | 319.616 us / 50,353 B | 24.801 us |
| `unicode` | 54.975 us / 48,329 B | 36.223 us | 58.131 us / 48,297 B | 36.214 us |

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
