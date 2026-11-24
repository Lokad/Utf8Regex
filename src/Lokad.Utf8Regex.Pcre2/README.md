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

Snapshot SHA-256: `02450708622D3452E07B01A79EDF9BE8E294EB7DA720CECAFCCD0D239105A5A1`.

Comparator Status: `69` managed faster, `0` equivalent, `11` native faster, `5` inconclusive, `0` unqualified, `23` excluded.

Measured source `77b3c71f31ff`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.390 us | 1.191 us | Native faster | 1.16x |
| `common/ip-match` | 0.469 us | 0.425 us | Native faster | 1.10x |
| `industry/boostdocs-postcode-match` | 0.269 us | 0.252 us | Native faster | 1.06x |
| `common/uri-match` | 0.332 us | 0.318 us | Native faster | 1.04x |
| `common/date-miss` | 0.690 us | 0.700 us | Inconclusive | 0.98x |
| `common/email-match` | 0.400 us | 0.419 us | Managed faster | 0.95x |
| `industry/boostdocs-credit-card-match` | 0.253 us | 0.291 us | Managed faster | 0.87x |
| `simple/httpclient-caseless` | 0.203 us | 0.246 us | Managed faster | 0.83x |
| `backtracking/capture-backreference` | 0.198 us | 0.241 us | Managed faster | 0.83x |
| `industry/boostdocs-float-match` | 0.195 us | 0.250 us | Managed faster | 0.78x |
| `industry/boostdocs-date-match` | 0.189 us | 0.251 us | Managed faster | 0.75x |
| `common/date-match` | 0.210 us | 0.279 us | Managed faster | 0.75x |
| `common/uri-miss` | 0.535 us | 0.737 us | Managed faster | 0.73x |
| `simple/ab-plus` | 0.167 us | 0.234 us | Managed faster | 0.71x |
| `industry/boostdocs-ftp-line-match` | 0.262 us | 0.378 us | Managed faster | 0.69x |
| `simple/loglevel-multiline` | 0.214 us | 0.314 us | Managed faster | 0.68x |
| `literal/missing` | 0.081 us | 0.217 us | Managed faster | 0.37x |
| `common/one-node-backtracking` | 0.071 us | 0.191 us | Managed faster | 0.37x |
| `literal/late` | 0.088 us | 0.259 us | Managed faster | 0.34x |
| `backtracking/assertion-capture` | 0.086 us | 0.306 us | Managed faster | 0.28x |
| `literal/early` | 0.057 us | 0.231 us | Managed faster | 0.25x |
| `literal/absolute-anchored` | 0.051 us | 0.226 us | Managed faster | 0.22x |
| `simple/foo-dense` | 0.043 us | 0.228 us | Managed faster | 0.19x |
| `common/backtracking` | 0.195 us | 1.306 us | Managed faster | 0.15x |
| `common/email-miss` | 0.064 us | 0.483 us | Managed faster | 0.13x |
| `simple/foo-optional-bar` | 0.015 us | 0.249 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.736 us | 0.354 us | Native faster | 2.08x |
| `simple/loglevel-multiline` | 0.498 us | 0.373 us | Native faster | 1.34x |
| `backtracking/alternation-repeat` | 142.872 us | 155.437 us | Managed faster | 0.92x |
| `simple/foo-optional-bar` | 0.330 us | 0.437 us | Managed faster | 0.75x |
| `industry/rust-sherlock-ing-count` | 9,757.400 us | 13,119.967 us | Managed faster | 0.75x |
| `industry/rust-sherlock-nonnewline-count` | 1,538.769 us | 2,439.863 us | Managed faster | 0.63x |
| `simple/foo-dense` | 0.070 us | 0.237 us | Inconclusive | 0.30x |
| `industry/leipzig-twain-count` | 663.338 us | 2,324.238 us | Managed faster | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | 164.398 us | 659.459 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,383.209 us | 15,047.500 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.082 us | 0.389 us | Managed faster | 0.21x |
| `common/matches-words` | 9.428 us | 77.046 us | Managed faster | 0.12x |
| `common/matches-word` | 1.235 us | 18.852 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,126.285 us | 18,897.050 us | Managed faster | 0.06x |
| `common/matches-boundary` | 11.571 us | 208.085 us | Managed faster | 0.06x |
| `common/matches-set` | 11.591 us | 210.544 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,883.276 us | 36,615.400 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,045.811 us | 45,900.600 us | Inconclusive | 0.04x |
| `character/unicode-class-dense` | 9.497 us | 274.823 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,848.121 us | 125,499.800 us | Inconclusive | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 275.507 us | 19,563.700 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 445.763 us | 137,749.100 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,091.073 us | 472,461.500 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.069 us | 0.811 us | Native faster | 1.32x |
| `simple/ab-plus` | 1.389 us | 1.100 us | Native faster | 1.26x |
| `simple/foo-dense` | 0.669 us | 0.820 us | Managed faster | 0.81x |
| `simple/foo-optional-bar` | 0.789 us | 1.206 us | Managed faster | 0.66x |
| `simple/httpclient-caseless` | 0.710 us | 1.091 us | Managed faster | 0.65x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 46.828 us | — | Excluded | — |
| `simple/ab-plus` | 1.927 us | — | Excluded | — |
| `simple/foo-dense` | 0.846 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.974 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.955 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.244 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.122 us | 0.780 us | Native faster | 1.44x |
| `pcre2/recursive-palindrome` | 0.904 us | 0.661 us | Native faster | 1.37x |
| `pcre2/subroutine-prefix-digits` | 0.233 us | 0.245 us | Managed faster | 0.95x |
| `pcre2/conditional-accept-negative-lookahead` | 0.210 us | 0.245 us | Managed faster | 0.86x |
| `pcre2/conditional-negative-lookahead` | 0.205 us | 0.244 us | Managed faster | 0.84x |
| `pcre2/conditional-lookahead` | 0.211 us | 0.261 us | Managed faster | 0.81x |
| `pcre2/grapheme-cluster` | 0.142 us | 0.221 us | Managed faster | 0.64x |
| `pcre2/backslash-c-literal` | 0.125 us | 0.227 us | Managed faster | 0.55x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.249 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.567 us | 2.865 us | Native faster | 1.59x |
| `pcre2/kreset-atomic-alt` | 0.275 us | 0.301 us | Managed faster | 0.92x |
| `literal/empty-unicode` | 9.086 us | 10.270 us | Managed faster | 0.88x |
| `pcre2/duplicate-names` | 0.297 us | 0.353 us | Managed faster | 0.84x |
| `pcre2/branch-reset-basic` | 0.337 us | 0.452 us | Managed faster | 0.75x |
| `pcre2/branch-reset-followup` | 0.302 us | 0.436 us | Managed faster | 0.69x |
| `pcre2/kreset-bar-or-baz` | 0.259 us | 0.376 us | Managed faster | 0.69x |
| `pcre2/branch-reset-backref` | 0.312 us | 0.458 us | Managed faster | 0.68x |
| `pcre2/same-start-global` | 0.403 us | 0.659 us | Managed faster | 0.61x |
| `pcre2/kreset-global` | 0.163 us | 0.267 us | Managed faster | 0.61x |
| `pcre2/branch-reset-nested` | 0.326 us | 0.568 us | Managed faster | 0.57x |
| `pcre2/kreset-captured-repeat` | 2.401 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.347 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.761 us | 0.880 us | Managed faster | 0.87x |
| `pcre2/duplicate-names` | 0.763 us | 0.929 us | Managed faster | 0.82x |
| `pcre2/kreset-global` | 0.682 us | 0.837 us | Managed faster | 0.82x |
| `pcre2/kreset-bar-or-baz` | 0.757 us | 0.939 us | Managed faster | 0.81x |
| `pcre2/branch-reset-followup` | 0.755 us | 0.989 us | Managed faster | 0.76x |
| `pcre2/branch-reset-backref` | 0.774 us | 1.028 us | Managed faster | 0.75x |
| `pcre2/branch-reset-basic` | 0.796 us | 1.170 us | Managed faster | 0.68x |
| `pcre2/branch-reset-nested` | 0.786 us | 1.181 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 5.165 us | 8.529 us | Managed faster | 0.60x |
| `pcre2/same-start-global` | 0.671 us | 1.238 us | Managed faster | 0.54x |
| `literal/empty-unicode` | 8.230 us | 28.264 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.371 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.275 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.596 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.883 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.967 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.864 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.889 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.861 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.449 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.904 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.891 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.663 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.816 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.270 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.690 us | — | Excluded | — |

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
