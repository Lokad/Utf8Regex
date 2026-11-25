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

Snapshot SHA-256: `884F74D8BCFE4630E8CDC6DD3CDB7EEAD64A2E18966B9B1A4AC73C8C69802ED2`.

Comparator Status: `70` managed faster, `0` equivalent, `8` native faster, `7` inconclusive, `0` unqualified, `23` excluded.

Measured source `adb38fbdac02`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.406 us | 1.183 us | Native faster | 1.18x |
| `common/ip-match` | 0.458 us | 0.421 us | Native faster | 1.09x |
| `industry/boostdocs-postcode-match` | 0.268 us | 0.249 us | Native faster | 1.07x |
| `common/uri-match` | 0.317 us | 0.313 us | Inconclusive | 1.01x |
| `common/date-miss` | 0.664 us | 0.689 us | Managed faster | 0.96x |
| `simple/httpclient-caseless` | 0.221 us | 0.243 us | Managed faster | 0.91x |
| `common/email-match` | 0.377 us | 0.420 us | Inconclusive | 0.90x |
| `industry/boostdocs-credit-card-match` | 0.247 us | 0.287 us | Managed faster | 0.86x |
| `backtracking/capture-backreference` | 0.184 us | 0.236 us | Managed faster | 0.77x |
| `industry/boostdocs-float-match` | 0.192 us | 0.248 us | Managed faster | 0.77x |
| `common/date-match` | 0.207 us | 0.278 us | Managed faster | 0.75x |
| `simple/loglevel-multiline` | 0.229 us | 0.308 us | Inconclusive | 0.74x |
| `industry/boostdocs-date-match` | 0.180 us | 0.249 us | Managed faster | 0.72x |
| `common/uri-miss` | 0.519 us | 0.724 us | Managed faster | 0.71x |
| `simple/ab-plus` | 0.153 us | 0.231 us | Managed faster | 0.67x |
| `industry/boostdocs-ftp-line-match` | 0.245 us | 0.373 us | Inconclusive | 0.65x |
| `literal/missing` | 0.076 us | 0.221 us | Managed faster | 0.35x |
| `literal/late` | 0.081 us | 0.264 us | Managed faster | 0.31x |
| `common/one-node-backtracking` | 0.055 us | 0.189 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.082 us | 0.300 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.044 us | 0.224 us | Managed faster | 0.20x |
| `literal/early` | 0.042 us | 0.230 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.036 us | 0.224 us | Managed faster | 0.16x |
| `common/backtracking` | 0.195 us | 1.293 us | Managed faster | 0.15x |
| `common/email-miss` | 0.045 us | 0.477 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.015 us | 0.248 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 140.770 us | 154.505 us | Managed faster | 0.91x |
| `industry/rust-sherlock-ing-count` | 10,038.275 us | 12,944.233 us | Managed faster | 0.78x |
| `simple/foo-optional-bar` | 0.298 us | 0.444 us | Managed faster | 0.67x |
| `simple/loglevel-multiline` | 0.226 us | 0.370 us | Managed faster | 0.61x |
| `industry/rust-sherlock-nonnewline-count` | 1,251.803 us | 2,405.465 us | Managed faster | 0.52x |
| `simple/ab-plus` | 0.182 us | 0.358 us | Managed faster | 0.51x |
| `simple/foo-dense` | 0.070 us | 0.230 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 633.567 us | 2,290.694 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 157.107 us | 652.874 us | Managed faster | 0.24x |
| `industry/mariomka-ip-count` | 3,235.033 us | 14,874.700 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.083 us | 0.386 us | Managed faster | 0.22x |
| `common/matches-words` | 9.318 us | 76.554 us | Managed faster | 0.12x |
| `common/matches-word` | 1.209 us | 18.469 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,122.638 us | 18,625.550 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,863.068 us | 36,000.100 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,800.673 us | 44,678.500 us | Managed faster | 0.04x |
| `common/matches-boundary` | 7.838 us | 203.583 us | Managed faster | 0.04x |
| `common/matches-set` | 7.661 us | 207.675 us | Inconclusive | 0.04x |
| `character/unicode-class-dense` | 9.303 us | 276.593 us | Managed faster | 0.03x |
| `industry/rust-sherlock-word-holmes-count` | 278.564 us | 19,563.550 us | Managed faster | 0.01x |
| `industry/mariomka-uri-count` | 1,736.900 us | 125,477.500 us | Inconclusive | 0.01x |
| `industry/mariomka-email-count` | 441.962 us | 136,656.200 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,059.608 us | 468,768.600 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.086 us | 0.815 us | Native faster | 1.33x |
| `simple/ab-plus` | 1.124 us | 1.092 us | Native faster | 1.03x |
| `simple/foo-dense` | 0.713 us | 0.822 us | Managed faster | 0.87x |
| `simple/foo-optional-bar` | 0.839 us | 1.177 us | Managed faster | 0.71x |
| `simple/httpclient-caseless` | 0.762 us | 1.115 us | Managed faster | 0.68x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 44.101 us | — | Excluded | — |
| `simple/ab-plus` | 1.766 us | — | Excluded | — |
| `simple/foo-dense` | 0.868 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.966 us | — | Excluded | — |
| `simple/httpclient-caseless` | 1.000 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.339 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.893 us | 0.652 us | Native faster | 1.37x |
| `pcre2/recursive-optional` | 0.915 us | 0.779 us | Native faster | 1.18x |
| `pcre2/subroutine-prefix-digits` | 0.232 us | 0.243 us | Managed faster | 0.95x |
| `pcre2/conditional-accept-negative-lookahead` | 0.199 us | 0.246 us | Managed faster | 0.81x |
| `pcre2/conditional-negative-lookahead` | 0.198 us | 0.247 us | Managed faster | 0.80x |
| `pcre2/conditional-lookahead` | 0.205 us | 0.262 us | Managed faster | 0.78x |
| `pcre2/grapheme-cluster` | 0.126 us | 0.222 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.115 us | 0.226 us | Managed faster | 0.51x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.249 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.463 us | 2.880 us | Native faster | 1.55x |
| `literal/empty-unicode` | 9.425 us | 10.167 us | Managed faster | 0.93x |
| `pcre2/kreset-atomic-alt` | 0.256 us | 0.299 us | Managed faster | 0.86x |
| `pcre2/duplicate-names` | 0.274 us | 0.351 us | Managed faster | 0.78x |
| `pcre2/kreset-bar-or-baz` | 0.264 us | 0.376 us | Managed faster | 0.70x |
| `pcre2/branch-reset-basic` | 0.299 us | 0.450 us | Managed faster | 0.67x |
| `pcre2/same-start-global` | 0.427 us | 0.661 us | Managed faster | 0.65x |
| `pcre2/branch-reset-followup` | 0.272 us | 0.433 us | Managed faster | 0.63x |
| `pcre2/kreset-global` | 0.164 us | 0.262 us | Managed faster | 0.63x |
| `pcre2/branch-reset-backref` | 0.284 us | 0.457 us | Managed faster | 0.62x |
| `pcre2/branch-reset-nested` | 0.300 us | 0.579 us | Managed faster | 0.52x |
| `pcre2/kreset-captured-repeat` | 2.398 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.358 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.807 us | 0.882 us | Managed faster | 0.92x |
| `pcre2/duplicate-names` | 0.795 us | 0.938 us | Managed faster | 0.85x |
| `pcre2/kreset-global` | 0.718 us | 0.848 us | Managed faster | 0.85x |
| `pcre2/kreset-bar-or-baz` | 0.802 us | 0.951 us | Managed faster | 0.84x |
| `pcre2/branch-reset-followup` | 0.803 us | 1.011 us | Managed faster | 0.80x |
| `pcre2/branch-reset-backref` | 0.809 us | 1.033 us | Managed faster | 0.78x |
| `pcre2/branch-reset-basic` | 0.828 us | 1.178 us | Managed faster | 0.71x |
| `pcre2/branch-reset-nested` | 0.826 us | 1.180 us | Managed faster | 0.70x |
| `pcre2/grapheme-cluster` | 5.130 us | 8.601 us | Managed faster | 0.60x |
| `pcre2/same-start-global` | 0.706 us | 1.251 us | Managed faster | 0.56x |
| `literal/empty-unicode` | 8.269 us | 28.297 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.383 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.290 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.336 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.875 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.939 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.862 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.873 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.861 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.308 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.896 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.879 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.697 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.799 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.376 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.726 us | — | Excluded | — |

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
