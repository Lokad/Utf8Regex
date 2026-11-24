# Managed PCRE2 profile

`Lokad.Utf8Regex.Pcre2` implements selected PCRE2 10.47 standard-matcher
semantics in managed code. Compile, match, substitution and probe options have
separate types. The adapter owns its grammar, captures and errors. Core loads
independently. [Supported scope](../src/Lokad.Utf8Regex.Pcre2/README.md#supported-scope-and-limitations) ·
[API snapshot](../tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt)

## Subject input contract

Supply valid UTF-8 and keep borrowed storage unchanged throughout use. Validate
external bytes where they become text. Regex operations trust this guarantee.
Benchmarks use native `PCRE2_NO_UTF_CHECK` under the same input guarantee.
Pattern and template validation remain independent.

Byte starts range from zero through the input length and must be scalar
boundaries. The prefix remains available to anchors and lookbehind. Results
expose byte coordinates. The rules for `\C` and `\K` below govern byte fragments
and noncontiguous reported ranges.

## Operations and results

| Operation | Result |
| --- | --- |
| `IsMatch` and `Count` | Existence and nonoverlapping match count from the supplied start. |
| `Match` | Borrowed whole-match value and optional caller-owned last-capture ranges. |
| `EnumerateMatches` | Whole-match ranges over a borrowed span. |
| `CreateMatchCursor` | Immutable last-capture snapshots over borrowed memory. |
| `Probe` | Full, partial or no-match outcome under explicit probe mode. |
| `Replace` and `TryReplace` | Byte-template substitution into owned bytes or a caller destination. |

`Match` writes the requested prefix of numbered slots, including zero.
Short buffers preserve the match and excess slots stay untouched. Entry
cancellation leaves the destination unchanged. A miss or later failure clears
the requested prefix. Keep the subject and capture destination disjoint.
Caller slots consume publication work and matching can require admitted
scratch. Caller slots incur no result-byte charge.

`Mark` reports the last relevant named verb on successful, partial or failed
matches. It is available on Match, cursor results and Probe. Span enumeration
publishes ranges only.

### Reported ranges and global progress

Reported byte start and exclusive end are independent of the consumed range.
Opting into lookaround `\K` can report start greater than end. `Match`
exposes such ranges with `HasContiguousByteRange = false`. Byte extraction
throws. Global operations can reject non-monotone patterns before discovery
or reject a yielded non-monotone result with `NotSupportedException`.

Opted-in `\C` consumes one byte and is forbidden in UTF lookbehind. The
subject remains valid UTF-8 even when a range contains a byte fragment.
Check `IsUtf8SliceWellFormed` before decoding a range. Replacement in this
mode can produce byte output that is invalid UTF-8.

Global discovery resumes at the consumed end of a nonempty match. After
an empty match it retries anchored and nonempty at that position. A failed
retry stops anchored iteration or advances to the next scalar boundary.
Caller `NotEmpty` and `NotEmptyAtStart` options apply to each attempt.
Substitution can retain a reset-empty `\K` result suppressed by enumeration.

With CRLF-aware newlines, unanchored wildcard discovery skips the LF in a
CRLF pair after a failed CR candidate. An explicit LF start or an explicit
newline atom can still match there. DotAll and supported partial probes follow
the selected wildcard and newline options.

### Captures and metadata

Capture numbers follow declaration order and branch-reset slot reuse.
`CaptureGroupCount` excludes slot zero. Duplicate names can identify
different slots. Names start with a Unicode letter or underscore, followed
by letters, digits or underscores. Numeric names fail with
`InvalidAfterParensQuery`. Capture names are limited to 128 UTF-8 bytes and
verb labels to 255.

`TryCopyPatternUtf8` exports the original pattern. `NameEntryCount` and
`TryCopyNameEntryUtf8` export names and numbers in first-declaration order.
A branch-reset declaration sharing both name and number has one entry.
With a short destination, the method reports the required length and slot
and preserves the destination. [Shared metadata contract](SPEC.md#native-pattern-metadata)

`CopyNumbersForUtf8Name` compares strict UTF-8 names ordinally and copies
all fitting numbers in table order. It reports how many fit and whether
more remain. An unknown name copies zero. Malformed names throw with a byte
offset before writing. Resolve the first participating value for a name by
testing its numbers for `Success` in that order.

Metadata queries observe their supplied token before funded-open and
argument checks. They use neither the match deadline nor its numerical
limits. Copied metadata remains valid after program close.

## Grammar and lexical rules

PCRE2 syntax is validated before Core search or execution can be reused.
Unsupported features fail with `NotSupportedException`. Grammar failures use
`Pcre2CompileException` and substitution failures have their own exception.

| Area | Rules |
| --- | --- |
| Quantifiers | Finite bounds are at most 65535. The minimum cannot exceed the maximum. |
| Classes | Range endpoints are scalars and cannot be predicates. Invalid or reversed ranges report `ClassRangeOrder`. Leading and trailing literal hyphens retain their meaning. |
| Quoting | `\Q...\E` quotes inside and outside brackets. Quote markers and isolated `\E` add no atom. Quoting a closing bracket leaves the class open. |
| Verb labels | Recognized labels are literal through the first closing parenthesis. `ALT_VERBNAMES` is outside this profile. |
| Extended modes | Comments end at the configured newline. Escaped or quoted hashes and hashes in classes are literal. `x` after `xx` selects ordinary `x`. Repeated `x` does not imply `xx`. `-x` clears both. Only `xx` ignores unescaped spaces and tabs in brackets. |
| Option directives | At most one hyphen. A flag on both sides is disabled. `(?)`, `(?-)` and `(?-:...)` are no-ops. `(?^)` clears supported `i/m/n/s/x/xx/r`, retains `U/J` and disallows a hyphen. Scoped flags restore on exit. |
| Duplicate names | `J` is lexical. Disabling it rejects later duplicate declarations and preserves earlier slots. A group exit restores inherited permission. Branch-reset names follow their same-number rule. |
| Subroutines | `DEFINE` preserves local flags and declaration slots even when uncalled. Calls can backtrack. Ordinary or `ACCEPT` return restores the caller's captures, including unset slots. |

Default settings forbid duplicate names, `\C` and lookaround `\K`, and
select Default newline and BSR.

### Unicode behavior

Simple-fold equivalence is symmetric and culture invariant, with Unicode 16
data for BMP and supplementary pairs. Dotted and dotless I remain distinct.
Inline `r` with `i` blocks ASCII/non-ASCII folding across literals, classes
and references. Explicit members remain included. POSIX lower/upper under
UCP retain their categories when caseless. Without UCP they use ASCII folds.

`Xuc` includes dollar, at sign, grave accent and valid scalars from U+00A0.
Caseless `Xuc` does not add ASCII K. `Xps`, `Xsp` and UCP space include NEL
and U+180E. Without UCP, generic and POSIX space are ASCII. Explicit property
escapes remain Unicode-aware.

`\X` uses pinned Unicode 16 grapheme data, including Indic conjunct and
no-break-before-ZWJ rules that differ from native 10.47 in some cases.
Extended substitution uppercases final sigma to uppercase sigma, a disclosed
native difference. Other Unicode properties are not all pinned.

## Partial probing

`Probe` returns `NoMatch`, `FullMatch` or `PartialMatch`. Partial outcomes
expose substring zero and MARK. `None` requests ordinary full probing.
`Soft` and `Hard` support the qualified literal and structural patterns.
Outside those patterns, Soft can return a proved full match. Other partial
decisions can throw `NotSupportedException`, including a Hard full match
whose priority cannot be established or a marked Soft miss. Read the
supported scope before relying on partial outcomes. [Profile scope](../src/Lokad.Utf8Regex.Pcre2/README.md#supported-scope-and-limitations)

Partial combined with compile or match `EndAnchored` throws
`Pcre2MatchException` with `BadOption`. Pre-cancellation takes precedence.
Operational failures throw without a probe outcome.

## Substitution

Use PCRE2 byte-template syntax. Default forms include `$$`, `$n`, `${n}`,
`$0`, `$&`, prefix/suffix, `$_`, `$+`, `$*MARK`, `${*MARK}` and `$<name>`.
Backslash processing requires Extended. Strict UTF-8 validation covers the
complete template before matching, including misses and `SubstituteLiteral`.
Syntax errors preserve native no-match and unselected-fallback deferral.

Discovery, capture and MARK access, output growth and copying share one
invocation. Cold template keys and parser buffers require admission.
`TryReplace` accepts input or template bytes that overlap its destination.
Staging a result requires working and result admission. A short destination
stays unchanged and returns `DestinationTooSmall`, with zero bytes written
or the required length under `SubstituteOverflowLength`. Interruption during
the final copy can leave partial output.
[Output ownership](SPEC.md#results-splitting-and-replacement)

## Interruption and resource controls

### Construction

`Utf8RegexPolicy` is separate from PCRE2 options and settings. Invalid
policy values name `policy`. `Compile` checks its token before argument
validation or acquisition. Construction limits and `ConstructionTimeout`
apply before publication. An omitted construction timeout is infinite.
Default constructors can borrow an active construction scope. Explicit
compilation owns its scope. Byte patterns are validated and copied under
admission. Invalid UTF-8 reports its byte offset. `Pattern` decodes byte
source on request.

PCRE2 caps backtracking and lexical construction at 256 live frames. A
caller can tighten that bound. A funded build with no explicit `MaxBytes`
uses the account for covered bytes, subject to the other construction
backstops. [Shared construction rules](SPEC.md#construction-preparation-and-caches)

### Invocation

Entry cancellation precedes program access and argument validation. Bounded
checkpoints report the originating token. A single absolute deadline covers
discovery, projection, publication, cold replacement preparation and cursor
waits. Expiry throws `Pcre2MatchException` with `Timeout`. An omitted
`MatchTimeout` inherits `Utf8Pcre2Regex.DefaultMatchTimeout`, initially infinite.

`ResourcePolicy` exposes the policy. `DefaultExecutionLimits` projects null
as zero and preserves these PCRE2 units. `MaxWorkSteps` is at most
`uint.MaxValue`. A numeric denial throws `Pcre2MatchException` with the
corresponding kind.

| Policy field | Projected limit | Covered scope | Error kind |
| --- | --- | --- | --- |
| `MaxWorkSteps` | `MatchLimit` | Cumulative work, including search, retries and publication | `MatchLimit` |
| `MaxDepth` | `DepthLimit` | Live backtracking, subroutine and assertion depth | `DepthLimit` |
| `MaxWorkingBytes` | `HeapLimitInBytes` | Major live buffers, including growth overlap | `HeapLimit` |
| `MaxResultBytes` | `ResultLimitInBytes` | Cumulative covered results and staging | `ResultLimit` |

Work and storage requirements vary by version and pattern. Borrowed input,
caller destinations, host retention and metadata queries fall outside
numeric execution limits. [Shared resource rules](SPEC.md#cancellation-deadlines-and-resource-limits)

The backtracking matcher finds lookbehind candidates with a single reverse
scalar walk. The assertion step covers the first 64 scalar retreats. Each
additional block charges work and checks the original token and deadline
before scanning. Verifying candidate starts has its own work charges.

`(*ACCEPT)` closes open captures at the current call depth. Wide scans of
capture slots use bounded work and interruption checkpoints, including
duplicate-name backreferences and capture conditions.

### Program and working allowances

`ProgramMemoryAllowance` admits the program and reserves capacity for the
bounded replacement cache, including 4 MiB of payload and bounded metadata. `IsMatch`, `Match`, `Count`, `CreateMatchCursor`
and `TryReplace` accept a working account. The default inherits the program
account when present. Probe and owned-byte Replace retain their numeric
controls and program use. Shared refusal throws `Utf8MemoryAdmissionException`.
Numeric limits still apply and temporary charges refund when use ends.

Disposal of a funded program closes it to new operations and metadata
queries. Admitted operations finish and a memory cursor can retain its use
through close. The charge refunds after the last use. Pattern, options,
settings and policy properties remain readable. Unfunded disposal is a no-op.

### Cursor and result lifetimes

Span enumerator copies have independent positions and share their numeric
budget and deadline. Creation and each advance require an open funded
expression, including after exhaustion. They carry no shared working loan
between advances. Denials remain in effect across copies. Earlier values
stay valid over unchanged subject bytes.

Memory cursor aliases share position and disposal. Concurrent or reentrant
access is rejected. The original deadline and cumulative limits cover every
advance. An advance token applies only to that call and the creation token
wins if both are canceled. Completion, failure and disposal refund exclusive
workspace and release program use synchronously. Dispose an abandoned cursor.
Published ranges and MARK survive later advances, cursor disposal and program
close. Keep the borrowed input and its memory manager alive during use.

`MoveNextRetained` requires a working account and returns a disposable
`Utf8Pcre2RetainedRangeMatch`. Calling without a working account faults the
cursor. The charge covers the receipt, capture array and MARK text when present. An alternate result account must admit this
charge before the working loan refunds. Admission refusal ends the cursor.
Disposal closes `Match` access and refunds once, including through aliases.
`ChargedBytes` remains readable. Previously obtained ranges stay valid under
their original input contract. Result traffic stays cumulative after refund.

## Isolation and scope

Core is the adapter's only non-BCL implementation dependency. Shipped
matching is managed. Callouts, the DFA matcher, invalid-UTF mode, RTL,
split and full capture history are outside this profile. Use cursor ranges
for host-defined output. [Profile examples](../src/Lokad.Utf8Regex.Pcre2/README.md)
