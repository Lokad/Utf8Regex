# Core contract

Version **0.4.0** targets .NET 10. See
[release notes](../CHANGELOG.md) for release status and [migration](MIGRATION.md)
for upgrade guidance. This contract describes supported behavior; the
[API snapshot](../tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt) and executable
fixtures establish the implemented surface.

## Matching and coordinates

Core matches complete Unicode scalars over strict UTF-8. Dot consumes one scalar
subject to newline options; quantifiers repeat atoms, and combining marks remain
separate scalars. No normalization or implicit grapheme matching occurs.

| Operation | Required result |
| --- | --- |
| Dot on `😀` | One match, byte range 0/4 |
| Dot on `a😀b` | Byte ranges 0/1, 1/4, 5/1 |
| Empty pattern on `😀` | Two matches, at byte boundaries 0 and 4 |
| `^(.)\1$` on `😀😀` | Success; group 1 contains the complete first scalar |
| `a\|aa` on `aa` | `a`, following ordered alternatives |

Captures, references, lookarounds, RTL traversal and empty-match advancement
respect scalar boundaries. Greedy/lazy and alternative order apply at each
viable start. RTL changes search direction, not alternative priority.
POSIX leftmost-longest and POSIX subcapture selection are not implemented.

Byte offsets and lengths are primary. UTF-16 coordinates and string APIs are
derived interoperability. Explicit `FromUtf16Offset` entry points reject an
offset inside a supplementary scalar with `ArgumentOutOfRangeException`.
Offset resolution retains the complete subject's assertion context and the same
invocation controls.

## Input and pattern language

The entire subject must be well-formed UTF-8, including a suffix after an early
match. Malformed, overlong, surrogate-encoding and truncated input throws
`ArgumentException` with an offending byte offset. UTF-8 pattern and replacement
inputs are strict too. Interruption or resource denial can occur before a later
invalid byte is visited; successful matching cannot bypass validation.

Core admits documented .NET-style syntax: literals/classes/subtraction, inline
options, greedy/lazy repetition, captures/history, references, balancing groups,
conditionals, lookarounds, contiguous anchors, RTL and the supported
`ECMAScript` behavior. Options use `RegexOptions`. Unsupported combinations or
syntax fail during construction. BCL parsing/matching is not an execution fallback;
BCL exception messages and UTF-16 matching defects are not the contract.

Adjacent raw or escaped high/low surrogates join into one scalar within a literal
run or class member. Separate groups/assertions/classes do not join them.
Unpaired surrogate atoms cannot match strict UTF-8; zero repetitions remain
possible, and negation admits the valid scalar complement. Class ranges use
numeric scalar order through U+10FFFF, excluding the surrogate gap.
`[\u0000-\uffff]` covers BMP scalars; singleline dot covers every plane.
Invalid syntax reports `ArgumentException` naming `pattern`, with a reason and
a UTF-16 pattern offset. Representation/resource limits are distinct from syntax.

Finite repeat maximum 2147483647 is distinct from infinity. Consuming expansion
requires instruction/state admission. Impossible classes and compact zero-width
repeats preserve syntax checks, predicates and capture declarations without
expansion proportional to the bound.

`NonBacktracking` is a separate owned linear mode for its admitted regular
subset. It preserves ordered choice, greedy/lazy behavior, scalar empty progress
and each group's last completed capture. Unsupported grammar fails before planning.
Nullable cycles and zero-width repeats must terminate without bound-sized
expansion. This mode does not expose ordinary backtracking capture history.

## Case and culture

`CultureInvariant` is the primary supported/performance-tested mode.
Culture-sensitive folding captures construction culture, including inline scopes
and cache identity; later ambient culture changes cannot alter the expression.

| I-cluster mode | Equivalent members |
| --- | --- |
| Invariant | I/i; İ and ı remain distinct |
| Non-Turkic culture | I/i/İ; ı remains distinct |
| Turkic culture | I/ı and İ/i |

Supplementary simple-case mappings use owned Unicode 16.0 data. General
categories/shorthands use runtime scalar properties; this is not a claim that
every Core property table is pinned. [PythonRe](SPEC-PYTHONRE.md) owns its
separate Unicode 15.1 rules.

## Results, splitting and replacement

