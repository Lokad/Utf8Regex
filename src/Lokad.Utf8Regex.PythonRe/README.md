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

Snapshot SHA-256: `5015EC17F6EECF8EC47F590D9DE620BAE0AE1CC861A76C65831328010C533B9C`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `66` managed faster, `0` equivalent, `0` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `b3bae2e09757`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `verbose/fullmatch-hit` | `FullMatch` | 0.372 us | 0.418 us | Managed faster | 0.89x |
| `multiline/search-miss` | `Search` | 0.251 us | 0.293 us | Managed faster | 0.86x |
| `common/date-miss` | `IsMatch` | 0.660 us | 0.857 us | Managed faster | 0.77x |
| `reluctant/search-hit` | `Search` | 0.312 us | 0.461 us | Managed faster | 0.67x |
| `common/ip-match` | `IsMatch` | 0.216 us | 0.324 us | Managed faster | 0.67x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.230 us | 0.374 us | Managed faster | 0.61x |
| `atomic/search-hit` | `Search` | 0.262 us | 0.440 us | Managed faster | 0.60x |
| `scoped-inline/search-hit` | `Search` | 0.242 us | 0.409 us | Managed faster | 0.59x |
| `lookahead/search-miss` | `Search` | 0.124 us | 0.224 us | Managed faster | 0.56x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.241 us | 0.438 us | Managed faster | 0.55x |
| `possessive/search-hit` | `Search` | 0.228 us | 0.425 us | Managed faster | 0.54x |
| `prefix/match` | `Match` | 0.201 us | 0.396 us | Managed faster | 0.51x |
| `ignorecase/search-hit` | `Search` | 0.219 us | 0.473 us | Managed faster | 0.46x |
| `lookbehind/search-hit` | `Search` | 0.210 us | 0.455 us | Managed faster | 0.46x |
| `unicode-category/search-miss` | `Search` | 0.112 us | 0.244 us | Managed faster | 0.46x |
| `multiline/search-hit` | `Search` | 0.209 us | 0.461 us | Managed faster | 0.45x |
| `leading-inline/search-hit` | `Search` | 0.220 us | 0.490 us | Managed faster | 0.45x |
| `alternation/search-miss` | `Search` | 0.108 us | 0.265 us | Managed faster | 0.41x |
| `ignorecase/search-miss` | `Search` | 0.130 us | 0.318 us | Managed faster | 0.41x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.113 us | 0.325 us | Managed faster | 0.35x |
| `greedy/search-miss` | `Search` | 0.083 us | 0.248 us | Managed faster | 0.33x |
| `unicode-option/search-hit` | `Search` | 0.206 us | 0.651 us | Managed faster | 0.32x |
| `ascii-option/search-hit` | `Search` | 0.134 us | 0.422 us | Managed faster | 0.32x |
| `possessive/search-miss` | `Search` | 0.072 us | 0.239 us | Managed faster | 0.30x |
| `atomic/search-miss` | `Search` | 0.083 us | 0.292 us | Managed faster | 0.29x |
| `lookahead/search-hit` | `Search` | 0.114 us | 0.427 us | Managed faster | 0.27x |
| `literal/search-early` | `Search` | 0.083 us | 0.319 us | Managed faster | 0.26x |
| `unicode-literal/search-miss` | `Search` | 0.081 us | 0.318 us | Managed faster | 0.25x |
| `literal/fullmatch` | `FullMatch` | 0.070 us | 0.458 us | Managed faster | 0.15x |
| `unicode/fullmatch` | `FullMatch` | 3.694 us | 26.458 us | Managed faster | 0.14x |
| `unicode-literal/search-hit` | `Search` | 0.076 us | 0.657 us | Managed faster | 0.12x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.069 us | 0.603 us | Managed faster | 0.12x |
| `literal/search` | `Search` | 1.092 us | 22.496 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.091 us | 22.550 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.076 us | 22.379 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.201 us | 0.211 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.227 us | 0.227 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.225 us | 0.249 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.634 us | 1.672 us | Managed faster | 0.38x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 93.993 us | 219.549 us | Managed faster | 0.43x |
| `count/from-offset` | `CountFromOffset` | 20.077 us | 49.272 us | Managed faster | 0.41x |
| `zero-width/count` | `Count` | 390.579 us | 1,106.470 us | Managed faster | 0.35x |
| `count/empty-progression` | `Count` | 104.817 us | 368.333 us | Managed faster | 0.28x |
| `family/count` | `Count` | 461.300 us | 2,477.632 us | Managed faster | 0.19x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 117.436 us | 653.884 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 48.300 us | 427.364 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 287.455 us | 2,695.595 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.190 us | 1.968 us | Managed faster | 0.10x |
| `count/zero` | `Count` | 0.266 us | 90.976 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.265 us | 142.971 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.058 us | 0.476 us | Inconclusive | 2.20x |
| `replacement/empty-progression-string` | `ReplaceString` | 21.107 us | 23.638 us | Managed faster | 0.89x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.808 us | 47.597 us | Managed faster | 0.88x |
| `replacement/fixed-string` | `ReplaceString` | 147.873 us | 178.757 us | Managed faster | 0.83x |
| `replacement/subn-string` | `SubnString` | 73.228 us | 90.595 us | Managed faster | 0.81x |
| `replacement/named-template-string` | `ReplaceString` | 0.779 us | 0.963 us | Managed faster | 0.81x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 49.630 us | 64.127 us | Managed faster | 0.77x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 137.599 us | 179.029 us | Managed faster | 0.77x |
| `replacement/subn-utf8` | `SubnUtf8` | 68.556 us | 90.163 us | Managed faster | 0.76x |
| `replacement/no-match-string` | `ReplaceString` | 1.703 us | 2.398 us | Inconclusive | 0.71x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 194.462 us | 274.663 us | Managed faster | 0.71x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 191.119 us | 278.417 us | Managed faster | 0.69x |
| `replacement/one-template-string` | `ReplaceString` | 1.801 us | 16,439.714 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.894 us | 0.575 us | Inconclusive | 1.58x |
| `split/captures` | `SplitStrings` | 272.081 us | 263.740 us | Inconclusive | 1.03x |
| `split/unicode` | `SplitStrings` | 38.181 us | 39.728 us | Managed faster | 0.96x |
| `split/zero-width` | `SplitStrings` | 68.597 us | 77.559 us | Managed faster | 0.88x |
| `split/no-captures` | `SplitStrings` | 174.527 us | 232.368 us | Managed faster | 0.75x |
| `split/optional-capture` | `SplitStrings` | 57.837 us | 82.431 us | Managed faster | 0.70x |
| `split/detailed` | `SplitDetailed` | 61.507 us | 136.031 us | Managed faster | 0.45x |
| `split/one-separator` | `SplitStrings` | 0.810 us | 12.001 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 0.900 us | 24.939 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.585 us | 395.029 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 76.570 us | 627.682 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 54.685 us | 579.654 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 974.279 us | 356,659.807 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.23 %/15.90 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.18 %/15.01 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.19 %/13.55 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 88.059 us / 36,921 B | 60.671 us | 88.778 us / 36,953 B | 60.880 us |
| `fallback` | 116.688 us / 38,401 B | 42.620 us | 99.200 us / 38,401 B | 42.630 us |
| `literal` | 59.369 us / 11,929 B | 25.727 us | 54.659 us / 11,929 B | 25.375 us |
| `unicode` | 48.087 us / 14,305 B | 36.297 us | 25.022 us / 14,305 B | 36.705 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
