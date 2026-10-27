# Resource controls and host ownership

This is the shared 0.4 host contract. See [release notes](CHANGELOG.md) for current
source/candidate status. Both adapters require their exact Core version.

## Select controls for each phase

`Utf8ResourcePolicy` accepts positive nullable `MaxWorkSteps`, `MaxDepth`,
`MaxWorkingBytes` and `MaxResultBytes`. Null is unlimited; zero is invalid.
**Invocation defaults and static cached calls remain unlimited.**

`Utf8ConstructionPolicy` accepts positive nullable `MaxWorkSteps`, `MaxDepth`,
`MaxInstructionCapacity` and `MaxBytes`. Null retains fixed construction
backstops; it does not mean unlimited construction.

In the calls below, `policy` governs later execution; `construction`,
`constructionTimeout` and `token` govern eager compilation only.

| Profile | Instance with execution policy | Explicit eager construction |
| --- | --- | --- |
| Core | `new Utf8Regex(pattern, options, matchTimeout, policy)` | `Utf8Regex.Compile(pattern, options, matchTimeout, policy, construction, constructionTimeout, token)` |
| PythonRe | `new Utf8PythonRegex(pattern, options, matchTimeout, policy)` | `Utf8PythonRegex.Compile(pattern, options, matchTimeout, policy, construction, constructionTimeout, token)` |
| PCRE2 | `Utf8Pcre2Regex.Create(pattern, options, settings, policy, matchTimeout)` | `Utf8Pcre2Regex.Compile(pattern, options, settings, matchTimeout, policy, construction, constructionTimeout, token)` |

Core's factory takes text; adapters also accept strict UTF-8 patterns.
Compile bypasses static caches and does not retain its construction authority.
PythonRe Compile eagerly prepares FullMatch; constructors defer it. Pass the
execution token on each later call. Pre-cancellation wins at explicit Compile
and execution entry. Null text patterns name `pattern` after the existing
constructor argument checks; empty text/default spans are empty patterns.

## Invocation scope and failures

| Common control | Core/PythonRe | PCRE2 |
| --- | --- | --- |
| Work | Cumulative validation, matching and required shaping | `MatchLimit`; common values above `uint.MaxValue` reject |
| Depth | Live frames/continuations or pending linear alternatives | `DepthLimit`, using the selected route's depth units |
| Working bytes | Major active capacity, growth overlap and published reservations | `HeapLimitInBytes`, with its profile-specific scope |
| Result bytes | Cumulative modeled major result/staging capacity before acquisition; no refunds | `ResultLimitInBytes`, with its profile-specific scope |

PCRE2 common null maps to legacy zero/unlimited. Work units differ between routes,
profiles and native PCRE2; numerical thresholds can change when real work/storage
disappears. A stack-free count may need no depth/result payload.

| Failure | Core/PythonRe | PCRE2 |
| --- | --- | --- |
| Invocation cap | `Utf8ResourceLimitExceededException`, `Execution*` kind, Limit/Requested | `Pcre2MatchException`: MatchLimit, DepthLimit, HeapLimit or ResultLimit |
| Invocation deadline | `RegexMatchTimeoutException` | `Pcre2MatchException`: Timeout |
| Eager construction cap/deadline | `Utf8ResourceLimitExceededException` with Construction* / `RegexMatchTimeoutException` | Same shared construction exceptions |
| Cancellation | `OperationCanceledException` with original token | Same |

One invocation spans strict full-subject validation, discovery, required
capture/coordinate projection, output and lazy advances. The absolute deadline
includes waits between moves; copying cannot reset it or cumulative allowance.
The first denial stays sticky. Refusal is failure, never truncated success.

Core/PythonRe timeout exceptions omit subject text when either byte cap is finite.
This avoids an unadmitted diagnostic decode, not all exception allocations.
Callbacks cannot be forcibly interrupted; checks surround them, and required
copying/encoding of their return stays covered.

Covered major arrays/text, output controls, capture/name roots, coordinate maps
and staging/final growth admit capacity before acquisition. Result traffic never
refunds; shared storage may count again. Exclusive scratch releases synchronously.
Published Core value-cursor map/table reservations remain charged for the
invocation, even after EOF or physical collection. The numeric ledger does not
retain abandoned payloads.

## Construction, caches and preparation

Construction bounds major buffers, instruction/state expansion and traversal
with interruption checkpoints. Core fixed ceilings are 1,048,576 instruction slots,
64 MiB covered bytes, 67,108,864 work units and 65,536 live frames. PCRE2 lexical
and recursive backtracking construction also has a 256-frame backstop.
Positive caller caps tighten these ceilings. Conservative shared/transient
charges may cause earlier refusal.

PCRE2 backtracking metadata uses a conservative 256-byte charge per UTF-16 pattern
character, plus admitted program/name buffers; its literal/character compilers
admit token/class/property/UTF-8 extents and overlap separately.
These are modeled capacities, not every helper allocation.

