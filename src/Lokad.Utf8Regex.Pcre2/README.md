# Lokad.Utf8Regex.Pcre2

A managed implementation of selected PCRE2 10.47 standard-matcher semantics
for .NET 10 and 11. The adapter requires the exact matching Core package. It
ships without native binaries or interop.

## Subject input contract

Pass valid UTF-8 by default and keep borrowed bytes unchanged during use. Validate
external bytes as they become text. Patterns and UTF-8 templates have their
own validation rules. Starts and reported endpoints use byte offsets.
Select `Pcre2CompileOptions.MatchInvalidUtf8` when a subject can contain malformed
bytes. Matches stay inside valid fragments and retain their original offsets.
[Input and range rules](../../docs/SPEC-PCRE2.md#subject-input-contract)

## Supported scope and limitations

The adapter supports captures, backreferences, assertions, branch reset,
duplicate names, recursion, conditionals, atomic groups and backtracking
verbs. Use `Probe` for ordinary MARK results and supported soft or hard
partial decisions. Numbered callouts such as `(?C1)` match without invoking
a callback. The scope excludes string callouts, DFA matching, RTL, split and
full capture history.
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
ranges and host-owned output. Extended case conversion uses managed scalar
casing and can differ from native PCRE2. [Substitution rules](../../docs/SPEC-PCRE2.md#substitution)

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

`Utf8RegexPolicy` sets match timeouts and optional depth, working-byte
and result-byte limits. Construction has separate limits and a deadline.
`Compile` also accepts a construction token. Operations check the original
cancellation token. Match timeouts and numeric denials throw
`Pcre2MatchException` with a specific error kind. The limits cover modeled
engine depth and storage capacities.
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

Snapshot SHA-256: `6EE4D125B96B97901DAAE5A9B6960BF17D87C293688368E335D81DD2ED1908A7`.

Comparator Status: `73` managed faster, `0` equivalent, `4` native faster, `8` inconclusive, `0` unqualified, `23` excluded.

Measured source `c37ca066c91f`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.349 us | 1.137 us | Native faster | 1.19x |
| `common/ip-match` | 0.426 us | 0.403 us | Native faster | 1.05x |
| `industry/boostdocs-postcode-match` | 0.245 us | 0.241 us | Inconclusive | 1.02x |
| `common/uri-match` | 0.277 us | 0.303 us | Managed faster | 0.91x |
| `common/email-match` | 0.353 us | 0.403 us | Managed faster | 0.88x |
| `common/date-miss` | 0.564 us | 0.665 us | Managed faster | 0.84x |
| `simple/httpclient-caseless` | 0.171 us | 0.232 us | Managed faster | 0.74x |
| `backtracking/capture-backreference` | 0.163 us | 0.229 us | Managed faster | 0.71x |
| `industry/boostdocs-credit-card-match` | 0.188 us | 0.276 us | Managed faster | 0.68x |
| `industry/boostdocs-float-match` | 0.160 us | 0.240 us | Managed faster | 0.67x |
| `common/uri-miss` | 0.444 us | 0.697 us | Managed faster | 0.64x |
| `industry/boostdocs-date-match` | 0.150 us | 0.240 us | Managed faster | 0.62x |
| `common/date-match` | 0.165 us | 0.268 us | Managed faster | 0.62x |
| `industry/boostdocs-ftp-line-match` | 0.220 us | 0.362 us | Managed faster | 0.61x |
| `simple/loglevel-multiline` | 0.177 us | 0.298 us | Managed faster | 0.60x |
| `simple/ab-plus` | 0.132 us | 0.223 us | Managed faster | 0.59x |
| `literal/missing` | 0.073 us | 0.213 us | Managed faster | 0.34x |
| `literal/late` | 0.077 us | 0.251 us | Managed faster | 0.31x |
| `backtracking/assertion-capture` | 0.079 us | 0.294 us | Managed faster | 0.27x |
| `common/one-node-backtracking` | 0.042 us | 0.182 us | Managed faster | 0.23x |
| `simple/foo-dense` | 0.040 us | 0.212 us | Managed faster | 0.19x |
| `literal/early` | 0.040 us | 0.222 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.033 us | 0.219 us | Managed faster | 0.15x |
| `common/backtracking` | 0.182 us | 1.290 us | Managed faster | 0.14x |
| `common/email-miss` | 0.036 us | 0.459 us | Managed faster | 0.08x |
| `simple/foo-optional-bar` | 0.014 us | 0.233 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 137.495 us | 149.288 us | Managed faster | 0.92x |
| `industry/rust-sherlock-ing-count` | 8,916.380 us | 12,511.867 us | Managed faster | 0.71x |
| `simple/loglevel-multiline` | 0.202 us | 0.360 us | Managed faster | 0.56x |
| `industry/rust-sherlock-nonnewline-count` | 1,202.479 us | 2,336.463 us | Managed faster | 0.51x |
| `simple/ab-plus` | 0.159 us | 0.343 us | Managed faster | 0.47x |
| `simple/foo-optional-bar` | 0.138 us | 0.422 us | Managed faster | 0.33x |
| `simple/foo-dense` | 0.067 us | 0.228 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 614.886 us | 2,225.678 us | Managed faster | 0.28x |
| `industry/mariomka-ip-count` | 3,089.408 us | 14,438.200 us | Managed faster | 0.21x |
| `simple/httpclient-caseless` | 0.078 us | 0.376 us | Managed faster | 0.21x |
| `industry/rust-sherlock-holmes-window-count` | 129.981 us | 631.803 us | Managed faster | 0.21x |
| `common/matches-words` | 8.998 us | 73.534 us | Managed faster | 0.12x |
| `common/matches-word` | 1.166 us | 17.908 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 994.334 us | 17,900.100 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,923.335 us | 35,451.200 us | Managed faster | 0.05x |
| `common/matches-boundary` | 8.503 us | 196.345 us | Managed faster | 0.04x |
| `common/matches-set` | 8.546 us | 202.099 us | Managed faster | 0.04x |
| `industry/leipzig-river-window-count` | 1,804.695 us | 43,370.900 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 8.904 us | 260.862 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,771.517 us | 120,538.700 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 275.679 us | 18,821.800 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 404.014 us | 131,467.700 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,006.456 us | 453,715.700 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.754 us | 0.785 us | Managed faster | 0.96x |
| `simple/ab-plus` | 0.811 us | 1.038 us | Managed faster | 0.78x |
| `simple/foo-dense` | 0.596 us | 0.783 us | Managed faster | 0.76x |
| `simple/httpclient-caseless` | 0.641 us | 1.057 us | Managed faster | 0.61x |
| `simple/foo-optional-bar` | 0.588 us | 1.130 us | Managed faster | 0.52x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 32.432 us | — | Excluded | — |
| `simple/ab-plus` | 1.529 us | — | Excluded | — |
| `simple/foo-dense` | 0.839 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.855 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.931 us | — | Excluded | — |
| `simple/loglevel-multiline` | 1.996 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.819 us | 0.624 us | Native faster | 1.31x |
| `pcre2/recursive-optional` | 0.838 us | 0.750 us | Native faster | 1.12x |
| `pcre2/subroutine-prefix-digits` | 0.213 us | 0.236 us | Managed faster | 0.90x |
| `pcre2/conditional-accept-negative-lookahead` | 0.161 us | 0.234 us | Managed faster | 0.69x |
| `pcre2/conditional-negative-lookahead` | 0.161 us | 0.236 us | Managed faster | 0.68x |
| `pcre2/conditional-lookahead` | 0.163 us | 0.252 us | Managed faster | 0.65x |
| `pcre2/grapheme-cluster` | 0.110 us | 0.213 us | Managed faster | 0.51x |
| `pcre2/backslash-c-literal` | 0.096 us | 0.218 us | Managed faster | 0.44x |
| `pcre2/branch-reset-backref` | 0.012 us | 0.239 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 8.556 us | 9.900 us | Managed faster | 0.87x |
| `pcre2/same-start-global` | 0.404 us | 0.637 us | Managed faster | 0.63x |
| `pcre2/grapheme-cluster` | 1.585 us | 2.792 us | Managed faster | 0.57x |
| `pcre2/kreset-atomic-alt` | 0.122 us | 0.290 us | Managed faster | 0.42x |
| `pcre2/duplicate-names` | 0.133 us | 0.342 us | Inconclusive | 0.39x |
| `pcre2/kreset-global` | 0.102 us | 0.260 us | Inconclusive | 0.39x |
| `pcre2/kreset-bar-or-baz` | 0.130 us | 0.360 us | Managed faster | 0.36x |
| `pcre2/branch-reset-basic` | 0.153 us | 0.429 us | Inconclusive | 0.35x |
| `pcre2/branch-reset-followup` | 0.138 us | 0.417 us | Inconclusive | 0.33x |
| `pcre2/branch-reset-backref` | 0.134 us | 0.442 us | Inconclusive | 0.30x |
| `pcre2/branch-reset-nested` | 0.136 us | 0.546 us | Inconclusive | 0.25x |
| `pcre2/kreset-captured-repeat` | 2.290 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.270 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-global` | 0.620 us | 0.810 us | Managed faster | 0.76x |
| `pcre2/kreset-atomic-alt` | 0.556 us | 0.839 us | Managed faster | 0.66x |
| `pcre2/kreset-bar-or-baz` | 0.566 us | 0.907 us | Managed faster | 0.62x |
| `pcre2/duplicate-names` | 0.562 us | 0.906 us | Managed faster | 0.62x |
| `pcre2/branch-reset-followup` | 0.566 us | 0.986 us | Managed faster | 0.58x |
| `pcre2/branch-reset-backref` | 0.573 us | 1.005 us | Managed faster | 0.57x |
| `pcre2/grapheme-cluster` | 4.545 us | 8.186 us | Managed faster | 0.55x |
| `pcre2/branch-reset-nested` | 0.596 us | 1.110 us | Managed faster | 0.54x |
| `pcre2/branch-reset-basic` | 0.588 us | 1.132 us | Managed faster | 0.52x |
| `pcre2/same-start-global` | 0.571 us | 1.193 us | Managed faster | 0.48x |
| `literal/empty-unicode` | 8.162 us | 27.267 us | Managed faster | 0.30x |
| `pcre2/kreset-captured-repeat` | 3.070 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.009 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 13.934 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.779 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.830 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.761 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.786 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.758 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 5.937 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.791 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.795 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 12.494 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.752 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 9.287 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.021 us | — | Excluded | — |

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
