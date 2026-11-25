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

Snapshot SHA-256: `14759055DC0D70B46D05E4A76D9C07CD898CE160677054C1E4BCD20A093513B5`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `66` managed faster, `0` equivalent, `0` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `78c47b37343c`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `multiline/search-miss` | `Search` | 0.247 us | 0.290 us | Managed faster | 0.85x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.338 us | 0.409 us | Managed faster | 0.83x |
| `common/date-miss` | `IsMatch` | 0.662 us | 0.856 us | Managed faster | 0.77x |
| `common/ip-match` | `IsMatch` | 0.218 us | 0.319 us | Managed faster | 0.68x |
| `reluctant/search-hit` | `Search` | 0.304 us | 0.460 us | Managed faster | 0.66x |
| `leading-inline/search-hit` | `Search` | 0.285 us | 0.444 us | Managed faster | 0.64x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.226 us | 0.369 us | Managed faster | 0.61x |
| `atomic/search-hit` | `Search` | 0.264 us | 0.448 us | Managed faster | 0.59x |
| `scoped-inline/search-hit` | `Search` | 0.231 us | 0.406 us | Managed faster | 0.57x |
| `lookahead/search-miss` | `Search` | 0.123 us | 0.219 us | Managed faster | 0.56x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.235 us | 0.424 us | Managed faster | 0.56x |
| `possessive/search-hit` | `Search` | 0.238 us | 0.429 us | Managed faster | 0.55x |
| `lookbehind/search-hit` | `Search` | 0.226 us | 0.438 us | Managed faster | 0.51x |
| `prefix/match` | `Match` | 0.198 us | 0.390 us | Managed faster | 0.51x |
| `unicode-category/search-miss` | `Search` | 0.115 us | 0.240 us | Managed faster | 0.48x |
| `multiline/search-hit` | `Search` | 0.210 us | 0.456 us | Managed faster | 0.46x |
| `ignorecase/search-hit` | `Search` | 0.212 us | 0.467 us | Managed faster | 0.45x |
| `alternation/search-miss` | `Search` | 0.109 us | 0.260 us | Managed faster | 0.42x |
| `ignorecase/search-miss` | `Search` | 0.126 us | 0.311 us | Managed faster | 0.41x |
| `ascii-option/search-hit` | `Search` | 0.153 us | 0.426 us | Managed faster | 0.36x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.110 us | 0.319 us | Managed faster | 0.34x |
| `greedy/search-miss` | `Search` | 0.082 us | 0.246 us | Managed faster | 0.34x |
| `unicode-option/search-hit` | `Search` | 0.208 us | 0.630 us | Managed faster | 0.33x |
| `possessive/search-miss` | `Search` | 0.074 us | 0.236 us | Managed faster | 0.31x |
| `atomic/search-miss` | `Search` | 0.082 us | 0.288 us | Managed faster | 0.29x |
| `lookahead/search-hit` | `Search` | 0.113 us | 0.411 us | Managed faster | 0.28x |
| `unicode-literal/search-miss` | `Search` | 0.072 us | 0.310 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.066 us | 0.314 us | Managed faster | 0.21x |
| `unicode/fullmatch` | `FullMatch` | 3.417 us | 26.062 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.054 us | 0.470 us | Managed faster | 0.11x |
| `unicode-literal/search-hit` | `Search` | 0.066 us | 0.636 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.052 us | 0.603 us | Managed faster | 0.09x |
| `literal/search-miss` | `Search` | 1.069 us | 22.107 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.087 us | 22.608 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.054 us | 22.085 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.198 us | 0.208 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.226 us | 0.225 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.230 us | 0.246 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.616 us | 1.672 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 88.447 us | 219.020 us | Managed faster | 0.40x |
| `zero-width/count` | `Count` | 382.862 us | 1,091.388 us | Managed faster | 0.35x |
| `count/from-offset` | `CountFromOffset` | 16.040 us | 48.682 us | Managed faster | 0.33x |
| `count/empty-progression` | `Count` | 101.456 us | 358.229 us | Managed faster | 0.28x |
| `family/count` | `Count` | 468.249 us | 2,506.172 us | Managed faster | 0.19x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 115.148 us | 653.131 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 47.160 us | 422.802 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 281.664 us | 2,570.632 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.157 us | 1.948 us | Managed faster | 0.08x |
| `count/zero` | `Count` | 0.294 us | 89.601 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.255 us | 141.945 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.987 us | 0.474 us | Inconclusive | 2.11x |
| `replacement/empty-progression-string` | `ReplaceString` | 21.233 us | 23.160 us | Managed faster | 0.92x |
| `replacement/subn-string` | `SubnString` | 72.475 us | 88.927 us | Managed faster | 0.81x |
| `replacement/unmatched-template-string` | `ReplaceString` | 37.899 us | 46.720 us | Managed faster | 0.81x |
| `replacement/named-template-string` | `ReplaceString` | 0.776 us | 0.966 us | Managed faster | 0.81x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 136.379 us | 177.453 us | Managed faster | 0.77x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 48.474 us | 63.333 us | Managed faster | 0.76x |
| `replacement/subn-utf8` | `SubnUtf8` | 67.573 us | 89.543 us | Managed faster | 0.76x |
| `replacement/fixed-string` | `ReplaceString` | 146.293 us | 200.601 us | Managed faster | 0.73x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 189.209 us | 274.825 us | Managed faster | 0.69x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 187.682 us | 276.164 us | Managed faster | 0.68x |
| `replacement/no-match-string` | `ReplaceString` | 1.593 us | 2.391 us | Inconclusive | 0.67x |
| `replacement/one-template-string` | `ReplaceString` | 1.784 us | 16,327.140 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.968 us | 0.580 us | Inconclusive | 1.70x |
| `split/captures` | `SplitStrings` | 256.147 us | 261.244 us | Inconclusive | 0.99x |
| `split/unicode` | `SplitStrings` | 37.663 us | 39.239 us | Managed faster | 0.96x |
| `split/zero-width` | `SplitStrings` | 67.424 us | 77.631 us | Managed faster | 0.87x |
| `split/no-captures` | `SplitStrings` | 170.844 us | 225.792 us | Managed faster | 0.76x |
| `split/optional-capture` | `SplitStrings` | 51.024 us | 83.530 us | Managed faster | 0.61x |
| `split/detailed` | `SplitDetailed` | 59.905 us | 134.022 us | Managed faster | 0.45x |
| `split/one-separator` | `SplitStrings` | 0.744 us | 11.887 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 0.910 us | 24.606 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.205 us | 387.492 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 75.245 us | 616.438 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 49.527 us | 571.154 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 937.198 us | 352,524.388 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.27 %/16.66 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.21 %/15.01 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.19 %/13.55 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 108.909 us / 48,513 B | 60.071 us | 109.653 us / 48,545 B | 57.225 us |
| `fallback` | 168.819 us / 55,897 B | 41.725 us | 138.528 us / 55,897 B | 41.805 us |
| `literal` | 90.478 us / 17,521 B | 24.741 us | 78.931 us / 17,521 B | 24.743 us |
| `unicode` | 56.819 us / 22,161 B | 35.834 us | 56.797 us / 22,161 B | 36.331 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
