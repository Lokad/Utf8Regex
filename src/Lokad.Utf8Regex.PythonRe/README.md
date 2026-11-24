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

Snapshot SHA-256: `80492873751B54CF48E2C6CFDC351CE238E0610C2ACD60FE5768E1A66D42D422`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `61` managed faster, `0` equivalent, `5` CPython faster, `8` inconclusive, `2` unqualified.

Measured source `59ff946cdba9`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `reluctant/search-hit` | `Search` | 0.507 us | 0.432 us | CPython faster | 1.18x |
| `lookbehind/search-miss` | `Search` | 0.269 us | 0.249 us | CPython faster | 1.08x |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.244 us | 0.226 us | CPython faster | 1.08x |
| `common/ip-match` | `IsMatch` | 0.342 us | 0.325 us | CPython faster | 1.05x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.335 us | 0.383 us | Managed faster | 0.88x |
| `common/date-miss` | `IsMatch` | 0.719 us | 0.866 us | Managed faster | 0.83x |
| `atomic/search-hit` | `Search` | 0.327 us | 0.411 us | Managed faster | 0.80x |
| `possessive/search-hit` | `Search` | 0.301 us | 0.396 us | Managed faster | 0.76x |
| `scoped-inline/search-hit` | `Search` | 0.254 us | 0.377 us | Managed faster | 0.67x |
| `leading-inline/search-hit` | `Search` | 0.274 us | 0.421 us | Managed faster | 0.65x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.237 us | 0.377 us | Managed faster | 0.63x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.233 us | 0.386 us | Managed faster | 0.60x |
| `lookahead/search-miss` | `Search` | 0.136 us | 0.227 us | Managed faster | 0.60x |
| `lookbehind/search-hit` | `Search` | 0.239 us | 0.418 us | Managed faster | 0.57x |
| `prefix/match` | `Match` | 0.190 us | 0.362 us | Managed faster | 0.53x |
| `multiline/search-hit` | `Search` | 0.218 us | 0.417 us | Managed faster | 0.52x |
| `alternation/search-miss` | `Search` | 0.137 us | 0.265 us | Managed faster | 0.52x |
| `unicode-category/search-miss` | `Search` | 0.125 us | 0.243 us | Managed faster | 0.52x |
| `ignorecase/search-hit` | `Search` | 0.226 us | 0.441 us | Managed faster | 0.51x |
| `ignorecase/search-miss` | `Search` | 0.137 us | 0.317 us | Managed faster | 0.43x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.122 us | 0.307 us | Managed faster | 0.40x |
| `ascii-option/search-hit` | `Search` | 0.153 us | 0.393 us | Managed faster | 0.39x |
| `greedy/search-miss` | `Search` | 0.092 us | 0.249 us | Managed faster | 0.37x |
| `possessive/search-miss` | `Search` | 0.084 us | 0.240 us | Managed faster | 0.35x |
| `unicode-option/search-hit` | `Search` | 0.219 us | 0.631 us | Managed faster | 0.35x |
| `lookahead/search-hit` | `Search` | 0.124 us | 0.384 us | Managed faster | 0.32x |
| `atomic/search-miss` | `Search` | 0.092 us | 0.293 us | Managed faster | 0.31x |
| `unicode-literal/search-miss` | `Search` | 0.073 us | 0.317 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.068 us | 0.304 us | Managed faster | 0.22x |
| `unicode/fullmatch` | `FullMatch` | 3.919 us | 26.461 us | Managed faster | 0.15x |
| `literal/fullmatch` | `FullMatch` | 0.054 us | 0.431 us | Managed faster | 0.13x |
| `unicode-literal/search-hit` | `Search` | 0.067 us | 0.655 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.053 us | 0.601 us | Managed faster | 0.09x |
| `literal/search-miss` | `Search` | 1.098 us | 22.347 us | Managed faster | 0.05x |
| `literal/search` | `Search` | 1.110 us | 22.663 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.067 us | 22.342 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.202 us | 0.210 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.275 us | 0.293 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.638 us | 1.728 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 88.380 us | 220.937 us | Managed faster | 0.40x |
| `zero-width/count` | `Count` | 394.945 us | 1,104.358 us | Managed faster | 0.36x |
| `count/empty-progression` | `Count` | 127.960 us | 367.911 us | Managed faster | 0.35x |
| `count/from-offset` | `CountFromOffset` | 17.199 us | 50.056 us | Managed faster | 0.34x |
| `family/count` | `Count` | 609.331 us | 2,512.525 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 119.974 us | 653.406 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 48.505 us | 428.344 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 285.265 us | 2,680.691 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.180 us | 1.993 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.294 us | 91.005 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.264 us | 143.075 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.098 us | 0.475 us | Inconclusive | 2.30x |
| `replacement/empty-progression-string` | `ReplaceString` | 24.014 us | 23.535 us | Inconclusive | 1.02x |
| `replacement/named-template-string` | `ReplaceString` | 0.822 us | 0.941 us | Managed faster | 0.87x |
| `replacement/fixed-string` | `ReplaceString` | 147.606 us | 177.215 us | Managed faster | 0.83x |
| `replacement/unmatched-template-string` | `ReplaceString` | 38.440 us | 47.840 us | Managed faster | 0.81x |
| `replacement/subn-string` | `SubnString` | 73.578 us | 91.335 us | Managed faster | 0.81x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 136.742 us | 178.748 us | Managed faster | 0.77x |
| `replacement/subn-utf8` | `SubnUtf8` | 69.541 us | 91.766 us | Managed faster | 0.76x |
| `replacement/no-match-string` | `ReplaceString` | 1.771 us | 2.417 us | Inconclusive | 0.73x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 45.856 us | 64.206 us | Managed faster | 0.72x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 181.472 us | 276.900 us | Managed faster | 0.66x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 178.877 us | 278.588 us | Managed faster | 0.64x |
| `replacement/one-template-string` | `ReplaceString` | 1.955 us | 16,508.942 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 1.018 us | 0.582 us | Inconclusive | 1.75x |
| `split/captures` | `SplitStrings` | 278.168 us | 265.814 us | CPython faster | 1.05x |
| `split/unicode` | `SplitStrings` | 38.674 us | 39.477 us | Inconclusive | 0.98x |
| `split/zero-width` | `SplitStrings` | 71.083 us | 77.497 us | Managed faster | 0.92x |
| `split/no-captures` | `SplitStrings` | 179.285 us | 231.876 us | Managed faster | 0.77x |
| `split/optional-capture` | `SplitStrings` | 57.022 us | 82.435 us | Managed faster | 0.69x |
| `split/detailed` | `SplitDetailed` | 61.676 us | 136.313 us | Managed faster | 0.45x |
| `split/one-separator` | `SplitStrings` | 0.858 us | 12.057 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.124 us | 24.968 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 68.955 us | 397.235 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 81.415 us | 628.877 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 51.013 us | 581.252 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 968.121 us | 359,172.435 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `anchor/fullmatch-miss`: Removable harness floors are 0.20 %/16.04 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.16 %/11.53 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 114.997 us / 48,465 B | 60.534 us | 112.019 us / 48,529 B | 59.394 us |
| `fallback` | 160.984 us / 55,849 B | 41.303 us | 136.528 us / 55,849 B | 40.850 us |
| `literal` | 99.731 us / 19,513 B | 24.634 us | 94.656 us / 19,513 B | 24.819 us |
| `unicode` | 78.356 us / 25,713 B | 36.207 us | 73.969 us / 25,713 B | 36.120 us |

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
