# Lokad.Utf8Regex.Pcre2

**0.5.0 development**, .NET 10 and .NET 11. A strictly managed, UTF-8-native
selected PCRE2 10.47 standard profile, requiring the exact Core package version
matching the adapter. It can coexist with PythonRe at the same version. There is
no P/Invoke, native PCRE2 binary, RID-specific payload or implementation dependency
other than Core and the BCL.

## Supported scope and limitations

The admitted profile includes literals/classes, branching/repetition, captures,
backreferences, assertions, scoped options, branch reset, duplicate names,
recursion/subroutines, conditionals, atomic groups, backtracking control, `\K`,
Unicode and opted-in `\C`. Normal operations include `IsMatch`, `Match`,
`MatchDetailed`, `Count`, `EnumerateMatches`, `MatchMany` and substitution.

`Analyze().UsesBranchReset` reports parsed branch-reset syntax, including nested
groups and groups without captures. `IsFullyNative` means all supported operations
use the owned managed engines without delegation to another regex implementation;
it does not identify machine code or a particular fast path.
`MinRequiredLengthInBytes` is a conservative byte lower bound across the selected
backends. A scalar token can contribute one byte even when its accepted scalars
require more; zero can mean that no positive bound was established.

Subjects are strictly validated UTF-8 throughout. Normal character matching and
empty-match progression use scalars and primary byte coordinates. Explicit `\C`
is a separately opted-in byte atom: it can report a slice splitting a scalar.
`\K` in an opted-in lookaround can report non-slice ranges. Check the reported
slice-validity properties before decoding or slicing these special results.

With CRLF-aware newline settings, unanchored wildcard search skips the LF after
a failed CR candidate: `.` does not find a match in `\r\n`. Explicitly starting
at LF still permits it. Explicit newline atoms/alternatives, leading class
filtering and DotAll retain their own rules; CRLF is not a blanket ban on
matching either byte. This also applies to the supported wildcard partial probes.

`Probe` offers literal/curated soft/hard partial-match support. Outside those
drivers, generic Soft full matches keep their captures/MARK; unsupported Hard
decisions and ordinary marked misses throw `NotSupportedException`. Ordinary
`Probe(None)` and its MARK results remain available. DFA matching, invalid-UTF subject mode, callouts,
split, full capture history and substitution forms outside the selected profile
are excluded. Operation/option combinations have explicit admission boundaries;
similarity to .NET syntax does not establish PCRE2 support. See the
[profile specification](../../docs/SPEC-PCRE2.md)
and its executable fixtures for exact coverage.

Default shorthand classes are ASCII unless the selected UCP/options say otherwise.
The documented managed uppercasing replacement of final sigma U+03C2 produces
uppercase U+03A3; it intentionally does not copy the observed native 10.47
replacement anomaly that yields lowercase sigma. Complete native PCRE2
compatibility is not promised.

UTF-8 replacement templates are parsed and cached directly as bytes through the
existing simple/extended grammar and execution drivers. Warm queries borrow the
caller span; cold keys are copied after admission. Full strict template validation,
deferred syntax errors, captures/MARK, aliasing and original operation limits
remain. Text and byte keys share one bounded cache with independent entries;
native construction extents use bytes and can require larger position tables.
Funded program owners expose byte-template `TryReplace` into caller-owned storage.
Cold preparation and live output share working admission through final copying;
temporary loans refund synchronously. Program admission conservatively prepays
the bounded replacement cache, including before first use. Substitution flags,
byte starts, lazy errors and original operation controls remain. Output survives
owner disposal; no additional retained-output receipt is needed for this route.

## Example

```csharp
using System.Text;
using Lokad.Utf8Regex.Pcre2;

var regex = new Utf8Pcre2Regex(
    @"(?<scheme>https?)://(?<host>[A-Za-z0-9.-]+)",
    Pcre2CompileOptions.None);

ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(
    "Visit https://example.com and http://localhost.");

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

byte[] redacted = regex.Replace(input, "<$host>");
```

Replacement strings use PCRE2 substitution syntax, not .NET replacement
syntax. The API also exposes UTF-8 replacement patterns, callback replacement,
`TryReplace`, `ReplaceToString`, detailed numeric-slot/name-table results, and
explicit compile and execution settings.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

## Resource controls and ownership

