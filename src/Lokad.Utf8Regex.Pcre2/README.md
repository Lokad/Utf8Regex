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

Snapshot SHA-256: `B7EECC081AB045D98D97118DE81A22E69F9BB3F765786F3BE6115ACE6087C72D`.

Comparator Status: `78` managed faster, `0` equivalent, `11` native faster, `12` inconclusive, `0` unqualified, `25` excluded.

Measured source `03f6839ec23c`: .NET 11.0.0-rc.3.26522.148. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/date-match` | 0.695 us | 0.158 us | Native faster | 4.36x |
| `common/date-miss` | 2.451 us | 0.575 us | Native faster | 4.29x |
| `common/one-node-backtracking` | 0.174 us | 0.072 us | Native faster | 2.40x |
| `simple/httpclient-caseless` | 0.116 us | 0.121 us | Managed faster | 0.96x |
| `simple/foo-dense` | 0.068 us | 0.088 us | Managed faster | 0.77x |
| `industry/boostdocs-float-match` | 0.058 us | 0.115 us | Managed faster | 0.50x |
| `simple/loglevel-multiline` | 0.076 us | 0.205 us | Managed faster | 0.37x |
| `common/email-match` | 0.099 us | 0.300 us | Managed faster | 0.33x |
| `industry/boostdocs-ftp-line-match` | 0.093 us | 0.288 us | Managed faster | 0.32x |
| `common/email-miss` | 0.107 us | 0.358 us | Managed faster | 0.30x |
| `backtracking/assertion-capture` | 0.602 us | 2.071 us | Inconclusive | 0.29x |
| `common/ip-match` | 0.080 us | 0.288 us | Managed faster | 0.28x |
| `common/ip-miss` | 0.081 us | 0.313 us | Managed faster | 0.26x |
| `industry/boostdocs-credit-card-match` | 0.033 us | 0.156 us | Managed faster | 0.21x |
| `industry/boostdocs-postcode-match` | 0.024 us | 0.117 us | Managed faster | 0.21x |
| `industry/boostdocs-date-match` | 0.020 us | 0.111 us | Managed faster | 0.18x |
| `common/uri-match` | 0.034 us | 0.197 us | Managed faster | 0.17x |
| `simple/foo-optional-bar` | 0.014 us | 0.109 us | Managed faster | 0.12x |
| `common/uri-miss` | 0.060 us | 0.614 us | Managed faster | 0.10x |
| `simple/ab-plus` | 0.009 us | 0.098 us | Managed faster | 0.09x |
| `literal/missing` | 0.154 us | 2.080 us | Inconclusive | 0.07x |
| `literal/late` | 0.152 us | 2.096 us | Inconclusive | 0.07x |
| `literal/early` | 0.124 us | 1.898 us | Inconclusive | 0.07x |
| `literal/absolute-anchored` | 0.117 us | 1.820 us | Managed faster | 0.06x |
| `backtracking/capture-backreference` | 0.730 us | 13.343 us | Inconclusive | 0.05x |
| `common/backtracking` | 0.011 us | 1.237 us | Managed faster | 0.01x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/matches-set` | 239.952 us | 206.328 us | Native faster | 1.16x |
| `industry/rust-sherlock-ing-count` | 11,767.967 us | 13,517.633 us | Managed faster | 0.87x |
| `simple/httpclient-caseless` | 0.887 us | 1.135 us | Managed faster | 0.78x |
| `simple/foo-dense` | 0.497 us | 0.830 us | Managed faster | 0.60x |
| `backtracking/alternation-repeat` | 79.376 us | 160.027 us | Managed faster | 0.50x |
| `industry/leipzig-twain-count` | 5,375.200 us | 11,496.000 us | Inconclusive | 0.47x |
| `simple/foo-optional-bar` | 0.300 us | 1.178 us | Managed faster | 0.25x |
| `industry/mariomka-ip-count` | 4,943.429 us | 21,721.000 us | Managed faster | 0.23x |
| `simple/loglevel-multiline` | 0.163 us | 0.862 us | Inconclusive | 0.19x |
| `industry/mariomka-uri-count` | 22,466.450 us | 133,905.100 us | Managed faster | 0.17x |
| `industry/leipzig-name-family-count` | 4,226.025 us | 28,332.300 us | Inconclusive | 0.15x |
| `industry/rust-sherlock-holmes-window-count` | 139.929 us | 986.060 us | Managed faster | 0.14x |
| `character/unicode-class-dense` | 39.643 us | 278.394 us | Managed faster | 0.14x |
| `common/matches-word` | 3.406 us | 23.908 us | Managed faster | 0.14x |
| `industry/mariomka-email-count` | 19,100.600 us | 145,397.200 us | Managed faster | 0.13x |
| `simple/ab-plus` | 0.137 us | 1.096 us | Managed faster | 0.12x |
| `common/matches-words` | 9.599 us | 81.056 us | Managed faster | 0.12x |
| `industry/rust-sherlock-word-holmes-count` | 1,561.231 us | 19,901.950 us | Managed faster | 0.08x |
| `industry/rust-sherlock-nonnewline-count` | 180.932 us | 2,734.513 us | Managed faster | 0.07x |
| `industry/leipzig-river-window-count` | 3,390.025 us | 55,795.700 us | Inconclusive | 0.06x |
| `common/matches-boundary` | 9.427 us | 211.065 us | Managed faster | 0.04x |
| `industry/rust-sherlock-letter-count` | 2,823.507 us | 98,315.000 us | Managed faster | 0.03x |
| `industry/leipzig-symbol-count` | 1,929.938 us | 482,851.000 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 1.194 us | 1.145 us | Native faster | 1.04x |
| `simple/foo-dense` | 0.786 us | 0.833 us | Managed faster | 0.94x |
| `simple/loglevel-multiline` | 0.807 us | 0.867 us | Managed faster | 0.93x |
| `simple/foo-optional-bar` | 1.028 us | 1.189 us | Managed faster | 0.86x |
| `simple/ab-plus` | 0.895 us | 1.104 us | Managed faster | 0.81x |

