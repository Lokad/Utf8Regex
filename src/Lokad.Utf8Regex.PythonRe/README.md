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

Snapshot SHA-256: `B8AB8F733F7E206448E81C50E5278288DF102ED9815ED9249CB84280360CEEFF`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `65` managed faster, `1` equivalent, `1` CPython faster, `7` inconclusive, `2` unqualified.

Measured source `022dedc80645`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.243 us | 0.225 us | CPython faster | 1.08x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.339 us | 0.384 us | Managed faster | 0.89x |
| `multiline/search-miss` | `Search` | 0.247 us | 0.292 us | Managed faster | 0.85x |
| `common/date-miss` | `IsMatch` | 0.704 us | 0.845 us | Managed faster | 0.83x |
| `common/ip-match` | `IsMatch` | 0.268 us | 0.323 us | Managed faster | 0.82x |
| `reluctant/search-hit` | `Search` | 0.277 us | 0.431 us | Managed faster | 0.64x |
| `leading-inline/search-hit` | `Search` | 0.264 us | 0.419 us | Managed faster | 0.63x |
| `atomic/search-hit` | `Search` | 0.253 us | 0.404 us | Managed faster | 0.62x |
| `lookahead/search-miss` | `Search` | 0.136 us | 0.221 us | Managed faster | 0.62x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.230 us | 0.373 us | Managed faster | 0.62x |
| `scoped-inline/search-hit` | `Search` | 0.222 us | 0.372 us | Managed faster | 0.60x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.228 us | 0.386 us | Managed faster | 0.59x |
| `possessive/search-hit` | `Search` | 0.228 us | 0.393 us | Managed faster | 0.58x |
| `lookbehind/search-hit` | `Search` | 0.225 us | 0.412 us | Managed faster | 0.54x |
| `prefix/match` | `Match` | 0.192 us | 0.361 us | Managed faster | 0.53x |
| `alternation/search-miss` | `Search` | 0.136 us | 0.265 us | Managed faster | 0.51x |
| `unicode-category/search-miss` | `Search` | 0.123 us | 0.242 us | Managed faster | 0.51x |
| `multiline/search-hit` | `Search` | 0.203 us | 0.412 us | Managed faster | 0.49x |
| `ignorecase/search-hit` | `Search` | 0.211 us | 0.449 us | Managed faster | 0.47x |
| `ignorecase/search-miss` | `Search` | 0.140 us | 0.313 us | Managed faster | 0.45x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.120 us | 0.301 us | Managed faster | 0.40x |
| `ascii-option/search-hit` | `Search` | 0.152 us | 0.392 us | Managed faster | 0.39x |
| `greedy/search-miss` | `Search` | 0.092 us | 0.247 us | Managed faster | 0.37x |
| `possessive/search-miss` | `Search` | 0.084 us | 0.238 us | Managed faster | 0.35x |
| `unicode-option/search-hit` | `Search` | 0.214 us | 0.650 us | Managed faster | 0.33x |
| `lookahead/search-hit` | `Search` | 0.122 us | 0.380 us | Managed faster | 0.32x |
| `atomic/search-miss` | `Search` | 0.091 us | 0.287 us | Managed faster | 0.32x |
| `unicode-literal/search-miss` | `Search` | 0.072 us | 0.316 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.067 us | 0.299 us | Managed faster | 0.23x |
| `unicode/fullmatch` | `FullMatch` | 3.563 us | 26.248 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.053 us | 0.421 us | Managed faster | 0.13x |
| `unicode-literal/search-hit` | `Search` | 0.066 us | 0.646 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.054 us | 0.606 us | Managed faster | 0.09x |
| `literal/search` | `Search` | 1.082 us | 22.516 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.094 us | 22.532 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.051 us | 22.179 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.196 us | 0.209 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.239 us | 0.247 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.619 us | 1.685 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 86.308 us | 220.622 us | Managed faster | 0.39x |
| `count/from-offset` | `CountFromOffset` | 17.672 us | 49.131 us | Managed faster | 0.36x |
| `zero-width/count` | `Count` | 380.692 us | 1,101.601 us | Managed faster | 0.35x |
| `count/empty-progression` | `Count` | 95.451 us | 363.904 us | Managed faster | 0.26x |
| `family/count` | `Count` | 603.395 us | 2,480.507 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 115.808 us | 646.101 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 48.198 us | 426.625 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 280.903 us | 2,642.498 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.179 us | 2.002 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.293 us | 89.959 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.263 us | 141.700 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.112 us | 0.477 us | Inconclusive | 2.35x |
| `replacement/empty-progression-string` | `ReplaceString` | 19.988 us | 23.643 us | Managed faster | 0.85x |
| `replacement/named-template-string` | `ReplaceString` | 0.787 us | 0.948 us | Managed faster | 0.84x |
| `replacement/fixed-string` | `ReplaceString` | 146.012 us | 175.947 us | Managed faster | 0.83x |
| `replacement/subn-string` | `SubnString` | 73.128 us | 91.199 us | Managed faster | 0.80x |
| `replacement/unmatched-template-string` | `ReplaceString` | 37.175 us | 47.043 us | Managed faster | 0.79x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 136.949 us | 179.783 us | Managed faster | 0.76x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.663 us | 89.365 us | Managed faster | 0.76x |
| `replacement/no-match-string` | `ReplaceString` | 1.726 us | 2.373 us | Inconclusive | 0.73x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 45.247 us | 64.186 us | Managed faster | 0.71x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 179.178 us | 274.580 us | Managed faster | 0.65x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 175.520 us | 275.739 us | Managed faster | 0.64x |
| `replacement/one-template-string` | `ReplaceString` | 1.906 us | 16,179.594 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.047 us | 0.576 us | Inconclusive | 1.89x |
| `split/captures` | `SplitStrings` | 269.588 us | 261.840 us | Inconclusive | 1.03x |
| `split/unicode` | `SplitStrings` | 39.309 us | 39.090 us | Equivalent | 1.01x |
| `split/zero-width` | `SplitStrings` | 65.127 us | 76.177 us | Managed faster | 0.85x |
| `split/no-captures` | `SplitStrings` | 178.741 us | 233.880 us | Managed faster | 0.76x |
| `split/optional-capture` | `SplitStrings` | 50.865 us | 82.494 us | Managed faster | 0.62x |
| `split/detailed` | `SplitDetailed` | 59.623 us | 134.627 us | Managed faster | 0.44x |
| `split/one-separator` | `SplitStrings` | 0.801 us | 11.923 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.087 us | 24.690 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 70.396 us | 391.552 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 77.469 us | 620.533 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 50.782 us | 579.099 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 950.835 us | 355,356.634 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `anchor/fullmatch-miss`: Removable harness floors are 0.22 %/15.87 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.21 %/13.77 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 102.050 us / 48,465 B | 60.501 us | 109.003 us / 48,529 B | 60.725 us |
| `fallback` | 158.650 us / 55,849 B | 41.466 us | 135.272 us / 55,849 B | 40.383 us |
| `literal` | 98.091 us / 19,513 B | 23.550 us | 93.009 us / 19,513 B | 24.665 us |
| `unicode` | 73.500 us / 25,713 B | 36.095 us | 72.519 us / 25,713 B | 36.258 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
