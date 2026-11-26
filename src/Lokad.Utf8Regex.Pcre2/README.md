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

Snapshot SHA-256: `450B036D463C090685F5A14AD8E451B927AD54E62C7264D040761456354607AA`.

Comparator Status: `72` managed faster, `2` equivalent, `5` native faster, `6` inconclusive, `0` unqualified, `23` excluded.

Measured source `4ab5765580e5`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.341 us | 1.160 us | Native faster | 1.16x |
| `common/ip-match` | 0.447 us | 0.421 us | Native faster | 1.06x |
| `industry/boostdocs-postcode-match` | 0.253 us | 0.249 us | Equivalent | 1.02x |
| `common/uri-match` | 0.310 us | 0.315 us | Equivalent | 0.99x |
| `common/date-miss` | 0.670 us | 0.685 us | Inconclusive | 0.98x |
| `common/email-match` | 0.375 us | 0.414 us | Managed faster | 0.91x |
| `simple/httpclient-caseless` | 0.201 us | 0.237 us | Managed faster | 0.85x |
| `industry/boostdocs-credit-card-match` | 0.220 us | 0.284 us | Managed faster | 0.78x |
| `backtracking/capture-backreference` | 0.181 us | 0.244 us | Managed faster | 0.75x |
| `common/uri-miss` | 0.508 us | 0.723 us | Managed faster | 0.70x |
| `industry/boostdocs-float-match` | 0.171 us | 0.246 us | Managed faster | 0.69x |
| `common/date-match` | 0.192 us | 0.279 us | Managed faster | 0.69x |
| `simple/ab-plus` | 0.148 us | 0.224 us | Managed faster | 0.66x |
| `industry/boostdocs-date-match` | 0.162 us | 0.249 us | Managed faster | 0.65x |
| `industry/boostdocs-ftp-line-match` | 0.237 us | 0.386 us | Managed faster | 0.61x |
| `simple/loglevel-multiline` | 0.180 us | 0.302 us | Managed faster | 0.60x |
| `literal/missing` | 0.075 us | 0.218 us | Managed faster | 0.34x |
| `literal/late` | 0.079 us | 0.260 us | Managed faster | 0.30x |
| `common/one-node-backtracking` | 0.055 us | 0.185 us | Inconclusive | 0.30x |
| `backtracking/assertion-capture` | 0.082 us | 0.296 us | Managed faster | 0.28x |
| `simple/foo-dense` | 0.043 us | 0.220 us | Managed faster | 0.20x |
| `literal/early` | 0.041 us | 0.230 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.035 us | 0.226 us | Managed faster | 0.16x |
| `common/backtracking` | 0.194 us | 1.341 us | Managed faster | 0.14x |
| `common/email-miss` | 0.046 us | 0.474 us | Managed faster | 0.10x |
| `simple/foo-optional-bar` | 0.015 us | 0.235 us | Managed faster | 0.07x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 138.597 us | 154.160 us | Managed faster | 0.90x |
| `industry/rust-sherlock-ing-count` | 9,879.750 us | 12,803.200 us | Managed faster | 0.77x |
| `simple/loglevel-multiline` | 0.236 us | 0.362 us | Managed faster | 0.66x |
| `industry/rust-sherlock-nonnewline-count` | 1,260.575 us | 2,415.281 us | Managed faster | 0.52x |
| `simple/ab-plus` | 0.171 us | 0.349 us | Managed faster | 0.49x |
| `simple/foo-optional-bar` | 0.152 us | 0.432 us | Inconclusive | 0.35x |
| `simple/foo-dense` | 0.068 us | 0.239 us | Managed faster | 0.29x |
| `industry/leipzig-twain-count` | 628.823 us | 2,276.667 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 164.734 us | 651.861 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,189.267 us | 14,830.467 us | Managed faster | 0.21x |
| `simple/httpclient-caseless` | 0.080 us | 0.390 us | Managed faster | 0.21x |
| `common/matches-words` | 9.178 us | 75.367 us | Managed faster | 0.12x |
| `common/matches-word` | 1.207 us | 18.319 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,101.337 us | 18,312.800 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,877.800 us | 35,660.300 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,184.836 us | 44,305.800 us | Managed faster | 0.05x |
| `common/matches-set` | 7.727 us | 193.500 us | Managed faster | 0.04x |
| `common/matches-boundary` | 7.718 us | 201.080 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 9.295 us | 272.001 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,813.958 us | 124,248.200 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 274.640 us | 19,334.700 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 455.940 us | 135,586.000 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,045.911 us | 464,328.100 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.832 us | 0.784 us | Native faster | 1.06x |
| `simple/ab-plus` | 0.975 us | 1.055 us | Managed faster | 0.93x |
| `simple/foo-dense` | 0.629 us | 0.800 us | Managed faster | 0.78x |
| `simple/httpclient-caseless` | 0.676 us | 1.064 us | Managed faster | 0.64x |
| `simple/foo-optional-bar` | 0.700 us | 1.139 us | Managed faster | 0.62x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 49.513 us | — | Excluded | — |
| `simple/ab-plus` | 1.686 us | — | Excluded | — |
| `simple/foo-dense` | 0.815 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.889 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.892 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.139 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.896 us | 0.637 us | Native faster | 1.41x |
| `pcre2/recursive-optional` | 0.894 us | 0.762 us | Native faster | 1.17x |
| `pcre2/subroutine-prefix-digits` | 0.225 us | 0.238 us | Managed faster | 0.95x |
| `pcre2/conditional-accept-negative-lookahead` | 0.184 us | 0.239 us | Managed faster | 0.77x |
| `pcre2/conditional-negative-lookahead` | 0.185 us | 0.239 us | Managed faster | 0.77x |
| `pcre2/conditional-lookahead` | 0.191 us | 0.256 us | Managed faster | 0.74x |
| `pcre2/grapheme-cluster` | 0.116 us | 0.219 us | Managed faster | 0.53x |
| `pcre2/backslash-c-literal` | 0.101 us | 0.225 us | Managed faster | 0.45x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.248 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 8.367 us | 10.086 us | Managed faster | 0.83x |
| `pcre2/grapheme-cluster` | 1.901 us | 2.791 us | Managed faster | 0.68x |
| `pcre2/same-start-global` | 0.408 us | 0.742 us | Managed faster | 0.55x |
| `pcre2/kreset-bar-or-baz` | 0.185 us | 0.370 us | Managed faster | 0.50x |
| `pcre2/duplicate-names` | 0.185 us | 0.375 us | Managed faster | 0.49x |
| `pcre2/branch-reset-basic` | 0.205 us | 0.448 us | Managed faster | 0.46x |
| `pcre2/kreset-atomic-alt` | 0.138 us | 0.299 us | Inconclusive | 0.46x |
| `pcre2/kreset-global` | 0.113 us | 0.269 us | Managed faster | 0.42x |
| `pcre2/branch-reset-followup` | 0.187 us | 0.453 us | Managed faster | 0.41x |
| `pcre2/branch-reset-backref` | 0.183 us | 0.476 us | Inconclusive | 0.39x |
| `pcre2/branch-reset-nested` | 0.192 us | 0.577 us | Managed faster | 0.33x |
| `pcre2/kreset-captured-repeat` | 2.676 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.759 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.680 us | 0.854 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.682 us | 0.917 us | Managed faster | 0.75x |
| `pcre2/duplicate-names` | 0.666 us | 0.904 us | Managed faster | 0.74x |
| `pcre2/kreset-global` | 0.597 us | 0.812 us | Managed faster | 0.73x |
| `pcre2/branch-reset-followup` | 0.665 us | 0.987 us | Managed faster | 0.68x |
| `pcre2/branch-reset-backref` | 0.681 us | 1.020 us | Managed faster | 0.67x |
| `pcre2/branch-reset-basic` | 0.697 us | 1.143 us | Managed faster | 0.61x |
| `pcre2/branch-reset-nested` | 0.701 us | 1.156 us | Managed faster | 0.61x |
| `pcre2/grapheme-cluster` | 4.805 us | 8.277 us | Managed faster | 0.58x |
| `pcre2/same-start-global` | 0.635 us | 1.203 us | Managed faster | 0.53x |
| `literal/empty-unicode` | 7.809 us | 27.627 us | Managed faster | 0.28x |
| `pcre2/kreset-captured-repeat` | 3.254 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.201 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.186 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.859 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.959 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.844 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.854 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.854 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.143 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.847 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.855 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.119 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.754 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.093 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.232 us | — | Excluded | — |

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
