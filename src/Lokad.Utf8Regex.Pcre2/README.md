# Lokad.Utf8Regex.Pcre2

**0.5.0 development**, .NET 10 and .NET 11. A strictly managed, UTF-8-native
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

UTF-8 templates use PCRE2 substitution syntax. Replace returns owned bytes
through `Replace` or write caller storage through `TryReplace`. Decode valid output
in the host when text is needed; the extra string is outside regex resource
controls. Opt-in `\C` can produce raw malformed UTF-8, so check that output first.
Callback replacement, evaluator delegates and the public callback writer are
removed. Custom computation uses `CreateMatchCursor` and host-owned buffers.
Detailed numeric-slot/name-table results and explicit compile/execution settings
remain available. Cursor advances preserve regex controls; host computation and
final output admission/cancellation checks belong to the host.

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

See the [release notes](../../CHANGELOG.md) for upgrades and version history.
The [PCRE2 summary](../../bench/Snapshots/Pcre2.json) records the measured source,
runtime and comparator qualification for each byte-operation row.

The `simple/ab-plus` Count ratio reflects the archived paired run. In fixed-CPU
replays with matching results and native settings, the native Count median
ranged from 0.34 to 1.07 µs across processes. Running a Count workload first
reproduced the faster timing with the same benchmark binary.

<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->
## Benchmarks

Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.

Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.

Snapshot SHA-256: `11899A9D956267991800093D5011BFD7BC6189D4FAF340213946BF2CE4436CF0`.

Comparator Status: `70` managed faster, `2` equivalent, `9` native faster, `3` inconclusive, `1` unqualified, `23` excluded.

Measured source `e471837e2beb`: .NET 11.0.0. Selective updates retain each row's identity.

### Compatible IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/ip-miss` | 1.301 us | 1.119 us | Native faster | 1.17x |
| `common/ip-match` | 0.423 us | 0.404 us | Native faster | 1.04x |
| `common/uri-match` | 0.299 us | 0.298 us | Equivalent | 1.00x |
| `common/date-miss` | 0.654 us | 0.657 us | Equivalent | 1.00x |
| `industry/boostdocs-postcode-match` | 0.233 us | 0.238 us | Inconclusive | 0.98x |
| `common/email-match` | 0.341 us | 0.394 us | Managed faster | 0.87x |
| `industry/boostdocs-credit-card-match` | 0.223 us | 0.271 us | Managed faster | 0.82x |
| `simple/httpclient-caseless` | 0.188 us | 0.231 us | Managed faster | 0.81x |
| `industry/boostdocs-float-match` | 0.171 us | 0.236 us | Managed faster | 0.73x |
| `common/date-match` | 0.189 us | 0.266 us | Managed faster | 0.71x |
| `common/uri-miss` | 0.491 us | 0.696 us | Managed faster | 0.70x |
| `simple/ab-plus` | 0.151 us | 0.224 us | Managed faster | 0.68x |
| `industry/boostdocs-date-match` | 0.161 us | 0.240 us | Managed faster | 0.67x |
| `industry/boostdocs-ftp-line-match` | 0.225 us | 0.359 us | Managed faster | 0.62x |
| `simple/loglevel-multiline` | 0.182 us | 0.298 us | Managed faster | 0.61x |
| `literal/missing` | 0.073 us | 0.212 us | Managed faster | 0.34x |
| `literal/late` | 0.077 us | 0.250 us | Managed faster | 0.31x |
| `backtracking/assertion-capture` | 0.079 us | 0.288 us | Managed faster | 0.27x |
| `common/one-node-backtracking` | 0.043 us | 0.179 us | Managed faster | 0.24x |
| `simple/foo-dense` | 0.041 us | 0.213 us | Managed faster | 0.19x |
| `literal/early` | 0.039 us | 0.222 us | Managed faster | 0.18x |
| `literal/absolute-anchored` | 0.034 us | 0.216 us | Managed faster | 0.16x |
| `common/backtracking` | 0.173 us | 1.282 us | Managed faster | 0.14x |
| `common/email-miss` | 0.037 us | 0.454 us | Managed faster | 0.08x |
| `simple/foo-optional-bar` | 0.014 us | 0.234 us | Managed faster | 0.06x |
| `backtracking/capture-backreference` | 0.173 us | 0.225 us | Unqualified | — |

