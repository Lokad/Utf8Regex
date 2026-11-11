# Lokad.Utf8Regex.Pcre2

**0.5.0 development**, .NET 10 and .NET 11. A strictly managed, UTF-8-native
selected PCRE2 10.47 standard profile, requiring the exact Core package version
matching the adapter. It can coexist with PythonRe at the same version. There is
no P/Invoke, native PCRE2 binary, RID-specific payload or implementation dependency
other than Core and the BCL.

## Subject validation policy

Construct with `inputValidation: Utf8InputValidation.AssumeValid` only when the
host guarantees valid UTF-8 and unchanged borrowed storage. Read-only
`InputValidation` reports the choice; `Validate` is default and checks the whole
subject at most once per logical operation or cursor lifetime. Matching, counting,
replacement/splitting where supported and cursors inherit the engine policy.
Pattern/replacement validation, checked accesses, scalar/byte semantics,
cooperative cancellation/deadlines and admission remain. Required coordinate or
output work may still inspect input. See the [shared contract](../../docs/SPEC.md#subject-input-validation).
Existing benchmark tables use Validate unless explicitly labeled otherwise.

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

Subjects must be valid UTF-8 throughout under the selected validation policy. Normal character matching and
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

## Delivery evidence

Core, PythonRe and PCRE2 0.4.0 are published. The last frozen development boundary
is the [four-package dev66 candidate](../../bench/Experiments/DeliveryDev66-20261110/README.md).
The dev66 candidate includes `inputValidation`. Frozen packages and measurements
retain their own identities.

Use `tools/qualify-package-candidate.ps1 -Profiles Core,PythonRe,Pcre2,Posix` at a
coherent boundary with a fresh common version and exact Core pins. It checks
package/symbol provenance, APIs, restore identities and isolated consumers.
Windows/Linux correctness, original consumer acceptance and current performance
remain separate checks. Publication does not establish downstream adoption or
complete profile equivalence. Historical [RC1 rejection](../../bench/Experiments/ReleaseCandidate-20261020/ERRATA.md)
and [RC2 performance uncertainty](../../bench/Experiments/ReleaseCandidate2-20261026/ERRATA.md)
stay with their original receipts.

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

Snapshot SHA-256: `12583A7EB2F5E4FC442F4F09C90AC656FDDC978FEAE01E46973B1839DA0294E7`.

Comparator Status: `81` managed faster, `1` equivalent, `8` native faster, `11` inconclusive, `0` unqualified, `25` excluded.

Measured source `2c5e341619f8`: .NET 11.0.0-rc.3.26522.148. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/one-node-backtracking` | 0.196 us | 0.079 us | Native faster | 2.47x |
| `simple/httpclient-caseless` | 0.143 us | 0.121 us | Native faster | 1.18x |
| `simple/foo-dense` | 0.090 us | 0.085 us | Native faster | 1.06x |
| `simple/loglevel-multiline` | 0.106 us | 0.208 us | Managed faster | 0.51x |
| `industry/boostdocs-float-match` | 0.057 us | 0.121 us | Managed faster | 0.47x |
| `common/date-miss` | 0.224 us | 0.575 us | Managed faster | 0.39x |
| `common/email-match` | 0.099 us | 0.298 us | Managed faster | 0.33x |
| `common/email-miss` | 0.108 us | 0.359 us | Managed faster | 0.30x |
| `industry/boostdocs-ftp-line-match` | 0.083 us | 0.278 us | Managed faster | 0.30x |
| `common/ip-match` | 0.080 us | 0.293 us | Managed faster | 0.28x |
| `backtracking/assertion-capture` | 0.642 us | 2.390 us | Inconclusive | 0.27x |
| `common/ip-miss` | 0.079 us | 0.313 us | Managed faster | 0.25x |
| `industry/boostdocs-credit-card-match` | 0.035 us | 0.154 us | Managed faster | 0.23x |
| `industry/boostdocs-postcode-match` | 0.025 us | 0.116 us | Managed faster | 0.22x |
| `simple/foo-optional-bar` | 0.020 us | 0.111 us | Inconclusive | 0.18x |
| `industry/boostdocs-date-match` | 0.020 us | 0.113 us | Managed faster | 0.18x |
| `common/uri-match` | 0.036 us | 0.198 us | Managed faster | 0.18x |
| `common/date-match` | 0.028 us | 0.158 us | Managed faster | 0.18x |
| `simple/ab-plus` | 0.011 us | 0.100 us | Managed faster | 0.11x |
| `common/uri-miss` | 0.062 us | 0.610 us | Managed faster | 0.10x |
| `literal/missing` | 0.166 us | 1.849 us | Inconclusive | 0.09x |
| `literal/late` | 0.174 us | 1.980 us | Inconclusive | 0.09x |
| `literal/early` | 0.141 us | 1.841 us | Managed faster | 0.08x |
| `literal/absolute-anchored` | 0.148 us | 1.880 us | Inconclusive | 0.08x |
| `backtracking/capture-backreference` | 0.706 us | 10.190 us | Inconclusive | 0.07x |
| `common/backtracking` | 0.013 us | 1.244 us | Managed faster | 0.01x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `industry/rust-sherlock-ing-count` | 11,501.733 us | 13,406.567 us | Managed faster | 0.86x |
| `simple/httpclient-caseless` | 0.896 us | 1.136 us | Managed faster | 0.79x |
| `simple/foo-dense` | 0.506 us | 0.832 us | Managed faster | 0.61x |
| `backtracking/alternation-repeat` | 81.946 us | 160.450 us | Managed faster | 0.51x |
| `industry/leipzig-twain-count` | 5,356.517 us | 11,037.100 us | Inconclusive | 0.49x |
| `simple/foo-optional-bar` | 0.334 us | 1.188 us | Managed faster | 0.28x |
| `industry/mariomka-ip-count` | 4,988.229 us | 22,093.400 us | Managed faster | 0.23x |
| `simple/loglevel-multiline` | 0.183 us | 0.856 us | Managed faster | 0.21x |
| `industry/mariomka-uri-count` | 22,400.850 us | 134,370.000 us | Managed faster | 0.17x |
| `simple/ab-plus` | 0.166 us | 1.095 us | Managed faster | 0.15x |
| `common/matches-word` | 3.391 us | 22.728 us | Managed faster | 0.15x |
| `industry/rust-sherlock-holmes-window-count` | 141.665 us | 984.461 us | Managed faster | 0.14x |
| `character/unicode-class-dense` | 39.796 us | 283.189 us | Managed faster | 0.14x |
| `industry/mariomka-email-count` | 19,085.900 us | 144,662.500 us | Managed faster | 0.13x |
| `common/matches-set` | 24.915 us | 206.417 us | Managed faster | 0.12x |
| `common/matches-words` | 9.567 us | 81.398 us | Managed faster | 0.12x |
| `industry/rust-sherlock-word-holmes-count` | 1,606.280 us | 20,037.800 us | Managed faster | 0.08x |
| `industry/rust-sherlock-nonnewline-count` | 186.796 us | 2,810.300 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,611.372 us | 29,200.800 us | Inconclusive | 0.06x |
| `common/matches-boundary` | 10.268 us | 211.472 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 2,330.559 us | 55,570.600 us | Managed faster | 0.04x |
| `industry/rust-sherlock-letter-count` | 2,864.800 us | 98,577.200 us | Managed faster | 0.03x |
| `industry/leipzig-symbol-count` | 1,987.290 us | 489,364.400 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 1.237 us | 1.137 us | Native faster | 1.09x |
| `simple/foo-dense` | 0.894 us | 0.841 us | Native faster | 1.06x |
| `simple/loglevel-multiline` | 0.827 us | 0.865 us | Managed faster | 0.96x |
| `simple/foo-optional-bar` | 1.046 us | 1.186 us | Managed faster | 0.88x |
| `simple/ab-plus` | 0.957 us | 1.110 us | Managed faster | 0.86x |

### Compatible MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 0.983 us | 1.139 us | Managed faster | 0.86x |
| `simple/foo-dense` | 0.555 us | 0.841 us | Managed faster | 0.66x |
| `simple/foo-optional-bar` | 0.707 us | 1.186 us | Managed faster | 0.60x |
| `simple/ab-plus` | 0.645 us | 1.110 us | Managed faster | 0.58x |
| `simple/loglevel-multiline` | 0.498 us | 0.861 us | Managed faster | 0.58x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 55.809 us | — | Excluded | — |
| `simple/ab-plus` | 3.375 us | — | Excluded | — |
| `simple/foo-dense` | 1.105 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.231 us | — | Excluded | — |
| `simple/httpclient-caseless` | 1.588 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.755 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/backslash-c-literal` | 0.192 us | 0.083 us | Inconclusive | 2.32x |
| `pcre2/grapheme-cluster` | 0.387 us | 0.347 us | Native faster | 1.12x |
| `pcre2/branch-reset-backref` | 0.020 us | 0.121 us | Inconclusive | 0.17x |
| `pcre2/subroutine-prefix-digits` | 0.014 us | 0.108 us | Managed faster | 0.13x |
| `pcre2/conditional-accept-negative-lookahead` | 0.012 us | 0.105 us | Managed faster | 0.12x |
| `pcre2/conditional-negative-lookahead` | 0.012 us | 0.106 us | Managed faster | 0.12x |
| `pcre2/conditional-lookahead` | 0.013 us | 0.116 us | Managed faster | 0.11x |
| `pcre2/recursive-palindrome` | 0.012 us | 0.531 us | Managed faster | 0.02x |
| `pcre2/recursive-optional` | 0.012 us | 0.645 us | Managed faster | 0.02x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.290 us | 0.314 us | Managed faster | 0.92x |
| `pcre2/duplicate-names` | 0.314 us | 0.369 us | Managed faster | 0.86x |
| `pcre2/kreset-bar-or-baz` | 0.328 us | 0.401 us | Managed faster | 0.82x |
| `pcre2/same-start-global` | 0.549 us | 0.687 us | Managed faster | 0.80x |
| `literal/empty-unicode` | 8.447 us | 10.563 us | Managed faster | 0.80x |
| `pcre2/branch-reset-basic` | 0.344 us | 0.473 us | Managed faster | 0.73x |
| `pcre2/kreset-global` | 0.211 us | 0.292 us | Managed faster | 0.72x |
| `pcre2/branch-reset-followup` | 0.319 us | 0.457 us | Managed faster | 0.70x |
| `pcre2/grapheme-cluster` | 4.992 us | 8.875 us | Managed faster | 0.56x |
| `pcre2/branch-reset-nested` | 0.328 us | 0.600 us | Managed faster | 0.54x |
| `pcre2/branch-reset-backref` | 0.332 us | 1.048 us | Managed faster | 0.32x |
| `pcre2/kreset-captured-repeat` | 3.729 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.638 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 1.028 us | 0.897 us | Native faster | 1.15x |
| `pcre2/duplicate-names` | 0.995 us | 0.969 us | Native faster | 1.03x |
| `pcre2/kreset-global` | 0.857 us | 0.871 us | Equivalent | 0.98x |
| `pcre2/kreset-bar-or-baz` | 0.941 us | 0.972 us | Managed faster | 0.97x |
| `pcre2/branch-reset-backref` | 0.993 us | 1.057 us | Managed faster | 0.94x |
| `pcre2/branch-reset-followup` | 0.969 us | 1.043 us | Managed faster | 0.93x |
| `pcre2/branch-reset-basic` | 1.019 us | 1.198 us | Managed faster | 0.85x |
| `pcre2/branch-reset-nested` | 0.958 us | 1.169 us | Managed faster | 0.82x |
| `pcre2/same-start-global` | 0.964 us | 1.266 us | Managed faster | 0.76x |
| `pcre2/grapheme-cluster` | 5.687 us | 8.896 us | Managed faster | 0.64x |
| `literal/empty-unicode` | 9.979 us | 29.082 us | Managed faster | 0.34x |
| `pcre2/kreset-captured-repeat` | 4.804 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.710 us | — | Excluded | — |

### PCRE2-only MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 2.062 us | 2.188 us | Managed faster | 0.94x |
| `pcre2/kreset-atomic-alt` | 0.622 us | 0.896 us | Managed faster | 0.69x |
| `pcre2/branch-reset-backref` | 0.715 us | 1.057 us | Managed faster | 0.68x |
| `pcre2/duplicate-names` | 0.638 us | 0.965 us | Managed faster | 0.66x |
| `literal/empty-unicode` | 1.494 us | 2.307 us | Managed faster | 0.65x |
| `pcre2/kreset-bar-or-baz` | 0.618 us | 0.974 us | Managed faster | 0.63x |
| `pcre2/branch-reset-followup` | 0.626 us | 1.043 us | Managed faster | 0.60x |
| `pcre2/branch-reset-basic` | 0.689 us | 1.196 us | Managed faster | 0.58x |
| `pcre2/branch-reset-nested` | 0.661 us | 1.171 us | Managed faster | 0.56x |
| `pcre2/kreset-global` | 0.473 us | 0.870 us | Managed faster | 0.54x |
| `pcre2/same-start-global` | 0.540 us | 1.265 us | Managed faster | 0.43x |
| `pcre2/kreset-captured-repeat` | 4.600 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.487 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 17.726 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 1.163 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.266 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 1.147 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 1.153 us | — | Excluded | — |
| `pcre2/duplicate-names` | 1.143 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.861 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 1.157 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 1.149 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 19.203 us | — | Excluded | — |
| `pcre2/kreset-global` | 1.082 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 14.161 us | — | Excluded | — |
| `pcre2/same-start-global` | 4.185 us | — | Excluded | — |

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
