# Resource controls and host ownership

The qualified private Core/PythonRe input is **0.4.0-scalar.103**. Its
[handoff](artifacts/core-python-scalar103-20261019/HANDOFF.md) records exact
identities, same-byte Windows/Linux consumer checks, current costs and exclusions.
Independent PCRE2 uses its exact Core/PCRE2 scalar.94 pair; no PCRE102 is designated.
Earlier packages retain their recorded contracts and limitations.

Scalar.103 fixes a scoped-multiline anchor defect:
grouped literals such as `(?m:^(abc))` could incorrectly use an absolute-start
shortcut, missing later lines or undercounting matches. The fix reuses the
existing admitted prefix proof; scoped line anchors keep their line/scalar
route. Immutable scalar.102 is unchanged and does not contain this correction.

Development source after scalar.103 delays PythonRe scalar substitution's output
buffer until the first match. With no replacements, it returns an owned copy of
the validated input directly, using its known character count for string output.
This removes unused staging from working/result admission; a smaller finite
budget may now succeed. Final payload admission, independent mutable byte results,
template validation and original interruption remain. Scalar.103 is unchanged.

The shared Core scalar template/owned-writer replacement path also projects the
validated subject directly when no match was found. Its existing callback scope
and control charges stay; returned bytes remain independent. Final payloads keep
their numeric admission, and `TryReplace` preserves overlapping copies and an
unchanged destination when capacity is insufficient. Unused byte staging is no
longer charged on that path. This development change is outside immutable103.

Scalar.101 supports overlapping Core `TryReplace` subject/destination spans by
staging scalar output before copying. Staging may allocate even for a literal;
insufficient destinations stay unchanged. UTF-8 templates may alias the
destination too. Use disjoint subject/destination storage with scalar.100 and
earlier. Detailed Python snapshots may share an immutable string for captures
covering the whole match; capture metadata and returned mutable arrays remain
independent.

Scalar.102 adds changes outside immutable101: eligible controlled
Core `IsMatch`/`Count` calls reject a planner-proved absent required literal before
VM acquisition, and scalar prefix Count rechecks the original token after its
terminal work checkpoint. The first can admit smaller limits; a one-byte modeled
working cap does not imply one physical allocated byte. The second corrects a
shared-invocation cancellation edge. Controlled PythonRe template substitution
also admits one invocation-work step per 2,048 UTF-16 template code units before
cache lookup, for cold and warm calls. Large templates can deny before parsing,
even on empty subjects; detailed construction work keeps its separate backstop.
These changes are qualified in scalar.102; earlier bytes remain unchanged.

Choose eager-construction and invocation limits separately. The common policy
types describe admission scopes; work units and execution failure types depend
on the regex profile. Matching remains strict UTF-8/scalar with byte coordinates.

## Select the public controls

`Utf8ResourcePolicy` has positive nullable `MaxWorkSteps`, `MaxDepth`,
`MaxWorkingBytes` and `MaxResultBytes`. Null means unlimited; zero is invalid.
`Utf8ConstructionPolicy` has positive nullable `MaxWorkSteps`, `MaxDepth`,
`MaxInstructionCapacity` and `MaxBytes`. Its null values keep the fixed
construction backstops; they do **not** mean unlimited construction.

These are existing calls, using each profile's option type. `policy` applies to
later invocations; `construction`, `constructionTimeout` and `token` apply only
to eager compilation.

| Profile | Invocation policy with default construction backstops | Explicit eager construction |
| --- | --- | --- |
| Core | `new Utf8Regex(pattern, options, matchTimeout, policy)` | `Utf8Regex.Compile(pattern, options, matchTimeout, policy, construction, constructionTimeout, token)` |
| PythonRe | `new Utf8PythonRegex(pattern, options, matchTimeout, policy)` | `Utf8PythonRegex.Compile(pattern, options, matchTimeout, policy, construction, constructionTimeout, token)` |
| PCRE2 | `Utf8Pcre2Regex.Create(pattern, options, settings, policy, matchTimeout)` | `Utf8Pcre2Regex.Compile(pattern, options, settings, matchTimeout, policy, construction, constructionTimeout, token)` |

Core's factory takes a string pattern. PythonRe and PCRE2 also accept strict
UTF-8 pattern spans. Explicit `Compile` does not consult a static pattern cache;
its construction token is not retained by the returned expression. PythonRe's
factory eagerly prepares the derived FullMatch program too; its constructors
defer that preparation. Replacement preparation and unmodeled CLR/JIT metadata
retain their separate scope. Pass the invocation token to each later operation.

