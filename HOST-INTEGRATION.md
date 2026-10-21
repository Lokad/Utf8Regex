# Resource controls and host ownership

This is the development **0.4.0-rc.2-dev** host contract for Core, PythonRe and
the managed PCRE2 adapter. Frozen RC1 remains unpublished; its PCRE2 bytes have
known defects and require replacement before release. Both adapters require exactly the same Core version. The controls
below are enforceable within their documented modeled scope, not a total
managed-heap/RSS or application-containment guarantee. Defaults remain unlimited.

## Select the public controls

`Utf8ResourcePolicy` has positive nullable `MaxWorkSteps`, `MaxDepth`,
`MaxWorkingBytes` and `MaxResultBytes`. Null means unlimited; zero is invalid.
`Utf8ConstructionPolicy` has positive nullable `MaxWorkSteps`, `MaxDepth`,
`MaxInstructionCapacity` and `MaxBytes`. Its null values keep the fixed
construction backstops; they do **not** mean unlimited construction.

Construction charges are conservative and versioned. Major buffers, growth
overlap and instruction/state expansion require admission before allocation.
Coarse traversal/probe checkpoints bound interruption; individual helper objects,
views and enumerator boxes do not each receive a charge. Cache retention uses
conservative per-program charges and eviction, which can count shared storage
again. Exact numeric thresholds may change when real work or storage disappears.
Actual `Unsafe.SizeOf<T>()` queries remain; they perform no unchecked access.
Replacement plans retain their real instructions without an unused deduplicated
capture-reference map/vector. Native replacement admits only group zero; other
captures use the existing owned expansion. Removing capture placeholders, tags
and lowered syntax flags can reduce construction/instruction/cache charges and
the working capacity of range staging. Major range/output arrays, growth overlap
and cumulative result capacity remain admitted using their actual layout.
Cached Core facades use one conservative 512-byte allowance before allocation,
with their major children admitted separately. The former field-layout mirror is
removed. Compared with its 208-byte modeled descriptor, this can cause earlier
construction refusal or cache eviction; it does not change actual object size.
The prepared-program, scalar-program and dynamic IL builder carriers use coarse
2,048 / 512 / 512-byte allowances respectively, replacing modeled field mirrors
of 1,024 / 144 / 144 bytes. Direct construction keeps admission before allocation;
major children and real array/value layout queries retain separate coverage.
These larger conservative charges can cause earlier refusal/eviction.
Every cache producer supplies a conservative bounded payload policy. Small
runtime/verifier wrappers retain their 128-byte admission floors; the larger
carriers use the allowances above. Major children retain separate admission.
The test-only unbounded cache mode remains removed. These controls do not extend
the documented memory scope or change RC1 package bytes.
Development PCRE2 literal compilation also admits its own atom capacities and
UTF-8 buffer, including growth overlap and bounded interruption checkpoints.
It retains runtime UTF-8 bytes and releases temporary Rune staging, without
compiling an unused Core expression. Construction instruction capacity covers
the atom vector; numeric requirements can change as the redundant Core work and
copies disappear. Character token/class/property buffers now use the same owner,
with token capacity admitted as instructions and bytes. Growth/final-copy overlap
and bounded work/interruption remain covered; incidental helper objects and RSS
remain excluded. Character programs omit unused Core preparation and syntax-node
vectors; literal Rune staging is temporary. Other qualified Core-dependent paths
remain. Invocation semantics and resource scope are unchanged.

PCRE2 lexical guards admit group-scope buffers before growth, including live
old/new extents. Their prospective group nesting shares the caller's depth
ceiling and the existing 256-frame PCRE2 backstop. Bounded scans retain the
original cancellation token/deadline. Candidate-presence checks skip irrelevant
policy scans; no new grammar parser is introduced. Earlier denial before a later
syntax error is intentional when the construction allowance is insufficient.
Selected operations retain only Core expressions they execute; shared slices
own their immutable program and PCRE2 fallback directly.
PCRE2 finite-literal and bounded-ASCII optimizers also share construction work,
depth and major expansion-capacity admission before acquisition. Temporary state
and payloads may be charged again and held until eager construction completes;
this deliberately favors an enforceable bound over exact liveness accounting.
Capture maps are omitted when no reference uses them; actual capture results
still come from the owned matcher. Compilation selects complete operation slots
before publishing one program; dummy legacy foundations are removed.

