# Composing finite invocation limits with a host owner

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

Development after scalar.51 also omits the diagnostic subject string on a
timeout when either byte cap is finite (`RegexMatchTimeoutException.Input` is
empty). This avoids a subject-sized decode after deadline expiry. It is outside
the immutable scalar.51 bytes and does not establish an exception/RSS cap.

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

Development after scalar.51 includes the derived FullMatch program in the
explicit PythonRe `Compile` construction allowance before publication. Hosts
can use that factory to finish this program eagerly; constructors still defer
it. This adds upfront construction work/storage and can deny tighter policies.
Replacement preparation and complete program/cache retention remain separate.

On denial or interruption, release exclusive cursor scratch through its normal
failure/EOF/Dispose path, but preserve reservations for surviving results. Prior
snapshots stay valid; they do not make a failed bulk operation successful or
authorize silently truncated output. Reusing an expression for another call does
not extend the previous invocation's allowance.

The producer keeps `Unsafe.SizeOf<T>()` as an accepted managed-size primitive;
Lython's stricter Core DLL API gate still rejects the current artifact. Complete
construction/lazy/default/live-retention policies and actual consumer ownership
integration remain open. This recipe is a conditional composition of already
qualified scope, with no consumer adoption, new shared engine meter or package
designation implied.

The proposed consumer reconciliation is an explicit member exception for
`System.Runtime.CompilerServices.Unsafe.SizeOf<T>()` only. It queries a managed
type's size and performs no unchecked read, pointer arithmetic or reference
reinterpretation. Keep rejecting the other Unsafe members and SIMD LoadUnsafe;
do not replace SizeOf with manual estimates. This is a producer proposal, not
downstream agreement: the unmodified strict gate still fails. Prepared scalar.52
also fails the cumulative performance gate and has no package designation.

The source witnesses are `Utf8ExecutionBudget.TryChargeResultBytes`,
`Utf8InvocationResultLedger.ReleaseSince` and `Utf8PythonMatchCursor.MoveNext`.
`PythonReFiniteResultPolicyTests.CursorTrafficSurvivesPublicationAndAliasesShareFailureCleanup`
checks cumulative denial, surviving snapshots and fresh-call allowance.
`PythonRePersistentCursorTests` and `PythonReFiniteWorkingPolicyTests` cover
explicit scratch release, aliases, interruption and overlapping working capacity.
