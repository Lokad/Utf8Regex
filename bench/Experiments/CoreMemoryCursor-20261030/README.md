# Core persistent byte-range capture cursors

Producer **cb61b569f3d588594a519ed5c1fca41b487e6c2f**, prior Core
**1a7e0b90fb884210e4d432f15145a4d518c680f9**. H2 ownership/H3 native-subject slice.
The complete byte-native pattern/template pipeline and PCRE2 admission remain open.

`Utf8RegexMatchCursor` validates borrowed memory once and reuses existing scalar
workspace/NonBacktracking state, capture histories and eligible native literal
result search. It produces immutable byte ranges without text or whole-subject
UTF-16 maps. Existing copied span cursors retain their contracts. The separate
class shares position/lifetime through aliases, rejects concurrent/reentrant use
and refunds exclusive scratch on EOF, failure or disposal. Funded program close
rejects new cursors; existing cursors keep compilation admitted until they finish.
Current-advance cancellation ends with the call, preserving original-token
precedence and the absolute deadline across waits. No finalizer or new matcher.

Ordinary snapshots transfer retention responsibility at publication. Explicit
`Utf8RetainedRangeMatch` receipts transfer admitted result loans unchanged, or
admit a separate result child before refunding working staging. Shared names and
numbers are conservatively charged again for independent retained lifetime.
There is no accounting gap, retry or identity registry. Copies remain valid after
receipt disposal, with further admission then the caller's responsibility.
Borrowed bytes are excluded; cumulative result traffic is never refunded.
[Contract](../../../docs/SPEC.md), [example](../../../docs/MIGRATION.md).

Release checks on .NET 10 and .NET 11 pass: **33 new cases/runtime**,
**60 focused cursor/owner/API/managed cases/runtime**, then complete Core
**40167/runtime** with reused matching binaries. Full local duration 2m23s while
sibling repositories were active. Tests cover sparse/repeated/balancing captures,
RTL, NonBacktracking, assertion context, scalar starts/emoji/empty progression,
strict malformed tails, current/original live cancellation, deadline across await,
close/aliases, concurrent/reentrant refusal, cumulative numeric limits and refunds.
A 500,000-match public iteration keeps live working funding below 1 KiB; a finite
work control distinguishes validation once from repeated subject rescanning.
Separate-result refusal/cancellation preserves previously returned results and
refunds both transient accounts before throwing. [Verification](verification.txt).

The one [public pilot](comparison.json) took **47.15s**, under a 120s cap;
minimum actual sample **82.11ms**. Ubuntu 24.04.4/Xeon Platinum 8573C,
.NET 11 RC3, CPU mask 4. Eight independent lanes, 600ms warmup each, calibration
toward 80ms, three alternating samples. Results/history digests are checked after
timing; caller balances are checked between samples and after final close.
No confirmation, catalog refresh or package matrix.

| Complete public capture workload | Existing repeated detailed calls (us) | Memory cursor (us) | Shared working (us) | Program default (us) | Retained same / separate result (us) |
| --- | --- | --- | --- | --- | --- |
| Literal, 32 matches | 36.104 | 5.086 | 5.977 | 6.042 | 10.937 / 11.471 |
| Captures, 32 matches | 60.154 | 38.373 | 40.546 | 40.653 | 47.608 / 47.817 |
| Unicode captures, 32 matches | 126.853 | 35.044 | 36.973 | 37.045 | 43.343 / 43.715 |
| NonBacktracking, 64 matches | 74.872 | 40.115 | 43.158 | 42.887 | 54.813 / 55.431 |
| Finite/live captures, 32 matches | 71.594 | 42.887 | 44.408 | 44.532 | 53.559 / 53.863 |

These capture lanes produce equal byte ranges and histories. Existing
`MatchDetailedFromUtf16Offset` validates/projects for each call; the new memory
cursor validates once and keeps admitted discovery state. This is a comparison of
different public flows, not instruction/kernel equivalence. The new funded and
retained paths provide extra guarantees; their costs are stated separately.
The finite/live case uses the same work/depth/working/result policy and live token
for existing/new flows. No comparator benchmark Status or parity gain is claimed.

| Workload | Allocated B/op: existing / memory / working / program / retained | Peak working / separate result loan (B) |
| --- | --- | --- |
| Literal | 6144 / 2784 / 2784 / 2808 / 5368 | 368 / 368 |
| Captures | 144512 / 16384 / 16384 / 16408 / 18968 | 4768 / 512 |
| Unicode captures | 228672 / 15824 / 15824 / 15848 / 18408 | 6496 / 448 |
| NonBacktracking | 132720 / 18256 / 18256 / 18280 / 23400 | 2544 / 432 |
| Finite/live captures | 148472 / 16384 / 16384 / 16408 / 18968 | 4768 / 512 |

Both retained lanes allocate the same bytes. Peaks are modeled live loans, not
GC allocations; the entire workload's allocation is per operation, not per match.
Before/after public Count controls on the exact same five subjects vary
**-4.44% to +4.20%**, unchanged allocations. Count is a separate fixed-space
operation, not a capture throughput comparator. Existing matching methods are
unchanged in this source slice. The preserved source bundle confirms that scope.

Immutable evidence under artifacts/experiments/core-memory-cursor-20261030/:

- core-memory-cursor-cb61b569.tar.gz: exact before/after Core DLL/PDB, source
  bundle, driver/build/preflight/raw results. SHA256
  `5E39D1CABB1EDC5DF56078D19287AD86E073CF23C7B29EFE01AC8643D7A5CA06`.
- correctness-logs.zip: focused/full local checks, setup/build logs and driver.
  SHA256 `48AC7DA39533C7DB1734183EFCAF145011CE03BFBD5C9EA4F3E1F37E4792136A`.

The UTF-8 pipeline handoff was integrated as high-priority H3 beside H2 in the
ignored root plan and removed. Next finish profile-specific funding/ranges and
native replacement, then direct UTF-8 compilation/metadata/cache bridges. Do not
describe current string-backed pattern/template preparation as a complete native
pipeline. Cumulative fixed-baseline cost, a current complete sub-hour refresh and
immutable package/actual consumer acceptance remain release gates.
