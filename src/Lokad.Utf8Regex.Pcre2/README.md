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
`CreateMatchCursor`, `Count`, `EnumerateMatches` and substitution.
`MatchDetailed` is removed: use caller-owned capture slots, or a persistent cursor
for owned snapshots. Dispose temporary cursors after the
required results; returned ranges remain valid with unchanged subject memory.
Cursor creation and owned publication have additional operating costs. First-only
callers can retain reusable capture buffers and copy UTF-8 name numbers once.

Subjects follow the caller-valid input contract above. Normal character matching and
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

Operation defaults now use one optional token-bearing signature per input/template
representation. `IsMatch` and `Count` also accept optional `workingMemoryAllowance`:
null keeps numeric controls without a shared account; a supplied account retains
terminal refusal and synchronous refunds. Funded owners inherit program funding
when that argument is null. For example, `regex.Match(subject, cancellationToken: token)`
starts at byte zero. Recompile old callers. When using TryReplace's retired
options-only fifth argument, write `substitutionOptions: options`. Cancellation,
finite limits and literal fast paths remain enforced.

## Example

To copy captures into reusable storage, use the primary Match signature:

```csharp
using Lokad.Utf8Regex.Pcre2;

Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[3];
var regex = new Utf8Pcre2Regex("(?<word>[a-z]+)-(?<number>[0-9]+)");
var match = regex.Match("abc-123"u8, captures: captures);
// Slot zero: [0,7); word: [0,3); number: [4,7), all byte coordinates.
```

Short destinations keep the same match; excess slots stay untouched. Caller-owned
slots require no producer-owned result capacity. Match also returns the last
relevant `Mark` on hits and misses, reusing the immutable compiled label without
another search or capture array. The label survives subsequent calls and program
close. Shared working admission and funded Match use the same numeric limits,
token and deadline. Capture cursors and full probes expose numbered slots. Resolve
names on the compiled regex before using those slots. Span enumeration publishes
whole-match ranges. Borrowed subjects must remain unchanged.
Use `CopyNumbersForUtf8Name` for regex-level number copies; the redundant string
variant is removed. Host key encoding is outside the call. Complete key validation
precedes destination writes, and the optional token remains synchronous.

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

