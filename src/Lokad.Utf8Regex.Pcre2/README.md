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

Snapshot SHA-256: `3489F1FF360646F772A782D6F299E1ECCB93A56BF6E8AB39A39CC7D6241DC7A1`.

Comparator Status: `69` managed faster, `2` equivalent, `9` native faster, `5` inconclusive, `0` unqualified, `23` excluded.

Measured source `022dedc80645`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.380 us | 1.149 us | Native faster | 1.20x |
| `common/ip-match` | 0.458 us | 0.413 us | Native faster | 1.11x |
| `common/uri-match` | 0.320 us | 0.310 us | Native faster | 1.03x |
| `common/date-miss` | 0.689 us | 0.682 us | Equivalent | 1.01x |
| `industry/boostdocs-postcode-match` | 0.247 us | 0.250 us | Equivalent | 0.99x |
| `common/email-match` | 0.380 us | 0.426 us | Managed faster | 0.89x |
| `simple/httpclient-caseless` | 0.210 us | 0.242 us | Managed faster | 0.87x |
| `industry/boostdocs-credit-card-match` | 0.231 us | 0.287 us | Managed faster | 0.81x |
| `backtracking/capture-backreference` | 0.186 us | 0.236 us | Managed faster | 0.79x |
| `common/date-match` | 0.207 us | 0.277 us | Managed faster | 0.75x |
| `industry/boostdocs-float-match` | 0.183 us | 0.246 us | Managed faster | 0.75x |
| `common/uri-miss` | 0.517 us | 0.713 us | Managed faster | 0.72x |
| `simple/loglevel-multiline` | 0.211 us | 0.307 us | Managed faster | 0.69x |
| `industry/boostdocs-date-match` | 0.171 us | 0.251 us | Managed faster | 0.68x |
| `industry/boostdocs-ftp-line-match` | 0.249 us | 0.378 us | Managed faster | 0.66x |
| `simple/ab-plus` | 0.146 us | 0.234 us | Managed faster | 0.62x |
| `literal/missing` | 0.076 us | 0.222 us | Inconclusive | 0.34x |
| `literal/late` | 0.082 us | 0.262 us | Managed faster | 0.31x |
| `common/one-node-backtracking` | 0.056 us | 0.188 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.083 us | 0.302 us | Managed faster | 0.28x |
| `simple/foo-dense` | 0.044 us | 0.223 us | Managed faster | 0.20x |
| `literal/early` | 0.043 us | 0.231 us | Managed faster | 0.19x |
| `literal/absolute-anchored` | 0.037 us | 0.227 us | Managed faster | 0.16x |
| `common/backtracking` | 0.182 us | 1.339 us | Managed faster | 0.14x |
| `common/email-miss` | 0.046 us | 0.480 us | Managed faster | 0.10x |
| `simple/foo-optional-bar` | 0.015 us | 0.242 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.613 us | 0.350 us | Native faster | 1.75x |
| `simple/loglevel-multiline` | 0.508 us | 0.365 us | Native faster | 1.40x |
| `backtracking/alternation-repeat` | 141.078 us | 146.820 us | Managed faster | 0.96x |
| `industry/rust-sherlock-ing-count` | 9,834.650 us | 12,827.267 us | Managed faster | 0.77x |
| `simple/foo-optional-bar` | 0.290 us | 0.428 us | Managed faster | 0.68x |
| `industry/rust-sherlock-nonnewline-count` | 1,286.119 us | 2,420.519 us | Managed faster | 0.53x |
| `simple/foo-dense` | 0.070 us | 0.230 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 633.758 us | 2,263.718 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 161.711 us | 657.044 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,345.182 us | 14,752.333 us | Managed faster | 0.23x |
| `simple/httpclient-caseless` | 0.083 us | 0.382 us | Managed faster | 0.22x |
| `common/matches-words` | 9.372 us | 74.537 us | Managed faster | 0.13x |
| `common/matches-word` | 1.207 us | 18.045 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,095.269 us | 18,342.750 us | Managed faster | 0.06x |
| `common/matches-set` | 11.437 us | 192.582 us | Managed faster | 0.06x |
| `common/matches-boundary` | 11.238 us | 199.146 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 2,042.785 us | 36,926.000 us | Inconclusive | 0.06x |
| `industry/leipzig-river-window-count` | 2,029.437 us | 43,748.200 us | Inconclusive | 0.05x |
| `character/unicode-class-dense` | 9.942 us | 270.847 us | Managed faster | 0.04x |
| `industry/mariomka-uri-count` | 1,837.058 us | 124,436.500 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 263.920 us | 19,272.850 us | Inconclusive | 0.01x |
| `industry/mariomka-email-count` | 454.267 us | 135,900.700 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,055.772 us | 470,817.000 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.997 us | 0.803 us | Native faster | 1.24x |
| `simple/ab-plus` | 1.025 us | 1.082 us | Managed faster | 0.95x |
| `simple/foo-dense` | 0.671 us | 0.812 us | Managed faster | 0.83x |
| `simple/foo-optional-bar` | 0.790 us | 1.172 us | Managed faster | 0.67x |
| `simple/httpclient-caseless` | 0.723 us | 1.097 us | Managed faster | 0.66x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 61.114 us | — | Excluded | — |
| `simple/ab-plus` | 1.739 us | — | Excluded | — |
| `simple/foo-dense` | 0.827 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.967 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.925 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.083 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.101 us | 0.776 us | Native faster | 1.42x |
| `pcre2/recursive-palindrome` | 0.893 us | 0.659 us | Native faster | 1.36x |
| `pcre2/subroutine-prefix-digits` | 0.230 us | 0.245 us | Managed faster | 0.94x |
| `pcre2/conditional-negative-lookahead` | 0.193 us | 0.245 us | Managed faster | 0.79x |
| `pcre2/conditional-accept-negative-lookahead` | 0.194 us | 0.245 us | Managed faster | 0.79x |
| `pcre2/conditional-lookahead` | 0.198 us | 0.266 us | Managed faster | 0.75x |
| `pcre2/grapheme-cluster` | 0.126 us | 0.220 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.116 us | 0.227 us | Managed faster | 0.51x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.253 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.576 us | 2.920 us | Native faster | 1.57x |
| `pcre2/kreset-atomic-alt` | 0.259 us | 0.292 us | Managed faster | 0.89x |
| `literal/empty-unicode` | 8.945 us | 10.105 us | Managed faster | 0.88x |
| `pcre2/duplicate-names` | 0.276 us | 0.350 us | Managed faster | 0.78x |
| `pcre2/kreset-bar-or-baz` | 0.257 us | 0.363 us | Managed faster | 0.71x |
| `pcre2/branch-reset-basic` | 0.296 us | 0.448 us | Managed faster | 0.66x |
| `pcre2/branch-reset-followup` | 0.269 us | 0.418 us | Managed faster | 0.64x |
| `pcre2/branch-reset-backref` | 0.284 us | 0.447 us | Managed faster | 0.63x |
| `pcre2/kreset-global` | 0.165 us | 0.261 us | Managed faster | 0.63x |
| `pcre2/same-start-global` | 0.406 us | 0.652 us | Managed faster | 0.62x |
| `pcre2/branch-reset-nested` | 0.299 us | 0.554 us | Managed faster | 0.54x |
| `pcre2/kreset-captured-repeat` | 2.422 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.357 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.755 us | 0.874 us | Managed faster | 0.86x |
| `pcre2/duplicate-names` | 0.753 us | 0.930 us | Managed faster | 0.81x |
| `pcre2/kreset-bar-or-baz` | 0.755 us | 0.932 us | Managed faster | 0.81x |
| `pcre2/kreset-global` | 0.675 us | 0.839 us | Managed faster | 0.80x |
| `pcre2/branch-reset-followup` | 0.758 us | 0.999 us | Managed faster | 0.76x |
| `pcre2/branch-reset-backref` | 0.767 us | 1.033 us | Managed faster | 0.74x |
| `pcre2/branch-reset-nested` | 0.789 us | 1.156 us | Managed faster | 0.68x |
| `pcre2/branch-reset-basic` | 0.787 us | 1.180 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 5.209 us | 8.526 us | Managed faster | 0.61x |
| `pcre2/same-start-global` | 0.671 us | 1.239 us | Managed faster | 0.54x |
| `literal/empty-unicode` | 8.495 us | 28.419 us | Managed faster | 0.30x |
| `pcre2/kreset-captured-repeat` | 3.303 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.238 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.473 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.909 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.005 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.898 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.908 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.897 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.519 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.893 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.878 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.671 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.813 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.321 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.675 us | — | Excluded | — |

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
