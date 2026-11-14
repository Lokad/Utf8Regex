# Lokad.Utf8Regex.Pcre2

**0.5.0 development**, .NET 10 and .NET 11. A strictly managed, UTF-8-native
selected PCRE2 10.47 standard profile, requiring the exact Core package version
matching the adapter. It can coexist with PythonRe at the same version. There is
no P/Invoke, native PCRE2 binary, RID-specific payload or implementation dependency
other than Core and the BCL.

## Subject validation policy

`Validate` is default and follows native UTF-mode checking: validate from the
requested byte start, extended backward by the pattern's maximum lookbehind in
scalars, through the end of the subject. A malformed suffix is rejected even on
an early hit; bytes before this window are ignored. Checking runs at most once
per logical operation or cursor lifetime under the original work/token/deadline.

Construct with `inputValidation: Utf8InputValidation.AssumeValid` only when the
host guarantees valid UTF-8 and unchanged borrowed storage. This is the subject
`PCRE2_NO_UTF_CHECK` equivalent; read-only `InputValidation` reports the choice.
Pattern/replacement validation, checked accesses, scalar semantics and matching/
output admission remain independent. Byte-only operations do not prepare whole
UTF-16 coordinates. Requested byte results remain usable when an ignored invalid
prefix prevents exact UTF-16 projection; consult `HasUtf16Projection`.
See the [PCRE2 contract](../../docs/SPEC-PCRE2.md#subject-input-validation).

The canonical benchmark compares default UTF-checked managed and native PCRE2
operations with matching options and requested outputs. Trusted-input coverage
uses focused checks rather than a second complete benchmark catalog.

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

Replacement strings use PCRE2 substitution syntax, not .NET replacement
syntax. The API also exposes UTF-8 replacement patterns, callback replacement,
`TryReplace`, `ReplaceToString`, detailed numeric-slot/name-table results, and
explicit compile and execution settings.

Replacement evaluator delegates must be non-null, even on misses; otherwise
`ArgumentNullException` names `evaluator`. An already-cancelled explicit token
still takes precedence. Return-value rules for valid callbacks are unchanged.

## Delivery evidence

Core, PythonRe and PCRE2 0.4.0 are published. The current development boundary
is the [four-package dev92 candidate](../../bench/Experiments/DeliveryDev92-20261114/README.md).
PCRE2 retains its immutable UTF validation/trusted-input choice. Frozen packages
and measurements retain their own identities.

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

Snapshot SHA-256: `113083C36F0983370B0C89FA0BDEBDDC5BD96A55461D0872C111488FE6BE79D4`.

Comparator Status: `79` managed faster, `0` equivalent, `11` native faster, `11` inconclusive, `0` unqualified, `25` excluded.

Measured source `528fabba5f5d`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/one-node-backtracking` | 0.184 us | 0.075 us | Inconclusive | 2.46x |
| `simple/httpclient-caseless` | 0.134 us | 0.121 us | Native faster | 1.10x |
| `simple/foo-dense` | 0.081 us | 0.083 us | Inconclusive | 0.98x |
| `industry/boostdocs-float-match` | 0.057 us | 0.117 us | Managed faster | 0.49x |
| `common/date-miss` | 0.223 us | 0.569 us | Managed faster | 0.39x |
| `simple/loglevel-multiline` | 0.072 us | 0.206 us | Managed faster | 0.35x |
| `common/email-match` | 0.096 us | 0.290 us | Managed faster | 0.33x |
| `common/email-miss` | 0.105 us | 0.346 us | Managed faster | 0.30x |
| `industry/boostdocs-ftp-line-match` | 0.080 us | 0.275 us | Managed faster | 0.29x |
| `common/ip-match` | 0.074 us | 0.288 us | Managed faster | 0.26x |
| `common/ip-miss` | 0.074 us | 0.328 us | Managed faster | 0.23x |
| `industry/boostdocs-credit-card-match` | 0.034 us | 0.158 us | Managed faster | 0.22x |
| `backtracking/assertion-capture` | 0.549 us | 2.653 us | Managed faster | 0.21x |
| `industry/boostdocs-postcode-match` | 0.023 us | 0.112 us | Managed faster | 0.20x |
| `industry/boostdocs-date-match` | 0.019 us | 0.108 us | Managed faster | 0.18x |
| `common/date-match` | 0.027 us | 0.158 us | Managed faster | 0.17x |
| `common/uri-match` | 0.035 us | 0.208 us | Managed faster | 0.17x |
| `simple/foo-optional-bar` | 0.014 us | 0.108 us | Managed faster | 0.13x |
| `common/uri-miss` | 0.062 us | 0.607 us | Managed faster | 0.10x |
| `simple/ab-plus` | 0.010 us | 0.097 us | Managed faster | 0.10x |
| `literal/missing` | 0.134 us | 1.824 us | Managed faster | 0.07x |
| `literal/late` | 0.136 us | 2.309 us | Inconclusive | 0.06x |
| `literal/absolute-anchored` | 0.101 us | 2.082 us | Inconclusive | 0.05x |
| `backtracking/capture-backreference` | 0.505 us | 10.821 us | Inconclusive | 0.05x |
| `literal/early` | 0.099 us | 2.079 us | Inconclusive | 0.05x |
| `common/backtracking` | 0.011 us | 1.184 us | Managed faster | 0.01x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `industry/rust-sherlock-ing-count` | 11,475.333 us | 13,273.200 us | Managed faster | 0.86x |
| `simple/httpclient-caseless` | 0.943 us | 1.130 us | Managed faster | 0.83x |
| `simple/foo-dense` | 0.524 us | 0.813 us | Managed faster | 0.65x |
| `backtracking/alternation-repeat` | 82.685 us | 158.503 us | Managed faster | 0.53x |
| `industry/leipzig-twain-count` | 5,398.067 us | 10,953.225 us | Inconclusive | 0.50x |
| `simple/foo-optional-bar` | 0.322 us | 1.166 us | Managed faster | 0.28x |
| `simple/loglevel-multiline` | 0.176 us | 0.838 us | Managed faster | 0.21x |
| `industry/rust-sherlock-holmes-window-count` | 158.615 us | 953.523 us | Managed faster | 0.17x |
| `industry/mariomka-uri-count` | 20,078.950 us | 131,814.600 us | Managed faster | 0.15x |
| `simple/ab-plus` | 0.168 us | 1.109 us | Managed faster | 0.15x |
| `common/matches-word` | 3.418 us | 23.449 us | Managed faster | 0.15x |
| `common/matches-words` | 9.434 us | 81.382 us | Managed faster | 0.12x |
| `common/matches-set` | 24.775 us | 213.771 us | Managed faster | 0.12x |
| `industry/mariomka-email-count` | 16,057.650 us | 144,878.000 us | Managed faster | 0.11x |
| `industry/mariomka-ip-count` | 2,266.321 us | 21,742.650 us | Inconclusive | 0.10x |
| `character/unicode-class-dense` | 29.641 us | 285.884 us | Managed faster | 0.10x |
| `industry/rust-sherlock-word-holmes-count` | 1,561.208 us | 20,218.700 us | Managed faster | 0.08x |
| `industry/rust-sherlock-nonnewline-count` | 189.952 us | 2,705.264 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,681.326 us | 28,259.800 us | Managed faster | 0.06x |
| `industry/leipzig-river-window-count` | 2,882.486 us | 54,080.400 us | Inconclusive | 0.05x |
| `common/matches-boundary` | 9.887 us | 208.457 us | Managed faster | 0.05x |
| `industry/rust-sherlock-letter-count` | 2,747.671 us | 97,953.800 us | Managed faster | 0.03x |
| `industry/leipzig-symbol-count` | 1,829.035 us | 480,450.200 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 0.915 us | 0.842 us | Native faster | 1.09x |
| `simple/httpclient-caseless` | 1.219 us | 1.127 us | Native faster | 1.08x |
| `simple/foo-dense` | 0.858 us | 0.818 us | Native faster | 1.06x |
| `simple/foo-optional-bar` | 1.179 us | 1.229 us | Managed faster | 0.96x |
| `simple/ab-plus` | 0.992 us | 1.136 us | Managed faster | 0.87x |

### Compatible MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/httpclient-caseless` | 0.936 us | 1.124 us | Managed faster | 0.83x |
| `simple/foo-dense` | 0.518 us | 0.822 us | Managed faster | 0.63x |
| `simple/foo-optional-bar` | 0.759 us | 1.225 us | Managed faster | 0.62x |
| `simple/loglevel-multiline` | 0.495 us | 0.841 us | Managed faster | 0.59x |
| `simple/ab-plus` | 0.656 us | 1.126 us | Managed faster | 0.58x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 54.977 us | — | Excluded | — |
| `simple/ab-plus` | 3.379 us | — | Excluded | — |
| `simple/foo-dense` | 1.064 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.231 us | — | Excluded | — |
| `simple/httpclient-caseless` | 1.564 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.735 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/backslash-c-literal` | 0.103 us | 0.099 us | Native faster | 1.04x |
| `pcre2/grapheme-cluster` | 0.175 us | 0.339 us | Managed faster | 0.51x |
| `pcre2/subroutine-prefix-digits` | 0.014 us | 0.107 us | Managed faster | 0.13x |
| `pcre2/conditional-negative-lookahead` | 0.013 us | 0.104 us | Managed faster | 0.13x |
| `pcre2/conditional-accept-negative-lookahead` | 0.012 us | 0.105 us | Managed faster | 0.12x |
| `pcre2/branch-reset-backref` | 0.014 us | 0.120 us | Managed faster | 0.11x |
| `pcre2/conditional-lookahead` | 0.013 us | 0.118 us | Managed faster | 0.11x |
| `pcre2/recursive-palindrome` | 0.012 us | 0.494 us | Managed faster | 0.02x |
| `pcre2/recursive-optional` | 0.012 us | 0.630 us | Inconclusive | 0.02x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.306 us | 0.321 us | Managed faster | 0.96x |
| `pcre2/same-start-global` | 0.617 us | 0.686 us | Managed faster | 0.91x |
| `pcre2/duplicate-names` | 0.337 us | 0.381 us | Managed faster | 0.88x |
| `pcre2/kreset-bar-or-baz` | 0.320 us | 0.396 us | Managed faster | 0.80x |
| `pcre2/branch-reset-basic` | 0.365 us | 0.456 us | Managed faster | 0.80x |
| `literal/empty-unicode` | 8.275 us | 10.439 us | Managed faster | 0.79x |
| `pcre2/kreset-global` | 0.220 us | 0.292 us | Managed faster | 0.75x |
| `pcre2/branch-reset-followup` | 0.340 us | 0.468 us | Managed faster | 0.72x |
| `pcre2/branch-reset-nested` | 0.351 us | 0.590 us | Managed faster | 0.59x |
| `pcre2/grapheme-cluster` | 4.632 us | 8.690 us | Managed faster | 0.53x |
| `pcre2/branch-reset-backref` | 0.347 us | 1.048 us | Managed faster | 0.33x |
| `pcre2/kreset-captured-repeat` | 3.619 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.582 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 1.011 us | 0.881 us | Native faster | 1.15x |
| `pcre2/duplicate-names` | 1.087 us | 0.956 us | Native faster | 1.14x |
| `pcre2/kreset-global` | 0.942 us | 0.870 us | Native faster | 1.08x |
| `pcre2/kreset-bar-or-baz` | 1.039 us | 0.962 us | Native faster | 1.08x |
| `pcre2/branch-reset-backref` | 1.077 us | 1.043 us | Native faster | 1.03x |
| `pcre2/branch-reset-followup` | 1.055 us | 1.025 us | Native faster | 1.03x |
| `pcre2/branch-reset-basic` | 1.128 us | 1.187 us | Managed faster | 0.95x |
| `pcre2/branch-reset-nested` | 1.060 us | 1.164 us | Managed faster | 0.91x |
| `pcre2/same-start-global` | 1.045 us | 1.243 us | Managed faster | 0.84x |
| `pcre2/grapheme-cluster` | 5.356 us | 8.718 us | Managed faster | 0.61x |
| `literal/empty-unicode` | 10.212 us | 28.876 us | Managed faster | 0.35x |
| `pcre2/kreset-captured-repeat` | 4.624 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.565 us | — | Excluded | — |

### PCRE2-only MatchMany

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.691 us | 0.881 us | Managed faster | 0.79x |
| `pcre2/duplicate-names` | 0.699 us | 0.946 us | Managed faster | 0.74x |
| `pcre2/kreset-bar-or-baz` | 0.650 us | 0.960 us | Managed faster | 0.68x |
| `pcre2/grapheme-cluster` | 1.455 us | 2.194 us | Managed faster | 0.67x |
| `pcre2/branch-reset-followup` | 0.663 us | 1.021 us | Managed faster | 0.65x |
| `pcre2/branch-reset-backref` | 0.678 us | 1.046 us | Managed faster | 0.65x |
| `pcre2/branch-reset-basic` | 0.758 us | 1.182 us | Managed faster | 0.64x |
| `pcre2/kreset-global` | 0.541 us | 0.862 us | Managed faster | 0.63x |
| `pcre2/branch-reset-nested` | 0.670 us | 1.154 us | Managed faster | 0.58x |
| `pcre2/same-start-global` | 0.563 us | 1.246 us | Managed faster | 0.45x |
| `literal/empty-unicode` | 1.012 us | 2.279 us | Managed faster | 0.44x |
| `pcre2/kreset-captured-repeat` | 4.392 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.346 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 16.234 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 1.167 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.267 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 1.144 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 1.153 us | — | Excluded | — |
| `pcre2/duplicate-names` | 1.146 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.308 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 1.154 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 1.135 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 19.171 us | — | Excluded | — |
| `pcre2/kreset-global` | 1.068 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 13.667 us | — | Excluded | — |
| `pcre2/same-start-global` | 4.243 us | — | Excluded | — |

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
