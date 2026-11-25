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

Snapshot SHA-256: `DD7EDB1B8F2592DFA3B5A00BA4FA1D0448AE643BC7BE40860976863235F8D87C`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `65` managed faster, `1` equivalent, `0` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `5d54be2a8409`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `multiline/search-miss` | `Search` | 0.240 us | 0.290 us | Managed faster | 0.83x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.339 us | 0.425 us | Managed faster | 0.80x |
| `common/date-miss` | `IsMatch` | 0.677 us | 0.864 us | Managed faster | 0.78x |
| `common/ip-match` | `IsMatch` | 0.234 us | 0.321 us | Managed faster | 0.73x |
| `reluctant/search-hit` | `Search` | 0.308 us | 0.470 us | Managed faster | 0.65x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.227 us | 0.371 us | Managed faster | 0.61x |
| `leading-inline/search-hit` | `Search` | 0.277 us | 0.461 us | Managed faster | 0.60x |
| `atomic/search-hit` | `Search` | 0.254 us | 0.443 us | Managed faster | 0.58x |
| `lookahead/search-miss` | `Search` | 0.127 us | 0.223 us | Managed faster | 0.57x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.238 us | 0.417 us | Managed faster | 0.57x |
| `scoped-inline/search-hit` | `Search` | 0.227 us | 0.411 us | Managed faster | 0.55x |
| `possessive/search-hit` | `Search` | 0.231 us | 0.419 us | Managed faster | 0.55x |
| `prefix/match` | `Match` | 0.202 us | 0.380 us | Managed faster | 0.53x |
| `lookbehind/search-hit` | `Search` | 0.226 us | 0.444 us | Managed faster | 0.51x |
| `unicode-category/search-miss` | `Search` | 0.115 us | 0.241 us | Managed faster | 0.47x |
| `multiline/search-hit` | `Search` | 0.212 us | 0.455 us | Managed faster | 0.47x |
| `ignorecase/search-hit` | `Search` | 0.213 us | 0.480 us | Managed faster | 0.45x |
| `alternation/search-miss` | `Search` | 0.109 us | 0.261 us | Managed faster | 0.42x |
| `ignorecase/search-miss` | `Search` | 0.130 us | 0.312 us | Managed faster | 0.42x |
| `ascii-option/search-hit` | `Search` | 0.145 us | 0.419 us | Managed faster | 0.34x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.110 us | 0.321 us | Managed faster | 0.34x |
| `greedy/search-miss` | `Search` | 0.082 us | 0.244 us | Managed faster | 0.33x |
| `unicode-option/search-hit` | `Search` | 0.212 us | 0.644 us | Managed faster | 0.33x |
| `possessive/search-miss` | `Search` | 0.075 us | 0.235 us | Managed faster | 0.32x |
| `atomic/search-miss` | `Search` | 0.083 us | 0.290 us | Managed faster | 0.29x |
| `lookahead/search-hit` | `Search` | 0.113 us | 0.415 us | Managed faster | 0.27x |
| `unicode-literal/search-miss` | `Search` | 0.073 us | 0.315 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.066 us | 0.343 us | Managed faster | 0.19x |
| `unicode/fullmatch` | `FullMatch` | 3.629 us | 26.503 us | Managed faster | 0.14x |
| `literal/fullmatch` | `FullMatch` | 0.062 us | 0.469 us | Managed faster | 0.13x |
| `unicode-literal/search-hit` | `Search` | 0.067 us | 0.648 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.052 us | 0.600 us | Managed faster | 0.09x |
| `literal/search-miss` | `Search` | 1.075 us | 22.273 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.096 us | 22.629 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.059 us | 22.224 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.200 us | 0.208 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.227 us | 0.227 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.234 us | 0.247 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.634 us | 1.687 us | Managed faster | 0.38x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 88.863 us | 217.067 us | Managed faster | 0.41x |
| `count/from-offset` | `CountFromOffset` | 16.251 us | 47.729 us | Managed faster | 0.34x |
| `zero-width/count` | `Count` | 367.918 us | 1,095.547 us | Managed faster | 0.34x |
| `count/empty-progression` | `Count` | 99.694 us | 351.938 us | Managed faster | 0.28x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 122.861 us | 635.174 us | Managed faster | 0.19x |
| `family/count` | `Count` | 468.757 us | 2,483.649 us | Managed faster | 0.19x |
| `unicode/count` | `Count` | 47.456 us | 414.289 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 275.050 us | 2,552.242 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.151 us | 1.952 us | Managed faster | 0.08x |
| `count/zero` | `Count` | 0.293 us | 89.292 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.254 us | 137.321 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.941 us | 0.469 us | Inconclusive | 2.02x |
| `replacement/empty-progression-string` | `ReplaceString` | 21.009 us | 23.012 us | Managed faster | 0.91x |
| `replacement/named-template-string` | `ReplaceString` | 0.785 us | 0.900 us | Managed faster | 0.87x |
| `replacement/fixed-string` | `ReplaceString` | 146.388 us | 172.346 us | Managed faster | 0.85x |
| `replacement/unmatched-template-string` | `ReplaceString` | 38.560 us | 45.748 us | Managed faster | 0.85x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 51.281 us | 62.891 us | Managed faster | 0.82x |
| `replacement/subn-string` | `SubnString` | 73.751 us | 90.343 us | Managed faster | 0.81x |
| `replacement/subn-utf8` | `SubnUtf8` | 68.155 us | 87.862 us | Managed faster | 0.77x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 137.212 us | 178.545 us | Managed faster | 0.76x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 205.424 us | 275.919 us | Managed faster | 0.75x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 205.189 us | 281.100 us | Managed faster | 0.73x |
| `replacement/no-match-string` | `ReplaceString` | 1.519 us | 2.330 us | Inconclusive | 0.65x |
| `replacement/one-template-string` | `ReplaceString` | 1.667 us | 15,818.332 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.878 us | 0.549 us | Inconclusive | 1.62x |
| `split/captures` | `SplitStrings` | 261.585 us | 262.864 us | Inconclusive | 0.99x |
| `split/unicode` | `SplitStrings` | 38.902 us | 39.322 us | Equivalent | 0.99x |
| `split/zero-width` | `SplitStrings` | 64.512 us | 76.323 us | Managed faster | 0.85x |
| `split/no-captures` | `SplitStrings` | 174.362 us | 232.601 us | Managed faster | 0.75x |
| `split/optional-capture` | `SplitStrings` | 50.681 us | 79.938 us | Managed faster | 0.63x |
| `split/detailed` | `SplitDetailed` | 59.265 us | 133.652 us | Managed faster | 0.44x |
| `split/one-separator` | `SplitStrings` | 0.684 us | 11.567 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 0.883 us | 24.361 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 68.504 us | 393.159 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 76.673 us | 629.040 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 52.084 us | 582.711 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 950.676 us | 355,311.339 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.24 %/16.33 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.21 %/14.98 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.19 %/13.64 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 111.469 us / 48,513 B | 60.280 us | 105.791 us / 48,545 B | 60.586 us |
| `fallback` | 169.425 us / 55,897 B | 42.344 us | 138.156 us / 55,897 B | 41.436 us |
| `literal` | 86.494 us / 17,521 B | 24.665 us | 79.550 us / 17,521 B | 25.038 us |
| `unicode` | 56.763 us / 22,161 B | 36.207 us | 56.584 us / 22,161 B | 35.869 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