### Compatible Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/ab-plus` | 0.660 us | 0.337 us | Native faster | 1.96x |
| `simple/loglevel-multiline` | 0.454 us | 0.352 us | Native faster | 1.29x |
| `backtracking/alternation-repeat` | 133.326 us | 146.906 us | Managed faster | 0.91x |
| `industry/rust-sherlock-ing-count` | 9,087.725 us | 12,435.233 us | Managed faster | 0.73x |
| `industry/rust-sherlock-nonnewline-count` | 1,587.424 us | 2,303.224 us | Managed faster | 0.69x |
| `simple/foo-optional-bar` | 0.282 us | 0.413 us | Managed faster | 0.68x |
| `simple/foo-dense` | 0.067 us | 0.225 us | Managed faster | 0.30x |
| `industry/leipzig-twain-count` | 636.105 us | 2,161.911 us | Managed faster | 0.29x |
| `industry/mariomka-ip-count` | 3,214.383 us | 14,270.033 us | Managed faster | 0.23x |
| `industry/rust-sherlock-holmes-window-count` | 132.455 us | 620.742 us | Managed faster | 0.21x |
| `simple/httpclient-caseless` | 0.080 us | 0.375 us | Managed faster | 0.21x |
| `common/matches-words` | 8.848 us | 72.725 us | Managed faster | 0.12x |
| `common/matches-word` | 1.158 us | 17.772 us | Managed faster | 0.07x |
| `industry/leipzig-name-family-count` | 1,050.633 us | 17,556.500 us | Managed faster | 0.06x |
| `industry/rust-sherlock-letter-count` | 1,726.835 us | 34,323.600 us | Managed faster | 0.05x |
| `common/matches-set` | 9.121 us | 189.002 us | Managed faster | 0.05x |
| `common/matches-boundary` | 9.105 us | 192.008 us | Managed faster | 0.05x |
| `industry/leipzig-river-window-count` | 1,748.959 us | 42,624.300 us | Managed faster | 0.04x |
| `character/unicode-class-dense` | 9.142 us | 260.443 us | Managed faster | 0.04x |
| `industry/mariomka-uri-count` | 1,756.279 us | 119,160.700 us | Inconclusive | 0.01x |
| `industry/rust-sherlock-word-holmes-count` | 267.854 us | 18,558.700 us | Managed faster | 0.01x |
| `industry/mariomka-email-count` | 439.098 us | 130,157.200 us | Managed faster | 0.00x |
| `industry/leipzig-symbol-count` | 1,049.055 us | 434,635.500 us | Inconclusive | 0.00x |

### Compatible EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `simple/loglevel-multiline` | 1.001 us | 0.773 us | Native faster | 1.30x |
| `simple/ab-plus` | 1.251 us | 1.029 us | Native faster | 1.22x |
| `simple/foo-dense` | 0.625 us | 0.777 us | Managed faster | 0.81x |
| `simple/foo-optional-bar` | 0.750 us | 1.114 us | Managed faster | 0.67x |
| `simple/httpclient-caseless` | 0.671 us | 1.049 us | Managed faster | 0.64x |

### Compatible Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `common/replace-words` | 44.557 us | — | Excluded | — |
| `simple/ab-plus` | 1.729 us | — | Excluded | — |
| `simple/foo-dense` | 0.823 us | — | Excluded | — |
| `simple/foo-optional-bar` | 0.929 us | — | Excluded | — |
| `simple/httpclient-caseless` | 0.902 us | — | Excluded | — |
| `simple/loglevel-multiline` | 1.998 us | — | Excluded | — |

### PCRE2-only IsMatch

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/recursive-palindrome` | 1.335 us | 0.615 us | Native faster | 2.17x |
| `pcre2/recursive-optional` | 1.454 us | 0.739 us | Native faster | 1.97x |
| `pcre2/subroutine-prefix-digits` | 0.221 us | 0.233 us | Managed faster | 0.94x |
| `pcre2/conditional-accept-negative-lookahead` | 0.180 us | 0.231 us | Managed faster | 0.78x |
| `pcre2/conditional-negative-lookahead` | 0.180 us | 0.232 us | Managed faster | 0.77x |
| `pcre2/conditional-lookahead` | 0.185 us | 0.248 us | Managed faster | 0.74x |
| `pcre2/grapheme-cluster` | 0.120 us | 0.210 us | Managed faster | 0.57x |
| `pcre2/backslash-c-literal` | 0.106 us | 0.217 us | Managed faster | 0.49x |
| `pcre2/branch-reset-backref` | 0.012 us | 0.234 us | Managed faster | 0.05x |

### PCRE2-only Count

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/grapheme-cluster` | 4.352 us | 2.735 us | Native faster | 1.59x |
| `literal/empty-unicode` | 8.430 us | 9.586 us | Managed faster | 0.88x |
| `pcre2/kreset-atomic-alt` | 0.248 us | 0.288 us | Managed faster | 0.86x |
| `pcre2/duplicate-names` | 0.253 us | 0.332 us | Managed faster | 0.76x |
| `pcre2/kreset-bar-or-baz` | 0.247 us | 0.354 us | Managed faster | 0.70x |
| `pcre2/branch-reset-basic` | 0.277 us | 0.424 us | Managed faster | 0.65x |
| `pcre2/branch-reset-backref` | 0.270 us | 0.432 us | Managed faster | 0.62x |
| `pcre2/same-start-global` | 0.388 us | 0.624 us | Managed faster | 0.62x |
| `pcre2/branch-reset-followup` | 0.255 us | 0.411 us | Managed faster | 0.62x |
| `pcre2/kreset-global` | 0.156 us | 0.256 us | Managed faster | 0.61x |
| `pcre2/branch-reset-nested` | 0.280 us | 0.535 us | Managed faster | 0.53x |
| `pcre2/kreset-captured-repeat` | 2.548 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 2.455 us | — | Excluded | — |