Fixed finite numeric execution policies omit optional unmetered Core/adapter
preparation that those instances cannot use. A live token or timeout alone does
not imply the same elision. Program/cache retention has separate conservative
per-program charges and eviction; held programs/results survive eviction.
CLR/JIT metadata, incidental helpers and total heap/RSS remain outside the model.

| Preparation | Additional scope |
| --- | --- |
| PythonRe FullMatch | Compile prepares eagerly under construction policy. Execution-triggered cold preparation uses finite working bytes as a ceiling on additional covered construction capacity. |
| PythonRe replacement | Cold parser uses that separate additional-byte ceiling and fixed construction backstops; warm text-key hits need no preparation scope. Template lookup also charges one invocation step per 2,048 UTF-16 units, rounded up. |
| Core replacement | Cold parsing and UTF-8 key decoding use a nested additional-byte ceiling. Warm text keys need no scope; UTF-8 keys still fund decoding. Validation admits scan work before reading. |
| PCRE2 replacement | Preparation checkpoints also spend invocation work. Cold finite-heap preparation borrows remaining heap as a construction ceiling and holds additional peak capacity through replacement. Warm plans use the separate bounded cache. |

Controlled preparation observes the original invocation token/deadline.
`ConstructionBytes` may precede execution failure and include enclosing
construction reservations. Failure restores parent ceilings/loans without
refunding spent work or caching failed preparation. A cancelled waiter cannot
invalidate another caller's successful plan; bounded independent retry preserves
the waiter's own authority. Even an empty subject can trigger preparation.
Execution caps alone do not bound combined preparation/retention.

## Results, cursors and callbacks

Borrowed subjects must remain unchanged and their memory owner alive.
Owned snapshots survive advance, EOF, failure and disposal.

| Cursor | Ownership |
| --- | --- |
| Core/PCRE2 copied value cursor | Independent positions; shared invocation allowance/deadline. No concurrent advancement of aliases. |
| PythonRe memory cursor | Reference aliases share position/lifetime. Dispose when stopping early; EOF/failure release scratch and subject references synchronously. Concurrent/reentrant access rejects. |

Core/PCRE2 callback writers are valid only inside replacement callbacks.
Copies share initialized output; default/reset joins the active scope on append.
Outside a callback append throws even for empty data; ASCII-byte validation
occurs first. Nested calls restore the outer scope. Scoped byte/char span inputs
copy/encode before returning, including local stack buffers.
Output growth, resets and final shaping share the invocation's controls;
arbitrary callback allocations do not.

PCRE2 `MatchMany` requires disjoint input/result storage, without a runtime
overlap check. Only its returned prefix is valid, and an empty destination still
probes. `TryReplace` separately supports overlap by staging as required.

## Compose a Core/PythonRe host envelope

For finite working cap W and result cap R:

1. Reserve `checked(W + R)` from the host before engine entry/cursor creation.
   Refuse entry for unlimited caps, overflow or failed host admission.
2. Hold the reservation until all covered results and permitted aliases are gone
   or transferred to another admitted host owner. Return/EOF/Dispose alone is
   insufficient while snapshots survive.
3. Admit each new operation separately. Configure work, depth, timeout and
   cancellation independently.

Covered active working capacity is at most W and cumulative result/staging
acquisition is at most R. Their union is therefore conservatively bounded by
W+R, including intentional double-counting. A refused Requested total can exceed
the cap without acquiring the refused allocation.

This is a modeled payload envelope, not exact managed heap, cumulative GC traffic
or RSS. Separately fund programs/preparation, subjects, caches/pools, incidental
objects, standalone getters, callbacks, host containers/copies and escaped
storage. Host admission before engine entry needs the host's own deadline.
The recipe is qualified for Core/PythonRe; it does not infer a PCRE2 envelope
or actual downstream adoption.

Exact APIs: [Core](https://github.com/Lokad/Utf8Regex/blob/master/tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt),
[PythonRe](https://github.com/Lokad/Utf8Regex/blob/master/tests/Lokad.Utf8Regex.PythonRe.Tests/PublicApi.Shipped.txt),
[PCRE2](https://github.com/Lokad/Utf8Regex/blob/master/tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
Source witnesses include `Utf8ExecutionBudget.TryChargeResultBytes`,
`Utf8InvocationResultLedger.ReleaseSince`, `Utf8PythonMatchCursor.MoveNext`
and their finite-result/working/persistent-cursor fixtures.

Actual `Unsafe.SizeOf<T>()` is permitted by producer and downstream contracts:
it performs no unchecked memory access. Other unchecked reads/native matcher
dependencies remain excluded. Package qualification checks exact Core pins,
compiled configuration, source/PDB/DLL identity and isolated consumer fixtures;
it does not prove external adoption or attest every CLR/JIT option.
