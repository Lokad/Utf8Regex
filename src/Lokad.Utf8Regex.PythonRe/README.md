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

Snapshot SHA-256: `7EA0EABE6D0C7F4533F45A9146A269AE1BC639042E120FC6C5C45894375BC115`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `60` managed faster, `0` equivalent, `4` CPython faster, `8` inconclusive, `4` unqualified.

Measured source `efc3e32e0c45`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `atomic/search-miss` | `Search` | 0.352 us | 0.291 us | CPython faster | 1.21x |
| `reluctant/search-hit` | `Search` | 0.470 us | 0.435 us | CPython faster | 1.08x |
| `lookbehind/search-miss` | `Search` | 0.258 us | 0.248 us | CPython faster | 1.04x |
| `common/date-miss` | `IsMatch` | 0.769 us | 0.866 us | Managed faster | 0.89x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.331 us | 0.385 us | Managed faster | 0.86x |
| `possessive/search-hit` | `Search` | 0.292 us | 0.396 us | Managed faster | 0.74x |
| `atomic/search-hit` | `Search` | 0.308 us | 0.423 us | Managed faster | 0.73x |
| `scoped-inline/search-hit` | `Search` | 0.236 us | 0.377 us | Managed faster | 0.63x |
| `leading-inline/search-hit` | `Search` | 0.268 us | 0.432 us | Managed faster | 0.62x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.229 us | 0.375 us | Managed faster | 0.61x |
| `lookahead/search-miss` | `Search` | 0.135 us | 0.227 us | Managed faster | 0.60x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.228 us | 0.387 us | Managed faster | 0.59x |
| `lookbehind/search-hit` | `Search` | 0.238 us | 0.420 us | Managed faster | 0.57x |
| `prefix/match` | `Match` | 0.189 us | 0.360 us | Managed faster | 0.52x |
| `unicode-category/search-miss` | `Search` | 0.125 us | 0.242 us | Managed faster | 0.51x |
| `alternation/search-miss` | `Search` | 0.135 us | 0.262 us | Managed faster | 0.51x |
| `multiline/search-hit` | `Search` | 0.211 us | 0.422 us | Managed faster | 0.50x |
| `ignorecase/search-hit` | `Search` | 0.219 us | 0.444 us | Managed faster | 0.49x |
| `ignorecase/search-miss` | `Search` | 0.140 us | 0.315 us | Managed faster | 0.44x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.121 us | 0.307 us | Managed faster | 0.39x |
| `greedy/search-miss` | `Search` | 0.094 us | 0.250 us | Managed faster | 0.38x |
| `ascii-option/search-hit` | `Search` | 0.150 us | 0.408 us | Managed faster | 0.37x |
| `unicode-option/search-hit` | `Search` | 0.233 us | 0.649 us | Managed faster | 0.36x |
| `possessive/search-miss` | `Search` | 0.085 us | 0.240 us | Managed faster | 0.35x |
| `lookahead/search-hit` | `Search` | 0.122 us | 0.383 us | Managed faster | 0.32x |
| `unicode-literal/search-miss` | `Search` | 0.072 us | 0.318 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.068 us | 0.302 us | Managed faster | 0.22x |
| `unicode/fullmatch` | `FullMatch` | 3.545 us | 26.613 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.054 us | 0.428 us | Managed faster | 0.13x |
| `unicode-literal/search-hit` | `Search` | 0.066 us | 0.636 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.055 us | 0.613 us | Managed faster | 0.09x |
| `literal/search` | `Search` | 1.092 us | 22.618 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.074 us | 22.291 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.065 us | 22.317 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.197 us | 0.212 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.231 us | 0.227 us | Unqualified | — |
| `common/ip-match` | `IsMatch` | 0.324 us | 0.324 us | Unqualified | — |
| `multiline/search-miss` | `Search` | 0.256 us | 0.294 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.626 us | 1.745 us | Managed faster | 0.36x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 85.225 us | 222.589 us | Managed faster | 0.38x |
| `count/empty-progression` | `Count` | 140.219 us | 369.998 us | Managed faster | 0.38x |
| `zero-width/count` | `Count` | 413.376 us | 1,110.901 us | Managed faster | 0.37x |
| `count/from-offset` | `CountFromOffset` | 17.274 us | 49.086 us | Managed faster | 0.35x |
| `family/count` | `Count` | 600.354 us | 2,504.995 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 114.916 us | 651.808 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 47.862 us | 428.802 us | Managed faster | 0.11x |
| `class-run/count` | `Count` | 267.424 us | 2,594.217 us | Managed faster | 0.10x |
| `count/one` | `Count` | 0.180 us | 1.974 us | Managed faster | 0.09x |
| `count/zero` | `Count` | 0.296 us | 90.552 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.275 us | 140.085 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.112 us | 0.476 us | Inconclusive | 2.31x |
| `replacement/empty-progression-string` | `ReplaceString` | 26.419 us | 23.575 us | CPython faster | 1.13x |
| `replacement/named-template-string` | `ReplaceString` | 0.850 us | 0.966 us | Managed faster | 0.88x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.711 us | 47.607 us | Managed faster | 0.88x |
| `replacement/fixed-string` | `ReplaceString` | 148.534 us | 176.650 us | Managed faster | 0.84x |
| `replacement/subn-string` | `SubnString` | 73.288 us | 90.674 us | Managed faster | 0.81x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 137.065 us | 177.538 us | Managed faster | 0.77x |
| `replacement/subn-utf8` | `SubnUtf8` | 68.809 us | 89.818 us | Managed faster | 0.77x |
| `replacement/no-match-string` | `ReplaceString` | 1.719 us | 2.414 us | Inconclusive | 0.71x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 45.441 us | 64.425 us | Managed faster | 0.71x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 179.328 us | 278.185 us | Managed faster | 0.65x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 176.248 us | 279.639 us | Managed faster | 0.63x |
| `replacement/one-template-string` | `ReplaceString` | 1.912 us | 16,484.197 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.998 us | 0.581 us | Inconclusive | 1.69x |
| `split/captures` | `SplitStrings` | 268.692 us | 266.699 us | Inconclusive | 1.01x |
| `split/unicode` | `SplitStrings` | 38.663 us | 39.339 us | Inconclusive | 0.98x |
| `split/zero-width` | `SplitStrings` | 73.136 us | 77.964 us | Managed faster | 0.94x |
| `split/no-captures` | `SplitStrings` | 179.281 us | 233.732 us | Managed faster | 0.77x |
| `split/optional-capture` | `SplitStrings` | 55.661 us | 82.449 us | Managed faster | 0.68x |
| `split/detailed` | `SplitDetailed` | 62.331 us | 136.169 us | Managed faster | 0.46x |
| `split/one-separator` | `SplitStrings` | 0.795 us | 11.986 us | Inconclusive | 0.07x |
| `split/no-separator` | `SplitStrings` | 1.112 us | 24.955 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.361 us | 397.268 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 78.539 us | 627.896 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 50.411 us | 582.351 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 979.412 us | 361,282.058 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/16.00 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.17 %/14.99 % of the lanes.
- `common/ip-match`: Removable harness floors are 0.12 %/10.59 % of the lanes.
- `multiline/search-miss`: Removable harness floors are 0.17 %/11.61 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 108.875 us / 48,465 B | 59.756 us | 96.072 us / 48,529 B | 60.520 us |
| `fallback` | 164.759 us / 55,849 B | 42.050 us | 135.144 us / 55,849 B | 42.299 us |
| `literal` | 99.669 us / 19,513 B | 24.661 us | 92.612 us / 19,513 B | 24.648 us |
| `unicode` | 76.588 us / 25,713 B | 36.051 us | 73.394 us / 25,713 B | 36.310 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Reject |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Pass |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