### PCRE2-only EnumerateMatches

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `pcre2/kreset-atomic-alt` | 0.731 us | 0.847 us | Managed faster | 0.86x |
| `pcre2/duplicate-names` | 0.711 us | 0.896 us | Managed faster | 0.79x |
| `pcre2/kreset-bar-or-baz` | 0.716 us | 0.900 us | Managed faster | 0.79x |
| `pcre2/kreset-global` | 0.638 us | 0.810 us | Managed faster | 0.79x |
| `pcre2/branch-reset-followup` | 0.713 us | 0.972 us | Managed faster | 0.73x |
| `pcre2/branch-reset-backref` | 0.723 us | 0.991 us | Managed faster | 0.73x |
| `pcre2/branch-reset-nested` | 0.743 us | 1.092 us | Managed faster | 0.68x |
| `pcre2/branch-reset-basic` | 0.747 us | 1.111 us | Managed faster | 0.67x |
| `pcre2/grapheme-cluster` | 4.844 us | 8.126 us | Managed faster | 0.60x |
| `pcre2/same-start-global` | 0.629 us | 1.169 us | Managed faster | 0.54x |
| `literal/empty-unicode` | 7.685 us | 27.073 us | Managed faster | 0.28x |
| `pcre2/kreset-captured-repeat` | 3.393 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 3.364 us | — | Excluded | — |

### PCRE2-only Replace

| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |
| --- | ---: | ---: | --- | ---: |
| `literal/empty-unicode` | 13.607 us | — | Excluded | — |
| `pcre2/branch-reset-backref` | 0.856 us | — | Excluded | — |
| `pcre2/branch-reset-basic` | 0.935 us | — | Excluded | — |
| `pcre2/branch-reset-followup` | 0.844 us | — | Excluded | — |
| `pcre2/branch-reset-nested` | 0.840 us | — | Excluded | — |
| `pcre2/duplicate-names` | 0.844 us | — | Excluded | — |
| `pcre2/grapheme-cluster` | 6.173 us | — | Excluded | — |
| `pcre2/kreset-atomic-alt` | 0.865 us | — | Excluded | — |
| `pcre2/kreset-bar-or-baz` | 0.865 us | — | Excluded | — |
| `pcre2/kreset-captured-repeat` | 12.985 us | — | Excluded | — |
| `pcre2/kreset-global` | 0.779 us | — | Excluded | — |
| `pcre2/kreset-repeat` | 9.720 us | — | Excluded | — |
| `pcre2/same-start-global` | 2.523 us | — | Excluded | — |

<details>
<summary>Qualification, allocations and scaling evidence</summary>

The [summary and raw-evidence reference](../../bench/Snapshots/Pcre2.json) retains per-row confidence intervals, exclusion reasons, input/source/binary identities, allocation probes, native build/plan fingerprints and managed/comparator workspace contracts. Managed-thread allocation probes do not measure retained native memory. Reusable match buffers are neither thread-safe nor reentrant; finite/token/deadline costs remain separate from these ordinary rows.

Benchmark comparator: [PCRE.NET 1.5.0](https://www.nuget.org/packages/PCRE.NET/1.5.0), native PCRE2 `10.47 2025-10-21`, license `BSD-3-Clause WITH PCRE2-exception`. This private benchmark dependency is absent from shipped projects.

- `pcre2-managed-compatible-ismatch/backtracking/capture-backreference`: At least one paired lane sample was shorter than 20 ms.
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
