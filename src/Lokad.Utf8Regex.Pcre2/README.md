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

Snapshot SHA-256: `4B1AE76F09AF972A07E94B92992E80B8690CC1528548BFEAAC5FAEB8F2656F74`.

Comparator Status: `71` managed faster, `1` equivalent, `8` native faster, `4` inconclusive, `1` unqualified, `23` excluded.

Measured source `78c47b37343c`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.356 us | 1.165 us | Native faster | 1.17x |
| `common/ip-match` | 0.448 us | 0.420 us | Native faster | 1.06x |
| `industry/boostdocs-postcode-match` | 0.245 us | 0.247 us | Equivalent | 1.00x |
| `common/uri-match` | 0.301 us | 0.308 us | Inconclusive | 0.98x |
| `common/date-miss` | 0.650 us | 0.683 us | Managed faster | 0.96x |
| `common/email-match` | 0.371 us | 0.416 us | Managed faster | 0.89x |
| `simple/httpclient-caseless` | 0.208 us | 0.242 us | Managed faster | 0.86x |
| `industry/boostdocs-credit-card-match` | 0.238 us | 0.282 us | Managed faster | 0.84x |
| `industry/boostdocs-float-match` | 0.181 us | 0.248 us | Managed faster | 0.73x |
| `simple/loglevel-multiline` | 0.224 us | 0.311 us | Managed faster | 0.72x |
| `common/date-match` | 0.194 us | 0.281 us | Managed faster | 0.69x |
| `common/uri-miss` | 0.491 us | 0.719 us | Managed faster | 0.68x |
| `industry/boostdocs-date-match` | 0.170 us | 0.248 us | Managed faster | 0.68x |
| `simple/ab-plus` | 0.157 us | 0.232 us | Managed faster | 0.68x |
| `industry/boostdocs-ftp-line-match` | 0.241 us | 0.375 us | Managed faster | 0.64x |
| `literal/missing` | 0.075 us | 0.218 us | Managed faster | 0.34x |
| `literal/late` | 0.079 us | 0.263 us | Managed faster | 0.30x |
| `common/one-node-backtracking` | 0.051 us | 0.186 us | Managed faster | 0.28x |
| `backtracking/assertion-capture` | 0.081 us | 0.303 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.043 us | 0.221 us | Managed faster | 0.19x |
| `literal/early` | 0.040 us | 0.228 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.034 us | 0.224 us | Managed faster | 0.15x |
| `common/backtracking` | 0.193 us | 1.327 us | Managed faster | 0.15x |
| `common/email-miss` | 0.043 us | 0.474 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.016 us | 0.249 us | Managed faster | 0.06x |
| `backtracking/capture-backreference` | 0.185 us | 0.239 us | Unqualified | — |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.629 us | 0.354 us | Native faster | 1.78x |
| `simple/loglevel-multiline` | 0.502 us | 0.373 us | Native faster | 1.35x |
| `backtracking/alternation-repeat` | 140.685 us | 153.572 us | Inconclusive | 0.91x |
| `industry/rust-sherlock-ing-count` | 10,071.175 us | 12,827.267 us | Managed faster | 0.79x |
| `simple/foo-optional-bar` | 0.301 us | 0.441 us | Managed faster | 0.69x |
| `industry/rust-sherlock-nonnewline-count` | 1,259.984 us | 2,429.582 us | Managed faster | 0.52x |
| `simple/foo-dense` | 0.072 us | 0.235 us | Managed faster | 0.31x |
| `industry/leipzig-twain-count` | 618.736 us | 2,250.212 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 160.757 us | 655.713 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,256.682 us | 14,877.200 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.082 us | 0.391 us | Managed faster | 0.21x |
| `common/matches-words` | 9.222 us | 83.107 us | Managed faster | 0.11x |
| `common/matches-word` | 1.199 us | 18.142 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,101.651 us | 18,446.500 us | Managed faster | 0.06x |
| `common/matches-set` | 10.950 us | 193.816 us | Managed faster | 0.06x |
| `common/matches-boundary` | 10.932 us | 198.740 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,934.350 us | 36,712.000 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,790.018 us | 44,020.400 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 10.392 us | 269.145 us | Managed faster | 0.04x |
| `industry/mariomka-uri-count` | 1,814.355 us | 125,152.300 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 268.743 us | 19,315.850 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 437.409 us | 134,986.400 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,037.508 us | 478,476.400 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.004 us | 0.810 us | Native faster | 1.24x |
| `simple/ab-plus` | 1.019 us | 1.090 us | Managed faster | 0.93x |
| `simple/foo-dense` | 0.672 us | 0.810 us | Managed faster | 0.83x |
| `simple/foo-optional-bar` | 0.832 us | 1.176 us | Managed faster | 0.71x |
| `simple/httpclient-caseless` | 0.726 us | 1.097 us | Managed faster | 0.66x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 54.081 us | — | Excluded | — |
| `simple/ab-plus` | 1.738 us | — | Excluded | — |
| `simple/foo-dense` | 0.881 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.007 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.965 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.323 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.906 us | 0.644 us | Native faster | 1.41x |
| `pcre2/recursive-optional` | 0.917 us | 0.762 us | Native faster | 1.20x |
| `pcre2/subroutine-prefix-digits` | 0.230 us | 0.245 us | Managed faster | 0.94x |
| `pcre2/conditional-accept-negative-lookahead` | 0.191 us | 0.243 us | Managed faster | 0.79x |
| `pcre2/conditional-negative-lookahead` | 0.193 us | 0.244 us | Managed faster | 0.79x |
| `pcre2/conditional-lookahead` | 0.198 us | 0.261 us | Managed faster | 0.76x |
| `pcre2/grapheme-cluster` | 0.120 us | 0.219 us | Managed faster | 0.55x |
| `pcre2/backslash-c-literal` | 0.107 us | 0.225 us | Managed faster | 0.47x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.242 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.346 us | 2.906 us | Native faster | 1.50x |
| `pcre2/kreset-atomic-alt` | 0.260 us | 0.298 us | Managed faster | 0.88x |
| `literal/empty-unicode` | 8.689 us | 10.170 us | Managed faster | 0.85x |
| `pcre2/duplicate-names` | 0.267 us | 0.348 us | Managed faster | 0.77x |
| `pcre2/kreset-bar-or-baz` | 0.260 us | 0.369 us | Managed faster | 0.71x |
| `pcre2/branch-reset-basic` | 0.291 us | 0.444 us | Managed faster | 0.66x |
| `pcre2/branch-reset-backref` | 0.281 us | 0.447 us | Managed faster | 0.63x |
| `pcre2/branch-reset-followup` | 0.266 us | 0.426 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.408 us | 0.658 us | Managed faster | 0.62x |
| `pcre2/kreset-global` | 0.163 us | 0.266 us | Inconclusive | 0.62x |
| `pcre2/branch-reset-nested` | 0.294 us | 0.567 us | Managed faster | 0.52x |
| `pcre2/kreset-captured-repeat` | 2.430 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.363 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.770 us | 0.861 us | Managed faster | 0.90x |
| `pcre2/duplicate-names` | 0.780 us | 0.916 us | Managed faster | 0.85x |
| `pcre2/kreset-bar-or-baz` | 0.786 us | 0.932 us | Managed faster | 0.84x |
| `pcre2/kreset-global` | 0.667 us | 0.833 us | Managed faster | 0.80x |
| `pcre2/branch-reset-followup` | 0.781 us | 0.995 us | Managed faster | 0.79x |
| `pcre2/branch-reset-backref` | 0.787 us | 1.023 us | Managed faster | 0.77x |
| `pcre2/branch-reset-nested` | 0.812 us | 1.137 us | Managed faster | 0.71x |
| `pcre2/branch-reset-basic` | 0.817 us | 1.163 us | Managed faster | 0.70x |
| `pcre2/grapheme-cluster` | 4.979 us | 8.524 us | Managed faster | 0.58x |
| `pcre2/same-start-global` | 0.674 us | 1.236 us | Managed faster | 0.54x |
| `literal/empty-unicode` | 7.915 us | 27.937 us | Managed faster | 0.28x |
| `pcre2/kreset-captured-repeat` | 3.345 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.223 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.557 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.894 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.970 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.885 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.908 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.882 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.339 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.917 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.917 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.328 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.856 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.038 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.669 us | — | Excluded | — |

<details>
<summary>Qualification, allocations and scaling evidence</summary>

The [summary and raw-evidence reference](../../bench/Snapshots/Pcre2.json) retains per-row confidence intervals, exclusion reasons, input/source/binary identities, allocation probes, native build/plan fingerprints and managed/comparator workspace contracts. Managed-thread allocation probes do not measure retained native memory. Reusable match buffers are neither thread-safe nor reentrant; finite/token/deadline costs remain separate from these ordinary rows.

Benchmark comparator: [PCRE.NET 1.5.0](https://www.nuget.org/packages/PCRE.NET/1.5.0), native PCRE2 `10.47 2025-10-21`, license `BSD-3-Clause WITH PCRE2-exception`. This private benchmark dependency is absent from shipped projects.

- `pcre2-managed-compatible-ismatch/backtracking/capture-backreference`: At least one paired lane sample was shorter than 20 ms.
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
