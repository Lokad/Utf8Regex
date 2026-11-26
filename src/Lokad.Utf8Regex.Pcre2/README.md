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

Snapshot SHA-256: `2226350E552946D9521630886969D8EB825253C6536F60F7945F4033CDA54DD2`.

Comparator Status: `70` managed faster, `0` equivalent, `6` native faster, `9` inconclusive, `0` unqualified, `23` excluded.

Measured source `bcb9dd0ceba9`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.409 us | 1.150 us | Native faster | 1.22x |
| `common/ip-match` | 0.460 us | 0.417 us | Native faster | 1.10x |
| `industry/boostdocs-postcode-match` | 0.257 us | 0.246 us | Native faster | 1.05x |
| `common/date-miss` | 0.642 us | 0.672 us | Managed faster | 0.96x |
| `common/uri-match` | 0.285 us | 0.307 us | Managed faster | 0.93x |
| `common/email-match` | 0.376 us | 0.409 us | Managed faster | 0.91x |
| `backtracking/capture-backreference` | 0.187 us | 0.236 us | Managed faster | 0.79x |
| `simple/httpclient-caseless` | 0.182 us | 0.240 us | Managed faster | 0.76x |
| `industry/boostdocs-credit-card-match` | 0.203 us | 0.280 us | Managed faster | 0.73x |
| `common/uri-miss` | 0.489 us | 0.720 us | Managed faster | 0.68x |
| `common/date-match` | 0.184 us | 0.273 us | Managed faster | 0.67x |
| `industry/boostdocs-float-match` | 0.161 us | 0.240 us | Managed faster | 0.67x |
| `industry/boostdocs-ftp-line-match` | 0.236 us | 0.369 us | Managed faster | 0.64x |
| `simple/ab-plus` | 0.149 us | 0.233 us | Managed faster | 0.64x |
| `industry/boostdocs-date-match` | 0.153 us | 0.247 us | Managed faster | 0.62x |
| `simple/loglevel-multiline` | 0.186 us | 0.306 us | Managed faster | 0.61x |
| `literal/missing` | 0.074 us | 0.217 us | Managed faster | 0.34x |
| `literal/late` | 0.078 us | 0.256 us | Managed faster | 0.30x |
| `common/one-node-backtracking` | 0.055 us | 0.184 us | Managed faster | 0.30x |
| `backtracking/assertion-capture` | 0.080 us | 0.291 us | Managed faster | 0.28x |
| `simple/foo-dense` | 0.044 us | 0.222 us | Managed faster | 0.20x |
| `literal/early` | 0.041 us | 0.226 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.035 us | 0.222 us | Managed faster | 0.16x |
| `common/backtracking` | 0.193 us | 1.298 us | Managed faster | 0.15x |
| `common/email-miss` | 0.046 us | 0.463 us | Managed faster | 0.10x |
| `simple/foo-optional-bar` | 0.015 us | 0.240 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 138.251 us | 152.939 us | Managed faster | 0.90x |
| `industry/rust-sherlock-ing-count` | 10,270.000 us | 12,782.000 us | Managed faster | 0.80x |
| `simple/loglevel-multiline` | 0.239 us | 0.373 us | Managed faster | 0.64x |
| `industry/rust-sherlock-nonnewline-count` | 1,253.333 us | 2,438.200 us | Managed faster | 0.52x |
| `simple/ab-plus` | 0.184 us | 0.355 us | Inconclusive | 0.52x |
| `simple/foo-optional-bar` | 0.156 us | 0.437 us | Inconclusive | 0.36x |
| `simple/foo-dense` | 0.068 us | 0.242 us | Inconclusive | 0.28x |
| `industry/leipzig-twain-count` | 613.391 us | 2,246.482 us | Managed faster | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | 161.341 us | 642.716 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,142.242 us | 14,527.400 us | Inconclusive | 0.22x |
| `simple/httpclient-caseless` | 0.083 us | 0.391 us | Managed faster | 0.21x |
| `common/matches-words` | 9.081 us | 74.043 us | Managed faster | 0.12x |
| `common/matches-word` | 1.186 us | 18.317 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,079.661 us | 18,054.250 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,924.040 us | 35,204.800 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,177.978 us | 43,241.100 us | Managed faster | 0.05x |
| `common/matches-set` | 9.517 us | 190.781 us | Managed faster | 0.05x |
| `common/matches-boundary` | 9.585 us | 196.683 us | Managed faster | 0.05x |
| `character/unicode-class-dense` | 9.488 us | 272.372 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,695.385 us | 122,673.300 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 262.992 us | 19,272.650 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 452.950 us | 133,711.100 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,021.712 us | 469,392.600 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.847 us | 0.813 us | Native faster | 1.04x |
| `simple/ab-plus` | 0.997 us | 1.075 us | Managed faster | 0.93x |
| `simple/foo-dense` | 0.642 us | 0.810 us | Managed faster | 0.80x |
| `simple/httpclient-caseless` | 0.683 us | 1.081 us | Managed faster | 0.63x |
| `simple/foo-optional-bar` | 0.715 us | 1.158 us | Managed faster | 0.62x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 55.294 us | — | Excluded | — |
| `simple/ab-plus` | 1.681 us | — | Excluded | — |
| `simple/foo-dense` | 0.847 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.906 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.925 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.209 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.848 us | 0.642 us | Native faster | 1.32x |
| `pcre2/recursive-optional` | 0.865 us | 0.763 us | Native faster | 1.13x |
| `pcre2/subroutine-prefix-digits` | 0.222 us | 0.242 us | Managed faster | 0.92x |
| `pcre2/conditional-negative-lookahead` | 0.172 us | 0.242 us | Managed faster | 0.71x |
| `pcre2/conditional-accept-negative-lookahead` | 0.171 us | 0.242 us | Managed faster | 0.71x |
| `pcre2/conditional-lookahead` | 0.174 us | 0.262 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 0.113 us | 0.219 us | Managed faster | 0.52x |
| `pcre2/backslash-c-literal` | 0.096 us | 0.223 us | Managed faster | 0.43x |
| `pcre2/branch-reset-backref` | 0.012 us | 0.240 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 8.706 us | 10.046 us | Managed faster | 0.87x |
| `pcre2/grapheme-cluster` | 1.925 us | 2.784 us | Managed faster | 0.69x |
| `pcre2/same-start-global` | 0.401 us | 0.660 us | Managed faster | 0.61x |
| `pcre2/kreset-bar-or-baz` | 0.195 us | 0.372 us | Inconclusive | 0.52x |
| `pcre2/duplicate-names` | 0.182 us | 0.352 us | Managed faster | 0.52x |
| `pcre2/branch-reset-basic` | 0.207 us | 0.439 us | Managed faster | 0.47x |
| `pcre2/kreset-atomic-alt` | 0.137 us | 0.301 us | Inconclusive | 0.45x |
| `pcre2/branch-reset-followup` | 0.188 us | 0.429 us | Managed faster | 0.44x |
| `pcre2/kreset-global` | 0.116 us | 0.275 us | Managed faster | 0.42x |
| `pcre2/branch-reset-backref` | 0.183 us | 0.454 us | Inconclusive | 0.40x |
| `pcre2/branch-reset-nested` | 0.188 us | 0.556 us | Inconclusive | 0.34x |
| `pcre2/kreset-captured-repeat` | 2.390 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.353 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.684 us | 0.869 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.681 us | 0.930 us | Managed faster | 0.73x |
| `pcre2/duplicate-names` | 0.673 us | 0.926 us | Managed faster | 0.73x |
| `pcre2/kreset-global` | 0.593 us | 0.832 us | Managed faster | 0.71x |
| `pcre2/branch-reset-followup` | 0.668 us | 0.993 us | Managed faster | 0.67x |
| `pcre2/branch-reset-backref` | 0.675 us | 1.017 us | Managed faster | 0.66x |
| `pcre2/branch-reset-nested` | 0.733 us | 1.146 us | Managed faster | 0.64x |
| `pcre2/branch-reset-basic` | 0.698 us | 1.141 us | Managed faster | 0.61x |
| `pcre2/grapheme-cluster` | 4.807 us | 8.352 us | Managed faster | 0.58x |
| `pcre2/same-start-global` | 0.633 us | 1.223 us | Managed faster | 0.52x |
| `literal/empty-unicode` | 8.402 us | 27.725 us | Managed faster | 0.30x |
| `pcre2/kreset-captured-repeat` | 3.174 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.141 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.367 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.815 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.899 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.824 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.921 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.814 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.174 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.844 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.822 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.225 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.747 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.167 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.199 us | — | Excluded | — |

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
