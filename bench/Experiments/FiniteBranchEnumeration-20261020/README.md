# Reuse the controlled branch cursor for finite enumeration

Parentb15e3141, development118. OwnedSliceEnumerateMatches constructs the original
InvocationLimits and validates complete input once. For complete nonempty ASCII
branch plans it now transfers that invocation into the existing native cursor,
omitting per-result scalar attempt/slot scratch. Plans are immutable and were
already admitted during eager preparation. No helper, type, field, owner mechanism,
API or switch is added; the unlimited native helper's fences are unchanged.

Deterministic programs, non-ASCII subjects, RTL/NonBacktracking, folded, empty,
lookaround/conditional/boundary/contiguous and shared/explicit-capture declarations
retain their existing paths. Ordinary captures do not change value-result semantics.
Search/candidate/token/depth/projection/terminal work, original cancellation and
absolute deadline, shared allowance and typed denials remain. Copies keep their
independent positions; prior values survive other moves, EOF and failure.

**Intentional numeric revision:** real scratch removal can lower work/depth/working
requirements. Fixed value results acquire no result payload; that capacity scope
does not become a match-count cap. Major acquisitions still require admission.
Defaults, host W+R exclusions, construction and caller-retention scope are unchanged.
This is not a heap/RSS promise. SPEC and HOST-INTEGRATION record this contract.

## One bounded public pair — accepted

Quiet Xeon8573C CPU2/.NET10.0.12, FullOpts/R2Roff. The existing [public fixture](Program.cs)
keeps its60 lanes and calibrated loop, adding only two original-deadline lanes
before timing. Core-DLL-only swap; same driver/companions,120ms warmup,80ms target,
five alternating lane-order samples,60s phase/180s batch caps. One78.536s pair;
all620samples>=61.450ms, maximum within-lane spread1.095. No confirmation, tuning
or repeated campaign. Constructors/startup excluded; full enumeration consumes
every public byte range and verifies its complete checksum.

Ordinary public full-enumeration medians, microseconds:

| Subject | Finite before -> after | Finite/live before -> after | B/op before -> after |
| --- | --- | --- | --- |
| Optional hit,13KB/2000matches |2546.341->397.009|2523.634->363.380|7107664->112|
| Miss,13KB |2147.928->369.464|2136.178->354.057|3664->112|
| Dense false prefix,4099B/1match |759.176->273.860|752.854->275.379|7216->112|
| Short hit,8B/2matches |3.416->2.674|3.458->2.690|10768->112|
| Shared first byte,15KB/2000matches |2334.912->419.454|2310.097->408.243|7105792->112|

Compiled rows agree with these trends. The original10s-deadline hit enumeration
falls2843.594->698.194us and7107712->272B. Fixed control bookkeeping remains;
the new route is not zero-allocation. Unrestricted routes and Count are controls:
short unrestricted enumeration moves+3.2..+3.3%, other Core changes are small.
True .NET predecoded/decode controls move-0.51..+0.50%. No affected operation
exceeds the10% regression allowance. Adapter neighbors remain close; tiny pooled
PCRE fractional allocation differences do not establish exact parity or a gain.

This supports the measured eligible path, not universal finite overhead, .NET11,
default-tier or immutable117 performance. Experimental limits50Mwork/depth1024/
64MiBworking/128MiBresult and10s timeout are not host defaults. No CPython snapshot,
package qualification, consumer source/pin change or immutable-artifact edit.

## Focused Release acceptance

Core474 admission/simple/working/depth plus738 work/result/copy/loan/abandonment/
deadline/cancellation, Python745 finite/native/persistent/ownership and PCRE94
grammar/resource/global/cancellation/timeout checks pass without failures/skips.
Every test output loads measured CoreB979566C. New public cases cover all work
cutoffs, one-byte working/result and depth1 success, borrowed offsets, empty and
malformed subjects, declined grammar/scalar modes, independent copies/shared
denial, retained ranges, fresh invocations, abandonment, borrowed reservations
and deterministic original cancellation/expiry on the final MoveNext. Both timed
phases agree on25 first-range/subject and62 complete-operation preflights.

An initial test used IDisposable syntax for the diagnostic session; it was fixed
to its existing Complete/finally contract before execution. One new tiny-budget
case assumed lazy `a??b` was native; actual metadata proves FallbackRegex with no
branches. Its scalar path is unchanged and its semantic assertion now uses the
normal funded decline fixture. Neither production eligibility nor existing tests
were weakened. Failed setup logs remain alongside the corrected results.

Baseline CoreB7D4336AF24D6F4E4F8F9EF998E6364D11CF8EBD0674E90AB2ED7C9D0E51D926;
candidateB979566C9977B6D07F78B900BCBFE98935B0EFC2C94D13B12FBF19A434D4A09C.
Driver4FF0B37B45617B64AE6A83313AE696B8EDB64F446359D34BA3B01C895271A903.
[Raw samples, preflights, identities and summary](measurement.json). Archive SHA256
930E971A60FB6FC2EF7DF06E6DC6CC5C53C54499E62A7FD75C451D17D030A96D.
Private predeclaration/source/probes/logs:artifacts/finite-branch-enumeration-20261020.
Source5d14afc3 [CI39382340297](ci.json) is terminal green on Windows/Linux,
completed06:50:32/06:51:19UTC. Closed; no terminal repoll. The coherent scalar118
package boundary follows separately; broader original R06/R07/R08/R09/R10 is open.
