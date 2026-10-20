# Second simplification milestone — 2026-10-20

Development source **0.4.0-rc.2-dev**, following reviewed `d0fd579b`. The user
activated five cleanup priorities and deferred the rest. Frozen common RC1
packages/symbols/receipts remain immutable and unpublished; no new package
candidate, release tag or external consumer change is part of this work.

## Delivery and retained scope

1. Remove unproduced `CategoryNode`, its sole old Category instruction emitter,
   empty CategoryTables buffer/plumbing and exclusive runner cases. Live
   CategorySet tables/predicates remain. Remove the unread literal `_backend`
   field and unused internal timeout/native-pattern/cache-byte properties.
2. Compile PCRE2 literals directly without an unused Core regex/overlay. Their
   atom vector and UTF-8 payload use the existing construction admission,
   growth-overlap/instruction bounds and interruption checkpoints; immutable
   buffers transfer without duplicate copies. Group-zero metadata, operations,
   captures, options, validation, invocation limits and lifetimes remain.
   Literal translation diagnostics now correctly say no Core preparation.
   **Character preparation retains Core:** its major parser buffers/checkpoints
   are not yet independently admitted. Core-backed slice/ASCII/backtracking/native
   routes keep their real owners. General foundation pruning is a later bounded
   task, not justification to weaken useful construction controls.
3. Reuse compiler/build servers in ordinary test/benchmark/detached builds.
   Keep observed-lock repository-host recovery. Concurrent stdout/stderr draining
   through PowerShell preserves complete redirected receipts, literal arguments,
   child exit codes and bounded descendant termination; cap exit is 124.
4. Replace exact repeated eager-metadata pairs with the existing ownership
   witness once per direct test method. Preserve every test case, cold instance,
   public operation and semantic/capture/scalar/resource assertion. The existing
   scalar-operation helper retains its initial metadata witness. This is not a
   test-count reduction or a claimed suite-wall-time gain.
5. Separate common RC1/development identities from frozen scalar.119/114 cost
   evidence. Archive obsolete August PCRE2 priority prose and old checkpoint
   detail into git/local ignored HISTORY. Original measurements/hashes/caveats
   remain; no old snapshot is rerun or promoted to current qualification.

## Evidence

[Measurement](measurement.json) records the one predeclared local Release batch,
complete captured logs/hashes and actual DLL identities. Literal construction
allocation fell **38,568 to 2,168 B/op**; its median was 30.150 to 0.681 us/op,
with wide first-sample ranges. This is a small shared-JIT-primed constructor
observation, not a universal/cold-process or matching-throughput claim.

The public character Count median was 90.518 to 41.288 us/op, while unchanged
.NET decode/predecoded controls also moved materially (367.485 to 252.428 and
315.129 to 254.395 us/op). Preserve that host/run variation: the public reduction
is not attributed to this change. No public slowdown above the allowance was
observed; no confirmation or further benchmark campaign was run. Finite policies
are covered functionally, not timed by this observation.

[Live-body equality](live-runner-equality.json) proves the literal matching runner
and entire character implementation stayed unchanged.
[Test-source equality](test-witness-equality.json) proves only repeated metadata
witnesses changed, preserving case attributes and other source bytes.
[Workflow checks](workflow-checks.json) describe bounded launcher probes and
real script smoke checks, without package qualification or a new test framework.

Focused Release checks passed: Core Unicode/scalar/NB/construction/cache,
PythonRe reuse/resource/scalar/captures, PCRE2 properties/ownership/construction
and literal/comment/translation/numeric/name cases, plus the affected ASCII
operation fixture. Initial Core failure was the obsolete exact33-field test
constant; field-type coverage/immutability remains. Initial workflow/source-proof
harness mistakes and their corrected runs do not remove semantic assertions.
Logs remain under `artifacts/s1-*`, `s2-*`, `s3-*` and `s4-*`.

[Initial source CI](initial-ci.json) caught one stale architecture assertion on
both platforms: it demanded a Core owner for the wholly owned literal program.
The correction tests literal ownership and exact identity on a real delegated
pattern; all public behavior checks remain. The full corrected local PCRE2
Release suite passes. [Final source CI](source-ci.json) is terminal green on
Windows/Linux at `2ef80c41`, with the full Core/differential/PythonRe/PCRE2 suites
and actual API/member/resource/semantic checks passing. Existing duplicate-case
and compiler/analyzer warnings remain disclosed. Later receipts/docs do not alter
that C# source; the initial failed run is not hidden or relabeled.

[RC1 identity checks](rc1-unchanged.json) preserve the original frozen bytes.
Complete Windows/Linux source CI is separate from package qualification.
Broader profile/default/retention/performance/publication work remains deferred.
