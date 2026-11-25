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

Snapshot SHA-256: `55AE2C6453697317C3D923DEDE31A18A6B9EF74678576163653708A9AA618217`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `64` managed faster, `1` equivalent, `2` CPython faster, `7` inconclusive, `2` unqualified.

Measured source `b200ed7a9363`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `lookbehind/search-miss` | `Search` | 0.272 us | 0.249 us | CPython faster | 1.09x |
| `multiline/search-miss` | `Search` | 0.276 us | 0.296 us | Inconclusive | 0.93x |
| `common/date-miss` | `IsMatch` | 0.711 us | 0.863 us | Managed faster | 0.83x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.343 us | 0.426 us | Managed faster | 0.80x |
| `common/ip-match` | `IsMatch` | 0.230 us | 0.323 us | Managed faster | 0.71x |
| `reluctant/search-hit` | `Search` | 0.307 us | 0.481 us | Managed faster | 0.64x |
| `atomic/search-hit` | `Search` | 0.279 us | 0.440 us | Managed faster | 0.63x |
| `leading-inline/search-hit` | `Search` | 0.279 us | 0.462 us | Managed faster | 0.60x |
| `possessive/search-hit` | `Search` | 0.250 us | 0.424 us | Managed faster | 0.59x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.219 us | 0.377 us | Managed faster | 0.58x |
| `lookahead/search-miss` | `Search` | 0.128 us | 0.226 us | Managed faster | 0.57x |
| `scoped-inline/search-hit` | `Search` | 0.236 us | 0.425 us | Managed faster | 0.56x |
| `lookbehind/search-hit` | `Search` | 0.249 us | 0.453 us | Managed faster | 0.55x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.232 us | 0.437 us | Managed faster | 0.53x |
| `prefix/match` | `Match` | 0.191 us | 0.383 us | Managed faster | 0.50x |
| `multiline/search-hit` | `Search` | 0.217 us | 0.447 us | Managed faster | 0.48x |
| `unicode-category/search-miss` | `Search` | 0.119 us | 0.246 us | Managed faster | 0.48x |
| `ignorecase/search-hit` | `Search` | 0.217 us | 0.476 us | Managed faster | 0.46x |
| `alternation/search-miss` | `Search` | 0.116 us | 0.266 us | Managed faster | 0.44x |
| `ignorecase/search-miss` | `Search` | 0.135 us | 0.318 us | Managed faster | 0.42x |
| `greedy/search-miss` | `Search` | 0.090 us | 0.251 us | Managed faster | 0.36x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.115 us | 0.323 us | Managed faster | 0.35x |
| `unicode-option/search-hit` | `Search` | 0.219 us | 0.632 us | Managed faster | 0.35x |
| `ascii-option/search-hit` | `Search` | 0.146 us | 0.438 us | Managed faster | 0.34x |
| `possessive/search-miss` | `Search` | 0.080 us | 0.239 us | Managed faster | 0.33x |
| `atomic/search-miss` | `Search` | 0.090 us | 0.293 us | Managed faster | 0.31x |
| `lookahead/search-hit` | `Search` | 0.117 us | 0.411 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.073 us | 0.319 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.067 us | 0.321 us | Managed faster | 0.21x |
| `unicode/fullmatch` | `FullMatch` | 3.531 us | 26.781 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.054 us | 0.477 us | Managed faster | 0.11x |
| `unicode-literal/search-hit` | `Search` | 0.067 us | 0.643 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.053 us | 0.600 us | Managed faster | 0.09x |
| `literal/search` | `Search` | 1.093 us | 22.611 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.091 us | 22.537 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.063 us | 22.330 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.196 us | 0.211 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.227 us | 0.228 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.633 us | 1.673 us | Managed faster | 0.38x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 88.568 us | 219.979 us | Managed faster | 0.40x |
| `zero-width/count` | `Count` | 396.774 us | 1,112.212 us | Managed faster | 0.36x |
| `count/from-offset` | `CountFromOffset` | 16.232 us | 49.155 us | Managed faster | 0.33x |
| `count/empty-progression` | `Count` | 104.563 us | 365.101 us | Managed faster | 0.29x |
| `family/count` | `Count` | 470.070 us | 2,490.009 us | Managed faster | 0.19x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 118.125 us | 654.979 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 47.702 us | 430.076 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 283.129 us | 2,602.039 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.154 us | 1.959 us | Managed faster | 0.08x |
| `count/zero` | `Count` | 0.297 us | 90.333 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.276 us | 142.183 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.039 us | 0.474 us | Inconclusive | 2.19x |
| `replacement/empty-progression-string` | `ReplaceString` | 21.214 us | 23.451 us | Managed faster | 0.90x |
| `replacement/unmatched-template-string` | `ReplaceString` | 39.441 us | 46.876 us | Managed faster | 0.84x |
| `replacement/fixed-string` | `ReplaceString` | 144.851 us | 174.730 us | Managed faster | 0.83x |
| `replacement/named-template-string` | `ReplaceString` | 0.782 us | 0.955 us | Managed faster | 0.82x |
| `replacement/subn-string` | `SubnString` | 71.971 us | 90.877 us | Managed faster | 0.79x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 49.353 us | 64.087 us | Managed faster | 0.77x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 135.171 us | 177.960 us | Managed faster | 0.76x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.915 us | 90.240 us | Managed faster | 0.75x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 191.665 us | 276.057 us | Managed faster | 0.69x |
| `replacement/no-match-string` | `ReplaceString` | 1.653 us | 2.399 us | Inconclusive | 0.69x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 191.169 us | 277.820 us | Managed faster | 0.69x |
| `replacement/one-template-string` | `ReplaceString` | 1.880 us | 16,418.264 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.001 us | 0.571 us | Inconclusive | 1.79x |
| `split/captures` | `SplitStrings` | 278.701 us | 265.045 us | CPython faster | 1.05x |
| `split/unicode` | `SplitStrings` | 39.222 us | 39.457 us | Equivalent | 0.99x |
| `split/zero-width` | `SplitStrings` | 71.104 us | 77.061 us | Managed faster | 0.92x |
| `split/no-captures` | `SplitStrings` | 179.410 us | 232.410 us | Managed faster | 0.77x |
| `split/optional-capture` | `SplitStrings` | 54.124 us | 83.923 us | Managed faster | 0.64x |
| `split/detailed` | `SplitDetailed` | 60.345 us | 135.981 us | Managed faster | 0.44x |
| `split/one-separator` | `SplitStrings` | 0.785 us | 11.969 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 0.922 us | 24.827 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.220 us | 396.066 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 74.837 us | 626.489 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 49.848 us | 579.660 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 963.811 us | 358,235.012 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/16.04 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.18 %/14.80 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 123.669 us / 48,513 B | 58.395 us | 127.028 us / 48,545 B | 59.401 us |
| `fallback` | 253.134 us / 55,897 B | 43.669 us | 155.113 us / 55,897 B | 43.502 us |
| `literal` | 88.253 us / 17,521 B | 24.711 us | 84.759 us / 17,521 B | 23.751 us |
| `unicode` | 58.747 us / 22,161 B | 37.166 us | 58.172 us / 22,161 B | 34.771 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
