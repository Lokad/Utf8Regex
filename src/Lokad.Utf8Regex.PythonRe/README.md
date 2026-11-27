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

`Utf8RegexPolicy` sets match timeouts and optional depth, working-byte
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

Snapshot SHA-256: `0CC5A93A0176DB9591A1FCB24A368806DD8C83B2FE910B0281956A1A9C46C1F9`. Catalog SHA-256: `F6BD468373285EBCF911F7BC6EC28FEC18721E9397A5B4C544F3DDCA579BF38F`.

Public Status: `66` managed faster, `0` equivalent, `0` CPython faster, `7` inconclusive, `3` unqualified.

Measured source `c37ca066c91f`: .NET 11.0.0; CPython 3.13.2. Selective refreshes retain each row's recorded identity.

### Direct matching

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `multiline/search-miss` | `Search` | 0.231 us | 0.280 us | Managed faster | 0.82x |
| `verbose/fullmatch-hit` | `FullMatch` | 0.316 us | 0.405 us | Managed faster | 0.78x |
| `common/date-miss` | `IsMatch` | 0.636 us | 0.824 us | Managed faster | 0.77x |
| `common/ip-match` | `IsMatch` | 0.204 us | 0.314 us | Managed faster | 0.66x |
| `reluctant/search-hit` | `Search` | 0.292 us | 0.460 us | Managed faster | 0.63x |
| `atomic/search-hit` | `Search` | 0.259 us | 0.420 us | Managed faster | 0.62x |
| `backreference/fullmatch-hit` | `FullMatch` | 0.211 us | 0.360 us | Managed faster | 0.59x |
| `possessive/search-hit` | `Search` | 0.232 us | 0.419 us | Managed faster | 0.55x |
| `scoped-inline/search-hit` | `Search` | 0.218 us | 0.400 us | Managed faster | 0.55x |
| `dotall/fullmatch-hit` | `FullMatch` | 0.227 us | 0.420 us | Managed faster | 0.54x |
| `lookahead/search-miss` | `Search` | 0.116 us | 0.217 us | Managed faster | 0.53x |
| `prefix/match` | `Match` | 0.185 us | 0.367 us | Managed faster | 0.50x |
| `leading-inline/search-hit` | `Search` | 0.207 us | 0.434 us | Managed faster | 0.48x |
| `multiline/search-hit` | `Search` | 0.202 us | 0.436 us | Managed faster | 0.46x |
| `ignorecase/search-hit` | `Search` | 0.210 us | 0.457 us | Managed faster | 0.46x |
| `unicode-category/search-miss` | `Search` | 0.106 us | 0.233 us | Managed faster | 0.45x |
| `lookbehind/search-hit` | `Search` | 0.195 us | 0.435 us | Managed faster | 0.45x |
| `ignorecase/search-miss` | `Search` | 0.121 us | 0.305 us | Managed faster | 0.40x |
| `alternation/search-miss` | `Search` | 0.099 us | 0.257 us | Managed faster | 0.39x |
| `search-offset/literal-hit` | `SearchFromOffset` | 0.105 us | 0.311 us | Managed faster | 0.34x |
| `unicode-option/search-hit` | `Search` | 0.201 us | 0.609 us | Managed faster | 0.33x |
| `greedy/search-miss` | `Search` | 0.075 us | 0.241 us | Managed faster | 0.31x |
| `ascii-option/search-hit` | `Search` | 0.124 us | 0.409 us | Managed faster | 0.30x |
| `possessive/search-miss` | `Search` | 0.066 us | 0.229 us | Managed faster | 0.29x |
| `atomic/search-miss` | `Search` | 0.075 us | 0.280 us | Managed faster | 0.27x |
| `lookahead/search-hit` | `Search` | 0.107 us | 0.409 us | Managed faster | 0.26x |
| `unicode-literal/search-miss` | `Search` | 0.073 us | 0.303 us | Managed faster | 0.24x |
| `literal/search-early` | `Search` | 0.069 us | 0.308 us | Managed faster | 0.22x |
| `literal/fullmatch` | `FullMatch` | 0.062 us | 0.451 us | Managed faster | 0.14x |
| `unicode/fullmatch` | `FullMatch` | 3.358 us | 25.691 us | Managed faster | 0.13x |
| `unicode-literal/search-hit` | `Search` | 0.069 us | 0.617 us | Managed faster | 0.11x |
| `supplementary/fullmatch-hit` | `FullMatch` | 0.060 us | 0.579 us | Managed faster | 0.10x |
| `literal/search` | `Search` | 1.045 us | 21.616 us | Managed faster | 0.05x |
| `literal/search-miss` | `Search` | 1.037 us | 21.478 us | Managed faster | 0.05x |
| `literal/ismatch` | `IsMatch` | 1.025 us | 21.411 us | Managed faster | 0.05x |
| `anchor/fullmatch-miss` | `FullMatch` | 0.188 us | 0.203 us | Unqualified | — |
| `backreference/named-fullmatch-miss` | `FullMatch` | 0.206 us | 0.221 us | Unqualified | — |
| `lookbehind/search-miss` | `Search` | 0.204 us | 0.237 us | Unqualified | — |

### Detailed and scalar projections

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `capture/search-detailed` | `SearchDetailed` | 0.604 us | 1.637 us | Managed faster | 0.37x |

