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

Snapshot SHA-256: `B1514B6DC61AC51062E574A13F43FD167F2D9F20E75205CCF2C61728E73CEA81`.

Comparator Status: `72` managed faster, `1` equivalent, `8` native faster, `4` inconclusive, `0` unqualified, `23` excluded.

Measured source `1128c579a517`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.273 us | 1.135 us | Native faster | 1.12x |
| `common/ip-match` | 0.422 us | 0.406 us | Native faster | 1.04x |
| `common/uri-match` | 0.299 us | 0.300 us | Equivalent | 1.00x |
| `industry/boostdocs-postcode-match` | 0.230 us | 0.241 us | Managed faster | 0.95x |
| `common/date-miss` | 0.637 us | 0.667 us | Managed faster | 0.95x |
| `simple/httpclient-caseless` | 0.203 us | 0.233 us | Managed faster | 0.87x |
| `common/email-match` | 0.350 us | 0.406 us | Inconclusive | 0.86x |
| `industry/boostdocs-credit-card-match` | 0.233 us | 0.275 us | Managed faster | 0.84x |
| `industry/boostdocs-float-match` | 0.178 us | 0.238 us | Managed faster | 0.75x |
| `backtracking/capture-backreference` | 0.168 us | 0.227 us | Managed faster | 0.74x |
| `common/date-match` | 0.195 us | 0.268 us | Managed faster | 0.73x |
| `industry/boostdocs-date-match` | 0.169 us | 0.240 us | Managed faster | 0.70x |
| `common/uri-miss` | 0.489 us | 0.702 us | Managed faster | 0.70x |
| `simple/loglevel-multiline` | 0.202 us | 0.296 us | Managed faster | 0.68x |
| `simple/ab-plus` | 0.137 us | 0.221 us | Managed faster | 0.62x |
| `industry/boostdocs-ftp-line-match` | 0.223 us | 0.368 us | Managed faster | 0.61x |
| `literal/missing` | 0.073 us | 0.214 us | Managed faster | 0.34x |
| `literal/late` | 0.078 us | 0.256 us | Managed faster | 0.30x |
| `common/one-node-backtracking` | 0.051 us | 0.182 us | Managed faster | 0.28x |
| `backtracking/assertion-capture` | 0.079 us | 0.294 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.042 us | 0.214 us | Managed faster | 0.19x |
| `literal/early` | 0.040 us | 0.224 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.035 us | 0.219 us | Managed faster | 0.16x |
| `common/backtracking` | 0.174 us | 1.248 us | Managed faster | 0.14x |
| `common/email-miss` | 0.043 us | 0.463 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.014 us | 0.232 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.617 us | 0.345 us | Native faster | 1.79x |
| `simple/loglevel-multiline` | 0.483 us | 0.361 us | Native faster | 1.33x |
| `backtracking/alternation-repeat` | 135.431 us | 147.959 us | Managed faster | 0.91x |
| `industry/rust-sherlock-ing-count` | 9,084.425 us | 12,460.433 us | Managed faster | 0.73x |
| `simple/foo-optional-bar` | 0.286 us | 0.421 us | Managed faster | 0.68x |
| `industry/rust-sherlock-nonnewline-count` | 1,102.131 us | 2,329.029 us | Managed faster | 0.47x |
| `simple/foo-dense` | 0.067 us | 0.230 us | Inconclusive | 0.29x |
| `industry/leipzig-twain-count` | 604.593 us | 2,207.878 us | Managed faster | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | 164.247 us | 627.839 us | Managed faster | 0.26x |
| `industry/mariomka-ip-count` | 3,165.175 us | 14,301.900 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.081 us | 0.378 us | Managed faster | 0.21x |
| `common/matches-words` | 9.005 us | 73.927 us | Managed faster | 0.12x |
| `common/matches-word` | 1.167 us | 17.941 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,079.844 us | 17,922.550 us | Managed faster | 0.06x |
| `common/matches-boundary` | 11.401 us | 195.407 us | Managed faster | 0.06x |
| `common/matches-set` | 10.453 us | 186.836 us | Inconclusive | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,826.673 us | 35,073.900 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,811.623 us | 43,261.300 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 9.226 us | 258.527 us | Managed faster | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | 268.884 us | 18,779.450 us | Managed faster | 0.01x |
| `industry/mariomka-uri-count` | 1,717.105 us | 121,021.900 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 426.567 us | 131,487.700 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,009.310 us | 447,167.800 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.957 us | 0.774 us | Native faster | 1.24x |
| `simple/ab-plus` | 0.989 us | 1.044 us | Managed faster | 0.95x |
| `simple/foo-dense` | 0.637 us | 0.777 us | Managed faster | 0.82x |
| `simple/foo-optional-bar` | 0.803 us | 1.123 us | Managed faster | 0.71x |
| `simple/httpclient-caseless` | 0.691 us | 1.048 us | Managed faster | 0.66x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 42.318 us | — | Excluded | — |
| `simple/ab-plus` | 1.642 us | — | Excluded | — |
| `simple/foo-dense` | 0.820 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.958 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.914 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.117 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-optional` | 1.055 us | 0.753 us | Native faster | 1.40x |
| `pcre2/recursive-palindrome` | 0.852 us | 0.625 us | Native faster | 1.37x |
| `pcre2/subroutine-prefix-digits` | 0.221 us | 0.234 us | Managed faster | 0.94x |
| `pcre2/conditional-negative-lookahead` | 0.189 us | 0.236 us | Managed faster | 0.80x |
| `pcre2/conditional-accept-negative-lookahead` | 0.189 us | 0.242 us | Managed faster | 0.78x |
| `pcre2/conditional-lookahead` | 0.194 us | 0.253 us | Managed faster | 0.77x |
| `pcre2/grapheme-cluster` | 0.121 us | 0.212 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.109 us | 0.219 us | Managed faster | 0.50x |
| `pcre2/branch-reset-backref` | 0.012 us | 0.240 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.405 us | 2.736 us | Native faster | 1.61x |
| `pcre2/kreset-atomic-alt` | 0.265 us | 0.290 us | Managed faster | 0.91x |
| `literal/empty-unicode` | 9.011 us | 9.851 us | Managed faster | 0.91x |
| `pcre2/duplicate-names` | 0.266 us | 0.359 us | Managed faster | 0.74x |
| `pcre2/kreset-bar-or-baz` | 0.254 us | 0.363 us | Managed faster | 0.69x |
| `pcre2/branch-reset-basic` | 0.295 us | 0.428 us | Managed faster | 0.69x |
| `pcre2/kreset-global` | 0.162 us | 0.261 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.397 us | 0.639 us | Managed faster | 0.62x |
| `pcre2/branch-reset-backref` | 0.283 us | 0.460 us | Managed faster | 0.62x |
| `pcre2/branch-reset-followup` | 0.269 us | 0.438 us | Managed faster | 0.61x |
| `pcre2/branch-reset-nested` | 0.306 us | 0.543 us | Managed faster | 0.56x |
| `pcre2/kreset-captured-repeat` | 2.365 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.305 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.781 us | 0.833 us | Managed faster | 0.93x |
| `pcre2/kreset-bar-or-baz` | 0.785 us | 0.910 us | Managed faster | 0.86x |
| `pcre2/kreset-global` | 0.696 us | 0.809 us | Managed faster | 0.86x |
| `pcre2/duplicate-names` | 0.777 us | 0.928 us | Managed faster | 0.84x |
| `pcre2/branch-reset-followup` | 0.781 us | 0.988 us | Managed faster | 0.79x |
| `pcre2/branch-reset-backref` | 0.786 us | 1.000 us | Managed faster | 0.79x |
| `pcre2/branch-reset-nested` | 0.808 us | 1.101 us | Managed faster | 0.73x |
| `pcre2/branch-reset-basic` | 0.811 us | 1.127 us | Managed faster | 0.72x |
| `pcre2/grapheme-cluster` | 4.967 us | 8.194 us | Managed faster | 0.61x |
| `pcre2/same-start-global` | 0.692 us | 1.180 us | Managed faster | 0.59x |
| `literal/empty-unicode` | 7.884 us | 27.454 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.501 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.400 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 13.993 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.857 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.946 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.846 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.882 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.850 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.315 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.890 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.898 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.044 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.824 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 9.820 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.516 us | — | Excluded | — |

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
