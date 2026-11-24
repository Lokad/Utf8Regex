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

Snapshot SHA-256: `F95D46F317364719EE3140147325C16C3245E99323DC581763404B050FB79A24`.

Comparator Status: `67` managed faster, `1` equivalent, `9` native faster, `8` inconclusive, `0` unqualified, `23` excluded.

Measured source `efc3e32e0c45`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.368 us | 1.205 us | Native faster | 1.13x |
| `common/ip-match` | 0.452 us | 0.426 us | Native faster | 1.06x |
| `industry/boostdocs-postcode-match` | 0.251 us | 0.251 us | Equivalent | 1.00x |
| `common/date-miss` | 0.685 us | 0.697 us | Inconclusive | 0.99x |
| `common/uri-match` | 0.312 us | 0.318 us | Inconclusive | 0.99x |
| `common/email-match` | 0.379 us | 0.420 us | Managed faster | 0.90x |
| `simple/httpclient-caseless` | 0.207 us | 0.241 us | Managed faster | 0.86x |
| `industry/boostdocs-credit-card-match` | 0.244 us | 0.289 us | Managed faster | 0.84x |
| `backtracking/capture-backreference` | 0.197 us | 0.242 us | Managed faster | 0.81x |
| `simple/ab-plus` | 0.174 us | 0.235 us | Managed faster | 0.74x |
| `industry/boostdocs-float-match` | 0.185 us | 0.250 us | Managed faster | 0.74x |
| `common/date-match` | 0.203 us | 0.282 us | Managed faster | 0.72x |
| `common/uri-miss` | 0.508 us | 0.733 us | Managed faster | 0.69x |
| `simple/loglevel-multiline` | 0.216 us | 0.313 us | Managed faster | 0.69x |
| `industry/boostdocs-date-match` | 0.173 us | 0.254 us | Managed faster | 0.68x |
| `industry/boostdocs-ftp-line-match` | 0.249 us | 0.381 us | Managed faster | 0.65x |
| `literal/missing` | 0.076 us | 0.224 us | Managed faster | 0.34x |
| `literal/late` | 0.082 us | 0.263 us | Managed faster | 0.31x |
| `common/one-node-backtracking` | 0.055 us | 0.190 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.084 us | 0.308 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.043 us | 0.223 us | Managed faster | 0.19x |
| `literal/early` | 0.042 us | 0.233 us | Managed faster | 0.18x |
| `common/backtracking` | 0.209 us | 1.310 us | Managed faster | 0.16x |
| `literal/absolute-anchored` | 0.036 us | 0.227 us | Managed faster | 0.16x |
| `common/email-miss` | 0.045 us | 0.483 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.015 us | 0.243 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.762 us | 0.357 us | Native faster | 2.13x |
| `simple/loglevel-multiline` | 0.507 us | 0.375 us | Native faster | 1.35x |
| `backtracking/alternation-repeat` | 141.350 us | 154.289 us | Managed faster | 0.92x |
| `industry/rust-sherlock-ing-count` | 10,055.575 us | 13,000.567 us | Managed faster | 0.77x |
| `simple/foo-optional-bar` | 0.296 us | 0.439 us | Managed faster | 0.67x |
| `industry/rust-sherlock-nonnewline-count` | 1,122.543 us | 2,435.635 us | Managed faster | 0.46x |
| `simple/foo-dense` | 0.076 us | 0.240 us | Managed faster | 0.32x |
| `industry/leipzig-twain-count` | 624.990 us | 2,282.218 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 163.902 us | 657.097 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,315.973 us | 14,957.400 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.084 us | 0.398 us | Managed faster | 0.21x |
| `common/matches-words` | 9.310 us | 76.502 us | Managed faster | 0.12x |
| `common/matches-word` | 1.220 us | 18.633 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,099.377 us | 18,676.750 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,977.265 us | 36,232.100 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,048.311 us | 44,864.800 us | Inconclusive | 0.05x |
| `common/matches-set` | 8.236 us | 194.580 us | Managed faster | 0.04x |
| `common/matches-boundary` | 8.185 us | 202.722 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 9.344 us | 279.091 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,809.284 us | 124,809.500 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 274.913 us | 19,482.150 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 448.701 us | 137,105.000 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,053.450 us | 470,567.700 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.159 us | 0.811 us | Native faster | 1.43x |
| `simple/ab-plus` | 1.473 us | 1.084 us | Native faster | 1.36x |
| `simple/foo-dense` | 0.717 us | 0.810 us | Inconclusive | 0.89x |
| `simple/foo-optional-bar` | 0.846 us | 1.174 us | Inconclusive | 0.72x |
| `simple/httpclient-caseless` | 0.786 us | 1.102 us | Managed faster | 0.71x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 56.170 us | — | Excluded | — |
| `simple/ab-plus` | 1.889 us | — | Excluded | — |
| `simple/foo-dense` | 0.873 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.012 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.964 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.212 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.118 us | 0.776 us | Native faster | 1.44x |
| `pcre2/recursive-palindrome` | 0.889 us | 0.649 us | Native faster | 1.37x |
| `pcre2/subroutine-prefix-digits` | 0.228 us | 0.246 us | Managed faster | 0.93x |
| `pcre2/conditional-accept-negative-lookahead` | 0.195 us | 0.245 us | Managed faster | 0.79x |
| `pcre2/conditional-negative-lookahead` | 0.194 us | 0.245 us | Managed faster | 0.79x |
| `pcre2/conditional-lookahead` | 0.198 us | 0.265 us | Managed faster | 0.75x |
| `pcre2/grapheme-cluster` | 0.124 us | 0.221 us | Managed faster | 0.56x |
| `pcre2/backslash-c-literal` | 0.115 us | 0.229 us | Managed faster | 0.50x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.249 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.620 us | 2.846 us | Native faster | 1.63x |
| `pcre2/kreset-atomic-alt` | 0.260 us | 0.303 us | Managed faster | 0.86x |
| `literal/empty-unicode` | 8.739 us | 10.422 us | Managed faster | 0.84x |
| `pcre2/duplicate-names` | 0.272 us | 0.357 us | Managed faster | 0.76x |
| `pcre2/kreset-bar-or-baz` | 0.262 us | 0.376 us | Managed faster | 0.70x |
| `pcre2/branch-reset-basic` | 0.299 us | 0.449 us | Managed faster | 0.67x |
| `pcre2/branch-reset-followup` | 0.272 us | 0.431 us | Managed faster | 0.63x |
| `pcre2/branch-reset-backref` | 0.288 us | 0.462 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.411 us | 0.662 us | Managed faster | 0.62x |
| `pcre2/kreset-global` | 0.167 us | 0.272 us | Managed faster | 0.62x |
| `pcre2/branch-reset-nested` | 0.300 us | 0.566 us | Managed faster | 0.53x |
| `pcre2/kreset-captured-repeat` | 2.400 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.330 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.808 us | 0.876 us | Inconclusive | 0.93x |
| `pcre2/kreset-bar-or-baz` | 0.800 us | 0.944 us | Managed faster | 0.85x |
| `pcre2/duplicate-names` | 0.771 us | 0.922 us | Managed faster | 0.84x |
| `pcre2/kreset-global` | 0.700 us | 0.839 us | Managed faster | 0.83x |
| `pcre2/branch-reset-followup` | 0.778 us | 1.008 us | Managed faster | 0.77x |
| `pcre2/branch-reset-backref` | 0.781 us | 1.030 us | Inconclusive | 0.76x |
| `pcre2/branch-reset-nested` | 0.814 us | 1.145 us | Managed faster | 0.71x |
| `pcre2/branch-reset-basic` | 0.816 us | 1.175 us | Managed faster | 0.69x |
| `pcre2/grapheme-cluster` | 5.321 us | 8.550 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.705 us | 1.239 us | Managed faster | 0.57x |
| `literal/empty-unicode` | 8.315 us | 28.625 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.378 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.303 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.809 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.910 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.971 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.876 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.912 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.893 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.519 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.919 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.920 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.730 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.837 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.147 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.722 us | — | Excluded | — |

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