| Common invocation control | Core / PythonRe | PCRE2 mapping |
| --- | --- | --- |
| `MaxWorkSteps` | Cumulative covered validation, matching and required shaping work | `MatchLimit`; values above `uint.MaxValue` reject |
| `MaxDepth` | Live backtracking/continuation frames or pending linear alternatives; a stack-free route uses no depth allowance | `DepthLimit`, with the selected route's depth units |
| `MaxWorkingBytes` | Modeled major active capacities, including growth overlap and published cursor reservations | `HeapLimitInBytes`, covering its documented workspace/output capacities |
| `MaxResultBytes` | Cumulative modeled major result/staging capacity before acquisition, without refunds | `ResultLimitInBytes`, with the PCRE2 result/staging scope |

PCRE2 translates a common null to legacy zero, which means unlimited. Its named
`Create` factory preserves unambiguous legacy constructor calls using bare
`default` limits. Work counts are not interchangeable between profiles or native
PCRE2. Direct literal calls retain validation, discovery and required projection
metering; a boolean result can need fewer units than a value result.

| Failure | Core / PythonRe | PCRE2 |
| --- | --- | --- |
| Invocation resource denial | `Utf8ResourceLimitExceededException`, with an `Execution*` resource kind | `Pcre2MatchException`: `MatchLimit`, `DepthLimit`, `HeapLimit` or `ResultLimit` |
| Invocation deadline | `RegexMatchTimeoutException` | `Pcre2MatchException`: `Timeout` |
| Eager construction denial / deadline | `Utf8ResourceLimitExceededException` with a `Construction*` kind / `RegexMatchTimeoutException` | Same construction exception types |
| Cancellation | `OperationCanceledException` carrying the original token | Same cancellation identity |

An already-canceled token takes precedence at explicit compilation and execution
entry. Controlled lazy advances retain the original invocation's cumulative work
and absolute deadline; copying a cursor does not create a fresh allowance.
Defaults and static cached execution remain unlimited unless finite limits are
selected explicitly. Match timeout is independent of these budgets. Program/cache
retention uses separate conservative accounting; eviction must preserve held
programs and results. Shared storage can be counted again and cause earlier denial.

