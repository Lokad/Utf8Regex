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

[Benchmark procedure](../../docs/BENCHMARKS.md) · [Release notes](../../CHANGELOG.md)

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `3B9E5E3766506B5ACD143F590D93CA4EC04A4094D7C487BB1F4C24C57D650CF8`.

Comparator Status: `76` managed faster, `1` equivalent, `5` native faster, `3` inconclusive, `0` unqualified, `23` excluded.

Measured source `b3bae2e09757`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.456 us | 1.161 us | Native faster | 1.25x |
| `common/ip-match` | 0.475 us | 0.416 us | Native faster | 1.14x |
| `industry/boostdocs-postcode-match` | 0.256 us | 0.250 us | Native faster | 1.02x |
| `common/email-match` | 0.388 us | 0.418 us | Managed faster | 0.93x |
| `common/uri-match` | 0.279 us | 0.312 us | Managed faster | 0.90x |
| `common/date-miss` | 0.604 us | 0.687 us | Managed faster | 0.88x |
| `simple/httpclient-caseless` | 0.187 us | 0.243 us | Managed faster | 0.77x |
| `backtracking/capture-backreference` | 0.180 us | 0.244 us | Managed faster | 0.74x |
| `industry/boostdocs-credit-card-match` | 0.203 us | 0.288 us | Managed faster | 0.71x |
| `industry/boostdocs-float-match` | 0.169 us | 0.249 us | Managed faster | 0.68x |
| `common/uri-miss` | 0.472 us | 0.716 us | Managed faster | 0.66x |
| `industry/boostdocs-ftp-line-match` | 0.249 us | 0.378 us | Managed faster | 0.66x |
| `simple/loglevel-multiline` | 0.195 us | 0.309 us | Managed faster | 0.63x |
| `simple/ab-plus` | 0.149 us | 0.236 us | Managed faster | 0.63x |
| `industry/boostdocs-date-match` | 0.159 us | 0.252 us | Managed faster | 0.63x |
| `common/date-match` | 0.174 us | 0.278 us | Managed faster | 0.63x |
| `literal/missing` | 0.078 us | 0.224 us | Managed faster | 0.34x |
| `literal/late` | 0.082 us | 0.267 us | Managed faster | 0.31x |
| `backtracking/assertion-capture` | 0.084 us | 0.305 us | Managed faster | 0.27x |
| `common/one-node-backtracking` | 0.046 us | 0.190 us | Managed faster | 0.24x |
| `simple/foo-dense` | 0.045 us | 0.224 us | Managed faster | 0.20x |
| `literal/early` | 0.043 us | 0.232 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.037 us | 0.227 us | Managed faster | 0.16x |
| `common/backtracking` | 0.192 us | 1.317 us | Managed faster | 0.15x |
| `common/email-miss` | 0.039 us | 0.513 us | Managed faster | 0.08x |
| `simple/foo-optional-bar` | 0.015 us | 0.248 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 141.677 us | 153.319 us | Managed faster | 0.92x |
| `industry/rust-sherlock-ing-count` | 9,401.575 us | 12,799.233 us | Managed faster | 0.74x |
| `simple/loglevel-multiline` | 0.212 us | 0.377 us | Managed faster | 0.56x |
| `industry/rust-sherlock-nonnewline-count` | 1,288.955 us | 2,459.012 us | Managed faster | 0.52x |
| `simple/ab-plus` | 0.166 us | 0.363 us | Managed faster | 0.46x |
| `simple/foo-optional-bar` | 0.141 us | 0.445 us | Managed faster | 0.32x |
| `simple/foo-dense` | 0.071 us | 0.241 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 636.954 us | 2,279.653 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 141.474 us | 654.427 us | Managed faster | 0.22x |
| `industry/mariomka-ip-count` | 3,198.508 us | 14,844.533 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.083 us | 0.397 us | Managed faster | 0.21x |
| `common/matches-words` | 9.371 us | 75.403 us | Managed faster | 0.12x |
| `common/matches-word` | 1.221 us | 18.720 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,041.443 us | 18,513.250 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,958.074 us | 36,117.500 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,871.086 us | 44,397.700 us | Inconclusive | 0.04x |
| `common/matches-set` | 6.964 us | 197.363 us | Managed faster | 0.04x |
| `common/matches-boundary` | 6.975 us | 201.163 us | Managed faster | 0.03x |
| `character/unicode-class-dense` | 9.149 us | 280.891 us | Managed faster | 0.03x |
| `industry/rust-sherlock-word-holmes-count` | 274.572 us | 19,483.650 us | Managed faster | 0.01x |
| `industry/mariomka-uri-count` | 1,698.636 us | 125,022.200 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 425.876 us | 137,322.700 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,076.921 us | 473,135.200 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.791 us | 0.803 us | Equivalent | 0.99x |
| `simple/ab-plus` | 0.901 us | 1.089 us | Managed faster | 0.83x |
| `simple/foo-dense` | 0.578 us | 0.817 us | Managed faster | 0.71x |
| `simple/httpclient-caseless` | 0.629 us | 1.099 us | Managed faster | 0.57x |
| `simple/foo-optional-bar` | 0.611 us | 1.178 us | Managed faster | 0.52x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 44.728 us | — | Excluded | — |
| `simple/ab-plus` | 1.734 us | — | Excluded | — |
| `simple/foo-dense` | 0.850 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.878 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.934 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.272 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.883 us | 0.656 us | Native faster | 1.35x |
| `pcre2/recursive-optional` | 0.901 us | 0.782 us | Native faster | 1.15x |
| `pcre2/subroutine-prefix-digits` | 0.224 us | 0.245 us | Managed faster | 0.91x |
| `pcre2/conditional-accept-negative-lookahead` | 0.178 us | 0.244 us | Managed faster | 0.73x |
| `pcre2/conditional-negative-lookahead` | 0.178 us | 0.245 us | Managed faster | 0.73x |
| `pcre2/conditional-lookahead` | 0.181 us | 0.262 us | Managed faster | 0.69x |
| `pcre2/grapheme-cluster` | 0.117 us | 0.222 us | Managed faster | 0.53x |
| `pcre2/backslash-c-literal` | 0.099 us | 0.230 us | Managed faster | 0.43x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.254 us | Inconclusive | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 8.610 us | 10.387 us | Managed faster | 0.83x |
| `pcre2/grapheme-cluster` | 1.928 us | 2.856 us | Managed faster | 0.68x |
| `pcre2/same-start-global` | 0.393 us | 0.657 us | Managed faster | 0.60x |
| `pcre2/kreset-global` | 0.113 us | 0.274 us | Managed faster | 0.41x |
| `pcre2/kreset-atomic-alt` | 0.124 us | 0.305 us | Managed faster | 0.40x |
| `pcre2/duplicate-names` | 0.138 us | 0.370 us | Managed faster | 0.37x |
| `pcre2/kreset-bar-or-baz` | 0.135 us | 0.377 us | Managed faster | 0.36x |
| `pcre2/branch-reset-basic` | 0.154 us | 0.456 us | Managed faster | 0.34x |
| `pcre2/branch-reset-followup` | 0.143 us | 0.444 us | Managed faster | 0.32x |
| `pcre2/branch-reset-backref` | 0.138 us | 0.469 us | Managed faster | 0.30x |
| `pcre2/branch-reset-nested` | 0.140 us | 0.559 us | Managed faster | 0.25x |
| `pcre2/kreset-captured-repeat` | 2.363 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.307 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.587 us | 0.874 us | Managed faster | 0.67x |
| `pcre2/kreset-global` | 0.528 us | 0.839 us | Managed faster | 0.63x |
| `pcre2/kreset-bar-or-baz` | 0.591 us | 0.947 us | Managed faster | 0.62x |
| `pcre2/duplicate-names` | 0.578 us | 0.932 us | Managed faster | 0.62x |
| `pcre2/branch-reset-followup` | 0.579 us | 1.007 us | Managed faster | 0.58x |
| `pcre2/grapheme-cluster` | 4.891 us | 8.535 us | Managed faster | 0.57x |
| `pcre2/branch-reset-backref` | 0.596 us | 1.042 us | Managed faster | 0.57x |
| `pcre2/branch-reset-nested` | 0.616 us | 1.150 us | Managed faster | 0.54x |
| `pcre2/branch-reset-basic` | 0.601 us | 1.178 us | Managed faster | 0.51x |
| `pcre2/same-start-global` | 0.577 us | 1.237 us | Managed faster | 0.47x |
| `literal/empty-unicode` | 8.240 us | 28.526 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.254 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.159 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.465 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.789 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.847 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.774 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.803 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.768 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.179 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.808 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.810 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.420 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.774 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.249 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.301 us | — | Excluded | — |

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