### Count, FindAll, and FindIter

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `count/from-offset` | `CountFromOffset` | 20.073 us | 48.190 us | Managed faster | 0.42x |
| `findall/structural-many` | `FindAllStructural` | 83.610 us | 214.807 us | Managed faster | 0.39x |
| `zero-width/count` | `Count` | 377.546 us | 1,078.477 us | Managed faster | 0.35x |
| `count/empty-progression` | `Count` | 100.478 us | 350.949 us | Managed faster | 0.29x |
| `family/count` | `Count` | 442.509 us | 2,393.502 us | Managed faster | 0.18x |
| `iteration/finditer-detailed` | `FindIterDetailed` | 110.626 us | 615.079 us | Managed faster | 0.18x |
| `unicode/count` | `Count` | 54.736 us | 414.118 us | Managed faster | 0.13x |
| `count/one` | `Count` | 0.246 us | 1.906 us | Managed faster | 0.13x |
| `class-run/count` | `Count` | 238.537 us | 2,504.709 us | Managed faster | 0.10x |
| `count/zero` | `Count` | 0.246 us | 87.014 us | Managed faster | 0.00x |
| `finditer/zero-detailed` | `FindIterDetailed` | 0.223 us | 136.444 us | Managed faster | 0.00x |

### Replace and Subn

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `replacement/limited-string` | `ReplaceStringLimited` | 0.934 us | 0.455 us | Inconclusive | 2.01x |
| `replacement/empty-progression-string` | `ReplaceString` | 20.082 us | 22.539 us | Managed faster | 0.89x |
| `replacement/unmatched-template-string` | `ReplaceString` | 39.300 us | 46.288 us | Managed faster | 0.85x |
| `replacement/fixed-string` | `ReplaceString` | 138.576 us | 167.790 us | Managed faster | 0.82x |
| `replacement/named-template-string` | `ReplaceString` | 0.720 us | 0.906 us | Managed faster | 0.80x |
| `replacement/subn-string` | `SubnString` | 68.833 us | 88.681 us | Managed faster | 0.78x |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | 47.244 us | 61.383 us | Managed faster | 0.77x |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 128.420 us | 171.385 us | Managed faster | 0.75x |
| `replacement/subn-utf8` | `SubnUtf8` | 64.548 us | 88.160 us | Managed faster | 0.73x |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 185.775 us | 265.786 us | Managed faster | 0.70x |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 183.797 us | 267.551 us | Managed faster | 0.69x |
| `replacement/no-match-string` | `ReplaceString` | 1.482 us | 2.308 us | Inconclusive | 0.64x |
| `replacement/one-template-string` | `ReplaceString` | 1.546 us | 15,781.451 us | Inconclusive | 0.00x |

### Split

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `split/limited` | `SplitStringsLimited` | 0.621 us | 0.554 us | Inconclusive | 1.16x |
| `split/captures` | `SplitStrings` | 253.184 us | 254.194 us | Inconclusive | 0.99x |
| `split/unicode` | `SplitStrings` | 36.103 us | 37.728 us | Managed faster | 0.96x |
| `split/zero-width` | `SplitStrings` | 65.565 us | 73.979 us | Managed faster | 0.88x |
| `split/optional-capture` | `SplitStrings` | 53.426 us | 79.095 us | Managed faster | 0.67x |
| `split/no-captures` | `SplitStrings` | 100.562 us | 223.050 us | Managed faster | 0.45x |
| `split/detailed` | `SplitDetailed` | 58.256 us | 130.670 us | Managed faster | 0.44x |
| `split/one-separator` | `SplitStrings` | 0.540 us | 11.423 us | Inconclusive | 0.05x |
| `split/no-separator` | `SplitStrings` | 0.729 us | 23.803 us | Inconclusive | 0.03x |

### Real-corpus workloads

| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |
| --- | --- | ---: | ---: | --- | ---: |
| `corpus/sherlock-ru` | `Count` | 66.848 us | 381.121 us | Managed faster | 0.17x |
| `corpus/code-declarations` | `Count` | 73.276 us | 599.342 us | Managed faster | 0.12x |
| `corpus/code-log-methods` | `Count` | 49.735 us | 558.827 us | Managed faster | 0.09x |
| `corpus/mail-email-count` | `Count` | 927.407 us | 342,390.191 us | Managed faster | 0.00x |

<details>
<summary>Qualification, allocations and lifecycle evidence</summary>

The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows.

- `anchor/fullmatch-miss`: Removable harness floors are 0.21 %/16.65 % of the lanes.
- `backreference/named-fullmatch-miss`: Removable harness floors are 0.18 %/14.87 % of the lanes.
- `lookbehind/search-miss`: Removable harness floors are 0.24 %/13.82 % of the lanes.

### Construction and first call

Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.
These recorded construction-only rows include former metadata-array copies and PythonRe's duplicate name table. Current lifecycle consumers read capture counts and use canonical scalar names; historical figures do not qualify that revised work.

| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |
| --- | ---: | ---: | ---: | ---: |
| `captured` | 79.025 us / 34,537 B | 56.983 us | 79.666 us / 34,569 B | 56.693 us |
| `fallback` | 119.144 us / 34,841 B | 41.912 us | 102.278 us / 34,841 B | 39.848 us |
| `literal` | 53.631 us / 11,881 B | 23.704 us | 50.256 us / 11,881 B | 23.260 us |
| `unicode` | 20.569 us / 14,273 B | 36.169 us | 14.172 us / 14,273 B | 34.369 us |

### Scaling evidence

Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.

| Family | Dimension | Points | Fit gate |
| --- | --- | ---: | --- |
| `candidate-position` | candidate byte position | 4 | Pass |
| `capture-count` | capture group count | 4 | Reject |
| `input-length` | UTF-8 input bytes | 4 | Reject |
| `match-count` | discovered match count | 4 | Pass |
| `output-growth` | replacement output UTF-8 bytes | 4 | Pass |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | 4 | Pass |
| `zero-width-progression` | zero-width-aware result count | 4 | Reject |

Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.

</details>
<!-- END GENERATED PYTHONRE BENCHMARKS -->
