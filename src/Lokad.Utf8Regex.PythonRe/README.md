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

Snapshot SHA-256: `65A26C6F7ECB8C0D20DABD342F34708037139A1FBA552D7A06ACEF759263B737`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `64` managed faster, `0` equivalent, `0` CPython faster, `8` inconclusive, `4` unqualified.

Measured source `1128c579a517`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 0.653 us | 0.821 us | Managed faster | 0.79x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.323 us | 0.407 us | Managed faster | 0.79x |
| `common/ip-match` | `IsMatch` | 0.218 us | 0.309 us | Managed faster | 0.70x |
| `reluctant/search-hit` | `Search` | 0.300 us | 0.459 us | Managed faster | 0.65x |
| `leading-inline/search-hit` | `Search` | 0.272 us | 0.431 us | Managed faster | 0.63x |
| `atomic/search-hit` | `Search` | 0.268 us | 0.434 us | Managed faster | 0.62x |
| `possessive/search-hit` | `Search` | 0.249 us | 0.408 us | Managed faster | 0.61x |
| `scoped-inline/search-hit` | `Search` | 0.235 us | 0.390 us | Managed faster | 0.60x |
| `lookahead/search-miss` | `Search` | 0.128 us | 0.215 us | Managed faster | 0.60x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.213 us | 0.358 us | Managed faster | 0.59x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.220 us | 0.424 us | Managed faster | 0.52x |
| `lookbehind/search-hit` | `Search` | 0.228 us | 0.438 us | Managed faster | 0.52x |
| `prefix/match` | `Match` | 0.180 us | 0.363 us | Managed faster | 0.50x |
| `unicode-category/search-miss` | `Search` | 0.114 us | 0.232 us | Managed faster | 0.49x |
| `multiline/search-hit` | `Search` | 0.207 us | 0.429 us | Managed faster | 0.48x |
| `ignorecase/search-hit` | `Search` | 0.213 us | 0.466 us | Managed faster | 0.46x |
| `alternation/search-miss` | `Search` | 0.111 us | 0.254 us | Managed faster | 0.44x |
| `ignorecase/search-miss` | `Search` | 0.130 us | 0.307 us | Managed faster | 0.42x |
| `ascii-option/search-hit` | `Search` | 0.143 us | 0.408 us | Managed faster | 0.35x |
| `greedy/search-miss` | `Search` | 0.084 us | 0.241 us | Managed faster | 0.35x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.110 us | 0.320 us | Managed faster | 0.34x |
| `unicode-option/search-hit` | `Search` | 0.210 us | 0.622 us | Managed faster | 0.34x |
| `possessive/search-miss` | `Search` | 0.076 us | 0.230 us | Managed faster | 0.33x |
| `atomic/search-miss` | `Search` | 0.083 us | 0.276 us | Managed faster | 0.30x |
| `lookahead/search-hit` | `Search` | 0.113 us | 0.396 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.069 us | 0.303 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.065 us | 0.305 us | Managed faster | 0.21x |
| `unicode/fullmatch` | `FullMatch` | 3.721 us | 25.496 us | Managed faster | 0.15x |
| `literal/fullmatch` | `FullMatch` | 0.049 us | 0.451 us | Managed faster | 0.11x |
| `unicode-literal/search-hit` | `Search` | 0.063 us | 0.615 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.050 us | 0.571 us | Managed faster | 0.09x |
| `literal/search-miss` | `Search` | 1.031 us | 21.406 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.035 us | 21.575 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.017 us | 21.332 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.198 us | 0.205 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.216 us | 0.217 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.242 us | 0.238 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.250 us | 0.280 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.603 us | 1.613 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 86.168 us | 212.718 us | Managed faster | 0.40x |
| `zero-width/count` | `Count` | 413.953 us | 1,067.227 us | Managed faster | 0.39x |
| `count/from-offset` | `CountFromOffset` | 16.123 us | 47.061 us | Managed faster | 0.34x |
| `count/empty-progression` | `Count` | 101.006 us | 351.059 us | Managed faster | 0.29x |
| `family/count` | `Count` | 449.598 us | 2,407.906 us | Managed faster | 0.19x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 110.006 us | 619.314 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 46.349 us | 409.216 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 256.432 us | 2,476.489 us | Managed faster | 0.10x |
| `count/one` | `Count` | 0.174 us | 1.915 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.289 us | 87.047 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.254 us | 137.831 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.971 us | 0.459 us | Inconclusive | 2.12x |
| `replacement/empty-progression-string` | `ReplaceString` | 19.825 us | 22.659 us | Managed faster | 0.87x |
| `replacement/unmatched-template-string` | `ReplaceString` | 38.793 us | 45.921 us | Managed faster | 0.85x |
| `replacement/fixed-string` | `ReplaceString` | 142.276 us | 169.369 us | Managed faster | 0.84x |
| `replacement/named-template-string` | `ReplaceString` | 0.759 us | 0.925 us | Managed faster | 0.82x |
| `replacement/subn-string` | `SubnString` | 70.495 us | 86.787 us | Managed faster | 0.82x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 48.383 us | 61.248 us | Managed faster | 0.79x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 131.427 us | 169.407 us | Managed faster | 0.78x |
| `replacement/subn-utf8` | `SubnUtf8` | 65.902 us | 86.717 us | Managed faster | 0.76x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 196.641 us | 272.007 us | Managed faster | 0.72x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 191.031 us | 270.626 us | Managed faster | 0.71x |
| `replacement/no-match-string` | `ReplaceString` | 1.585 us | 2.312 us | Inconclusive | 0.69x |
| `replacement/one-template-string` | `ReplaceString` | 1.729 us | 15,750.601 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.946 us | 0.536 us | Inconclusive | 1.75x |
| `split/captures` | `SplitStrings` | 255.712 us | 254.281 us | Inconclusive | 1.00x |
| `split/unicode` | `SplitStrings` | 37.379 us | 37.821 us | Inconclusive | 0.99x |
| `split/zero-width` | `SplitStrings` | 69.048 us | 73.559 us | Managed faster | 0.94x |
| `split/no-captures` | `SplitStrings` | 176.736 us | 228.596 us | Managed faster | 0.77x |
| `split/optional-capture` | `SplitStrings` | 51.915 us | 78.552 us | Managed faster | 0.66x |
| `split/detailed` | `SplitDetailed` | 58.742 us | 131.336 us | Managed faster | 0.45x |
| `split/one-separator` | `SplitStrings` | 0.721 us | 11.489 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 0.877 us | 24.031 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.726 us | 381.881 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 76.064 us | 601.685 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 48.646 us | 557.071 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 898.784 us | 340,314.437 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/16.22 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.17 %/14.99 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.20 %/13.63 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.21 %/11.41 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 103.672 us / 48,513 B | 57.096 us | 107.097 us / 48,545 B | 59.060 us |
| `fallback` | 168.256 us / 55,897 B | 41.056 us | 136.275 us / 55,897 B | 39.699 us |
| `literal` | 87.169 us / 17,521 B | 24.048 us | 76.175 us / 17,521 B | 23.459 us |
| `unicode` | 52.309 us / 22,161 B | 34.390 us | 55.081 us / 22,161 B | 34.845 us |

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
