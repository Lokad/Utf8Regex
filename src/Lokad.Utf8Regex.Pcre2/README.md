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

Snapshot SHA-256: `A02687CC363D7274C6FDE416F8176DDBF7124FCB808D2F1F8C0453F3C6C23472`.

Comparator Status: `72` managed faster, `0` equivalent, `8` native faster, `5` inconclusive, `0` unqualified, `23` excluded.

Measured source `f2907ee35582`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.470 us | 1.158 us | Native faster | 1.27x |
| `common/ip-match` | 0.478 us | 0.414 us | Native faster | 1.15x |
| `industry/boostdocs-postcode-match` | 0.262 us | 0.251 us | Native faster | 1.04x |
| `common/date-miss` | 0.722 us | 0.691 us | Native faster | 1.04x |
| `common/uri-match` | 0.323 us | 0.312 us | Native faster | 1.04x |
| `common/email-match` | 0.376 us | 0.415 us | Managed faster | 0.91x |
| `simple/httpclient-caseless` | 0.208 us | 0.243 us | Managed faster | 0.86x |
| `industry/boostdocs-credit-card-match` | 0.237 us | 0.286 us | Managed faster | 0.83x |
| `common/date-match` | 0.224 us | 0.277 us | Managed faster | 0.81x |
| `backtracking/capture-backreference` | 0.185 us | 0.239 us | Managed faster | 0.77x |
| `industry/boostdocs-float-match` | 0.184 us | 0.247 us | Managed faster | 0.74x |
| `common/uri-miss` | 0.532 us | 0.724 us | Managed faster | 0.73x |
| `industry/boostdocs-date-match` | 0.172 us | 0.249 us | Managed faster | 0.69x |
| `simple/ab-plus` | 0.151 us | 0.237 us | Managed faster | 0.64x |
| `industry/boostdocs-ftp-line-match` | 0.236 us | 0.374 us | Managed faster | 0.64x |
| `simple/loglevel-multiline` | 0.189 us | 0.309 us | Managed faster | 0.61x |
| `literal/missing` | 0.077 us | 0.224 us | Managed faster | 0.34x |
| `literal/late` | 0.082 us | 0.266 us | Managed faster | 0.31x |
| `common/one-node-backtracking` | 0.056 us | 0.189 us | Managed faster | 0.29x |
| `backtracking/assertion-capture` | 0.082 us | 0.306 us | Managed faster | 0.27x |
| `simple/foo-dense` | 0.044 us | 0.224 us | Managed faster | 0.20x |
| `literal/early` | 0.043 us | 0.230 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.037 us | 0.227 us | Managed faster | 0.16x |
| `common/backtracking` | 0.197 us | 1.348 us | Managed faster | 0.15x |
| `common/email-miss` | 0.045 us | 0.505 us | Managed faster | 0.09x |
| `simple/foo-optional-bar` | 0.016 us | 0.249 us | Managed faster | 0.06x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `backtracking/alternation-repeat` | 142.250 us | 150.972 us | Managed faster | 0.94x |
| `industry/rust-sherlock-ing-count` | 10,611.575 us | 12,805.800 us | Managed faster | 0.83x |
| `simple/loglevel-multiline` | 0.241 us | 0.373 us | Managed faster | 0.65x |
| `industry/rust-sherlock-nonnewline-count` | 1,431.281 us | 2,499.941 us | Managed faster | 0.57x |
| `simple/ab-plus` | 0.187 us | 0.354 us | Inconclusive | 0.53x |
| `simple/foo-optional-bar` | 0.155 us | 0.436 us | Inconclusive | 0.35x |
| `simple/foo-dense` | 0.072 us | 0.237 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 636.844 us | 2,253.406 us | Managed faster | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | 166.019 us | 658.605 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 3,203.558 us | 14,849.100 us | Managed faster | 0.22x |
| `simple/httpclient-caseless` | 0.084 us | 0.393 us | Managed faster | 0.21x |
| `common/matches-words` | 9.366 us | 75.390 us | Managed faster | 0.12x |
| `common/matches-word` | 1.215 us | 18.396 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,131.243 us | 18,464.700 us | Managed faster | 0.06x |
| `common/matches-set` | 11.073 us | 196.464 us | Managed faster | 0.06x |
| `common/matches-boundary` | 11.204 us | 200.220 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,955.005 us | 36,231.800 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,221.022 us | 44,205.900 us | Managed faster | 0.05x |
| `character/unicode-class-dense` | 9.182 us | 275.861 us | Managed faster | 0.03x |
| `industry/mariomka-uri-count` | 1,813.972 us | 124,502.000 us | Managed faster | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 275.266 us | 19,531.900 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 449.353 us | 136,167.500 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,053.376 us | 471,424.600 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.861 us | 0.807 us | Native faster | 1.07x |
| `simple/ab-plus` | 0.957 us | 1.090 us | Managed faster | 0.88x |
| `simple/foo-dense` | 0.650 us | 0.816 us | Managed faster | 0.80x |
| `simple/httpclient-caseless` | 0.689 us | 1.097 us | Managed faster | 0.63x |
| `simple/foo-optional-bar` | 0.726 us | 1.183 us | Managed faster | 0.61x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 50.795 us | — | Excluded | — |
| `simple/ab-plus` | 1.738 us | — | Excluded | — |
| `simple/foo-dense` | 0.850 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.939 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.933 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.280 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 0.892 us | 0.649 us | Native faster | 1.38x |
| `pcre2/recursive-optional` | 0.912 us | 0.777 us | Native faster | 1.18x |
| `pcre2/subroutine-prefix-digits` | 0.230 us | 0.246 us | Managed faster | 0.94x |
| `pcre2/conditional-accept-negative-lookahead` | 0.198 us | 0.244 us | Managed faster | 0.81x |
| `pcre2/conditional-negative-lookahead` | 0.198 us | 0.246 us | Managed faster | 0.81x |
| `pcre2/conditional-lookahead` | 0.204 us | 0.262 us | Managed faster | 0.78x |
| `pcre2/grapheme-cluster` | 0.122 us | 0.221 us | Managed faster | 0.55x |
| `pcre2/backslash-c-literal` | 0.111 us | 0.227 us | Managed faster | 0.49x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.250 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 9.718 us | 10.280 us | Managed faster | 0.95x |
| `pcre2/grapheme-cluster` | 1.952 us | 2.865 us | Managed faster | 0.68x |
| `pcre2/same-start-global` | 0.417 us | 0.655 us | Managed faster | 0.64x |
| `pcre2/branch-reset-followup` | 0.214 us | 0.431 us | Managed faster | 0.50x |
| `pcre2/duplicate-names` | 0.174 us | 0.356 us | Managed faster | 0.49x |
| `pcre2/kreset-bar-or-baz` | 0.173 us | 0.371 us | Managed faster | 0.47x |
| `pcre2/kreset-atomic-alt` | 0.139 us | 0.297 us | Inconclusive | 0.47x |
| `pcre2/branch-reset-basic` | 0.196 us | 0.448 us | Managed faster | 0.44x |
| `pcre2/kreset-global` | 0.117 us | 0.268 us | Managed faster | 0.44x |
| `pcre2/branch-reset-backref` | 0.174 us | 0.454 us | Managed faster | 0.38x |
| `pcre2/branch-reset-nested` | 0.176 us | 0.558 us | Managed faster | 0.31x |
| `pcre2/kreset-captured-repeat` | 2.462 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.395 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.699 us | 0.869 us | Managed faster | 0.80x |
| `pcre2/kreset-bar-or-baz` | 0.729 us | 0.947 us | Inconclusive | 0.77x |
| `pcre2/duplicate-names` | 0.694 us | 0.931 us | Managed faster | 0.75x |
| `pcre2/kreset-global` | 0.603 us | 0.838 us | Managed faster | 0.72x |
| `pcre2/branch-reset-followup` | 0.693 us | 0.995 us | Managed faster | 0.70x |
| `pcre2/branch-reset-backref` | 0.702 us | 1.028 us | Managed faster | 0.68x |
| `pcre2/branch-reset-nested` | 0.727 us | 1.145 us | Managed faster | 0.63x |
| `pcre2/branch-reset-basic` | 0.728 us | 1.179 us | Managed faster | 0.62x |
| `pcre2/grapheme-cluster` | 5.080 us | 8.556 us | Managed faster | 0.59x |
| `pcre2/same-start-global` | 0.649 us | 1.233 us | Managed faster | 0.53x |
| `literal/empty-unicode` | 8.842 us | 28.361 us | Managed faster | 0.31x |
| `pcre2/kreset-captured-repeat` | 3.338 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.295 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.438 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.834 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.911 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.814 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.838 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.816 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.398 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.856 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.857 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 13.858 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.758 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 10.563 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.388 us | — | Excluded | — |

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
