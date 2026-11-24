# Lokad.Utf8Regex.Pcre2

A managed implementation of selected PCRE2 10.47 standard-matcher semantics
for .NET 10 and 11. The adapter requires the exact matching Core package. It
ships without native binaries or interop.

## Subject input contract

Pass valid UTF-8 and keep borrowed bytes unchanged during use. Validate
external bytes as they become text. Patterns and UTF-8 templates have their
own validation rules. Starts and reported endpoints use byte offsets.
[Input and range rules](../../docs/SPEC-PCRE2.md#subject-input-contract)

## Supported scope and limitations

The adapter supports captures, backreferences, assertions, branch reset,
duplicate names, recursion, conditionals, atomic groups and backtracking
verbs. Use `Probe` for ordinary MARK results and supported soft or hard
partial decisions. The scope excludes DFA matching, callouts, invalid-UTF
subject mode, RTL, split and full capture history.
[Grammar and probe coverage](../../docs/SPEC-PCRE2.md)

Opted-in `\C` can return byte fragments. Check `IsUtf8SliceWellFormed`
before decoding them. Opted-in lookaround `\K` can report endpoints that
cannot be sliced. Check `HasContiguousByteRange` on a match before extracting
its bytes. [Reported ranges and global progress](../../docs/SPEC-PCRE2.md#reported-ranges-and-global-progress)

## Example

To copy captures into reusable storage, pass a span to `Match`.

```csharp
using Lokad.Utf8Regex.Pcre2;

Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[3];
var regex = new Utf8Pcre2Regex("(?<word>[a-z]+)-(?<number>[0-9]+)");
var match = regex.Match("abc-123"u8, captures: captures);
// Slot zero: [0,7); word: [0,3); number: [4,7), all byte coordinates.
```

Slot zero holds the whole match. A short destination receives the slots that
fit, and additional entries stay untouched. `CopyNumbersForUtf8Name` maps
a name to its numbered slots. `Match` and `Probe` can also report MARK.
[Capture slots and metadata](../../docs/SPEC-PCRE2.md#operations-and-results)

Use `EnumerateMatches` to stream ranges, or `CreateMatchCursor` for
last-capture snapshots across calls. `Replace` returns owned bytes.
`TryReplace` writes a caller buffer. Custom substitutions can use cursor
ranges and host-owned output.

```csharp
using System.Text;
using Lokad.Utf8Regex.Pcre2;

var regex = new Utf8Pcre2Regex(
    @"(?<scheme>https?)://(?<host>[A-Za-z0-9.-]+)",
    Pcre2CompileOptions.None);

ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(
    "Visit https://example.com and http://localhost today.");

var matches = regex.EnumerateMatches(input);
while (matches.MoveNext())
{
    var match = matches.Current;
    if (match.Success && match.IsUtf8SliceWellFormed)
    {
        Console.WriteLine(
            Encoding.UTF8.GetString(input.Slice(match.StartOffsetInBytes, match.EndOffsetInBytes - match.StartOffsetInBytes)));
    }
}

byte[] redacted = regex.Replace(input, "<$host>"u8);
```

## Resource controls and ownership

`Utf8RegexPolicy` sets match timeouts and optional work, depth, working-byte
and result-byte limits. Construction has separate limits and a deadline.
`Compile` also accepts a construction token. Operations check the original
cancellation token. Match timeouts and numeric denials throw
`Pcre2MatchException` with a specific error kind. The limits cover modeled
engine work and storage capacities.
[Interruption and resource controls](../../docs/SPEC-PCRE2.md#interruption-and-resource-controls)

Dispose a funded regex when finished. Admitted calls and memory cursors
can finish after close. Copies of a span enumerator have independent
positions and each advance needs an open funded regex. Memory cursor aliases
share progress and disposal. Dispose an abandoned cursor and keep its
borrowed input alive while using published ranges.
[Cursor and result lifetimes](../../docs/SPEC-PCRE2.md#cursor-and-result-lifetimes)

The tables compare public operations with equivalent outputs and valid UTF-8
on both sides. The native comparator uses `PCRE2_NO_UTF_CHECK`.
[Benchmark procedure](../../docs/BENCHMARKS.md) · [Release notes](../../CHANGELOG.md)

The `simple/ab-plus` Count ratio reflects the archived paired run. In fixed-CPU
replays with matching results and native settings, the native Count median
ranged from 0.34 to 1.07 µs across processes. Running a Count workload first
reproduced the faster timing with the same benchmark binary.

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `F1C94A7D0924EC301332967D37AD57D08EA3838319D9DDB4A71F4E1BBD3C52F2`.

Comparator Status: `73` managed faster, `1` equivalent, `8` native faster, `3` inconclusive, `0` unqualified, `23` excluded.

Measured source `2f863b8b1adf`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.307 us | 1.145 us | Native faster | 1.14x |
| `common/ip-match` | 0.429 us | 0.417 us | Native faster | 1.03x |
| `common/uri-match` | 0.299 us | 0.309 us | Managed faster | 0.97x |
| `common/date-miss` | 0.634 us | 0.667 us | Managed faster | 0.95x |
| `industry/boostdocs-postcode-match` | 0.232 us | 0.247 us | Managed faster | 0.94x |
| `common/email-match` | 0.354 us | 0.409 us | Managed faster | 0.86x |
| `simple/httpclient-caseless` | 0.191 us | 0.238 us | Managed faster | 0.80x |
| `industry/boostdocs-credit-card-match` | 0.216 us | 0.281 us | Managed faster | 0.77x |
| `industry/boostdocs-float-match` | 0.173 us | 0.242 us | Managed faster | 0.71x |
| `common/date-match` | 0.191 us | 0.278 us | Managed faster | 0.69x |
| `backtracking/capture-backreference` | 0.170 us | 0.249 us | Managed faster | 0.68x |
| `industry/boostdocs-date-match` | 0.165 us | 0.245 us | Managed faster | 0.67x |
| `common/uri-miss` | 0.480 us | 0.723 us | Managed faster | 0.67x |
| `simple/loglevel-multiline` | 0.198 us | 0.303 us | Managed faster | 0.65x |
| `industry/boostdocs-ftp-line-match` | 0.229 us | 0.365 us | Managed faster | 0.63x |
| `simple/ab-plus` | 0.136 us | 0.229 us | Managed faster | 0.60x |
| `literal/missing` | 0.073 us | 0.215 us | Managed faster | 0.34x |
| `literal/late` | 0.078 us | 0.258 us | Managed faster | 0.30x |
| `common/one-node-backtracking` | 0.051 us | 0.185 us | Managed faster | 0.28x |
| `backtracking/assertion-capture` | 0.080 us | 0.311 us | Managed faster | 0.26x |
| `simple/foo-dense` | 0.041 us | 0.219 us | Managed faster | 0.19x |
| `literal/early` | 0.040 us | 0.229 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.034 us | 0.219 us | Managed faster | 0.15x |
| `common/backtracking` | 0.176 us | 1.307 us | Managed faster | 0.13x |
| `common/email-miss` | 0.043 us | 0.464 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.015 us | 0.238 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.595 us | 0.357 us | Native faster | 1.68x |
| `simple/loglevel-multiline` | 0.463 us | 0.372 us | Native faster | 1.24x |
| `backtracking/alternation-repeat` | 137.092 us | 149.109 us | Managed faster | 0.92x |
| `industry/rust-sherlock-ing-count` | 9,505.000 us | 12,627.967 us | Managed faster | 0.75x |
| `simple/foo-optional-bar` | 0.287 us | 0.438 us | Managed faster | 0.66x |
| `industry/rust-sherlock-nonnewline-count` | 1,107.314 us | 2,372.765 us | Managed faster | 0.47x |
| `simple/foo-dense` | 0.068 us | 0.243 us | Inconclusive | 0.28x |
| `industry/leipzig-twain-count` | 615.891 us | 2,224.183 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 155.944 us | 636.814 us | Managed faster | 0.24x |
| `industry/mariomka-ip-count` | 3,231.008 us | 14,446.900 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.080 us | 0.389 us | Managed faster | 0.21x |
| `common/matches-words` | 9.094 us | 73.548 us | Managed faster | 0.12x |
| `common/matches-word` | 1.180 us | 18.091 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,092.364 us | 17,922.250 us | Managed faster | 0.06x |
| `common/matches-set` | 11.491 us | 191.131 us | Managed faster | 0.06x |
| `common/matches-boundary` | 11.468 us | 197.717 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,896.014 us | 35,655.000 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,762.073 us | 43,442.500 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 10.310 us | 263.535 us | Managed faster | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | 268.051 us | 19,160.650 us | Managed faster | 0.01x |
| `industry/mariomka-uri-count` | 1,689.040 us | 121,413.700 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 438.575 us | 133,233.000 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,039.115 us | 466,311.700 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.032 us | 0.776 us | Native faster | 1.33x |
| `simple/ab-plus` | 1.056 us | 1.051 us | Equivalent | 1.01x |
| `simple/foo-dense` | 0.644 us | 0.789 us | Managed faster | 0.82x |
| `simple/httpclient-caseless` | 0.722 us | 1.057 us | Managed faster | 0.68x |
| `simple/foo-optional-bar` | 0.770 us | 1.138 us | Managed faster | 0.68x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 51.825 us | — | Excluded | — |
| `simple/ab-plus` | 1.609 us | — | Excluded | — |
| `simple/foo-dense` | 0.822 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.965 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.919 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.157 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.071 us | 0.790 us | Native faster | 1.35x |
| `pcre2/recursive-palindrome` | 0.868 us | 0.663 us | Native faster | 1.31x |
| `pcre2/subroutine-prefix-digits` | 0.225 us | 0.240 us | Managed faster | 0.94x |
| `pcre2/conditional-accept-negative-lookahead` | 0.184 us | 0.237 us | Managed faster | 0.78x |
| `pcre2/conditional-negative-lookahead` | 0.184 us | 0.239 us | Managed faster | 0.77x |
| `pcre2/conditional-lookahead` | 0.190 us | 0.256 us | Managed faster | 0.74x |
| `pcre2/grapheme-cluster` | 0.124 us | 0.217 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.109 us | 0.224 us | Managed faster | 0.49x |
| `pcre2/branch-reset-backref` | 0.012 us | 0.249 us | Inconclusive | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.889 us | 2.812 us | Native faster | 1.73x |
| `literal/empty-unicode` | 8.416 us | 10.044 us | Managed faster | 0.84x |
| `pcre2/kreset-atomic-alt` | 0.250 us | 0.309 us | Managed faster | 0.81x |
| `pcre2/duplicate-names` | 0.264 us | 0.359 us | Managed faster | 0.73x |
| `pcre2/kreset-bar-or-baz` | 0.252 us | 0.371 us | Managed faster | 0.68x |
| `pcre2/branch-reset-basic` | 0.289 us | 0.448 us | Managed faster | 0.64x |
| `pcre2/branch-reset-followup` | 0.263 us | 0.427 us | Managed faster | 0.61x |
| `pcre2/same-start-global` | 0.401 us | 0.654 us | Managed faster | 0.61x |
| `pcre2/branch-reset-backref` | 0.278 us | 0.460 us | Managed faster | 0.60x |
| `pcre2/kreset-global` | 0.159 us | 0.275 us | Managed faster | 0.58x |
| `pcre2/branch-reset-nested` | 0.289 us | 0.562 us | Managed faster | 0.51x |
| `pcre2/kreset-captured-repeat` | 2.345 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.276 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.738 us | 0.844 us | Managed faster | 0.87x |
| `pcre2/duplicate-names` | 0.752 us | 0.891 us | Managed faster | 0.84x |
| `pcre2/kreset-global` | 0.657 us | 0.815 us | Managed faster | 0.81x |
| `pcre2/kreset-bar-or-baz` | 0.736 us | 0.906 us | Managed faster | 0.81x |
| `pcre2/branch-reset-followup` | 0.754 us | 0.969 us | Managed faster | 0.78x |
| `pcre2/branch-reset-backref` | 0.767 us | 1.001 us | Managed faster | 0.77x |
| `pcre2/branch-reset-nested` | 0.763 us | 1.104 us | Managed faster | 0.69x |
| `pcre2/branch-reset-basic` | 0.782 us | 1.142 us | Managed faster | 0.68x |
| `pcre2/grapheme-cluster` | 5.059 us | 8.228 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.649 us | 1.210 us | Managed faster | 0.54x |
| `literal/empty-unicode` | 7.854 us | 27.454 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.335 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.258 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 13.767 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.858 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.937 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.848 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.883 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.851 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.353 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.882 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.874 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.212 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.813 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 9.907 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.601 us | — | Excluded | — |

<details>
<summary>Qualification, allocations and scaling evidence</summary>

The [summary and raw-evidence reference](../../bench/Snapshots/Pcre2.json) retains per-row confidence intervals, exclusion reasons, input/source/binary identities, allocation probes, native build/plan fingerprints and managed/comparator workspace contracts. Managed-thread allocation probes do not measure retained native memory. Reusable match buffers are neither thread-safe nor reentrant; finite/token/deadline costs remain separate from these ordinary rows.

Benchmark comparator: [PCRE.NET 1.5.0](https://www.nuget.org/packages/PCRE.NET/1.5.0), native PCRE2 `10.47 2025-10-21`, license `BSD-3-Clause WITH PCRE2-exception`. This private benchmark dependency is absent from shipped projects.

- `pcre2-managed-compatible-replace/common/replace-words`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-managed-compatible-replace/simple/ab-plus`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-managed-compatible-replace/simple/foo-dense`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-managed-compatible-replace/simple/foo-optional-bar`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-managed-compatible-replace/simple/httpclient-caseless`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-managed-compatible-replace/simple/loglevel-multiline`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-count/pcre2/kreset-captured-repeat`: Structured checksum mismatch: managed=10D75503432F5794, comparator=9EB15884DB649A19.
- `pcre2-special-count/pcre2/kreset-repeat`: Structured checksum mismatch: managed=10D75503432F5794, comparator=9EB15884DB649A19.
- `pcre2-special-enumerate/pcre2/kreset-captured-repeat`: Structured checksum mismatch: managed=D112C845A4F1BD78, comparator=027ACCB0FF65E9DB.
- `pcre2-special-enumerate/pcre2/kreset-repeat`: Structured checksum mismatch: managed=D112C845A4F1BD78, comparator=027ACCB0FF65E9DB.
- `pcre2-special-replace/literal/empty-unicode`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/branch-reset-backref`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/branch-reset-basic`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/branch-reset-followup`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/branch-reset-nested`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/duplicate-names`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/grapheme-cluster`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/kreset-atomic-alt`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/kreset-bar-or-baz`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/kreset-captured-repeat`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/kreset-global`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/kreset-repeat`: PCRE.NET does not expose equivalent UTF-8 span replacement output.
- `pcre2-special-replace/pcre2/same-start-global`: PCRE.NET does not expose equivalent UTF-8 span replacement output.

### Scaling evidence

Contextual bounded families retain construction, first-call, warm and allocation observations in the snapshot; their point timings do not set paired Status.

| Family | Operation | Points |
| --- | --- | ---: |
| `branch-repeat-linear` | IsMatch | 4 |
| `branch-reset-coordinate-projection` | EnumerateMatches | 4 |
| `candidate-heavy-misses` | IsMatch | 4 |
| `capture-rollback` | Count | 4 |
| `cartesian-literal-families` | IsMatch | 4 |
| `character-class-dense` | Count | 4 |
| `dense-non-ascii-coordinates` | Count | 4 |
| `dense-plus-sparse-candidate-portfolios` | Count | 6 |
| `excluded-ascii-repeat-count` | Count | 4 |
| `leading-word-boundary-run-candidates` | Count | 4 |
| `literal-family-global-cursor` | Replace | 4 |
| `long-flat-patterns` | IsMatch | 4 |
| `replacement-growth` | Replace | 4 |
| `required-literal-all-a-miss` | Count | 4 |
| `single-token-repeat-vm` | IsMatch | 4 |
| `zero-width-iteration` | Count | 4 |

Regenerate with `--rewrite-pcre2-benchmark-markdown`; validate with `--verify-pcre2-benchmark-markdown`. Use `--verify-pcre2-qualification-consistency` and `--emit-pcre2-priority-report` for the full evidence through the repository benchmark script.

</details>
<!-- END GENERATED PCRE2 BENCHMARKS -->