Use `Utf8Pcre2Regex.Create(pattern, options, settings, policy, matchTimeout)` for
the common `Utf8ResourcePolicy`, or legacy `Utf8Pcre2ExecutionLimits` overloads.
Common positive work/depth/working/result caps map to `MatchLimit`, `DepthLimit`,
`HeapLimitInBytes` and `ResultLimitInBytes`. Common null maps to legacy zero
(unlimited); a common zero cap is invalid and work above `uint.MaxValue` rejects.
**Defaults remain unlimited.** Numeric work/depth units depend on the route and
are not native-PCRE2 or Core/PythonRe interchangeable units.

Covered validation, discovery, projection, capture/output shaping and controlled
replacement preparation share the original invocation's work, token and absolute
deadline. Cancellation throws with the original token. Deadline/resource denial
uses `Pcre2MatchException` with `Timeout`, `MatchLimit`, `DepthLimit`, `HeapLimit`
or `ResultLimit`; no silently truncated success is returned. Terminal cursor
advances also check interruption. The original deadline includes host waits.
Caller callback execution cannot be forcibly interrupted.

Concurrent cold replacements can share template preparation. A waiting call
observes its own cancellation, deadline and resource allowance; another
preparer's interruption can cause one independent preparation under the waiter's
controls, rather than transferring that preparer's failure to the waiter.

Major covered arrays, output controls and growth overlap require admission before
allocation. Result/staging capacity is cumulatively charged without refunds;
scalar value/boolean/count and inline partial results need no owned payload.
This is a modeled capacity policy, not total managed heap, process RSS or arbitrary
retained-storage containment. Subject/destination memory, incidental owners,
compiled/cache retention, standalone getters and callback allocations have
separate scope. Basic bounded state, backtracking and interruption metering
remain active even when they have a measurable execution cost.

Controlled `\X` scans and grapheme-aware advancement poll the original token and
deadline in bounded scalar blocks and charge that scan work. Large clusters can
therefore exhaust a finite work allowance earlier.

Grapheme boundaries now use owned Unicode 16.0 break, pictographic and Indic
properties. Earlier category/range approximations could join unrelated letters
or split spacing marks. The existing Unicode Indic-conjunct rule remains, including
its documented difference from native PCRE2 10.47; see SPEC-PCRE2.md.

Fixed finite settings also reach optional Core preparation, omitting unmetered
runtime/emission storage while preserving live search/scalar payloads and PCRE2
operation selection. Construction charges can fall; invocation units and scope
remain unchanged.

Explicit `Compile` factories provide separate construction work/depth/instruction/
byte limits, timeout and token. Construction admits backtracking
metadata conservatively at 256 bytes per UTF-16 pattern character, plus separate
name-lookup admission per lowerer and instruction/auxiliary-array capacity with
growth overlap. Its recursive compiler
has a fixed 256-frame construction-depth backstop; smaller public caps tighten it.
These charges can double-count shared storage and cause earlier refusal; they do
not cover every allocation. An all-declined pattern now fails during construction
with `NotSupportedException`, rather than returning an unusable expression.
Curated partial-probe preparation also shares the original construction owner:
fixed semantic checks replace whole-tree string serialization, and literal
operand storage and bounded scans are admitted before acquisition. This can
cause earlier construction refusal without changing supported Probe semantics.
Cold finite-heap replacement preparation borrows
remaining working capacity as a construction ceiling and retains its additional
peak through replacement. Warm plans use a separate conservative cache. See
[resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits) for exact mappings and scopes.

A declined simple-template optimization lends its complete prepared outcome to
the executor for that call, including templates too large for the cache. Work
and preparation capacity reflect one compilation; numeric implementation costs
are not a stable per-template quota or an exact managed-heap measurement.

Finite controls can select a materially slower route: the existing email corpus
Count can exhaust a 50-million-step policy even though unlimited Count succeeds.
This is a disclosed operating-cost limitation; an experimental candidate-search
change was reverted and is not included. Choose budgets for the required workload;
there is no universal finite default or new release-wide performance claim.

Keep borrowed subjects unchanged and their owners alive. Copied value cursors
retain independent positions but share allowance/deadline. Owned detailed results
remain valid after cursor advance/failure. `MatchMany` may have written a prefix
to caller storage before throwing. Its input and result storage must be disjoint;
overlap is unsupported and is not checked, since writes can corrupt the subject
before later discovery. Empty destinations retain their match-existence probe.
An already cancelled explicit token still wins. Interruption during TryReplace copying can
also leave modified destination bytes. DestinationTooSmall leaves destination
storage unchanged. Host-retained storage requires separate admission.