Cache interruption/size probes are closed library implementations and cannot
mutate their cache while its lock is held. Outside-lock preparation can still
race or reenter clear/shrink; generation coordination and cancellable waits
remain. Synthetic diagnostic mutation at every publication checkpoint is no
longer an internal contract. Public operations and retained aliases are unchanged.

These are existing calls, using each profile's option type. `policy` applies to
later invocations; `construction`, `constructionTimeout` and `token` apply only
to eager compilation.

Null text patterns report `ArgumentNullException` with parameter `pattern` after
the existing policy/options/timeout checks. Explicit Compile still observes
pre-cancellation first. Empty text and default UTF-8 spans are empty patterns.

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

Development after frozen RC1 covers PCRE2 backtracking parsing/lowering with the
same construction owner. Its recursive frames have a fixed 256-frame backstop,
tightened by smaller caller limits. A conservative 256-byte charge per UTF-16
pattern character admits transient AST/name/capture metadata; instruction and
auxiliary program buffers separately admit their actual capacity and growth
overlap. Shared metadata can be charged twice, so previously accepted patterns
may hit construction limits earlier. This adds coverage, not whole-heap bounds.

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

The exact surfaces are pinned by the [Core](https://github.com/Lokad/Utf8Regex/blob/master/tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt),
[PythonRe](https://github.com/Lokad/Utf8Regex/blob/master/tests/Lokad.Utf8Regex.PythonRe.Tests/PublicApi.Shipped.txt) and
[PCRE2](https://github.com/Lokad/Utf8Regex/blob/master/tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt) API snapshots.
[SPEC.md](https://github.com/Lokad/Utf8Regex/blob/master/SPEC.md#37-cancellation-and-resource-limits),
[SPEC-PYTHONRE.md](https://github.com/Lokad/Utf8Regex/blob/master/SPEC-PYTHONRE.md#cancellation-and-resource-contract) and
[SPEC-PCRE2.md](https://github.com/Lokad/Utf8Regex/blob/master/SPEC-PCRE2.md) define each covered scope. The composition recipe
below is qualified for Core/PythonRe; it does not infer an equivalent PCRE2 host
envelope or complete construction, lazy/default or retained-storage coverage.

## Compose the Core/PythonRe invocation envelope

Core and PythonRe provide invocation limits. A host
can conservatively compose their **covered payload** bounds using its existing
batch/result owners, without a new regex ownership API. This recipe does not
establish complete host memory containment or downstream adoption.

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

Core/PythonRe have the following preparation scope.
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

PCRE2 additionally charges controlled replacement
preparation checkpoints to invocation work. Cold finite-heap preparation borrows
the remaining heap capacity as a construction ceiling and holds its additional
peak reservation through replacement. Warm plans use their separate bounded cache.
This differs from the Core/PythonRe phase scope above; see
[SPEC-PCRE2.md](https://github.com/Lokad/Utf8Regex/blob/master/SPEC-PCRE2.md) for its covered capacities and failure mapping.

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

The qualified input is the exact three-package release recorded by the release
manifest, feed and locks. Adapters consume Core internals and must move with the
exact qualified Core version. Previous immutable packages retain their original
evidence. Producer qualification of handoff assertions does not establish actual
external adoption or completion of consumer host-ownership integration.

The source witnesses are `Utf8ExecutionBudget.TryChargeResultBytes`,
`Utf8InvocationResultLedger.ReleaseSince` and `Utf8PythonMatchCursor.MoveNext`.
`PythonReFiniteResultPolicyTests.CursorTrafficSurvivesPublicationAndAliasesShareFailureCleanup`
checks cumulative denial, surviving snapshots and fresh-call allowance.
`PythonRePersistentCursorTests` and `PythonReFiniteWorkingPolicyTests` cover
explicit scratch release, aliases, interruption and overlapping working capacity.
