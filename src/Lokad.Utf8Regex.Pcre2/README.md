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

Snapshot SHA-256: `39D9DD76607BE448814EF2CAF1749794C2B8466C42B567CA2913966B8935A286`.

Comparator Status: `66` managed faster, `0` equivalent, `13` native faster, `6` inconclusive, `0` unqualified, `23` excluded.

Measured source `0b451393aca5`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.389 us | 1.198 us | Native faster | 1.16x |
| `common/ip-match` | 0.452 us | 0.427 us | Native faster | 1.05x |
| `industry/boostdocs-postcode-match` | 0.246 us | 0.255 us | Managed faster | 0.96x |
| `common/uri-match` | 0.277 us | 0.319 us | Managed faster | 0.86x |
| `common/email-match` | 0.365 us | 0.426 us | Managed faster | 0.86x |
| `common/date-miss` | 0.590 us | 0.730 us | Managed faster | 0.81x |
| `simple/httpclient-caseless` | 0.185 us | 0.248 us | Managed faster | 0.74x |
| `backtracking/capture-backreference` | 0.179 us | 0.248 us | Managed faster | 0.72x |
| `industry/boostdocs-float-match` | 0.166 us | 0.253 us | Managed faster | 0.66x |
| `industry/boostdocs-credit-card-match` | 0.201 us | 0.310 us | Managed faster | 0.65x |
| `common/uri-miss` | 0.467 us | 0.752 us | Managed faster | 0.62x |
| `industry/boostdocs-ftp-line-match` | 0.236 us | 0.380 us | Managed faster | 0.62x |
| `simple/ab-plus` | 0.146 us | 0.237 us | Managed faster | 0.62x |
| `simple/loglevel-multiline` | 0.185 us | 0.315 us | Managed faster | 0.59x |
| `industry/boostdocs-date-match` | 0.159 us | 0.290 us | Managed faster | 0.55x |
| `common/date-match` | 0.171 us | 0.317 us | Managed faster | 0.54x |
| `literal/missing` | 0.077 us | 0.227 us | Managed faster | 0.34x |
| `literal/late` | 0.080 us | 0.271 us | Managed faster | 0.30x |
| `backtracking/assertion-capture` | 0.081 us | 0.310 us | Managed faster | 0.26x |
| `common/one-node-backtracking` | 0.046 us | 0.196 us | Managed faster | 0.24x |
| `simple/foo-dense` | 0.044 us | 0.230 us | Managed faster | 0.19x |
| `literal/early` | 0.043 us | 0.236 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.037 us | 0.232 us | Managed faster | 0.16x |
| `common/backtracking` | 0.191 us | 1.300 us | Managed faster | 0.15x |
| `common/email-miss` | 0.040 us | 0.481 us | Managed faster | 0.08x |
| `simple/foo-optional-bar` | 0.015 us | 0.249 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 143.625 us | 153.462 us | Managed faster | 0.93x |
| `industry/rust-sherlock-ing-count` | 9,505.450 us | 13,021.200 us | Managed faster | 0.73x |
| `industry/rust-sherlock-nonnewline-count` | 1,282.119 us | 2,448.929 us | Managed faster | 0.52x |
| `simple/loglevel-multiline` | 0.224 us | 0.561 us | Managed faster | 0.40x |
| `simple/ab-plus` | 0.167 us | 0.533 us | Managed faster | 0.31x |
| `industry/leipzig-twain-count` | 660.023 us | 2,329.806 us | Managed faster | 0.28x |
| `simple/foo-optional-bar` | 0.145 us | 0.622 us | Managed faster | 0.23x |
| `industry/mariomka-ip-count` | 3,266.342 us | 15,012.800 us | Managed faster | 0.22x |
| `industry/rust-sherlock-holmes-window-count` | 140.485 us | 656.946 us | Managed faster | 0.21x |
| `simple/foo-dense` | 0.069 us | 0.422 us | Managed faster | 0.16x |
| `simple/httpclient-caseless` | 0.084 us | 0.571 us | Managed faster | 0.15x |
| `common/matches-words` | 9.284 us | 76.056 us | Managed faster | 0.12x |
| `common/matches-word` | 1.219 us | 18.803 us | Managed faster | 0.06x |
| `industry/leipzig-name-family-count` | 1,081.119 us | 18,377.000 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,944.830 us | 36,632.700 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,080.653 us | 44,792.000 us | Managed faster | 0.05x |
| `common/matches-boundary` | 6.902 us | 203.795 us | Managed faster | 0.03x |
| `common/matches-set` | 6.877 us | 206.903 us | Managed faster | 0.03x |
| `character/unicode-class-dense` | 9.029 us | 383.703 us | Managed faster | 0.02x |
| `industry/rust-sherlock-word-holmes-count` | 274.408 us | 19,837.150 us | Managed faster | 0.01x |
| `industry/mariomka-uri-count` | 1,671.642 us | 125,860.500 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 424.512 us | 137,380.700 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,077.750 us | 479,967.500 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.946 us | 0.559 us | Native faster | 1.70x |
| `simple/foo-dense` | 0.649 us | 0.445 us | Native faster | 1.46x |
| `simple/loglevel-multiline` | 0.841 us | 0.587 us | Native faster | 1.43x |
| `simple/httpclient-caseless` | 0.701 us | 0.600 us | Native faster | 1.17x |
| `simple/foo-optional-bar` | 0.660 us | 0.649 us | Inconclusive | 1.02x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 45.557 us | — | Excluded | — |
| `simple/ab-plus` | 1.728 us | — | Excluded | — |
| `simple/foo-dense` | 0.851 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.885 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.945 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.251 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.883 us | 0.676 us | Native faster | 1.31x |
| `pcre2/recursive-optional` | 0.901 us | 0.828 us | Native faster | 1.08x |
| `pcre2/subroutine-prefix-digits` | 0.230 us | 0.259 us | Managed faster | 0.89x |
| `pcre2/conditional-accept-negative-lookahead` | 0.175 us | 0.246 us | Managed faster | 0.71x |
| `pcre2/conditional-negative-lookahead` | 0.175 us | 0.248 us | Managed faster | 0.71x |
| `pcre2/conditional-lookahead` | 0.178 us | 0.264 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 0.115 us | 0.226 us | Managed faster | 0.51x |
| `pcre2/backslash-c-literal` | 0.105 us | 0.234 us | Managed faster | 0.45x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.253 us | Inconclusive | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 8.694 us | 10.340 us | Managed faster | 0.84x |
| `pcre2/grapheme-cluster` | 1.919 us | 3.104 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.393 us | 0.843 us | Managed faster | 0.46x |
| `pcre2/duplicate-names` | 0.159 us | 0.536 us | Managed faster | 0.30x |
| `pcre2/kreset-bar-or-baz` | 0.161 us | 0.551 us | Managed faster | 0.29x |
| `pcre2/branch-reset-basic` | 0.175 us | 0.633 us | Managed faster | 0.28x |
| `pcre2/branch-reset-followup` | 0.164 us | 0.612 us | Managed faster | 0.27x |
| `pcre2/kreset-atomic-alt` | 0.129 us | 0.477 us | Managed faster | 0.27x |
| `pcre2/branch-reset-backref` | 0.159 us | 0.642 us | Managed faster | 0.25x |
| `pcre2/kreset-global` | 0.104 us | 0.454 us | Managed faster | 0.23x |
| `pcre2/branch-reset-nested` | 0.160 us | 0.754 us | Managed faster | 0.21x |
| `pcre2/kreset-captured-repeat` | 2.306 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.280 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.816 us | 3.060 us | Native faster | 1.57x |
| `pcre2/kreset-atomic-alt` | 0.642 us | 0.505 us | Native faster | 1.28x |
| `pcre2/kreset-global` | 0.602 us | 0.476 us | Native faster | 1.26x |
| `pcre2/duplicate-names` | 0.647 us | 0.568 us | Native faster | 1.14x |
| `pcre2/kreset-bar-or-baz` | 0.646 us | 0.583 us | Native faster | 1.11x |
| `pcre2/branch-reset-basic` | 0.678 us | 0.661 us | Inconclusive | 1.03x |
| `pcre2/branch-reset-followup` | 0.645 us | 0.634 us | Inconclusive | 1.02x |
| `pcre2/branch-reset-backref` | 0.664 us | 0.673 us | Inconclusive | 0.99x |
| `pcre2/branch-reset-nested` | 0.675 us | 0.780 us | Managed faster | 0.86x |
| `literal/empty-unicode` | 7.917 us | 10.582 us | Managed faster | 0.75x |
| `pcre2/same-start-global` | 0.638 us | 0.871 us | Managed faster | 0.73x |
| `pcre2/kreset-captured-repeat` | 3.225 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.197 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.306 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.793 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.854 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.779 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.795 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.776 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.189 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.812 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.814 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.285 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.785 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.101 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.257 us | — | Excluded | — |

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
