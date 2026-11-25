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

Snapshot SHA-256: `4A1BD70518F32022205E844D175C453505DA309F01040C95281801C8433CA21A`.

Comparator Status: `71` managed faster, `2` equivalent, `9` native faster, `3` inconclusive, `0` unqualified, `23` excluded.

Measured source `5d54be2a8409`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.378 us | 1.179 us | Native faster | 1.17x |
| `common/ip-match` | 0.443 us | 0.424 us | Native faster | 1.04x |
| `common/uri-match` | 0.319 us | 0.315 us | Equivalent | 1.01x |
| `industry/boostdocs-postcode-match` | 0.254 us | 0.253 us | Equivalent | 1.01x |
| `common/date-miss` | 0.657 us | 0.694 us | Managed faster | 0.95x |
| `common/email-match` | 0.387 us | 0.418 us | Managed faster | 0.92x |
| `simple/httpclient-caseless` | 0.200 us | 0.240 us | Managed faster | 0.83x |
| `industry/boostdocs-credit-card-match` | 0.236 us | 0.288 us | Managed faster | 0.82x |
| `backtracking/capture-backreference` | 0.192 us | 0.236 us | Managed faster | 0.81x |
| `industry/boostdocs-float-match` | 0.182 us | 0.248 us | Managed faster | 0.73x |
| `common/date-match` | 0.198 us | 0.278 us | Managed faster | 0.71x |
| `common/uri-miss` | 0.520 us | 0.730 us | Managed faster | 0.71x |
| `simple/loglevel-multiline` | 0.219 us | 0.310 us | Managed faster | 0.71x |
| `industry/boostdocs-date-match` | 0.171 us | 0.251 us | Managed faster | 0.68x |
| `simple/ab-plus` | 0.150 us | 0.232 us | Managed faster | 0.65x |
| `industry/boostdocs-ftp-line-match` | 0.243 us | 0.378 us | Managed faster | 0.64x |
| `literal/missing` | 0.076 us | 0.222 us | Managed faster | 0.35x |
| `literal/late` | 0.081 us | 0.265 us | Managed faster | 0.31x |
| `common/one-node-backtracking` | 0.055 us | 0.189 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.082 us | 0.302 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.044 us | 0.223 us | Managed faster | 0.20x |
| `literal/early` | 0.043 us | 0.231 us | Managed faster | 0.19x |
| `literal/absolute-anchored` | 0.037 us | 0.227 us | Managed faster | 0.16x |
| `common/backtracking` | 0.191 us | 1.885 us | Managed faster | 0.10x |
| `common/email-miss` | 0.046 us | 0.480 us | Managed faster | 0.10x |
| `simple/foo-optional-bar` | 0.015 us | 0.243 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.643 us | 0.358 us | Native faster | 1.79x |
| `simple/loglevel-multiline` | 0.514 us | 0.378 us | Native faster | 1.36x |
| `backtracking/alternation-repeat` | 140.806 us | 154.314 us | Managed faster | 0.91x |
| `industry/rust-sherlock-ing-count` | 9,577.275 us | 12,913.900 us | Managed faster | 0.74x |
| `simple/foo-optional-bar` | 0.300 us | 0.442 us | Managed faster | 0.68x |
| `industry/rust-sherlock-nonnewline-count` | 1,267.441 us | 2,415.541 us | Managed faster | 0.53x |
| `simple/foo-dense` | 0.070 us | 0.239 us | Inconclusive | 0.29x |
| `industry/leipzig-twain-count` | 626.152 us | 2,291.812 us | Managed faster | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | 168.206 us | 654.989 us | Managed faster | 0.26x |
| `industry/mariomka-ip-count` | 3,275.175 us | 15,029.733 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.084 us | 0.399 us | Managed faster | 0.21x |
| `common/matches-words` | 9.470 us | 76.887 us | Managed faster | 0.12x |
| `common/matches-word` | 1.222 us | 18.920 us | Managed faster | 0.06x |
| `industry/leipzig-name-family-count` | 1,114.191 us | 18,647.200 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,926.690 us | 36,099.100 us | Managed faster | 0.05x |
| `common/matches-boundary` | 8.743 us | 206.476 us | Managed faster | 0.04x |
| `common/matches-set` | 8.747 us | 209.850 us | Managed faster | 0.04x |
| `industry/leipzig-river-window-count` | 1,866.013 us | 45,191.600 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 9.857 us | 278.367 us | Managed faster | 0.04x |
| `industry/mariomka-uri-count` | 1,831.463 us | 125,049.600 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 261.888 us | 19,638.550 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 447.570 us | 137,672.100 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,052.251 us | 470,209.500 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.069 us | 0.803 us | Native faster | 1.32x |
| `simple/ab-plus` | 1.164 us | 1.080 us | Native faster | 1.08x |
| `simple/foo-dense` | 0.704 us | 0.805 us | Managed faster | 0.87x |
| `simple/foo-optional-bar` | 0.827 us | 1.175 us | Managed faster | 0.70x |
| `simple/httpclient-caseless` | 0.757 us | 1.091 us | Managed faster | 0.69x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 43.214 us | — | Excluded | — |
| `simple/ab-plus` | 1.770 us | — | Excluded | — |
| `simple/foo-dense` | 0.895 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.026 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.982 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.341 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.096 us | 0.769 us | Native faster | 1.42x |
| `pcre2/recursive-palindrome` | 0.888 us | 0.649 us | Native faster | 1.37x |
| `pcre2/subroutine-prefix-digits` | 0.234 us | 0.246 us | Managed faster | 0.95x |
| `pcre2/conditional-lookahead` | 0.209 us | 0.262 us | Inconclusive | 0.80x |
| `pcre2/conditional-accept-negative-lookahead` | 0.193 us | 0.243 us | Managed faster | 0.80x |
| `pcre2/conditional-negative-lookahead` | 0.194 us | 0.245 us | Managed faster | 0.79x |
| `pcre2/grapheme-cluster` | 0.126 us | 0.222 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.113 us | 0.228 us | Managed faster | 0.50x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.249 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.595 us | 2.858 us | Native faster | 1.60x |
| `literal/empty-unicode` | 9.185 us | 10.373 us | Managed faster | 0.89x |
| `pcre2/kreset-atomic-alt` | 0.260 us | 0.307 us | Managed faster | 0.85x |
| `pcre2/duplicate-names` | 0.274 us | 0.357 us | Managed faster | 0.77x |
| `pcre2/kreset-bar-or-baz` | 0.263 us | 0.378 us | Managed faster | 0.70x |
| `pcre2/branch-reset-basic` | 0.298 us | 0.449 us | Managed faster | 0.66x |
| `pcre2/branch-reset-followup` | 0.272 us | 0.434 us | Managed faster | 0.63x |
| `pcre2/same-start-global` | 0.417 us | 0.666 us | Managed faster | 0.62x |
| `pcre2/branch-reset-backref` | 0.288 us | 0.463 us | Managed faster | 0.62x |
| `pcre2/kreset-global` | 0.168 us | 0.273 us | Managed faster | 0.61x |
| `pcre2/branch-reset-nested` | 0.302 us | 0.562 us | Managed faster | 0.54x |
| `pcre2/kreset-captured-repeat` | 2.427 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.364 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.804 us | 0.873 us | Managed faster | 0.92x |
| `pcre2/kreset-bar-or-baz` | 0.796 us | 0.939 us | Managed faster | 0.85x |
| `pcre2/kreset-global` | 0.704 us | 0.832 us | Managed faster | 0.84x |
| `pcre2/duplicate-names` | 0.777 us | 0.918 us | Managed faster | 0.84x |
| `pcre2/branch-reset-followup` | 0.772 us | 0.999 us | Managed faster | 0.77x |
| `pcre2/branch-reset-backref` | 0.780 us | 1.025 us | Managed faster | 0.76x |
| `pcre2/branch-reset-nested` | 0.797 us | 1.139 us | Managed faster | 0.70x |
| `pcre2/branch-reset-basic` | 0.806 us | 1.167 us | Managed faster | 0.69x |
| `pcre2/grapheme-cluster` | 5.238 us | 8.515 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.729 us | 1.233 us | Managed faster | 0.59x |
| `literal/empty-unicode` | 8.579 us | 28.366 us | Managed faster | 0.30x |
| `pcre2/kreset-captured-repeat` | 3.382 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.294 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.652 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.918 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.999 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.898 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.922 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.908 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.550 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.999 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.952 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.532 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.873 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.318 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.654 us | — | Excluded | — |

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