### Compatible MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 0.911 us | 1.142 us | Managed faster | 0.80x |
| `simple/loglevel-multiline` | 0.540 us | 0.867 us | Managed faster | 0.62x |
| `simple/foo-dense` | 0.509 us | 0.834 us | Managed faster | 0.61x |
| `simple/foo-optional-bar` | 0.669 us | 1.191 us | Managed faster | 0.56x |
| `simple/ab-plus` | 0.566 us | 1.106 us | Managed faster | 0.51x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 62.654 us | — | Excluded | — |
| `simple/ab-plus` | 3.582 us | — | Excluded | — |
| `simple/foo-dense` | 1.104 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.259 us | — | Excluded | — |
| `simple/httpclient-caseless` | 1.535 us | — | Excluded | — |
| `simple/loglevel-multiline` | 3.364 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/subroutine-prefix-digits` | 0.478 us | 0.109 us | Inconclusive | 4.36x |
| `pcre2/conditional-negative-lookahead` | 0.393 us | 0.104 us | Native faster | 3.79x |
| `pcre2/conditional-accept-negative-lookahead` | 0.400 us | 0.106 us | Native faster | 3.79x |
| `pcre2/conditional-lookahead` | 0.391 us | 0.117 us | Native faster | 3.36x |
| `pcre2/grapheme-cluster` | 0.827 us | 0.343 us | Native faster | 2.41x |
| `pcre2/backslash-c-literal` | 0.091 us | 0.083 us | Native faster | 1.10x |
| `pcre2/branch-reset-backref` | 0.013 us | 0.127 us | Managed faster | 0.10x |
| `pcre2/recursive-palindrome` | 0.012 us | 0.503 us | Managed faster | 0.02x |
| `pcre2/recursive-optional` | 0.012 us | 0.643 us | Managed faster | 0.02x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/duplicate-names` | 0.319 us | 0.363 us | Managed faster | 0.88x |
| `pcre2/kreset-atomic-alt` | 0.264 us | 0.316 us | Managed faster | 0.83x |
| `literal/empty-unicode` | 8.706 us | 10.541 us | Managed faster | 0.83x |
| `pcre2/same-start-global` | 0.519 us | 0.733 us | Managed faster | 0.71x |
| `pcre2/branch-reset-basic` | 0.309 us | 0.457 us | Managed faster | 0.68x |
| `pcre2/kreset-global` | 0.194 us | 0.293 us | Managed faster | 0.66x |
| `pcre2/kreset-bar-or-baz` | 0.266 us | 0.404 us | Managed faster | 0.66x |
| `pcre2/branch-reset-followup` | 0.297 us | 0.468 us | Managed faster | 0.64x |
| `pcre2/grapheme-cluster` | 5.057 us | 8.853 us | Managed faster | 0.57x |
| `pcre2/branch-reset-nested` | 0.297 us | 0.596 us | Managed faster | 0.50x |
| `pcre2/branch-reset-backref` | 0.302 us | 1.059 us | Managed faster | 0.29x |
| `pcre2/kreset-captured-repeat` | 3.736 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.655 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.922 us | 0.885 us | Native faster | 1.04x |
| `pcre2/duplicate-names` | 0.940 us | 0.961 us | Inconclusive | 0.98x |
| `pcre2/kreset-global` | 0.831 us | 0.863 us | Managed faster | 0.96x |
| `pcre2/branch-reset-followup` | 0.992 us | 1.033 us | Managed faster | 0.96x |
| `pcre2/kreset-bar-or-baz` | 0.931 us | 0.988 us | Managed faster | 0.94x |
| `pcre2/branch-reset-backref` | 0.960 us | 1.067 us | Managed faster | 0.90x |
| `pcre2/branch-reset-basic` | 1.004 us | 1.192 us | Managed faster | 0.84x |
| `pcre2/branch-reset-nested` | 0.978 us | 1.177 us | Managed faster | 0.83x |
| `pcre2/same-start-global` | 0.923 us | 1.335 us | Managed faster | 0.69x |
| `pcre2/grapheme-cluster` | 5.807 us | 8.943 us | Managed faster | 0.65x |
| `literal/empty-unicode` | 9.956 us | 28.952 us | Managed faster | 0.34x |
| `pcre2/kreset-captured-repeat` | 4.928 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.888 us | — | Excluded | — |

