# Single-group capture rollback storage

Accepted bounded experiment against clean parent d492fdcd642aaf7ee4da709d728ea4c45d9e7071.
This is source evidence, not a new package qualification or catalog benchmark status.
Qualified/staged dev.44 bytes remain unchanged.

An assertion-free latest-value run with one plain capture slot needs undo entries
only while a frame can restore an earlier value. The existing assertion/history
driver retains every entry; opening journals and balancing retain their original
paths. Basic instruction metering, original tokens/deadlines and admission before
needed storage remain. Python's sole participating group is necessarily its last
closed group; completed-slot participation preserves empty/unmatched metadata.
Multi-group completion order still uses the journal. No new executor, public mode,
buffer, program metadata or accounting framework is introduced.

The untimed production-state probe uses `item-([0-9]+)` and the actual 17,408-byte
FindAll subject. Positioned storage falls from 320 to 96 modeled bytes and trail
entries from two to zero, with zero frames in both versions. Capture range and
LastIndex are checked; disposal refunds all storage. This is a live-state observation,
not a measurement of total process memory.

## Public costs

ONE 77.170581374-second Default-process ABBA comparison on the quiet eight-CPU
Linux VM, .NET 11 RC3. Affinity is set after runtime startup. Each process loads one
Core/Python graph, with matching DLL hashes, identical caller, inputs and complete
result checks after timing. Twelve public rows, 240 samples, 75 ms target and 20 ms
floor; actual minimum 38.6381 ms. Outer cap 120 seconds; no confirmation or retuning.
Raw per-process observations and uncertainty are in summary.json.

| Public operation | Before µs | After µs | Change | Allocated bytes before → after |
|---|---:|---:|---:|---:|
| Actual captured strings | 387.21 | 393.17 | +1.54% | 94,552 → 94,344 |
| Actual captured UTF-8 | 403.84 | 395.98 | −1.95% | 94,552 → 94,344 |
| Actual strings, finite/live token | 583.94 | 576.80 | −1.22% | 98,752 → 98,544 |
| Actual detailed captures/LastIndex | 731.24 | 692.33 | −5.32% | 414,040 → 413,832 |
| Single-group backtracking | 188.97 | 206.10 | +9.06% | 17,736 → 17,736 |
| False full prefixes, one hit | 38.88 | 39.17 | +0.75% | 336 → 128 |
| False first bytes, one hit | 76.33 | 76.61 | +0.37% | 336 → 128 |
| Longer alternative tails | 205.47 | 214.12 | +4.21% | 45,904 → 45,904 |
| Unicode-prefix captured strings | 415.80 | 402.47 | −3.21% | 94,552 → 94,344 |

Unchanged Core literal Count +1.15%, Python literal FindAll +1.33%, named-reference
FullMatch miss approximately 0%, with unchanged allocations. Controls are not
normalizers. Candidate detailed medians are lower in both processes; ordinary
strings/UTF-8/finite timings overlap. Preserve the adverse backtracking/alternative
costs. Accept for the eliminated storage and detailed-result gain, under the user's
documented trade-off policy; the closed checked-access 10% allowance is not a
universal delivery gate.

Separate overlapping-semantics BCL compiled-regex diagnostics consume complete
group-string arrays: predecoded 420.97 µs/901,496 B and decode-then-match
350.44 µs/936,336 B. Their counterintuitive ordering and single-process scope limit
interpretation; these do not refresh catalog statuses or establish parity.

## Validation and limits

Windows Release: Core 7,399 and PCRE2 841 per target framework. Python's unchanged
runtime passed 604 existing checks per framework, followed by its two corrected
finite-policy witnesses. Initial failed logs remain: the new witnesses first used
insufficient guessed budgets (64 then 128 bytes); their final adequate finite policy
is 1,024 bytes. Five Core assertions and one Python ownership assertion assumed
mandatory rollback storage. They now test actual slot admission, necessary history
denials, original-token interruption, reservations and cleanup. Work cutoffs,
capture/history, sparse declarations, branches, assertions, balancing, RTL and
NonBacktracking remain covered. No runtime changes were made to accommodate those
test corrections.

Linux focused checks and raw archive identities are recorded in validation.json.
No solution refresh, package rebuild, immutable artifact rewrite or checked-access
campaign replay is part of this experiment. Resource costs are route/version
dependent; this change lowers required scratch and some journal work, not public
semantic guarantees. Specifications document that numerical change.