Slice enumeration and `MatchMany` project derived UTF-16 coordinates incrementally
across results, preserving published snapshots and independent copied positions.
Removed prefix scans can lower modeled work requirements; validation, projection
and cumulative resource/interruption controls retain their scope.

`TryReplace` supports input and template buffers overlapping the destination.
Matched overlapping input stages output before copying to caller storage, so
later gaps and captures retain their original bytes. That temporary capacity
requires working and cumulative result admission under the same invocation.
Disjoint simple-template output and no-match copies retain their direct paths.

Read [release notes](../../CHANGELOG.md) and [migration guide](../../docs/MIGRATION.md).
Benchmark observations below retain their recorded source/runtime and qualification
scope.

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `B26E6012EA4A17458F61B6B49B34B42549673C4637B35132A7AB6EE364B3911F`.

Comparator Status: `76` managed faster, `1` equivalent, `13` native faster, `11` inconclusive, `0` unqualified, `25` excluded.

Measured source `d60679252cf6`: .NET 11.0.0-rc.3.26522.148. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/date-match` | 0.657 us | 0.156 us | Native faster | 4.21x |
| `common/date-miss` | 2.312 us | 0.565 us | Native faster | 4.10x |
| `common/one-node-backtracking` | 0.170 us | 0.077 us | Native faster | 2.21x |
| `simple/httpclient-caseless` | 0.112 us | 0.119 us | Managed faster | 0.94x |
| `simple/foo-dense` | 0.066 us | 0.086 us | Managed faster | 0.77x |
| `industry/boostdocs-float-match` | 0.057 us | 0.123 us | Inconclusive | 0.47x |
| `simple/loglevel-multiline` | 0.073 us | 0.199 us | Managed faster | 0.37x |
| `common/email-match` | 0.099 us | 0.296 us | Managed faster | 0.33x |
| `industry/boostdocs-ftp-line-match` | 0.087 us | 0.272 us | Managed faster | 0.32x |
| `common/ip-match` | 0.090 us | 0.287 us | Inconclusive | 0.31x |
| `common/email-miss` | 0.106 us | 0.348 us | Managed faster | 0.30x |
| `backtracking/assertion-capture` | 0.609 us | 2.291 us | Inconclusive | 0.26x |
| `common/ip-miss` | 0.078 us | 0.311 us | Managed faster | 0.25x |
| `industry/boostdocs-credit-card-match` | 0.032 us | 0.151 us | Managed faster | 0.21x |
| `industry/boostdocs-postcode-match` | 0.023 us | 0.113 us | Inconclusive | 0.21x |
| `industry/boostdocs-date-match` | 0.021 us | 0.110 us | Managed faster | 0.19x |
| `common/uri-match` | 0.033 us | 0.197 us | Managed faster | 0.17x |
| `simple/foo-optional-bar` | 0.013 us | 0.107 us | Managed faster | 0.12x |
| `common/uri-miss` | 0.060 us | 0.596 us | Managed faster | 0.10x |
| `simple/ab-plus` | 0.009 us | 0.098 us | Managed faster | 0.09x |
| `literal/late` | 0.154 us | 1.976 us | Managed faster | 0.08x |
| `literal/early` | 0.127 us | 1.693 us | Managed faster | 0.07x |
| `literal/missing` | 0.150 us | 2.168 us | Inconclusive | 0.07x |
| `backtracking/capture-backreference` | 0.694 us | 10.640 us | Inconclusive | 0.07x |
| `literal/absolute-anchored` | 0.116 us | 1.808 us | Managed faster | 0.06x |
| `common/backtracking` | 0.011 us | 1.212 us | Managed faster | 0.01x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/matches-set` | 236.579 us | 203.996 us | Native faster | 1.16x |
| `industry/rust-sherlock-ing-count` | 12,315.667 us | 13,320.800 us | Managed faster | 0.92x |
| `simple/httpclient-caseless` | 0.852 us | 1.106 us | Managed faster | 0.77x |
| `simple/foo-dense` | 0.485 us | 0.812 us | Managed faster | 0.60x |
| `backtracking/alternation-repeat` | 76.301 us | 156.708 us | Managed faster | 0.49x |
| `industry/leipzig-twain-count` | 5,274.850 us | 11,716.100 us | Inconclusive | 0.45x |
| `simple/foo-optional-bar` | 0.286 us | 1.160 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 4,826.500 us | 21,550.700 us | Managed faster | 0.22x |
| `simple/loglevel-multiline` | 0.148 us | 0.827 us | Managed faster | 0.18x |
| `industry/mariomka-uri-count` | 22,024.000 us | 131,611.400 us | Managed faster | 0.17x |
| `industry/leipzig-name-family-count` | 4,098.038 us | 28,404.500 us | Inconclusive | 0.15x |
| `common/matches-word` | 3.342 us | 23.522 us | Managed faster | 0.14x |
| `industry/rust-sherlock-holmes-window-count` | 135.798 us | 991.373 us | Managed faster | 0.14x |
| `simple/ab-plus` | 0.141 us | 1.083 us | Managed faster | 0.13x |
| `industry/mariomka-email-count` | 18,636.750 us | 143,405.300 us | Managed faster | 0.13x |
| `character/unicode-class-dense` | 39.364 us | 306.611 us | Managed faster | 0.13x |
| `common/matches-words` | 9.413 us | 80.108 us | Managed faster | 0.12x |
| `industry/rust-sherlock-word-holmes-count` | 1,557.377 us | 19,743.850 us | Inconclusive | 0.08x |
| `industry/rust-sherlock-nonnewline-count` | 184.381 us | 2,773.821 us | Managed faster | 0.07x |
| `industry/leipzig-river-window-count` | 3,329.842 us | 54,511.900 us | Managed faster | 0.06x |
| `common/matches-boundary` | 9.487 us | 206.966 us | Managed faster | 0.05x |
| `industry/rust-sherlock-letter-count` | 2,792.550 us | 97,018.500 us | Managed faster | 0.03x |
| `industry/leipzig-symbol-count` | 1,962.276 us | 486,816.900 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 1.302 us | 1.111 us | Native faster | 1.17x |
| `simple/foo-dense` | 0.790 us | 0.810 us | Inconclusive | 0.97x |
| `simple/loglevel-multiline` | 0.778 us | 0.848 us | Managed faster | 0.92x |
| `simple/foo-optional-bar` | 1.046 us | 1.172 us | Managed faster | 0.89x |
| `simple/ab-plus` | 0.937 us | 1.092 us | Managed faster | 0.86x |

