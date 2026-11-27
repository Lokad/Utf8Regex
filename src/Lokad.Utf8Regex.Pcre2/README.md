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
partial decisions. Numbered callouts such as `(?C1)` match without invoking
a callback. The scope excludes string callouts, DFA matching, invalid-UTF
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

`Utf8RegexPolicy` sets match timeouts and optional depth, working-byte
and result-byte limits. Construction has separate limits and a deadline.
`Compile` also accepts a construction token. Operations check the original
cancellation token. Match timeouts and numeric denials throw
`Pcre2MatchException` with a specific error kind. The limits cover modeled
engine depth and storage capacities.
[Interruption and resource controls](../../docs/SPEC-PCRE2.md#interruption-and-resource-controls)

Dispose a funded regex when finished. Admitted calls and memory cursors
can finish after close. Copies of a span enumerator have independent
positions and each advance needs an open funded regex. Memory cursor aliases
share progress and disposal. Dispose an abandoned cursor and keep its
borrowed input alive while using published ranges.
[Cursor and result lifetimes](../../docs/SPEC-PCRE2.md#cursor-and-result-lifetimes)

[Benchmark procedure](../../docs/BENCHMARKS.md) · [Release notes](../../CHANGELOG.md)

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `34127BCDE41E0EEBA1D2A623880292D5D8A02730737C624A60343D9FA3AFB83F`.

Comparator Status: `76` managed faster, `1` equivalent, `5` native faster, `3` inconclusive, `0` unqualified, `23` excluded.

Measured source `dabd1655590e`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.429 us | 1.182 us | Native faster | 1.22x |
| `common/ip-match` | 0.453 us | 0.424 us | Native faster | 1.07x |
| `industry/boostdocs-postcode-match` | 0.253 us | 0.253 us | Equivalent | 1.00x |
| `common/email-match` | 0.366 us | 0.417 us | Managed faster | 0.88x |
| `common/uri-match` | 0.267 us | 0.317 us | Managed faster | 0.84x |
| `common/date-miss` | 0.580 us | 0.690 us | Managed faster | 0.84x |
| `simple/httpclient-caseless` | 0.188 us | 0.242 us | Managed faster | 0.78x |
| `backtracking/capture-backreference` | 0.179 us | 0.247 us | Managed faster | 0.73x |
| `industry/boostdocs-credit-card-match` | 0.199 us | 0.290 us | Managed faster | 0.69x |
| `industry/boostdocs-float-match` | 0.163 us | 0.250 us | Managed faster | 0.65x |
| `simple/ab-plus` | 0.147 us | 0.233 us | Managed faster | 0.63x |
| `industry/boostdocs-date-match` | 0.157 us | 0.252 us | Managed faster | 0.62x |
| `common/date-match` | 0.169 us | 0.276 us | Managed faster | 0.61x |
| `industry/boostdocs-ftp-line-match` | 0.229 us | 0.378 us | Managed faster | 0.60x |
| `common/uri-miss` | 0.444 us | 0.736 us | Managed faster | 0.60x |
| `simple/loglevel-multiline` | 0.184 us | 0.309 us | Managed faster | 0.59x |
| `literal/missing` | 0.076 us | 0.222 us | Managed faster | 0.34x |
| `literal/late` | 0.081 us | 0.267 us | Managed faster | 0.30x |
| `backtracking/assertion-capture` | 0.083 us | 0.308 us | Managed faster | 0.27x |
| `common/one-node-backtracking` | 0.045 us | 0.189 us | Managed faster | 0.24x |
| `simple/foo-dense` | 0.044 us | 0.223 us | Managed faster | 0.20x |
| `literal/early` | 0.041 us | 0.232 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.035 us | 0.229 us | Managed faster | 0.15x |
| `common/backtracking` | 0.189 us | 1.290 us | Managed faster | 0.15x |
| `common/email-miss` | 0.038 us | 0.477 us | Managed faster | 0.08x |
| `simple/foo-optional-bar` | 0.015 us | 0.241 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 141.748 us | 153.143 us | Managed faster | 0.93x |
| `industry/rust-sherlock-ing-count` | 9,135.280 us | 13,010.600 us | Managed faster | 0.70x |
| `simple/loglevel-multiline` | 0.210 us | 0.374 us | Managed faster | 0.56x |
| `industry/rust-sherlock-nonnewline-count` | 1,282.810 us | 2,458.547 us | Managed faster | 0.52x |
| `simple/ab-plus` | 0.161 us | 0.356 us | Managed faster | 0.45x |
| `simple/foo-optional-bar` | 0.141 us | 0.450 us | Managed faster | 0.31x |
| `simple/foo-dense` | 0.069 us | 0.232 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 658.875 us | 2,315.375 us | Managed faster | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | 148.311 us | 657.989 us | Managed faster | 0.23x |
| `industry/mariomka-ip-count` | 3,205.383 us | 15,061.933 us | Managed faster | 0.21x |
| `simple/httpclient-caseless` | 0.083 us | 0.390 us | Managed faster | 0.21x |
| `common/matches-words` | 9.305 us | 76.162 us | Managed faster | 0.12x |
| `common/matches-word` | 1.216 us | 18.529 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,063.381 us | 18,534.200 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,961.340 us | 36,624.000 us | Managed faster | 0.05x |
| `common/matches-boundary` | 9.463 us | 206.745 us | Managed faster | 0.05x |
| `common/matches-set` | 9.392 us | 208.987 us | Managed faster | 0.04x |
| `industry/leipzig-river-window-count` | 1,958.775 us | 45,039.900 us | Inconclusive | 0.04x |
| `character/unicode-class-dense` | 10.380 us | 272.453 us | Managed faster | 0.04x |
| `industry/mariomka-uri-count` | 1,841.739 us | 126,895.300 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 271.488 us | 19,637.450 us | Inconclusive | 0.01x |
| `industry/mariomka-email-count` | 453.923 us | 138,052.600 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,084.949 us | 463,035.700 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.748 us | 0.804 us | Managed faster | 0.93x |
| `simple/ab-plus` | 0.877 us | 1.087 us | Managed faster | 0.81x |
| `simple/foo-dense` | 0.587 us | 0.816 us | Managed faster | 0.72x |
| `simple/httpclient-caseless` | 0.664 us | 1.105 us | Managed faster | 0.60x |
| `simple/foo-optional-bar` | 0.589 us | 1.178 us | Managed faster | 0.50x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 41.037 us | — | Excluded | — |
| `simple/ab-plus` | 1.741 us | — | Excluded | — |
| `simple/foo-dense` | 0.853 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.877 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.935 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.203 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.908 us | 0.660 us | Native faster | 1.38x |
| `pcre2/recursive-optional` | 0.934 us | 0.777 us | Native faster | 1.20x |
| `pcre2/subroutine-prefix-digits` | 0.255 us | 0.245 us | Native faster | 1.04x |
| `pcre2/conditional-accept-negative-lookahead` | 0.172 us | 0.243 us | Managed faster | 0.71x |
| `pcre2/conditional-negative-lookahead` | 0.174 us | 0.246 us | Managed faster | 0.70x |
| `pcre2/conditional-lookahead` | 0.177 us | 0.262 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 0.114 us | 0.219 us | Managed faster | 0.52x |
| `pcre2/backslash-c-literal` | 0.099 us | 0.228 us | Managed faster | 0.43x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.255 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 8.264 us | 10.270 us | Managed faster | 0.81x |
| `pcre2/grapheme-cluster` | 1.916 us | 2.895 us | Managed faster | 0.66x |
| `pcre2/same-start-global` | 0.388 us | 0.669 us | Managed faster | 0.58x |
| `pcre2/kreset-atomic-alt` | 0.123 us | 0.296 us | Managed faster | 0.41x |
| `pcre2/kreset-global` | 0.106 us | 0.269 us | Managed faster | 0.40x |
| `pcre2/duplicate-names` | 0.138 us | 0.366 us | Managed faster | 0.38x |
| `pcre2/kreset-bar-or-baz` | 0.136 us | 0.374 us | Managed faster | 0.36x |
| `pcre2/branch-reset-basic` | 0.162 us | 0.446 us | Managed faster | 0.36x |
| `pcre2/branch-reset-followup` | 0.145 us | 0.442 us | Managed faster | 0.33x |
| `pcre2/branch-reset-backref` | 0.139 us | 0.468 us | Managed faster | 0.30x |
| `pcre2/branch-reset-nested` | 0.139 us | 0.565 us | Managed faster | 0.25x |
| `pcre2/kreset-captured-repeat` | 2.326 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.263 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.565 us | 0.871 us | Managed faster | 0.65x |
| `pcre2/kreset-bar-or-baz` | 0.593 us | 0.943 us | Managed faster | 0.63x |
| `pcre2/kreset-global` | 0.530 us | 0.844 us | Managed faster | 0.63x |
| `pcre2/duplicate-names` | 0.571 us | 0.933 us | Managed faster | 0.61x |
| `pcre2/branch-reset-followup` | 0.571 us | 1.013 us | Managed faster | 0.56x |
| `pcre2/branch-reset-backref` | 0.582 us | 1.038 us | Managed faster | 0.56x |
| `pcre2/grapheme-cluster` | 4.731 us | 8.499 us | Managed faster | 0.56x |
| `pcre2/branch-reset-nested` | 0.601 us | 1.156 us | Managed faster | 0.52x |
| `pcre2/branch-reset-basic` | 0.596 us | 1.179 us | Managed faster | 0.51x |
| `pcre2/same-start-global` | 0.566 us | 1.236 us | Managed faster | 0.46x |
| `literal/empty-unicode` | 7.806 us | 28.389 us | Managed faster | 0.28x |
| `pcre2/kreset-captured-repeat` | 3.168 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.063 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.199 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.781 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.852 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.769 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.789 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.766 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.166 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.798 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.803 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.125 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.761 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 9.791 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.287 us | — | Excluded | — |

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