### PCRE2-only MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 2.067 us | 2.208 us | Managed faster | 0.94x |
| `pcre2/kreset-atomic-alt` | 0.635 us | 0.891 us | Managed faster | 0.71x |
| `pcre2/kreset-bar-or-baz` | 0.647 us | 0.985 us | Managed faster | 0.66x |
| `literal/empty-unicode` | 1.467 us | 2.274 us | Managed faster | 0.65x |
| `pcre2/duplicate-names` | 0.617 us | 0.959 us | Managed faster | 0.64x |
| `pcre2/branch-reset-followup` | 0.648 us | 1.026 us | Managed faster | 0.63x |
| `pcre2/branch-reset-backref` | 0.654 us | 1.068 us | Managed faster | 0.61x |
| `pcre2/kreset-global` | 0.526 us | 0.864 us | Managed faster | 0.61x |
| `pcre2/branch-reset-basic` | 0.687 us | 1.199 us | Managed faster | 0.57x |
| `pcre2/branch-reset-nested` | 0.637 us | 1.168 us | Managed faster | 0.54x |
| `pcre2/same-start-global` | 0.549 us | 1.338 us | Managed faster | 0.41x |
| `pcre2/kreset-captured-repeat` | 4.715 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.625 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 18.303 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 1.206 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.316 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 1.170 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 1.188 us | — | Excluded | — |
| `pcre2/duplicate-names` | 1.169 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 7.072 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 1.178 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 1.176 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 22.246 us | — | Excluded | — |
| `pcre2/kreset-global` | 1.113 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 16.746 us | — | Excluded | — |
| `pcre2/same-start-global` | 4.377 us | — | Excluded | — |

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
