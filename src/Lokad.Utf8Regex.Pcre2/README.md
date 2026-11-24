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

Snapshot SHA-256: `15D882E2AD1BC439BD489A83213156870F2FEDE24211AC0BD2D9D1007F7F5797`.

Comparator Status: `67` managed faster, `1` equivalent, `9` native faster, `8` inconclusive, `0` unqualified, `23` excluded.

Measured source `59ff946cdba9`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.353 us | 1.162 us | Native faster | 1.16x |
| `common/ip-match` | 0.453 us | 0.416 us | Native faster | 1.09x |
| `common/uri-match` | 0.311 us | 0.312 us | Equivalent | 1.00x |
| `industry/boostdocs-postcode-match` | 0.247 us | 0.252 us | Inconclusive | 0.98x |
| `common/date-miss` | 0.681 us | 0.745 us | Managed faster | 0.91x |
| `simple/httpclient-caseless` | 0.216 us | 0.242 us | Managed faster | 0.89x |
| `common/email-match` | 0.361 us | 0.417 us | Managed faster | 0.86x |
| `industry/boostdocs-credit-card-match` | 0.235 us | 0.296 us | Managed faster | 0.79x |
| `backtracking/capture-backreference` | 0.191 us | 0.246 us | Managed faster | 0.78x |
| `simple/ab-plus` | 0.172 us | 0.234 us | Managed faster | 0.74x |
| `simple/loglevel-multiline` | 0.223 us | 0.306 us | Managed faster | 0.73x |
| `industry/boostdocs-float-match` | 0.184 us | 0.253 us | Managed faster | 0.73x |
| `common/date-match` | 0.200 us | 0.279 us | Managed faster | 0.72x |
| `common/uri-miss` | 0.513 us | 0.722 us | Managed faster | 0.71x |
| `industry/boostdocs-date-match` | 0.173 us | 0.252 us | Managed faster | 0.69x |
| `industry/boostdocs-ftp-line-match` | 0.235 us | 0.377 us | Managed faster | 0.63x |
| `literal/late` | 0.090 us | 0.262 us | Inconclusive | 0.34x |
| `literal/missing` | 0.077 us | 0.224 us | Managed faster | 0.34x |
| `common/one-node-backtracking` | 0.056 us | 0.190 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.084 us | 0.306 us | Managed faster | 0.27x |
| `literal/absolute-anchored` | 0.053 us | 0.228 us | Managed faster | 0.23x |
| `simple/foo-dense` | 0.044 us | 0.223 us | Managed faster | 0.20x |
| `literal/early` | 0.042 us | 0.232 us | Managed faster | 0.18x |
| `common/backtracking` | 0.196 us | 1.424 us | Managed faster | 0.14x |
| `common/email-miss` | 0.046 us | 0.481 us | Managed faster | 0.10x |
| `simple/foo-optional-bar` | 0.016 us | 0.244 us | Inconclusive | 0.07x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.737 us | 0.348 us | Native faster | 2.12x |
| `simple/loglevel-multiline` | 0.500 us | 0.367 us | Native faster | 1.37x |
| `backtracking/alternation-repeat` | 142.505 us | 148.599 us | Managed faster | 0.96x |
| `industry/rust-sherlock-ing-count` | 9,570.300 us | 12,921.400 us | Managed faster | 0.74x |
| `simple/foo-optional-bar` | 0.291 us | 0.433 us | Managed faster | 0.67x |
| `industry/rust-sherlock-nonnewline-count` | 1,307.094 us | 2,444.812 us | Managed faster | 0.53x |
| `simple/foo-dense` | 0.070 us | 0.229 us | Inconclusive | 0.31x |
| `industry/leipzig-twain-count` | 660.754 us | 2,304.794 us | Managed faster | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | 163.413 us | 666.155 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,405.055 us | 14,895.033 us | Managed faster | 0.23x |
| `simple/httpclient-caseless` | 0.085 us | 0.386 us | Managed faster | 0.22x |
| `common/matches-words` | 9.402 us | 75.108 us | Managed faster | 0.13x |
| `common/matches-word` | 1.222 us | 21.717 us | Managed faster | 0.06x |
| `industry/leipzig-name-family-count` | 1,112.138 us | 21,486.850 us | Managed faster | 0.05x |
| `industry/rust-sherlock-letter-count` | 1,835.341 us | 36,891.400 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,089.879 us | 44,492.900 us | Inconclusive | 0.05x |
| `common/matches-set` | 10.176 us | 262.489 us | Managed faster | 0.04x |
| `common/matches-boundary` | 10.259 us | 269.115 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 10.268 us | 331.872 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,798.295 us | 125,767.600 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 263.821 us | 19,558.400 us | Inconclusive | 0.01x |
| `industry/mariomka-email-count` | 457.277 us | 137,033.600 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,081.861 us | 484,846.400 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.041 us | 0.811 us | Native faster | 1.28x |
| `simple/ab-plus` | 1.215 us | 1.086 us | Native faster | 1.12x |
| `simple/foo-dense` | 0.683 us | 0.817 us | Managed faster | 0.84x |
| `simple/foo-optional-bar` | 0.801 us | 1.177 us | Managed faster | 0.68x |
| `simple/httpclient-caseless` | 0.727 us | 1.097 us | Managed faster | 0.66x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 47.121 us | — | Excluded | — |
| `simple/ab-plus` | 1.915 us | — | Excluded | — |
| `simple/foo-dense` | 0.858 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.008 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.940 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.076 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.096 us | 0.770 us | Native faster | 1.42x |
| `pcre2/recursive-palindrome` | 0.883 us | 0.652 us | Native faster | 1.35x |
| `pcre2/subroutine-prefix-digits` | 0.231 us | 0.246 us | Managed faster | 0.94x |
| `pcre2/conditional-accept-negative-lookahead` | 0.198 us | 0.245 us | Managed faster | 0.81x |
| `pcre2/conditional-negative-lookahead` | 0.197 us | 0.246 us | Managed faster | 0.80x |
| `pcre2/conditional-lookahead` | 0.203 us | 0.263 us | Managed faster | 0.78x |
| `pcre2/grapheme-cluster` | 0.127 us | 0.222 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.118 us | 0.227 us | Managed faster | 0.52x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.251 us | Inconclusive | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.580 us | 2.882 us | Native faster | 1.59x |
| `literal/empty-unicode` | 9.285 us | 10.149 us | Managed faster | 0.92x |
| `pcre2/kreset-atomic-alt` | 0.257 us | 0.290 us | Managed faster | 0.88x |
| `pcre2/duplicate-names` | 0.274 us | 0.347 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.262 us | 0.365 us | Managed faster | 0.72x |
| `pcre2/branch-reset-basic` | 0.300 us | 0.448 us | Managed faster | 0.67x |
| `pcre2/branch-reset-followup` | 0.274 us | 0.419 us | Managed faster | 0.65x |
| `pcre2/branch-reset-backref` | 0.288 us | 0.441 us | Managed faster | 0.65x |
| `pcre2/kreset-global` | 0.164 us | 0.261 us | Managed faster | 0.63x |
| `pcre2/same-start-global` | 0.404 us | 0.651 us | Managed faster | 0.62x |
| `pcre2/branch-reset-nested` | 0.301 us | 0.553 us | Managed faster | 0.55x |
| `pcre2/kreset-captured-repeat` | 2.408 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.330 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.776 us | 0.874 us | Managed faster | 0.89x |
| `pcre2/kreset-bar-or-baz` | 0.768 us | 0.944 us | Managed faster | 0.81x |
| `pcre2/kreset-global` | 0.679 us | 0.841 us | Managed faster | 0.81x |
| `pcre2/duplicate-names` | 0.751 us | 0.933 us | Managed faster | 0.80x |
| `pcre2/branch-reset-followup` | 0.757 us | 1.011 us | Managed faster | 0.75x |
| `pcre2/branch-reset-backref` | 0.770 us | 1.041 us | Managed faster | 0.74x |
| `pcre2/branch-reset-nested` | 0.789 us | 1.146 us | Managed faster | 0.69x |
| `pcre2/branch-reset-basic` | 0.787 us | 1.185 us | Managed faster | 0.66x |
| `pcre2/grapheme-cluster` | 5.140 us | 8.586 us | Managed faster | 0.60x |
| `pcre2/same-start-global` | 0.689 us | 1.237 us | Managed faster | 0.56x |
| `literal/empty-unicode` | 8.527 us | 28.516 us | Managed faster | 0.30x |
| `pcre2/kreset-captured-repeat` | 3.354 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.294 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.815 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.902 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.985 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.891 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.909 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.891 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.468 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.916 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.928 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.610 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.829 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.106 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.709 us | — | Excluded | — |

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