The exact surfaces are pinned by the [Core](tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt),
[PythonRe](tests/Lokad.Utf8Regex.PythonRe.Tests/PublicApi.Shipped.txt) and
[PCRE2](tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt) API snapshots.
[SPEC.md](SPEC.md#37-cancellation-and-resource-limits),
[SPEC-PYTHONRE.md](SPEC-PYTHONRE.md#cancellation-and-resource-contract) and
[SPEC-PCRE2.md](SPEC-PCRE2.md) define each covered scope. The composition recipe
below is qualified for Core/PythonRe; it does not infer an equivalent PCRE2 host
envelope or complete construction, lazy/default or retained-storage coverage.

## Compose the Core/PythonRe invocation envelope

Core and PythonRe's qualified scalar packages provide invocation limits. A host
can conservatively compose their **covered payload** bounds using its existing
batch/result owners, without a new regex ownership API. This recipe does not
complete Lython's artifact or full memory-containment acceptance gates.

For an instance with positive finite `MaxWorkingBytes = W` and
`MaxResultBytes = R`, reserve `checked(W + R)` from the host **before** entering
the operation or creating its cursor. Refuse entry if either cap is unlimited,
the sum overflows, or the host cannot admit the reservation. Configure work,
depth, timeout and cancellation independently; byte caps do not replace them.
Use the instance carrying this policy. Static cached calls retain unlimited
defaults and cannot substitute for that instance.

The composition follows two existing invariants: covered active working capacity
never exceeds W, while covered result/staging capacity is charged cumulatively
before acquisition against R. Publication may release its working loan, but does
not refund result traffic. Their union is therefore bounded conservatively by
W + R, even when the same storage is counted twice. A refused request can make
the reported requested total exceed a cap; its refused allocation is not acquired.
This is a bound on the defined modeled capacities, not measured managed-heap
usage, cumulative allocations or RSS.

Core/PythonRe omit the diagnostic subject string on a
timeout when either byte cap is finite (`RegexMatchTimeoutException.Input` is
empty). This avoids a subject-sized decode after deadline expiry. It does not
establish an exception/RSS cap.

Attach the reservation to a host owner that survives every covered result and
permitted cursor alias. The simplest rule holds each invocation's whole envelope until the
batch closes and all escaping storage is either gone or admitted by another
host owner. Do not refund it merely because an operation returns, one cursor
alias reaches EOF, or a cursor is disposed. Owned snapshots remain valid after
those events. Separate calls and fresh cursors get fresh engine allowances, so
each needs a separate host admission while previous results survive. Keeping a
larger reservation longer is an intentional simplification, not a GC lease.

PythonRe reference aliases share position and disposal. Core value-cursor copies
have independent positions but share their invocation's allowance and published
working reservations. Keep the original subject unchanged and its memory owner
alive for the cursor/views that borrow it. Sequential waits between cursor moves
remain part of the original regex deadline; host admission before engine entry
needs the host's own deadline. Neither copying nor advancing restarts allowance.

The host must account separately for preparation and compiled programs, borrowed
subjects/metadata, shared pool/cache retention, incidental object overhead,
standalone getters, callback allocations and host containers/copies. Keep any
separate program/subject owner alive while a surviving result or view borrows its
storage. The envelope does not turn these exclusions into covered allocations.
Host-created lists of snapshots, for example, need their own admission.

Core/PythonRe scalar.102 and current source have the following preparation scope.
These are separate phase ceilings, not additions to the invocation W + R promise.

| Preparation | Admission and reuse |
| --- | --- |
| PythonRe FullMatch | Explicit `Compile` eagerly prepares its derived program under the construction policy. Constructors defer it. Execution-triggered preparation borrows finite `MaxWorkingBytes` as a ceiling on additional covered construction capacity. |
| PythonRe replacement | A cold template cache miss uses the same additional-byte ceiling and fixed construction work/depth backstops. A warm text-key hit needs no preparation scope. |
| Core replacement | Cold parsing and UTF-8 template-key decoding share a nested additional-byte ceiling. Warm text-key hits need no scope; UTF-8 lookups still fund decoded-key storage. Template validation admits scan work before reading through the active construction owner or fixed scan ceiling. |

The original invocation token and deadline govern controlled preparation. A byte
denial can report `ConstructionBytes`, including the enclosing construction
reservation, before execution starts. Tighter parent limits remain in force;
nested scopes preserve parent loans and restore their limits on exit. Fine-grained
construction work/depth, cache retention and host admission remain separate from the invocation
numeric envelope. An oversized malformed template may deny before its tail is
read. Even an empty subject can prepare and cache a template before finding no
matches. Reserve separately for preparation and surviving plans; finite invocation
caps alone do not bound their combined retained storage.

PCRE2's independent scalar.94 contract additionally charges controlled replacement
preparation checkpoints to invocation work. Cold finite-heap preparation borrows
the remaining heap capacity as a construction ceiling and holds its additional
peak reservation through replacement. Warm plans use their separate bounded cache.
This differs from the Core/PythonRe phase scope above; see
[SPEC-PCRE2.md](SPEC-PCRE2.md) for its covered capacities and failure mapping.

On denial or interruption, release exclusive cursor scratch through its normal
failure/EOF/Dispose path, but preserve reservations for surviving results. Prior
snapshots stay valid; they do not make a failed bulk operation successful or
authorize silently truncated output. Reusing an expression for another call does
not extend the previous invocation's allowance.

`System.Runtime.CompilerServices.Unsafe.SizeOf<T>()` is accepted by both the
producer and Lython, as confirmed by the user on 2026-10-16. It queries a managed
type's size and performs no unchecked read, pointer arithmetic or reference
reinterpretation. Keep excluding actual unchecked APIs, including other Unsafe
members and SIMD LoadUnsafe; do not replace SizeOf with manual estimates.

This resolves the former SizeOf acceptance disagreement. It does not establish
consumer adoption or complete construction/lazy/default/live-retention policies
and host ownership integration. This recipe composes already qualified scope;
it introduces no shared engine meter or new package designation.

The qualified input is the exact Core/adapter pair recorded by its manifest,
feed and locks. Adapters consume Core internals and move together with the exact
qualified Core version. Applications combining adapters need a common qualified
Core; deliberate dependency overrides have separate compatibility scope.
The immutable [Core/PythonRe scalar.102 handoff](artifacts/core-python-scalar102-20261018/HANDOFF.md)
records same-byte Windows/Linux consumers, exact identities, current costs and
the recorded semantic/resource subset. Independent PCRE2 evidence stays
with its [scalar.94 handoff](artifacts/pcre2-scalar94-delivery-20261018/HANDOFF.md).
Earlier candidates remain immutable; actual consumer ownership/adoption is separate.

The source witnesses are `Utf8ExecutionBudget.TryChargeResultBytes`,
`Utf8InvocationResultLedger.ReleaseSince` and `Utf8PythonMatchCursor.MoveNext`.
`PythonReFiniteResultPolicyTests.CursorTrafficSurvivesPublicationAndAliasesShareFailureCleanup`
checks cumulative denial, surviving snapshots and fresh-call allowance.
`PythonRePersistentCursorTests` and `PythonReFiniteWorkingPolicyTests` cover
explicit scratch release, aliases, interruption and overlapping working capacity.
