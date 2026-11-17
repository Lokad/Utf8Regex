# Lokad.Utf8Regex.Pcre2

**0.5.0 release candidate**, .NET 10 and .NET 11. A strictly managed, UTF-8-native
selected PCRE2 10.47 standard profile, requiring the exact Core package version
matching the adapter. It can coexist with PythonRe at the same version. There is
no P/Invoke, native PCRE2 binary, RID-specific payload or implementation dependency
other than Core and the BCL.

## Subject input contract

Pass already-valid UTF-8 text and keep borrowed bytes unchanged while results or
cursors use it. Regex operations do not validate subjects or expose an input
policy. Validate external bytes where they become text. Pattern/template/output
validation, checked accesses, scalar semantics and matching/output admission
remain independent. Results expose byte endpoints without prefix scans or UTF-16
coordinates. See the [PCRE2 contract](../../docs/SPEC-PCRE2.md#subject-input-contract).

Standalone views expose byte access rather than `GetValueString`. Inspect
`IsUtf8SliceWellFormed` before host decoding: opted-in `\C` can return scalar
fragments. Noncontiguous reported spans still reject byte access. Host decoding
and its allocations are outside regex resource/interruption controls.

Canonical benchmarks give both sides valid text and use native `NO_UTF_CHECK`,
equivalent options and matching requested outputs. This input contract deliberately
differs from native PCRE2's default validation. Frozen tables retain their original
producer and comparator guarantees until a coherent refresh.

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

Subjects follow the UTF checking or trusted-input contract above. Normal character matching and
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

Replacement strings use PCRE2 substitution syntax. Templates return owned bytes
through `Replace` or write caller storage through `TryReplace`. Decode valid output
in the host when text is needed; the extra string is outside regex resource
controls. Opt-in `\C` can produce raw malformed UTF-8, so check that output first.
Callback replacement remains pending a separate API cleanup. Detailed numeric-slot/
name-table results and explicit compile/execution settings remain available.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

## Delivery evidence

Core, PythonRe and PCRE2 0.4.0 are published. All four 0.5.0 packages form the
qualified candidate.
PCRE2 now requires caller-valid UTF-8, like the other profiles. Frozen packages
and measurements retain their own identities.

Use `tools/qualify-package-candidate.ps1 -Profiles Core,PythonRe,Pcre2,Posix` at a
coherent boundary with a fresh common version and exact Core pins. It checks
package/symbol provenance, APIs, restore identities and isolated consumers.
Windows/Linux correctness, original consumer acceptance and current performance
remain separate checks. Publication does not establish downstream adoption or
complete profile equivalence.

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

Enumeration and `MatchMany` publish byte endpoints and value status directly,
preserving snapshots and independent copied positions. Capture publication has
no coordinate table, sort or prefix scan. Numerical work/storage needs can drop;
validation, allocation admission and cumulative interruption/limits stay enforced.

`TryReplace` supports input and template buffers overlapping the destination.
Matched overlapping input stages output before copying to caller storage, so
later gaps and captures retain their original bytes. That temporary capacity
requires working and cumulative result admission under the same invocation.
Disjoint simple-template output and no-match copies retain their direct paths.

Read [release notes](../../CHANGELOG.md) and [migration guide](../../docs/MIGRATION.md).
Benchmark observations below retain their recorded source/runtime and qualification
scope. They predate the byte-only result migration and are historical observations,
not qualification of the current source.

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `82BC7CD4CB1EEC0438DAEC5EC1BB95BB7852C93AD67CB22F7247E3E0CD6842A6`.

Comparator Status: `80` managed faster, `1` equivalent, `9` native faster, `11` inconclusive, `0` unqualified, `25` excluded.

Measured source `0e10d86b6038`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/one-node-backtracking` | 0.184 us | 0.075 us | Native faster | 2.47x |
| `simple/httpclient-caseless` | 0.136 us | 0.136 us | Inconclusive | 1.00x |
| `simple/foo-dense` | 0.081 us | 0.104 us | Managed faster | 0.79x |
| `industry/boostdocs-float-match` | 0.060 us | 0.124 us | Managed faster | 0.48x |
| `common/date-miss` | 0.220 us | 0.575 us | Managed faster | 0.38x |
| `simple/loglevel-multiline` | 0.072 us | 0.206 us | Managed faster | 0.35x |
| `common/email-match` | 0.096 us | 0.293 us | Managed faster | 0.33x |
| `industry/boostdocs-ftp-line-match` | 0.084 us | 0.273 us | Inconclusive | 0.31x |
| `common/email-miss` | 0.103 us | 0.351 us | Managed faster | 0.29x |
| `common/ip-match` | 0.081 us | 0.288 us | Managed faster | 0.28x |
| `backtracking/assertion-capture` | 0.624 us | 2.320 us | Inconclusive | 0.27x |
| `common/ip-miss` | 0.080 us | 0.310 us | Managed faster | 0.26x |
| `industry/boostdocs-postcode-match` | 0.026 us | 0.112 us | Inconclusive | 0.23x |
| `industry/boostdocs-credit-card-match` | 0.035 us | 0.156 us | Managed faster | 0.23x |
| `industry/boostdocs-date-match` | 0.023 us | 0.111 us | Managed faster | 0.21x |
| `common/uri-match` | 0.036 us | 0.196 us | Managed faster | 0.18x |
| `common/date-match` | 0.028 us | 0.157 us | Managed faster | 0.18x |
| `simple/foo-optional-bar` | 0.015 us | 0.125 us | Managed faster | 0.12x |
| `simple/ab-plus` | 0.010 us | 0.097 us | Managed faster | 0.10x |
| `common/uri-miss` | 0.062 us | 0.610 us | Managed faster | 0.10x |
| `literal/missing` | 0.169 us | 1.969 us | Inconclusive | 0.09x |
| `literal/late` | 0.174 us | 2.206 us | Inconclusive | 0.08x |
| `literal/early` | 0.140 us | 1.789 us | Managed faster | 0.08x |
| `literal/absolute-anchored` | 0.135 us | 2.196 us | Inconclusive | 0.06x |
| `backtracking/capture-backreference` | 0.702 us | 11.468 us | Inconclusive | 0.06x |
| `common/backtracking` | 0.012 us | 1.219 us | Managed faster | 0.01x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `industry/rust-sherlock-ing-count` | 11,457.700 us | 13,413.333 us | Managed faster | 0.86x |
| `simple/httpclient-caseless` | 0.924 us | 1.128 us | Managed faster | 0.82x |
| `simple/foo-dense` | 0.560 us | 0.832 us | Managed faster | 0.67x |
| `backtracking/alternation-repeat` | 81.135 us | 158.024 us | Managed faster | 0.52x |
| `industry/leipzig-twain-count` | 5,529.750 us | 11,938.433 us | Inconclusive | 0.47x |
| `simple/foo-optional-bar` | 0.329 us | 1.168 us | Managed faster | 0.28x |
| `simple/loglevel-multiline` | 0.180 us | 0.846 us | Managed faster | 0.21x |
| `industry/rust-sherlock-holmes-window-count` | 153.325 us | 1,001.495 us | Managed faster | 0.15x |
| `industry/mariomka-uri-count` | 20,396.950 us | 133,162.300 us | Managed faster | 0.15x |
| `simple/ab-plus` | 0.160 us | 1.093 us | Managed faster | 0.15x |
| `common/matches-word` | 3.489 us | 24.241 us | Managed faster | 0.14x |
| `common/matches-set` | 24.815 us | 200.551 us | Managed faster | 0.12x |
| `common/matches-words` | 9.474 us | 81.398 us | Managed faster | 0.12x |
| `industry/mariomka-email-count` | 16,179.750 us | 144,980.000 us | Managed faster | 0.11x |
| `industry/mariomka-ip-count` | 2,281.564 us | 21,944.000 us | Managed faster | 0.10x |
| `character/unicode-class-dense` | 29.608 us | 285.144 us | Managed faster | 0.10x |
| `industry/rust-sherlock-word-holmes-count` | 1,607.816 us | 20,027.150 us | Managed faster | 0.08x |
| `industry/rust-sherlock-nonnewline-count` | 200.030 us | 2,838.408 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,684.833 us | 28,932.400 us | Managed faster | 0.06x |
| `industry/leipzig-river-window-count` | 2,873.593 us | 53,890.300 us | Managed faster | 0.05x |
| `common/matches-boundary` | 9.887 us | 210.696 us | Managed faster | 0.05x |
| `industry/rust-sherlock-letter-count` | 2,804.271 us | 98,285.400 us | Managed faster | 0.03x |
| `industry/leipzig-symbol-count` | 1,785.413 us | 479,406.300 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 1.331 us | 1.133 us | Native faster | 1.17x |
| `simple/loglevel-multiline` | 0.926 us | 0.849 us | Native faster | 1.10x |
| `simple/foo-dense` | 0.902 us | 0.832 us | Native faster | 1.08x |
| `simple/foo-optional-bar` | 1.140 us | 1.180 us | Managed faster | 0.97x |
| `simple/ab-plus` | 1.009 us | 1.101 us | Managed faster | 0.92x |

### Compatible MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 0.986 us | 1.133 us | Managed faster | 0.87x |
| `simple/foo-optional-bar` | 0.744 us | 1.168 us | Managed faster | 0.64x |
| `simple/foo-dense` | 0.517 us | 0.828 us | Managed faster | 0.63x |
| `simple/loglevel-multiline` | 0.503 us | 0.850 us | Managed faster | 0.59x |
| `simple/ab-plus` | 0.615 us | 1.092 us | Managed faster | 0.56x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 56.019 us | — | Excluded | — |
| `simple/ab-plus` | 3.412 us | — | Excluded | — |
| `simple/foo-dense` | 1.111 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.294 us | — | Excluded | — |
| `simple/httpclient-caseless` | 1.641 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.757 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/backslash-c-literal` | 0.104 us | 0.082 us | Native faster | 1.27x |
| `pcre2/grapheme-cluster` | 0.173 us | 0.346 us | Managed faster | 0.50x |
| `pcre2/subroutine-prefix-digits` | 0.016 us | 0.110 us | Managed faster | 0.15x |
| `pcre2/branch-reset-backref` | 0.014 us | 0.125 us | Managed faster | 0.11x |
| `pcre2/conditional-negative-lookahead` | 0.011 us | 0.105 us | Managed faster | 0.11x |
| `pcre2/conditional-accept-negative-lookahead` | 0.011 us | 0.105 us | Managed faster | 0.11x |
| `pcre2/conditional-lookahead` | 0.011 us | 0.119 us | Managed faster | 0.10x |
| `pcre2/recursive-palindrome` | 0.014 us | 0.512 us | Managed faster | 0.03x |
| `pcre2/recursive-optional` | 0.014 us | 0.635 us | Managed faster | 0.02x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/duplicate-names` | 0.333 us | 0.365 us | Managed faster | 0.91x |
| `pcre2/kreset-atomic-alt` | 0.288 us | 0.318 us | Managed faster | 0.91x |
| `pcre2/same-start-global` | 0.602 us | 0.670 us | Managed faster | 0.90x |
| `pcre2/branch-reset-basic` | 0.362 us | 0.458 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.318 us | 0.411 us | Managed faster | 0.78x |
| `pcre2/branch-reset-followup` | 0.362 us | 0.469 us | Managed faster | 0.78x |
| `pcre2/kreset-global` | 0.224 us | 0.291 us | Managed faster | 0.77x |
| `literal/empty-unicode` | 8.415 us | 11.144 us | Managed faster | 0.75x |
| `pcre2/branch-reset-nested` | 0.338 us | 0.588 us | Managed faster | 0.57x |
| `pcre2/grapheme-cluster` | 4.621 us | 8.749 us | Managed faster | 0.53x |
| `pcre2/branch-reset-backref` | 0.382 us | 1.039 us | Managed faster | 0.37x |
| `pcre2/kreset-captured-repeat` | 3.744 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.624 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-global` | 1.078 us | 0.864 us | Native faster | 1.25x |
| `pcre2/kreset-atomic-alt` | 1.104 us | 0.890 us | Inconclusive | 1.25x |
| `pcre2/duplicate-names` | 1.077 us | 0.955 us | Native faster | 1.13x |
| `pcre2/kreset-bar-or-baz` | 1.008 us | 0.971 us | Native faster | 1.04x |
| `pcre2/branch-reset-backref` | 1.085 us | 1.044 us | Native faster | 1.04x |
| `pcre2/branch-reset-followup` | 1.065 us | 1.053 us | Equivalent | 1.01x |
| `pcre2/branch-reset-basic` | 1.110 us | 1.183 us | Managed faster | 0.94x |
| `pcre2/branch-reset-nested` | 1.048 us | 1.160 us | Managed faster | 0.90x |
| `pcre2/same-start-global` | 0.978 us | 1.253 us | Managed faster | 0.78x |
| `pcre2/grapheme-cluster` | 5.466 us | 8.742 us | Managed faster | 0.63x |
| `literal/empty-unicode` | 10.519 us | 28.962 us | Managed faster | 0.36x |
| `pcre2/kreset-captured-repeat` | 4.883 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.787 us | — | Excluded | — |

### PCRE2-only MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.677 us | 0.887 us | Managed faster | 0.76x |
| `pcre2/duplicate-names` | 0.690 us | 0.950 us | Managed faster | 0.72x |
| `pcre2/kreset-bar-or-baz` | 0.651 us | 0.972 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 1.470 us | 2.194 us | Managed faster | 0.67x |
| `pcre2/kreset-global` | 0.572 us | 0.863 us | Managed faster | 0.66x |
| `pcre2/branch-reset-backref` | 0.682 us | 1.041 us | Managed faster | 0.65x |
| `pcre2/branch-reset-followup` | 0.672 us | 1.047 us | Managed faster | 0.64x |
| `pcre2/branch-reset-basic` | 0.745 us | 1.175 us | Managed faster | 0.64x |
| `pcre2/branch-reset-nested` | 0.674 us | 1.159 us | Managed faster | 0.58x |
| `pcre2/same-start-global` | 0.631 us | 1.249 us | Managed faster | 0.51x |
| `literal/empty-unicode` | 1.034 us | 2.281 us | Managed faster | 0.45x |
| `pcre2/kreset-captured-repeat` | 4.540 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.436 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 16.943 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 1.220 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.333 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 1.232 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 1.200 us | — | Excluded | — |
| `pcre2/duplicate-names` | 1.204 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.293 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 1.235 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 1.200 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 19.430 us | — | Excluded | — |
| `pcre2/kreset-global` | 1.112 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 14.253 us | — | Excluded | — |
| `pcre2/same-start-global` | 4.219 us | — | Excluded | — |

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
