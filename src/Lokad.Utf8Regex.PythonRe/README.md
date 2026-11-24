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

Snapshot SHA-256: `A9CAF870324507168F278876779CE80274FE2A9097BAADE9A05EB599029A320B`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `63` managed faster, `1` equivalent, `0` CPython faster, `8` inconclusive, `4` unqualified.

Measured source `60e68955ca4b`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `common/date-miss` | `IsMatch` | 0.714 us | 0.846 us | Managed faster | 0.85x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.337 us | 0.416 us | Managed faster | 0.81x |
| `multiline/search-miss` | `Search` | 0.233 us | 0.289 us | Managed faster | 0.81x |
| `reluctant/search-hit` | `Search` | 0.281 us | 0.461 us | Managed faster | 0.61x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.224 us | 0.367 us | Managed faster | 0.61x |
| `atomic/search-hit` | `Search` | 0.266 us | 0.442 us | Managed faster | 0.60x |
| `leading-inline/search-hit` | `Search` | 0.264 us | 0.444 us | Managed faster | 0.60x |
| `lookahead/search-miss` | `Search` | 0.128 us | 0.223 us | Managed faster | 0.58x |
| `possessive/search-hit` | `Search` | 0.240 us | 0.434 us | Managed faster | 0.55x |
| `scoped-inline/search-hit` | `Search` | 0.223 us | 0.407 us | Managed faster | 0.55x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.218 us | 0.414 us | Managed faster | 0.53x |
| `lookbehind/search-hit` | `Search` | 0.222 us | 0.439 us | Managed faster | 0.51x |
| `prefix/match` | `Match` | 0.187 us | 0.376 us | Managed faster | 0.50x |
| `unicode-category/search-miss` | `Search` | 0.118 us | 0.240 us | Managed faster | 0.49x |
| `alternation/search-miss` | `Search` | 0.128 us | 0.261 us | Managed faster | 0.49x |
| `ignorecase/search-hit` | `Search` | 0.210 us | 0.474 us | Managed faster | 0.44x |
| `multiline/search-hit` | `Search` | 0.199 us | 0.456 us | Managed faster | 0.44x |
| `ignorecase/search-miss` | `Search` | 0.135 us | 0.312 us | Managed faster | 0.43x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.117 us | 0.320 us | Managed faster | 0.36x |
| `greedy/search-miss` | `Search` | 0.087 us | 0.244 us | Managed faster | 0.36x |
| `unicode-option/search-hit` | `Search` | 0.214 us | 0.631 us | Managed faster | 0.34x |
| `ascii-option/search-hit` | `Search` | 0.147 us | 0.432 us | Managed faster | 0.34x |
| `possessive/search-miss` | `Search` | 0.079 us | 0.236 us | Managed faster | 0.33x |
| `atomic/search-miss` | `Search` | 0.086 us | 0.287 us | Managed faster | 0.30x |
| `lookahead/search-hit` | `Search` | 0.118 us | 0.409 us | Managed faster | 0.29x |
| `unicode-literal/search-miss` | `Search` | 0.072 us | 0.313 us | Managed faster | 0.23x |
| `literal/search-early` | `Search` | 0.066 us | 0.312 us | Managed faster | 0.21x |
| `unicode/fullmatch` | `FullMatch` | 3.473 us | 26.318 us | Managed faster | 0.13x |
| `literal/fullmatch` | `FullMatch` | 0.053 us | 0.453 us | Managed faster | 0.12x |
| `unicode-literal/search-hit` | `Search` | 0.065 us | 0.637 us | Managed faster | 0.10x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.053 us | 0.606 us | Managed faster | 0.09x |
| `literal/search` | `Search` | 1.089 us | 22.513 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.071 us | 22.141 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.050 us | 22.032 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.190 us | 0.208 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.221 us | 0.223 us | Unqualified | — |
| `common/ip-match` | `IsMatch` | 0.285 us | 0.324 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.237 us | 0.244 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.617 us | 1.683 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `findall/structural-many` | `FindAllStructural` | 86.741 us | 221.239 us | Managed faster | 0.39x |
| `count/from-offset` | `CountFromOffset` | 16.580 us | 48.498 us | Managed faster | 0.34x |
| `zero-width/count` | `Count` | 365.962 us | 1,090.870 us | Managed faster | 0.34x |
| `count/empty-progression` | `Count` | 99.497 us | 357.477 us | Managed faster | 0.28x |
| `family/count` | `Count` | 593.529 us | 2,467.501 us | Managed faster | 0.24x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 118.083 us | 635.346 us | Managed faster | 0.19x |
| `unicode/count` | `Count` | 47.326 us | 409.717 us | Managed faster | 0.12x |
| `class-run/count` | `Count` | 280.004 us | 2,632.234 us | Managed faster | 0.11x |
| `count/one` | `Count` | 0.157 us | 1.945 us | Managed faster | 0.08x |
| `count/zero` | `Count` | 0.295 us | 89.210 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.257 us | 139.961 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 1.045 us | 0.469 us | Inconclusive | 2.18x |
| `replacement/unmatched-template-string` | `ReplaceString` | 41.398 us | 45.905 us | Managed faster | 0.90x |
| `replacement/empty-progression-string` | `ReplaceString` | 20.334 us | 23.274 us | Managed faster | 0.87x |
| `replacement/fixed-string` | `ReplaceString` | 143.696 us | 174.431 us | Managed faster | 0.82x |
| `replacement/subn-string` | `SubnString` | 72.258 us | 88.549 us | Managed faster | 0.81x |
| `replacement/named-template-string` | `ReplaceString` | 0.772 us | 0.968 us | Inconclusive | 0.80x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 133.223 us | 173.716 us | Managed faster | 0.77x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 47.369 us | 63.211 us | Managed faster | 0.75x |
| `replacement/subn-utf8` | `SubnUtf8` | 66.048 us | 88.860 us | Managed faster | 0.75x |
| `replacement/no-match-string` | `ReplaceString` | 1.663 us | 2.364 us | Inconclusive | 0.70x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 183.829 us | 272.197 us | Managed faster | 0.68x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 182.168 us | 268.244 us | Managed faster | 0.68x |
| `replacement/one-template-string` | `ReplaceString` | 1.835 us | 16,090.241 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.928 us | 0.569 us | Inconclusive | 1.63x |
| `split/captures` | `SplitStrings` | 263.314 us | 261.269 us | Inconclusive | 1.01x |
| `split/unicode` | `SplitStrings` | 38.641 us | 39.069 us | Equivalent | 0.99x |
| `split/zero-width` | `SplitStrings` | 65.095 us | 75.815 us | Managed faster | 0.85x |
| `split/no-captures` | `SplitStrings` | 175.204 us | 229.990 us | Managed faster | 0.76x |
| `split/optional-capture` | `SplitStrings` | 50.456 us | 81.117 us | Managed faster | 0.62x |
| `split/detailed` | `SplitDetailed` | 58.526 us | 133.588 us | Managed faster | 0.44x |
| `split/one-separator` | `SplitStrings` | 0.744 us | 11.676 us | Inconclusive | 0.06x |
| `split/no-separator` | `SplitStrings` | 1.006 us | 24.482 us | Inconclusive | 0.04x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 69.173 us | 390.163 us | Managed faster | 0.18x |
| `corpus/code-declarations` | `Count` | 76.280 us | 610.703 us | Managed faster | 0.13x |
| `corpus/code-log-methods` | `Count` | 52.779 us | 569.331 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 968.340 us | 350,925.578 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/15.97 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.18 %/14.67 % of the lanes.
- `common/ip-match`: Removable harness floors are 0.14 %/10.03 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.18 %/13.85 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 109.631 us / 48,465 B | 58.441 us | 114.550 us / 48,497 B | 58.750 us |
| `fallback` | 171.294 us / 55,849 B | 41.657 us | 136.938 us / 55,849 B | 42.532 us |
| `literal` | 83.719 us / 17,473 B | 24.024 us | 79.963 us / 17,473 B | 24.617 us |
| `unicode` | 54.212 us / 22,113 B | 36.372 us | 52.872 us / 22,113 B | 36.570 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Pass |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Reject |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
