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

Snapshot SHA-256: `0C2CCCEF88B939ECBEA92FA729E3D50E132AB6C5539B6EDA0F15FD6ED78B5269`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `63` managed faster, `1` equivalent, `1` CPython faster, `9` inconclusive, `2` unqualified.

Measured source `dabd1655590e`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.242 us | 0.226 us | CPython faster | 1.08x |
| `common/date-miss` | `IsMatch` | 0.775 us | 0.864 us | Managed faster | 0.90x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.362 us | 0.412 us | Managed faster | 0.88x |
| `multiline/search-miss` | `Search` | 0.243 us | 0.291 us | Managed faster | 0.83x |
| `reluctant/search-hit` | `Search` | 0.329 us | 0.481 us | Managed faster | 0.68x |
| `lookahead/search-miss` | `Search` | 0.146 us | 0.223 us | Managed faster | 0.65x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.233 us | 0.375 us | Managed faster | 0.62x |
| `common/ip-match` | `IsMatch` | 0.200 us | 0.324 us | Managed faster | 0.62x |
| `atomic/search-hit` | `Search` | 0.273 us | 0.446 us | Managed faster | 0.61x |
| `possessive/search-hit` | `Search` | 0.246 us | 0.422 us | Managed faster | 0.58x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.242 us | 0.418 us | Managed faster | 0.58x |
| `prefix/match` | `Match` | 0.216 us | 0.377 us | Managed faster | 0.57x |
| `scoped-inline/search-hit` | `Search` | 0.232 us | 0.408 us | Managed faster | 0.57x |
| `unicode-category/search-miss` | `Search` | 0.122 us | 0.243 us | Managed faster | 0.50x |
| `leading-inline/search-hit` | `Search` | 0.222 us | 0.451 us | Managed faster | 0.49x |
| `ignorecase/search-hit` | `Search` | 0.229 us | 0.474 us | Managed faster | 0.48x |
| `lookbehind/search-hit` | `Search` | 0.207 us | 0.439 us | Managed faster | 0.47x |
| `multiline/search-hit` | `Search` | 0.210 us | 0.457 us | Managed faster | 0.46x |
| `alternation/search-miss` | `Search` | 0.118 us | 0.264 us | Managed faster | 0.45x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.135 us | 0.323 us | Managed faster | 0.42x |
| `ignorecase/search-miss` | `Search` | 0.133 us | 0.316 us | Managed faster | 0.42x |
| `greedy/search-miss` | `Search` | 0.094 us | 0.257 us | Managed faster | 0.36x |
| `possessive/search-miss` | `Search` | 0.084 us | 0.237 us | Managed faster | 0.35x |
| `unicode-option/search-hit` | `Search` | 0.214 us | 0.631 us | Managed faster | 0.34x |
| `ascii-option/search-hit` | `Search` | 0.142 us | 0.436 us | Managed faster | 0.33x |
| `atomic/search-miss` | `Search` | 0.094 us | 0.291 us | Managed faster | 0.32x |
| `lookahead/search-hit` | `Search` | 0.137 us | 0.426 us | Managed faster | 0.32x |
| `unicode-literal/search-miss` | `Search` | 0.081 us | 0.315 us | Managed faster | 0.26x |
| `literal/search-early` | `Search` | 0.075 us | 0.316 us | Managed faster | 0.24x |
| `literal/fullmatch` | `FullMatch` | 0.067 us | 0.469 us | Managed faster | 0.14x |
| `unicode/fullmatch` | `FullMatch` | 3.768 us | 26.336 us | Managed faster | 0.14x |
| `unicode-literal/search-hit` | `Search` | 0.075 us | 0.643 us | Managed faster | 0.12x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.069 us | 0.609 us | Managed faster | 0.11x |
| `literal/search-miss` | `Search` | 1.088 us | 22.285 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.089 us | 22.497 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.074 us | 22.245 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.209 us | 0.208 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.216 us | 0.250 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.653 us | 1.697 us | Inconclusive | 0.38x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `count/from-offset` | `CountFromOffset` | 20.737 us | 48.875 us | Managed faster | 0.42x |
| `findall/structural-many` | `FindAllStructural` | 87.191 us | 220.294 us | Managed faster | 0.40x |
| `zero-width/count` | `Count` | 377.302 us | 1,096.461 us | Managed faster | 0.34x |
| `count/empty-progression` | `Count` | 104.391 us | 361.904 us | Managed faster | 0.29x |
| `family/count` | `Count` | 475.015 us | 2,529.002 us | Managed faster | 0.19x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 118.179 us | 655.528 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 48.231 us | 429.646 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 267.069 us | 2,643.814 us | Managed faster | 0.10x |
| `count/one` | `Count` | 0.186 us | 1.935 us | Managed faster | 0.10x |
| `count/zero` | `Count` | 0.258 us | 90.252 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.257 us | 140.051 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.084 us | 0.469 us | Inconclusive | 2.30x |
| `replacement/empty-progression-string` | `ReplaceString` | 21.946 us | 23.670 us | Managed faster | 0.93x |
| `replacement/unmatched-template-string` | `ReplaceString` | 40.381 us | 47.250 us | Managed faster | 0.85x |
| `replacement/fixed-string` | `ReplaceString` | 145.487 us | 173.937 us | Managed faster | 0.84x |
| `replacement/subn-string` | `SubnString` | 73.098 us | 90.548 us | Managed faster | 0.81x |
| `replacement/named-template-string` | `ReplaceString` | 0.759 us | 0.958 us | Managed faster | 0.79x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 135.851 us | 176.788 us | Managed faster | 0.77x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.904 us | 89.862 us | Inconclusive | 0.76x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 48.387 us | 63.963 us | Managed faster | 0.76x |
| `replacement/no-match-string` | `ReplaceString` | 1.699 us | 2.376 us | Inconclusive | 0.71x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 191.250 us | 276.501 us | Managed faster | 0.69x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 185.786 us | 271.894 us | Managed faster | 0.68x |
| `replacement/one-template-string` | `ReplaceString` | 1.885 us | 16,106.823 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.914 us | 0.578 us | Inconclusive | 1.59x |
| `split/captures` | `SplitStrings` | 270.669 us | 265.065 us | Inconclusive | 1.02x |
| `split/unicode` | `SplitStrings` | 38.813 us | 39.031 us | Equivalent | 1.00x |
| `split/zero-width` | `SplitStrings` | 68.021 us | 76.883 us | Managed faster | 0.88x |
| `split/no-captures` | `SplitStrings` | 168.778 us | 229.929 us | Managed faster | 0.74x |
| `split/optional-capture` | `SplitStrings` | 52.796 us | 81.229 us | Managed faster | 0.66x |
| `split/detailed` | `SplitDetailed` | 61.409 us | 135.262 us | Managed faster | 0.46x |
| `split/one-separator` | `SplitStrings` | 0.770 us | 11.866 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 0.944 us | 24.748 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.172 us | 395.544 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 78.550 us | 618.552 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 55.999 us | 575.010 us | Managed faster | 0.10x |
| `corpus/mail-email-count` | `Count` | 966.485 us | 351,398.157 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/16.24 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.20 %/13.69 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 88.222 us / 36,497 B | 59.357 us | 88.931 us / 36,529 B | 59.697 us |
| `fallback` | 119.869 us / 38,057 B | 41.423 us | 99.519 us / 38,057 B | 41.284 us |
| `literal` | 55.756 us / 11,921 B | 23.722 us | 54.159 us / 11,921 B | 24.782 us |
| `unicode` | 44.572 us / 14,297 B | 36.381 us | 27.519 us / 14,297 B | 35.839 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Pass |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
