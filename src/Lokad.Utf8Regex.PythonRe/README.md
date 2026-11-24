# Lokad.Utf8Regex.PythonRe

A managed implementation of selected CPython 3.13.2 `re` semantics over
`str` for .NET 10 and 11. The adapter requires the exact matching Core
package and uses pinned Unicode 15.1 data.

## Subject text contract

Pass valid UTF-8 and keep borrowed bytes unchanged during use. Validate
external bytes as they become text. Patterns, templates and capture names
have their own checks. Match starts, ends and captures use byte offsets.
[Input and byte bounds](../../docs/SPEC-PYTHONRE.md#subject-text-contract)

## Supported scope

Use `Match` for one anchored result by default. Pass
`Utf8PythonMatchMode.Search` to seek from the start offset, or
`Utf8PythonMatchMode.FullMatch` to require the selected range. For repeated
matches use `Count` or `CreateMatchCursor`. Capture numbers follow opening
order, and `LastIndex` reports the last participating capture to close.
[Operations and result shape](../../docs/SPEC-PYTHONRE.md#operations-and-result-shape)

`Replace` and `Subn` accept byte templates, with the substitution count
available from `Subn`. `SplitRanges` returns segment and captured-separator
ranges. Decode or copy selected bytes in the host for text output. The
profile excludes bytes patterns, LOCALE and `\N{...}` Unicode-name lookup.
[Grammar and parsing](../../docs/SPEC-PYTHONRE.md#grammar-and-parsing)

## Controls and lifetimes

`Utf8RegexPolicy` sets match timeouts and optional work, depth, working-byte
and result-byte limits. Construction has separate limits and a deadline.
`Compile` also accepts a construction token. Calls and cursor advances
observe cancellation and the original match deadline. A cursor's advance
token applies to that move. [Construction and execution controls](../../docs/SPEC-PYTHONRE.md#construction-and-execution-controls)

Dispose a funded regex when finished. Admitted calls and memory cursors can
finish after close. Cursor aliases share progress and disposal. Dispose an
abandoned cursor and keep the borrowed subject alive and unchanged while
reading published matches. [Cursor and result lifetime](../../docs/SPEC-PYTHONRE.md#cursor-and-result-lifetime)

## Public API

```csharp
using System;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

var regex = new Utf8PythonRegex(
    pattern: @"(?P<word>foo)-(?P=word)",
    options: PythonReCompileOptions.None,
    policy: new Utf8RegexPolicy
    {
        MatchTimeout = TimeSpan.FromSeconds(1),
        ResourcePolicy = new()
        {
            MaxWorkSteps = 1_000_000,
            MaxDepth = 1024,
            MaxWorkingBytes = 16 * 1024 * 1024,
            MaxResultBytes = 16 * 1024 * 1024
        }
    });

bool isMatch = regex.IsMatch("foo-foo"u8);
var first = regex.Match("xx foo-foo yy"u8, mode: Utf8PythonMatchMode.Search);
int count = regex.Count("foo-foo x foo-foo"u8);
byte[] replaced = regex.Replace("foo-foo x foo-foo"u8, "<\\g<word>>"u8);
using var matches = regex.CreateMatchCursor("foo-foo xx foo-foo"u8.ToArray());
while (matches.MoveNext(out var match))
{
    Console.WriteLine($"{match.Value.StartOffsetInBytes}:{match.Value.EndOffsetInBytes}");
}
Utf8PythonCaptureRange[] parts = regex.SplitRanges("foo-foo xx foo-foo"u8);
```

Use `Match` with caller-owned capture slots for one result. Use the range
cursor for immutable capture snapshots across calls. The caller owns any
collection or decoding of those results.

```csharp
ReadOnlyMemory<byte> subject = System.Text.Encoding.UTF8.GetBytes("é😀");
using var cursor = new Utf8PythonRegex("(?P<scalar>.)").CreateMatchCursor(subject);
while (cursor.MoveNext(out var match))
{
    // The supplementary scalar occupies four bytes and is one match.
    Console.WriteLine(System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
}
```

[Profile specification](../../docs/SPEC-PYTHONRE.md) · [Release notes](../../CHANGELOG.md)

Use `--measure-pythonre-paired-case iteration/finditer-detailed 9` for a focused
paired comparison over already-valid text. Timing includes equivalent requested
output. The benchmark host collects and decodes byte ranges for detailed, structural
and string split results. It composes constant callback replacement over the range
cursor and decodes byte-template replacement when string output is requested.
CPython includes the corresponding coordinate and output conversion. Capture replay
and digest verification run outside those measurements.

<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->
## Benchmarks

Public PythonRe operations over caller-valid UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation. The paired pass includes required result materialization and measures after startup and pattern compilation. CPython uses a long-lived worker. Use `--measure-pythonre-case` for focused decode-inclusive and .NET controls.

Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.

FindAllStrings, FindAllUtf8 and FindAllStringsFromOffset rows are historical measurements of the removed FindAllToUtf8 API. They are no longer refreshed and do not qualify current cursor materialization. Structural rows compose the retained cursor in benchmark host code.

Snapshot SHA-256: `D4F0D7285DE58A29D3ACD51CE1FE711A557EDC6EF37C86D9F97581FAF3F20ED5`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `63` managed faster, `0` equivalent, `1` CPython faster, `7` inconclusive, `5` unqualified.

Measured source `2f863b8b1adf`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 0.681 us | 0.837 us | Managed faster | 0.82x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.315 us | 0.397 us | Managed faster | 0.79x |
| `reluctant/search-hit` | `Search` | 0.269 us | 0.447 us | Managed faster | 0.60x |
| `leading-inline/search-hit` | `Search` | 0.264 us | 0.446 us | Managed faster | 0.59x |
| `atomic/search-hit` | `Search` | 0.254 us | 0.432 us | Managed faster | 0.59x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.211 us | 0.361 us | Managed faster | 0.58x |
| `lookahead/search-miss` | `Search` | 0.123 us | 0.214 us | Managed faster | 0.57x |
| `possessive/search-hit` | `Search` | 0.228 us | 0.411 us | Managed faster | 0.55x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.218 us | 0.403 us | Managed faster | 0.54x |
| `scoped-inline/search-hit` | `Search` | 0.214 us | 0.404 us | Managed faster | 0.53x |
| `unicode-category/search-miss` | `Search` | 0.116 us | 0.230 us | Managed faster | 0.50x |
| `prefix/match` | `Match` | 0.183 us | 0.369 us | Managed faster | 0.50x |
| `lookbehind/search-hit` | `Search` | 0.218 us | 0.443 us | Managed faster | 0.49x |
| `multiline/search-hit` | `Search` | 0.208 us | 0.445 us | Managed faster | 0.47x |
| `alternation/search-miss` | `Search` | 0.115 us | 0.256 us | Managed faster | 0.45x |
| `ignorecase/search-hit` | `Search` | 0.207 us | 0.475 us | Managed faster | 0.43x |
| `ignorecase/search-miss` | `Search` | 0.132 us | 0.305 us | Managed faster | 0.43x |
| `unicode-option/search-hit` | `Search` | 0.211 us | 0.622 us | Managed faster | 0.34x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.112 us | 0.329 us | Managed faster | 0.34x |
| `ascii-option/search-hit` | `Search` | 0.145 us | 0.428 us | Managed faster | 0.34x |
| `possessive/search-miss` | `Search` | 0.078 us | 0.230 us | Managed faster | 0.34x |
| `greedy/search-miss` | `Search` | 0.086 us | 0.254 us | Managed faster | 0.34x |
| `atomic/search-miss` | `Search` | 0.086 us | 0.280 us | Managed faster | 0.31x |
| `lookahead/search-hit` | `Search` | 0.115 us | 0.395 us | Managed faster | 0.29x |
| `unicode-literal/search-miss` | `Search` | 0.069 us | 0.304 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.064 us | 0.308 us | Managed faster | 0.21x |
| `unicode/fullmatch` | `FullMatch` | 3.488 us | 25.810 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.052 us | 0.461 us | Managed faster | 0.11x |
| `unicode-literal/search-hit` | `Search` | 0.062 us | 0.603 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.053 us | 0.576 us | Managed faster | 0.09x |
| `literal/search-miss` | `Search` | 1.047 us | 21.681 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.061 us | 22.237 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.030 us | 21.698 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.184 us | 0.203 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.210 us | 0.216 us | Unqualified | — |
| `common/ip-match` | `IsMatch` | 0.300 us | 0.311 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.229 us | 0.239 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.243 us | 0.281 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.594 us | 1.633 us | Managed faster | 0.36x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 84.719 us | 212.297 us | Managed faster | 0.40x |
| `count/from-offset` | `CountFromOffset` | 16.630 us | 47.229 us | Managed faster | 0.35x |
| `zero-width/count` | `Count` | 351.214 us | 1,062.334 us | Managed faster | 0.33x |
| `count/empty-progression` | `Count` | 96.817 us | 355.682 us | Managed faster | 0.27x |
| `family/count` | `Count` | 445.026 us | 2,389.516 us | Managed faster | 0.19x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 116.063 us | 622.898 us | Managed faster | 0.19x |
| `unicode/count` | `Count` | 46.237 us | 413.209 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 276.817 us | 2,555.310 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.172 us | 1.927 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.290 us | 87.285 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.250 us | 135.061 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.988 us | 0.462 us | Inconclusive | 2.14x |
| `replacement/empty-progression-string` | `ReplaceString` | 20.890 us | 22.886 us | Managed faster | 0.92x |
| `replacement/named-template-string` | `ReplaceString` | 0.757 us | 0.902 us | Managed faster | 0.84x |
| `replacement/fixed-string` | `ReplaceString` | 140.073 us | 167.430 us | Managed faster | 0.84x |
| `replacement/subn-string` | `SubnString` | 70.017 us | 86.210 us | Managed faster | 0.81x |
| `replacement/unmatched-template-string` | `ReplaceString` | 36.709 us | 45.683 us | Managed faster | 0.80x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 129.875 us | 168.835 us | Managed faster | 0.77x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 45.780 us | 61.256 us | Managed faster | 0.75x |
| `replacement/no-match-string` | `ReplaceString` | 1.677 us | 2.302 us | Inconclusive | 0.74x |
| `replacement/subn-utf8` | `SubnUtf8` | 66.062 us | 94.738 us | Managed faster | 0.69x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 183.531 us | 271.545 us | Managed faster | 0.68x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 180.330 us | 266.781 us | Managed faster | 0.68x |
| `replacement/one-template-string` | `ReplaceString` | 1.681 us | 15,768.283 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.926 us | 0.550 us | Inconclusive | 1.73x |
| `split/captures` | `SplitStrings` | 272.736 us | 255.496 us | CPython faster | 1.07x |
| `split/unicode` | `SplitStrings` | 38.526 us | 37.584 us | Inconclusive | 1.03x |
| `split/zero-width` | `SplitStrings` | 64.281 us | 75.122 us | Managed faster | 0.85x |
| `split/no-captures` | `SplitStrings` | 175.731 us | 222.801 us | Managed faster | 0.79x |
| `split/optional-capture` | `SplitStrings` | 50.483 us | 79.933 us | Managed faster | 0.63x |
| `split/detailed` | `SplitDetailed` | 60.146 us | 130.908 us | Managed faster | 0.46x |
| `split/one-separator` | `SplitStrings` | 0.742 us | 11.551 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 0.982 us | 23.762 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 66.519 us | 382.362 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 71.036 us | 599.909 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 48.558 us | 558.028 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 914.357 us | 340,934.699 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.22 %/16.53 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.22 %/15.00 % of the lanes.
- `common/ip-match`: Removable harness floors are 0.14 %/10.48 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.16 %/13.68 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.18 %/11.82 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 114.188 us / 48,465 B | 58.249 us | 109.300 us / 48,497 B | 58.135 us |
| `fallback` | 168.691 us / 55,849 B | 40.489 us | 137.628 us / 55,849 B | 39.936 us |
| `literal` | 84.441 us / 17,473 B | 23.654 us | 77.638 us / 17,473 B | 23.694 us |
| `unicode` | 55.206 us / 22,113 B | 34.811 us | 53.913 us / 22,113 B | 33.680 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