byte[] redacted = regex.Replace(input, "<$host>"u8);
```

UTF-8 templates use PCRE2 substitution syntax. Replace returns owned bytes
through `Replace` or write caller storage through `TryReplace`. Decode valid output
in the host when text is needed; the extra string is outside regex resource
controls. Opt-in `\C` can produce raw malformed UTF-8, so check that output first.
Callback replacement, evaluator delegates and the public callback writer are
removed. Custom computation uses `CreateMatchCursor` and host-owned buffers.
Detailed numeric-slot/name-table results and explicit compile/execution settings
remain available. Cursor advances preserve regex controls; host computation and
final output admission/cancellation checks belong to the host.

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

Construct or Compile text/strict UTF-8 patterns with options, PCRE2 compile
settings and immutable `Utf8RegexPolicy`. Shared positive work/depth/working/result
caps map to the existing PCRE2 limits. Null means unlimited; zero is invalid and
work above `uint.MaxValue` rejects. `ResourcePolicy` and `DefaultExecutionLimits`
are read-only projections. No separate Create factory or funded wrapper remains.
**Defaults remain unlimited.** Numeric work/depth units depend on the route and
are not native-PCRE2 or Core/PythonRe interchangeable units.

Covered validation, discovery, projection, capture/output shaping and controlled
replacement preparation share the original invocation's work, token and absolute
deadline. Cancellation throws with the original token. Deadline/resource denial
uses `Pcre2MatchException` with `Timeout`, `MatchLimit`, `DepthLimit`, `HeapLimit`
or `ResultLimit`; no silently truncated success is returned. Terminal cursor
advances also check interruption. The original deadline includes host waits.
Host computation between advances is outside regex interruption controls.

Concurrent cold replacements can share template preparation. A waiting call
observes its own cancellation, deadline and resource allowance; another
preparer's interruption can cause one independent preparation under the waiter's
controls, rather than transferring that preparer's failure to the waiter.

Major covered arrays, output controls and growth overlap require admission before
allocation. Result/staging capacity is cumulatively charged without refunds;
scalar value/boolean/count and inline partial results need no owned payload.
This is a modeled capacity policy, not total managed heap, process RSS or arbitrary
retained-storage containment. Subject/destination memory, incidental owners,
compiled/cache retention, host decoding and custom-output allocations have
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

Policy construction controls provide separate work/depth/instruction/byte limits
and timeout on constructors and Compile. Compile additionally accepts a token,
which is not retained for matching. Construction admits backtracking
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

ProgramMemoryAllowance prepays compiled storage and bounded replacement-cache
capacity; dispose funded expressions. New calls and program-dependent metadata
reject after close. Admitted calls and existing persistent cursors finish under
their original controls; refunds wait for the final use, without GC timing.
IsMatch/Match/Count/cursor/TryReplace inherit program funding for null working
admission; explicit working accounts override only temporary storage. Span
enumerators keep numeric working controls and borrow an open producer for each
advance; close rejects further advances on every copy, while Current remains valid.
Use persistent cursors for shared working admission or traversal surviving close.
Unfunded Dispose is a no-op. Pattern/options/settings/resource projections remain
readable; other metadata, probes and owned output keep numeric limits and program
protection without an additional overload family.

Keep borrowed subjects unchanged and their owners alive. Copied value cursors
retain independent positions but share allowance/deadline. Owned detailed results
remain valid after cursor advance/failure. `MatchMany` and its whole-range carrier
are removed. Stream ranges with `EnumerateMatches`, or retain a memory cursor for
captures across host waits. Callers decide when to request another result; there
is no implicit bulk lookahead or `isMore` result. Caller-buffered prefixes remain
valid after later failure. Input and caller capture storage must be disjoint;
overlap is unsupported and is not checked, since writes can corrupt the subject
before later discovery.
An already cancelled explicit token still wins. Interruption during TryReplace copying can
also leave modified destination bytes. DestinationTooSmall leaves destination
storage unchanged. Host-retained storage requires separate admission.

Enumeration publishes byte endpoints and value status directly,
preserving snapshots and independent copied positions. Capture publication has
no coordinate table, sort or prefix scan. Numerical work/storage needs can drop;
pattern/template checks, allocation admission and cumulative interruption/limits stay enforced.

`TryReplace` supports input and template buffers overlapping the destination.
Matched overlapping input stages output before copying to caller storage, so
later gaps and captures retain their original bytes. That temporary capacity
requires working and cumulative result admission under the same invocation.
Disjoint simple-template output and no-match copies retain their direct paths.

Read [release notes](../../CHANGELOG.md) and [migration guide](../../docs/MIGRATION.md).
The [PCRE2 summary](../../bench/Snapshots/Pcre2.json) records the measured source,
runtime and comparator qualification for each byte-operation row.

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `D2115FD8060046C532DB834572D5CD47F4FCB5A42EF7D3D780AFF5402259E5DC`.

Comparator Status: `49` managed faster, `0` equivalent, `35` native faster, `1` inconclusive, `0` unqualified, `23` excluded.

Measured source `e0097a3fa7b8`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/uri-match` | 0.685 us | 0.157 us | Native faster | 4.37x |
| `common/email-match` | 0.806 us | 0.254 us | Native faster | 3.17x |
| `common/date-miss` | 1.607 us | 0.506 us | Native faster | 3.17x |
| `common/date-match` | 0.365 us | 0.121 us | Native faster | 3.02x |
| `backtracking/assertion-capture` | 0.417 us | 0.139 us | Native faster | 3.00x |
| `backtracking/capture-backreference` | 0.257 us | 0.092 us | Native faster | 2.82x |
| `simple/httpclient-caseless` | 0.245 us | 0.089 us | Native faster | 2.76x |
| `simple/ab-plus` | 0.212 us | 0.079 us | Native faster | 2.66x |
| `industry/boostdocs-postcode-match` | 0.265 us | 0.101 us | Native faster | 2.63x |
| `industry/boostdocs-credit-card-match` | 0.331 us | 0.131 us | Native faster | 2.52x |
| `industry/boostdocs-date-match` | 0.231 us | 0.097 us | Native faster | 2.39x |
| `common/ip-miss` | 2.292 us | 0.984 us | Native faster | 2.33x |
| `industry/boostdocs-float-match` | 0.242 us | 0.104 us | Native faster | 2.32x |
| `industry/boostdocs-ftp-line-match` | 0.466 us | 0.223 us | Native faster | 2.09x |
| `common/one-node-backtracking` | 0.070 us | 0.038 us | Native faster | 1.81x |
| `common/ip-match` | 0.371 us | 0.257 us | Native faster | 1.44x |
| `common/uri-miss` | 0.733 us | 0.559 us | Native faster | 1.31x |
| `simple/loglevel-multiline` | 0.191 us | 0.148 us | Native faster | 1.30x |
| `literal/missing` | 0.066 us | 0.061 us | Native faster | 1.07x |
| `literal/late` | 0.070 us | 0.098 us | Managed faster | 0.71x |
| `simple/foo-dense` | 0.032 us | 0.068 us | Managed faster | 0.47x |
| `literal/early` | 0.034 us | 0.078 us | Managed faster | 0.44x |
| `literal/absolute-anchored` | 0.025 us | 0.074 us | Managed faster | 0.34x |
| `common/backtracking` | 0.228 us | 1.118 us | Managed faster | 0.20x |
| `common/email-miss` | 0.045 us | 0.304 us | Managed faster | 0.15x |
| `simple/foo-optional-bar` | 0.011 us | 0.087 us | Managed faster | 0.12x |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `industry/leipzig-twain-count` | 4,593.175 us | 2,145.005 us | Native faster | 2.14x |
| `simple/httpclient-caseless` | 1.813 us | 1.038 us | Native faster | 1.75x |
| `backtracking/alternation-repeat` | 227.877 us | 146.256 us | Native faster | 1.56x |
| `simple/loglevel-multiline` | 0.676 us | 0.766 us | Managed faster | 0.88x |
| `simple/ab-plus` | 0.913 us | 1.037 us | Managed faster | 0.88x |
| `industry/rust-sherlock-nonnewline-count` | 1,746.413 us | 2,410.317 us | Managed faster | 0.73x |
| `industry/rust-sherlock-ing-count` | 7,585.720 us | 12,383.200 us | Managed faster | 0.61x |
| `simple/foo-dense` | 0.360 us | 0.771 us | Managed faster | 0.47x |
| `simple/foo-optional-bar` | 0.258 us | 1.113 us | Managed faster | 0.23x |
| `industry/rust-sherlock-holmes-window-count` | 142.652 us | 626.378 us | Managed faster | 0.23x |
| `common/matches-word` | 3.137 us | 17.723 us | Managed faster | 0.18x |
| `industry/mariomka-ip-count` | 2,185.588 us | 14,180.567 us | Managed faster | 0.15x |
| `common/matches-words` | 8.851 us | 72.706 us | Managed faster | 0.12x |
| `industry/leipzig-name-family-count` | 987.867 us | 17,684.450 us | Managed faster | 0.06x |
| `common/matches-boundary` | 8.483 us | 193.072 us | Managed faster | 0.04x |
| `common/matches-set` | 8.613 us | 201.512 us | Managed faster | 0.04x |
| `industry/leipzig-river-window-count` | 1,775.368 us | 43,295.000 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 9.923 us | 260.439 us | Managed faster | 0.04x |
| `industry/mariomka-uri-count` | 3,316.545 us | 118,663.100 us | Managed faster | 0.03x |
| `industry/rust-sherlock-letter-count` | 1,774.618 us | 92,441.600 us | Managed faster | 0.02x |
| `industry/rust-sherlock-word-holmes-count` | 305.620 us | 18,672.150 us | Managed faster | 0.02x |
| `industry/mariomka-email-count` | 472.298 us | 129,990.300 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,035.442 us | 435,802.800 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.732 us | 0.776 us | Native faster | 2.23x |
| `simple/httpclient-caseless` | 2.149 us | 1.056 us | Native faster | 2.03x |
| `simple/ab-plus` | 1.279 us | 1.043 us | Native faster | 1.23x |
| `simple/foo-dense` | 0.619 us | 0.780 us | Managed faster | 0.79x |
| `simple/foo-optional-bar` | 0.759 us | 1.109 us | Managed faster | 0.68x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 45.876 us | — | Excluded | — |
| `simple/ab-plus` | 2.772 us | — | Excluded | — |
| `simple/foo-dense` | 0.833 us | — | Excluded | — |
| `simple/foo-optional-bar` | 1.000 us | — | Excluded | — |
| `simple/httpclient-caseless` | 2.583 us | — | Excluded | — |
| `simple/loglevel-multiline` | 2.474 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 2.088 us | 0.468 us | Native faster | 4.46x |
| `pcre2/recursive-optional` | 2.280 us | 0.608 us | Native faster | 3.75x |
| `pcre2/subroutine-prefix-digits` | 0.350 us | 0.096 us | Native faster | 3.67x |
| `pcre2/grapheme-cluster` | 0.211 us | 0.069 us | Native faster | 3.06x |
| `pcre2/conditional-accept-negative-lookahead` | 0.256 us | 0.093 us | Native faster | 2.74x |
| `pcre2/backslash-c-literal` | 0.207 us | 0.077 us | Native faster | 2.70x |
| `pcre2/conditional-negative-lookahead` | 0.246 us | 0.095 us | Native faster | 2.58x |
| `pcre2/conditional-lookahead` | 0.252 us | 0.103 us | Native faster | 2.45x |
| `pcre2/branch-reset-backref` | 0.010 us | 0.098 us | Managed faster | 0.10x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 8.261 us | 8.056 us | Native faster | 1.03x |
| `pcre2/duplicate-names` | 0.261 us | 0.329 us | Managed faster | 0.80x |
| `pcre2/kreset-atomic-alt` | 0.224 us | 0.287 us | Managed faster | 0.78x |
| `pcre2/same-start-global` | 0.397 us | 0.627 us | Managed faster | 0.64x |
| `pcre2/branch-reset-basic` | 0.274 us | 0.429 us | Managed faster | 0.64x |
| `pcre2/kreset-bar-or-baz` | 0.231 us | 0.363 us | Managed faster | 0.64x |
| `pcre2/kreset-global` | 0.166 us | 0.262 us | Managed faster | 0.63x |
| `pcre2/branch-reset-followup` | 0.258 us | 0.410 us | Managed faster | 0.63x |
| `literal/empty-unicode` | 5.394 us | 9.451 us | Managed faster | 0.57x |
| `pcre2/branch-reset-nested` | 0.264 us | 0.555 us | Managed faster | 0.48x |
| `pcre2/branch-reset-backref` | 0.265 us | 0.981 us | Managed faster | 0.27x |
| `pcre2/kreset-captured-repeat` | 3.780 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.715 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 9.491 us | 8.117 us | Native faster | 1.17x |
| `pcre2/kreset-atomic-alt` | 0.730 us | 0.827 us | Managed faster | 0.88x |
| `pcre2/duplicate-names` | 0.770 us | 0.871 us | Managed faster | 0.88x |
| `pcre2/kreset-bar-or-baz` | 0.750 us | 0.904 us | Managed faster | 0.83x |
| `pcre2/kreset-global` | 0.662 us | 0.808 us | Managed faster | 0.82x |
| `pcre2/branch-reset-followup` | 0.750 us | 0.944 us | Managed faster | 0.79x |
| `pcre2/branch-reset-backref` | 0.773 us | 0.997 us | Managed faster | 0.78x |
| `pcre2/branch-reset-basic` | 0.792 us | 1.111 us | Managed faster | 0.71x |
| `pcre2/branch-reset-nested` | 0.748 us | 1.090 us | Managed faster | 0.69x |
| `pcre2/same-start-global` | 0.703 us | 1.174 us | Managed faster | 0.60x |
| `literal/empty-unicode` | 5.814 us | 26.840 us | Managed faster | 0.22x |
| `pcre2/kreset-captured-repeat` | 4.716 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 4.544 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 14.885 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.919 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 1.012 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.903 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.907 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.901 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 12.135 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.931 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.939 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 16.972 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.856 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 11.963 us | — | Excluded | — |
| `pcre2/same-start-global` | 3.720 us | — | Excluded | — |

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