`Utf8ValueMatch` is storable coordinate data; default means no match, distinct
from a successful empty match. Unsuccessful values have zero UTF-16 coordinates;
`TryGetByteRange` returns false/zero, and byte properties throw without a range.
Default borrowed match/capture views expose empty values and zero coordinates.
An unsuccessful operation retains unsuccessful group zero; a default view has no
groups. Detailed results expose the owned capture history.

Borrowed views require unchanged subject bytes and a live owner. Returned
histories and owned snapshots remain valid after later discovery, denial or cursor
closure. Split preserves count/empty/RTL ordering and does not insert delimiter
captures as extra elements.

Replacement uses .NET-style `$1`, `${name}`, `$&`, prefix/suffix, `$+`,
`$_` and `$$` forms with the supported numeric parsing rules. RTL discovery
assembles output in subject order. UTF-8 template overloads parse replacement
syntax. Each string literal/callback return is encoded independently: an unpaired
surrogate becomes U+FFFD and cannot join a fragment from another substitution.
Subject/template bytes and completed byte-callback output remain strict.

Evaluators must be non-null even on misses; failure names `evaluator`.
Pre-cancellation takes precedence. `Utf8ReplacementWriter` is callback-scoped;
copies share output, and resetting to default clears output before joining the
active invocation on append. Outside a callback, append throws even for empty
fragments; `AppendAsciiByte` validates its argument first. Scoped byte/char
span appends copy/encode before returning, so local stack buffers are supported.

`TryReplace` returns `Done` or `DestinationTooSmall`. Shortage writes zero
bytes and leaves the destination unchanged. Input/template overlap is supported;
overlapping input stages output. Other failures throw, and interruption during
the final copy can leave partial destination contents.

## Cancellation, deadlines and resource limits

Explicit token overloads observe pre-cancellation before validation and preserve
the original token in `OperationCanceledException`. Cooperative checks cover
bounded validation, search, matching, projection, copying, output and publication.
A long failed advance must check before reporting EOF. Tokens belong to the
invocation, never the reusable program/cache.

One absolute deadline covers an invocation and its lazy advances, including waits
between moves and controlled cold preparation. Timeout throws
`RegexMatchTimeoutException`. A finite working or result byte cap makes its
`Input` empty, avoiding an unadmitted diagnostic decode. Work/depth-only and
unlimited calls retain their existing diagnostic input. Scheduling, GC/JIT and
arbitrary caller callbacks cannot be preempted; checks surround callbacks.

Core/PythonRe accept positive nullable `Utf8ResourcePolicy` values; null is
unlimited and zero is invalid. Defaults and static cached calls are unlimited.

| Policy | Covered scope | Denial kind |
| --- | --- | --- |
| `MaxWorkSteps` | Cumulative bounded engine/checkpoint work | `ExecutionWorkSteps` |
| `MaxDepth` | Live backtracking/continuation/assertion frames or pending linear alternatives | `ExecutionDepth` |
| `MaxWorkingBytes` | Conservative major active capacities, including growth overlap | `ExecutionWorkingBytes` |
| `MaxResultBytes` | Cumulative modeled result/staging capacity, admitted before acquisition without refunds | `ExecutionResultBytes` |

Denial throws `Utf8ResourceLimitExceededException` with kind, limit and requested
total. Totals saturate, and first denial stays sticky across copies. Denial is not
no-match or partial success. Numerical charges depend on route/version; they are
not byte counts, CPU instructions, elapsed time or portable per-pattern minima.
Native shortcuts still meter bounded search, comparison and publication.

Copied value cursors have independent positions but share allowance/deadline and
published map/table reservations; concurrent advancement is unsupported.
Exclusive scratch releases synchronously. Published reservations are not refunded
at EOF or GC, and the numeric ledger does not root abandoned payloads.
No finalizer lease, alias registry or GC-dependent refund is required.

