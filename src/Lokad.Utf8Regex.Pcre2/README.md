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

Snapshot SHA-256: `253297A113AE8E823BB944FE5458561BF242CFD382583D06222BAF316C9FBBC3`.

Comparator Status: `73` managed faster, `0` equivalent, `7` native faster, `5` inconclusive, `0` unqualified, `23` excluded.

Measured source `611aa43eaf80`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.416 us | 1.177 us | Native faster | 1.20x |
| `common/ip-match` | 0.468 us | 0.418 us | Native faster | 1.12x |
| `common/uri-match` | 0.321 us | 0.307 us | Native faster | 1.04x |
| `industry/boostdocs-postcode-match` | 0.257 us | 0.248 us | Native faster | 1.04x |
| `common/date-miss` | 0.639 us | 0.681 us | Managed faster | 0.93x |
| `common/email-match` | 0.369 us | 0.419 us | Managed faster | 0.88x |
| `simple/httpclient-caseless` | 0.204 us | 0.243 us | Managed faster | 0.84x |
| `industry/boostdocs-credit-card-match` | 0.237 us | 0.283 us | Managed faster | 0.84x |
| `industry/boostdocs-float-match` | 0.187 us | 0.248 us | Managed faster | 0.76x |
| `backtracking/capture-backreference` | 0.184 us | 0.249 us | Managed faster | 0.74x |
| `simple/loglevel-multiline` | 0.219 us | 0.306 us | Managed faster | 0.71x |
| `industry/boostdocs-date-match` | 0.175 us | 0.248 us | Managed faster | 0.71x |
| `common/date-match` | 0.196 us | 0.278 us | Managed faster | 0.70x |
| `common/uri-miss` | 0.515 us | 0.729 us | Managed faster | 0.70x |
| `simple/ab-plus` | 0.160 us | 0.236 us | Managed faster | 0.68x |
| `industry/boostdocs-ftp-line-match` | 0.249 us | 0.372 us | Managed faster | 0.67x |
| `literal/missing` | 0.076 us | 0.218 us | Managed faster | 0.35x |
| `literal/late` | 0.080 us | 0.262 us | Managed faster | 0.30x |
| `common/one-node-backtracking` | 0.054 us | 0.186 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.081 us | 0.304 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.044 us | 0.220 us | Managed faster | 0.20x |
| `literal/early` | 0.043 us | 0.230 us | Managed faster | 0.19x |
| `literal/absolute-anchored` | 0.037 us | 0.224 us | Managed faster | 0.16x |
| `common/backtracking` | 0.196 us | 1.297 us | Managed faster | 0.15x |
| `common/email-miss` | 0.045 us | 0.485 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.018 us | 0.244 us | Managed faster | 0.07x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 139.450 us | 153.192 us | Managed faster | 0.91x |
| `industry/rust-sherlock-ing-count` | 9,606.050 us | 12,811.367 us | Inconclusive | 0.75x |
| `simple/foo-optional-bar` | 0.310 us | 0.442 us | Managed faster | 0.70x |
| `simple/loglevel-multiline` | 0.227 us | 0.367 us | Managed faster | 0.62x |
| `industry/rust-sherlock-nonnewline-count` | 1,259.059 us | 2,414.556 us | Managed faster | 0.52x |
| `simple/ab-plus` | 0.186 us | 0.362 us | Managed faster | 0.51x |
| `simple/foo-dense` | 0.070 us | 0.231 us | Inconclusive | 0.31x |
| `industry/leipzig-twain-count` | 628.136 us | 2,264.376 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 158.975 us | 647.902 us | Managed faster | 0.25x |
| `simple/httpclient-caseless` | 0.084 us | 0.385 us | Managed faster | 0.22x |
| `industry/mariomka-ip-count` | 3,218.508 us | 14,824.333 us | Managed faster | 0.22x |
| `common/matches-words` | 9.156 us | 74.956 us | Managed faster | 0.12x |
| `common/matches-word` | 1.204 us | 18.607 us | Managed faster | 0.06x |
| `industry/leipzig-name-family-count` | 1,109.417 us | 18,565.600 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,917.030 us | 35,664.900 us | Managed faster | 0.05x |
| `common/matches-set` | 9.622 us | 194.392 us | Managed faster | 0.05x |
| `common/matches-boundary` | 9.822 us | 199.024 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,892.852 us | 44,685.500 us | Inconclusive | 0.04x |
| `character/unicode-class-dense` | 9.179 us | 276.946 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,793.111 us | 124,818.900 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 271.155 us | 19,583.150 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 439.048 us | 134,600.600 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,018.480 us | 476,099.600 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.874 us | 0.814 us | Native faster | 1.08x |
| `simple/ab-plus` | 0.954 us | 1.090 us | Managed faster | 0.87x |
| `simple/foo-dense` | 0.658 us | 0.825 us | Managed faster | 0.80x |
| `simple/foo-optional-bar` | 0.793 us | 1.186 us | Managed faster | 0.67x |
| `simple/httpclient-caseless` | 0.721 us | 1.111 us | Managed faster | 0.65x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 55.711 us | — | Excluded | — |
| `simple/ab-plus` | 1.707 us | — | Excluded | — |
| `simple/foo-dense` | 0.853 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.991 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.957 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.209 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.896 us | 0.640 us | Native faster | 1.39x |
| `pcre2/recursive-optional` | 0.904 us | 0.768 us | Native faster | 1.17x |
| `pcre2/subroutine-prefix-digits` | 0.229 us | 0.244 us | Managed faster | 0.94x |
| `pcre2/conditional-accept-negative-lookahead` | 0.196 us | 0.244 us | Managed faster | 0.80x |
| `pcre2/conditional-negative-lookahead` | 0.197 us | 0.246 us | Managed faster | 0.80x |
| `pcre2/conditional-lookahead` | 0.205 us | 0.264 us | Managed faster | 0.78x |
| `pcre2/grapheme-cluster` | 0.122 us | 0.221 us | Managed faster | 0.55x |
| `pcre2/backslash-c-literal` | 0.113 us | 0.223 us | Managed faster | 0.51x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.248 us | Inconclusive | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.264 us | 0.296 us | Managed faster | 0.89x |
| `literal/empty-unicode` | 8.325 us | 10.040 us | Managed faster | 0.83x |
| `pcre2/duplicate-names` | 0.275 us | 0.346 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.264 us | 0.371 us | Managed faster | 0.71x |
| `pcre2/grapheme-cluster` | 1.924 us | 2.871 us | Managed faster | 0.67x |
| `pcre2/branch-reset-basic` | 0.299 us | 0.445 us | Managed faster | 0.67x |
| `pcre2/branch-reset-followup` | 0.275 us | 0.428 us | Managed faster | 0.65x |
| `pcre2/branch-reset-backref` | 0.290 us | 0.455 us | Managed faster | 0.64x |
| `pcre2/kreset-global` | 0.168 us | 0.267 us | Managed faster | 0.63x |
| `pcre2/same-start-global` | 0.402 us | 0.667 us | Managed faster | 0.60x |
| `pcre2/branch-reset-nested` | 0.300 us | 0.561 us | Managed faster | 0.54x |
| `pcre2/kreset-captured-repeat` | 2.383 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.310 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.749 us | 0.870 us | Managed faster | 0.86x |
| `pcre2/duplicate-names` | 0.735 us | 0.935 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.733 us | 0.942 us | Managed faster | 0.78x |
| `pcre2/kreset-global` | 0.636 us | 0.835 us | Managed faster | 0.76x |
| `pcre2/branch-reset-backref` | 0.770 us | 1.039 us | Managed faster | 0.74x |
| `pcre2/branch-reset-followup` | 0.733 us | 1.011 us | Managed faster | 0.72x |
| `pcre2/branch-reset-nested` | 0.758 us | 1.137 us | Managed faster | 0.67x |
| `pcre2/branch-reset-basic` | 0.786 us | 1.183 us | Managed faster | 0.66x |
| `pcre2/grapheme-cluster` | 5.136 us | 8.644 us | Managed faster | 0.59x |
| `pcre2/same-start-global` | 0.643 us | 1.232 us | Managed faster | 0.52x |
| `literal/empty-unicode` | 7.748 us | 28.177 us | Managed faster | 0.28x |
| `pcre2/kreset-captured-repeat` | 3.365 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.294 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.294 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.862 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.936 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.844 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.859 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.855 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.234 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.889 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.876 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.678 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.793 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.222 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.647 us | — | Excluded | — |

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
