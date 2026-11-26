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

Snapshot SHA-256: `93C9ABEC126D32E2A9B4DEBC4664EE8BD20DAE5B3F20707C3D4D1F2974F63EB1`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `63` managed faster, `0` equivalent, `2` CPython faster, `8` inconclusive, `3` unqualified.

Measured source `a4f100bc649c`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.234 us | 0.225 us | CPython faster | 1.04x |
| `multiline/search-miss` | `Search` | 0.254 us | 0.293 us | Managed faster | 0.87x |
| `common/date-miss` | `IsMatch` | 0.712 us | 0.856 us | Managed faster | 0.83x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.340 us | 0.410 us | Managed faster | 0.83x |
| `reluctant/search-hit` | `Search` | 0.312 us | 0.468 us | Managed faster | 0.67x |
| `leading-inline/search-hit` | `Search` | 0.288 us | 0.457 us | Managed faster | 0.63x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.232 us | 0.375 us | Inconclusive | 0.62x |
| `atomic/search-hit` | `Search` | 0.262 us | 0.449 us | Managed faster | 0.58x |
| `lookahead/search-miss` | `Search` | 0.128 us | 0.222 us | Managed faster | 0.58x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.240 us | 0.420 us | Managed faster | 0.57x |
| `possessive/search-hit` | `Search` | 0.240 us | 0.425 us | Managed faster | 0.57x |
| `scoped-inline/search-hit` | `Search` | 0.232 us | 0.414 us | Managed faster | 0.56x |
| `prefix/match` | `Match` | 0.204 us | 0.375 us | Managed faster | 0.54x |
| `unicode-category/search-miss` | `Search` | 0.115 us | 0.241 us | Managed faster | 0.48x |
| `multiline/search-hit` | `Search` | 0.213 us | 0.450 us | Managed faster | 0.47x |
| `lookbehind/search-hit` | `Search` | 0.210 us | 0.449 us | Managed faster | 0.47x |
| `ignorecase/search-hit` | `Search` | 0.215 us | 0.466 us | Managed faster | 0.46x |
| `ignorecase/search-miss` | `Search` | 0.138 us | 0.313 us | Managed faster | 0.44x |
| `alternation/search-miss` | `Search` | 0.113 us | 0.262 us | Managed faster | 0.43x |
| `greedy/search-miss` | `Search` | 0.086 us | 0.244 us | Managed faster | 0.35x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.111 us | 0.317 us | Managed faster | 0.35x |
| `unicode-option/search-hit` | `Search` | 0.212 us | 0.631 us | Managed faster | 0.33x |
| `possessive/search-miss` | `Search` | 0.076 us | 0.238 us | Managed faster | 0.32x |
| `ascii-option/search-hit` | `Search` | 0.133 us | 0.421 us | Managed faster | 0.32x |
| `atomic/search-miss` | `Search` | 0.086 us | 0.287 us | Managed faster | 0.30x |
| `lookahead/search-hit` | `Search` | 0.113 us | 0.411 us | Managed faster | 0.27x |
| `unicode-literal/search-miss` | `Search` | 0.071 us | 0.314 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.067 us | 0.314 us | Managed faster | 0.21x |
| `unicode/fullmatch` | `FullMatch` | 3.412 us | 26.213 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.055 us | 0.470 us | Managed faster | 0.12x |
| `unicode-literal/search-hit` | `Search` | 0.066 us | 0.660 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.054 us | 0.598 us | Managed faster | 0.09x |
| `literal/search` | `Search` | 1.086 us | 22.495 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.073 us | 22.320 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.066 us | 22.202 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.201 us | 0.206 us | Unqualified | — |
| `common/ip-match` | `IsMatch` | 0.299 us | 0.320 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.223 us | 0.247 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.626 us | 1.711 us | Inconclusive | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 91.910 us | 221.043 us | Managed faster | 0.41x |
| `count/from-offset` | `CountFromOffset` | 17.092 us | 48.764 us | Managed faster | 0.35x |
| `zero-width/count` | `Count` | 376.185 us | 1,102.248 us | Managed faster | 0.34x |
| `count/empty-progression` | `Count` | 103.852 us | 366.429 us | Managed faster | 0.28x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 122.930 us | 646.209 us | Managed faster | 0.19x |
| `family/count` | `Count` | 479.741 us | 2,502.386 us | Managed faster | 0.19x |
| `unicode/count` | `Count` | 47.355 us | 422.005 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 266.822 us | 2,679.854 us | Managed faster | 0.10x |
| `count/one` | `Count` | 0.180 us | 1.973 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.307 us | 90.354 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.260 us | 142.227 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.028 us | 0.470 us | Inconclusive | 2.16x |
| `replacement/fixed-string` | `ReplaceString` | 157.769 us | 175.801 us | Managed faster | 0.90x |
| `replacement/empty-progression-string` | `ReplaceString` | 20.937 us | 23.736 us | Managed faster | 0.88x |
| `replacement/subn-string` | `SubnString` | 77.335 us | 90.913 us | Managed faster | 0.85x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 149.888 us | 177.245 us | Managed faster | 0.84x |
| `replacement/subn-utf8` | `SubnUtf8` | 74.787 us | 90.025 us | Managed faster | 0.83x |
| `replacement/unmatched-template-string` | `ReplaceString` | 38.537 us | 46.855 us | Managed faster | 0.82x |
| `replacement/named-template-string` | `ReplaceString` | 0.785 us | 0.970 us | Managed faster | 0.81x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 50.231 us | 63.912 us | Managed faster | 0.78x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 196.579 us | 274.374 us | Managed faster | 0.72x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 193.599 us | 274.987 us | Managed faster | 0.71x |
| `replacement/no-match-string` | `ReplaceString` | 1.631 us | 2.388 us | Inconclusive | 0.69x |
| `replacement/one-template-string` | `ReplaceString` | 1.792 us | 16,341.819 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.820 us | 0.563 us | Inconclusive | 1.50x |
| `split/captures` | `SplitStrings` | 274.754 us | 262.393 us | CPython faster | 1.04x |
| `split/unicode` | `SplitStrings` | 37.661 us | 39.078 us | Managed faster | 0.96x |
| `split/zero-width` | `SplitStrings` | 67.950 us | 77.284 us | Managed faster | 0.88x |
| `split/no-captures` | `SplitStrings` | 170.606 us | 228.918 us | Managed faster | 0.74x |
| `split/optional-capture` | `SplitStrings` | 53.017 us | 80.439 us | Managed faster | 0.66x |
| `split/detailed` | `SplitDetailed` | 63.098 us | 135.655 us | Managed faster | 0.47x |
| `split/one-separator` | `SplitStrings` | 0.761 us | 11.909 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 0.889 us | 24.475 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 68.884 us | 394.297 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 75.153 us | 618.141 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 53.291 us | 568.377 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 968.567 us | 355,015.713 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/16.37 % of the lanes.
- `common/ip-match`: Removable harness floors are 0.13 %/10.61 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.19 %/13.56 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 122.997 us / 48,289 B | 58.867 us | 120.181 us / 48,321 B | 60.553 us |
| `fallback` | 179.925 us / 55,665 B | 41.503 us | 136.603 us / 55,665 B | 42.565 us |
| `literal` | 85.300 us / 17,529 B | 23.791 us | 79.900 us / 17,529 B | 24.819 us |
| `unicode` | 62.419 us / 22,169 B | 36.691 us | 60.462 us / 22,169 B | 36.436 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Pass |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