Required capture/projection arrays, maps, output controls, staging/final roots and
growth overlap are covered. Fixed scalar values, borrowed subjects/programs,
incidental helpers, standalone getters, arbitrary callback allocations and host
retention are excluded. Shared/temporary/final storage may count again.
The limits do not bound total heap/RSS. PCRE2 uses its own
[limit names and failure vocabulary](SPEC-PCRE2.md#interruption-and-resource-controls).

### Host admission

For a Core/PythonRe instance with finite working cap W and result cap R, a host
can reserve `checked(W + R)` before entry or cursor creation. Refuse entry if a cap
is unlimited, addition overflows, or host admission fails. Covered active working
capacity is at most W; covered result/staging acquisition is cumulatively at most R.
Their union is conservatively bounded by W+R, even when storage counts twice.
A refused Requested total can exceed a cap without acquiring that allocation.

Hold the reservation until all covered results and permitted cursor aliases are
gone or transferred to another admitted host owner. Return, EOF and disposal do
not authorize a refund while owned snapshots survive. Each new call needs separate
admission; configure work, depth, timeout and cancellation independently.

Fund programs/preparation, subjects, caches/pools, incidental objects, standalone
getters, callbacks and host containers/copies separately. Borrowed memory stays
alive and unchanged. Host admission before engine entry needs its own deadline.
This recipe covers modeled payloads, not exact heap/RSS, total allocations or a
PCRE2 envelope; it does not establish downstream adoption. Source witnesses are
`Utf8ExecutionBudget.TryChargeResultBytes`, `Utf8InvocationResultLedger.ReleaseSince`
and `Utf8PythonMatchCursor.MoveNext`, with finite-result/working/cursor fixtures.

## Construction, preparation and caches

Explicit `Compile` accepts separate construction policy, timeout and token;
pre-cancellation wins, nested preparation shares the owner, and authority is not
retained for execution. It does not consult static caches. Ordinary constructors
keep fixed backstops; positive construction values tighten them, null keeps them.

| Fixed construction backstop | Ceiling |
| --- | ---: |
| Instruction slots | 1,048,576 |
| Covered bytes | 64 MiB |
| Work units | 67,108,864 |
| Live frames | 65,536 |

Construction denial uses `ConstructionWorkSteps`, `ConstructionDepth`,
`ConstructionInstructionCapacity` or `ConstructionBytes`. Construction
timeout uses `RegexMatchTimeoutException`, empty Input, and the supplied text
Pattern (empty for byte-pattern diagnostics).

Admission precedes major buffers, instruction/state expansion and growth/copy
overlap. Work/checkpoints bound parsing, traversal, maps/sorting, decoding and
emission. Charges use actual `Unsafe.SizeOf<T>()` strides and conservative
headers/rounding; helper-object/hash-probe precision is not an objective.
Failure unwinds unpublished capacity/depth without refunding spent work or
invalidating borrowed objects. CLR/JIT metadata and complete heap accounting
are excluded.

Cold replacement preparation has separate construction backstops under the
original invocation token/deadline. Finite working bytes additionally cap covered
cold parser/key capacity; a denial can be `ConstructionBytes` before execution.
Warm text-key hits need no preparation scope; UTF-8 keys still fund decoding.
Preliminary byte-template scans admit work before reading through the construction
owner or fixed scan ceiling. Interrupted preparation is retryable and never
published. This is not one combined construction/execution/retention budget.

Caches use conservative per-program charges, separate metadata/payload caps and
eviction; shared storage may be counted twice. Eviction/clear/shrink preserve held
programs/results and cancellable preparation coordination. No retained-object
graph, identity deduplication or transactional manifest is required.

## Implementation and qualification

Production execution is owned and managed: parse/analyze/prepare, immutable
program, invocation runtime. No BCL matcher/result engine, native imports or
unchecked memory reads are shipped. Actual `Unsafe.SizeOf<T>()` is permitted.
`Compiled` affects strategy only; disabled dynamic code uses existing
non-emitted execution. NativeAOT/trimming are not qualified.

Native search is an optimization of the same scalar semantics. Eligibility must
prove full grammar/options/capture/branch order, scalar widths, assertion context,
case fibers and progress. Failure declines the optimization, not the grammar.
See [architecture](../src/Lokad.Utf8Regex/ARCHITECTURE.md) and
[search complexity](../src/Lokad.Utf8Regex/Internal/Search/COMPLEXITY.md).

Qualify scalar/boundary, grammar, captures, RTL/NonBacktracking, malformed-input,
interruption, admission and lifetime behavior through public fixtures. Benchmark
only equal-result workloads with warm throughput against both strict-decode and
predecoded controls. Historical measurements are not current qualification:
[benchmark snapshots](../README.Benchmarks.json).
