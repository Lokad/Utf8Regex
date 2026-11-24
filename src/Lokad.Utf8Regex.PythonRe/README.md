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

Snapshot SHA-256: `83B4F5701D699D805D54A643676A275FB7FD690BE34629294209F7F0254DDC8F`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `62` managed faster, `1` equivalent, `3` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `77b3c71f31ff`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `atomic/search-miss` | `Search` | 0.332 us | 0.291 us | CPython faster | 1.14x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.244 us | 0.228 us | CPython faster | 1.07x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.335 us | 0.382 us | Managed faster | 0.88x |
| `common/ip-match` | `IsMatch` | 0.271 us | 0.322 us | Managed faster | 0.84x |
| `multiline/search-miss` | `Search` | 0.242 us | 0.292 us | Managed faster | 0.82x |
| `common/date-miss` | `IsMatch` | 0.719 us | 0.869 us | Managed faster | 0.82x |
| `atomic/search-hit` | `Search` | 0.297 us | 0.406 us | Managed faster | 0.73x |
| `possessive/search-hit` | `Search` | 0.271 us | 0.393 us | Managed faster | 0.69x |
| `leading-inline/search-hit` | `Search` | 0.272 us | 0.416 us | Managed faster | 0.65x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.233 us | 0.373 us | Managed faster | 0.63x |
| `lookahead/search-miss` | `Search` | 0.137 us | 0.224 us | Managed faster | 0.61x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.235 us | 0.389 us | Managed faster | 0.60x |
| `scoped-inline/search-hit` | `Search` | 0.226 us | 0.375 us | Managed faster | 0.60x |
| `lookbehind/search-hit` | `Search` | 0.225 us | 0.414 us | Managed faster | 0.54x |
| `prefix/match` | `Match` | 0.188 us | 0.360 us | Managed faster | 0.52x |
| `unicode-category/search-miss` | `Search` | 0.125 us | 0.241 us | Managed faster | 0.52x |
| `alternation/search-miss` | `Search` | 0.137 us | 0.264 us | Managed faster | 0.52x |
| `ignorecase/search-hit` | `Search` | 0.211 us | 0.440 us | Managed faster | 0.48x |
| `multiline/search-hit` | `Search` | 0.195 us | 0.414 us | Managed faster | 0.47x |
| `ignorecase/search-miss` | `Search` | 0.139 us | 0.317 us | Managed faster | 0.44x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.122 us | 0.306 us | Managed faster | 0.40x |
| `ascii-option/search-hit` | `Search` | 0.153 us | 0.393 us | Managed faster | 0.39x |
| `greedy/search-miss` | `Search` | 0.092 us | 0.248 us | Managed faster | 0.37x |
| `unicode-option/search-hit` | `Search` | 0.222 us | 0.634 us | Managed faster | 0.35x |
| `possessive/search-miss` | `Search` | 0.084 us | 0.239 us | Managed faster | 0.35x |
| `lookahead/search-hit` | `Search` | 0.123 us | 0.382 us | Managed faster | 0.32x |
| `unicode-literal/search-miss` | `Search` | 0.073 us | 0.315 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.067 us | 0.300 us | Managed faster | 0.22x |
| `unicode/fullmatch` | `FullMatch` | 3.407 us | 26.319 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.054 us | 0.427 us | Managed faster | 0.13x |
| `unicode-literal/search-hit` | `Search` | 0.067 us | 0.642 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.053 us | 0.607 us | Managed faster | 0.09x |
| `literal/search` | `Search` | 1.086 us | 22.449 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.070 us | 22.117 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.051 us | 22.060 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.203 us | 0.210 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.241 us | 0.246 us | Unqualified | — |
| `reluctant/search-hit` | `Search` | 0.437 us | 0.442 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.633 us | 1.704 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 86.958 us | 220.915 us | Managed faster | 0.39x |
| `zero-width/count` | `Count` | 376.922 us | 1,091.746 us | Managed faster | 0.35x |
| `count/empty-progression` | `Count` | 122.500 us | 365.469 us | Managed faster | 0.34x |
| `count/from-offset` | `CountFromOffset` | 16.425 us | 49.258 us | Managed faster | 0.33x |
| `family/count` | `Count` | 600.746 us | 2,509.191 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 117.541 us | 654.407 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 48.041 us | 429.298 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 286.221 us | 2,610.949 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.155 us | 1.986 us | Managed faster | 0.08x |
| `count/zero` | `Count` | 0.298 us | 91.279 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.276 us | 140.341 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.124 us | 0.472 us | Inconclusive | 2.36x |
| `replacement/empty-progression-string` | `ReplaceString` | 24.842 us | 23.646 us | CPython faster | 1.05x |
| `replacement/fixed-string` | `ReplaceString` | 167.396 us | 177.153 us | Managed faster | 0.94x |
| `replacement/subn-string` | `SubnString` | 83.339 us | 90.929 us | Managed faster | 0.92x |
| `replacement/subn-utf8` | `SubnUtf8` | 79.106 us | 89.511 us | Managed faster | 0.89x |
| `replacement/named-template-string` | `ReplaceString` | 0.864 us | 0.970 us | Managed faster | 0.88x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 157.313 us | 177.583 us | Managed faster | 0.88x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.217 us | 47.551 us | Managed faster | 0.86x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 46.288 us | 64.344 us | Managed faster | 0.72x |
| `replacement/no-match-string` | `ReplaceString` | 1.646 us | 2.404 us | Inconclusive | 0.69x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 184.320 us | 276.674 us | Managed faster | 0.67x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 182.194 us | 278.948 us | Managed faster | 0.65x |
| `replacement/one-template-string` | `ReplaceString` | 1.957 us | 16,483.614 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.013 us | 0.567 us | Inconclusive | 1.80x |
| `split/unicode` | `SplitStrings` | 40.046 us | 39.850 us | Equivalent | 1.01x |
| `split/captures` | `SplitStrings` | 263.022 us | 263.867 us | Inconclusive | 0.99x |
| `split/zero-width` | `SplitStrings` | 67.607 us | 76.988 us | Managed faster | 0.87x |
| `split/no-captures` | `SplitStrings` | 180.249 us | 233.298 us | Managed faster | 0.78x |
| `split/optional-capture` | `SplitStrings` | 51.761 us | 82.681 us | Managed faster | 0.63x |
| `split/detailed` | `SplitDetailed` | 64.470 us | 135.301 us | Managed faster | 0.48x |
| `split/one-separator` | `SplitStrings` | 0.811 us | 12.014 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.039 us | 24.845 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 68.675 us | 395.697 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 79.172 us | 628.428 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 49.668 us | 578.013 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 947.113 us | 356,871.054 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `anchor/fullmatch-miss`: Removable harness floors are 0.20 %/15.99 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.18 %/13.75 % of the lanes.
- `reluctant/search-hit`: Removable harness floors are 0.09 %/7.65 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 109.266 us / 48,465 B | 60.483 us | 105.266 us / 48,529 B | 60.547 us |
| `fallback` | 158.213 us / 55,849 B | 43.669 us | 140.084 us / 55,849 B | 42.140 us |
| `literal` | 105.359 us / 19,513 B | 24.901 us | 92.322 us / 19,513 B | 24.876 us |
| `unicode` | 77.803 us / 25,713 B | 36.647 us | 77.284 us / 25,713 B | 37.828 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Reject |
| `output-growth` | replacement output UTF-8 bytes | 4 | Reject |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