### Compatible MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 0.905 us | 1.110 us | Managed faster | 0.82x |
| `simple/foo-dense` | 0.497 us | 0.815 us | Managed faster | 0.61x |
| `simple/foo-optional-bar` | 0.706 us | 1.174 us | Managed faster | 0.60x |
| `simple/loglevel-multiline` | 0.491 us | 0.851 us | Managed faster | 0.58x |
| `simple/ab-plus` | 0.599 us | 1.096 us | Managed faster | 0.55x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 59.882 us | — | Excluded | — |
| `simple/ab-plus` | 3.569 us | — | Excluded | — |
| `simple/foo-dense` | 1.035 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.206 us | — | Excluded | — |
| `simple/httpclient-caseless` | 1.474 us | — | Excluded | — |
| `simple/loglevel-multiline` | 3.507 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/subroutine-prefix-digits` | 0.461 us | 0.107 us | Native faster | 4.31x |
| `pcre2/conditional-accept-negative-lookahead` | 0.390 us | 0.103 us | Native faster | 3.77x |
| `pcre2/conditional-negative-lookahead` | 0.387 us | 0.106 us | Native faster | 3.66x |
| `pcre2/conditional-lookahead` | 0.388 us | 0.114 us | Native faster | 3.40x |
| `pcre2/grapheme-cluster` | 0.813 us | 0.341 us | Native faster | 2.39x |
| `pcre2/backslash-c-literal` | 0.085 us | 0.081 us | Native faster | 1.04x |
| `pcre2/branch-reset-backref` | 0.012 us | 0.115 us | Managed faster | 0.11x |
| `pcre2/recursive-palindrome` | 0.012 us | 0.502 us | Managed faster | 0.02x |
| `pcre2/recursive-optional` | 0.011 us | 0.617 us | Managed faster | 0.02x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.258 us | 0.310 us | Managed faster | 0.83x |
| `literal/empty-unicode` | 8.243 us | 10.100 us | Managed faster | 0.81x |
| `pcre2/duplicate-names` | 0.283 us | 0.374 us | Managed faster | 0.76x |
| `pcre2/same-start-global` | 0.496 us | 0.662 us | Managed faster | 0.75x |
| `pcre2/kreset-bar-or-baz` | 0.263 us | 0.387 us | Managed faster | 0.69x |
| `pcre2/branch-reset-basic` | 0.310 us | 0.460 us | Managed faster | 0.67x |
| `pcre2/kreset-global` | 0.183 us | 0.281 us | Managed faster | 0.66x |
| `pcre2/branch-reset-followup` | 0.284 us | 0.478 us | Managed faster | 0.59x |
| `pcre2/grapheme-cluster` | 4.927 us | 8.657 us | Managed faster | 0.57x |
| `pcre2/branch-reset-nested` | 0.294 us | 0.574 us | Managed faster | 0.51x |
| `pcre2/branch-reset-backref` | 0.293 us | 1.031 us | Managed faster | 0.28x |
| `pcre2/kreset-captured-repeat` | 3.795 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.661 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.910 us | 0.875 us | Native faster | 1.04x |
| `pcre2/duplicate-names` | 0.980 us | 0.944 us | Native faster | 1.04x |
| `pcre2/kreset-bar-or-baz` | 0.958 us | 0.951 us | Equivalent | 1.00x |
| `pcre2/kreset-global` | 0.813 us | 0.841 us | Managed faster | 0.97x |
| `pcre2/branch-reset-followup` | 0.924 us | 1.015 us | Managed faster | 0.91x |
| `pcre2/branch-reset-backref` | 0.951 us | 1.060 us | Managed faster | 0.89x |
| `pcre2/branch-reset-nested` | 0.968 us | 1.139 us | Managed faster | 0.85x |
| `pcre2/branch-reset-basic` | 0.993 us | 1.169 us | Managed faster | 0.85x |
| `pcre2/same-start-global` | 0.834 us | 1.228 us | Managed faster | 0.68x |
| `pcre2/grapheme-cluster` | 5.638 us | 8.636 us | Managed faster | 0.65x |
| `literal/empty-unicode` | 9.775 us | 30.264 us | Managed faster | 0.32x |
| `pcre2/kreset-captured-repeat` | 5.033 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.843 us | — | Excluded | — |

### PCRE2-only MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 1.995 us | 2.161 us | Managed faster | 0.92x |
| `pcre2/duplicate-names` | 0.645 us | 0.949 us | Managed faster | 0.68x |
| `pcre2/kreset-atomic-alt` | 0.593 us | 0.884 us | Managed faster | 0.67x |
| `literal/empty-unicode` | 1.456 us | 2.240 us | Managed faster | 0.65x |
| `pcre2/kreset-bar-or-baz` | 0.593 us | 0.955 us | Managed faster | 0.62x |
| `pcre2/branch-reset-followup` | 0.593 us | 1.006 us | Managed faster | 0.59x |
| `pcre2/branch-reset-backref` | 0.621 us | 1.058 us | Managed faster | 0.59x |
| `pcre2/branch-reset-basic` | 0.663 us | 1.166 us | Managed faster | 0.57x |
| `pcre2/kreset-global` | 0.481 us | 0.849 us | Managed faster | 0.57x |
| `pcre2/branch-reset-nested` | 0.628 us | 1.154 us | Managed faster | 0.54x |
| `pcre2/same-start-global` | 0.514 us | 1.235 us | Managed faster | 0.42x |
| `pcre2/kreset-captured-repeat` | 4.682 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.599 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 17.132 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 1.130 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.231 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 1.105 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 1.135 us | — | Excluded | — |
| `pcre2/duplicate-names` | 1.109 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.766 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 1.144 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 1.140 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 21.617 us | — | Excluded | — |
| `pcre2/kreset-global` | 1.064 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 16.458 us | — | Excluded | — |
| `pcre2/same-start-global` | 4.515 us | — | Excluded | — |

<details>
<summary>Qualification, allocations and scaling evidence</summary>

The [full snapshot](../../PCRE2.Benchmarks.json) retains per-row confidence intervals, exclusion reasons, input/source/binary identities, allocation probes, native build/plan fingerprints and managed/comparator workspace contracts. Managed-thread allocation probes do not measure retained native memory. Reusable match buffers are neither thread-safe nor reentrant; finite/token/deadline costs remain separate from these ordinary rows.

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
- `pcre2-special-matchmany/pcre2/kreset-captured-repeat`: Structured checksum mismatch: managed=A8517ACD40FBB4CC, comparator=454EE9D126605347.
- `pcre2-special-matchmany/pcre2/kreset-repeat`: Structured checksum mismatch: managed=A8517ACD40FBB4CC, comparator=454EE9D126605347.
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
