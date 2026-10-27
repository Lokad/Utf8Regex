# .NET 11 baseline and first public-path improvements

0.5.0 development targets .NET 10 and .NET 11. The available pinned SDK is
11.0.100-rc.3.26522.148, a go-live release candidate, not GA. This work does not
republish or modify 0.4.0 artifacts.

## Public Count comparison

[Raw samples and identities](public-count-comparison.json),
[elapsed time](public-count-comparison.txt), [driver](compare.cs.txt),
[project](compare.csproj.txt).

Ubuntu 24.04.4, Xeon Platinum 8573C, .NET 11 RC3, CPU2, default tiering.
Frozen baseline 427f6789 versus candidate DLL produced at 4465d08c (production
change 93d9c08d). Both assemblies execute in one process using separate assembly
load contexts. The frozen catalog supplies identical patterns, options and input;
results are checked against its BCL comparator before and during measurement.
Three alternating samples, independently calibrated toward 50 ms; a long call
remains one iteration. One bounded comparison, no confirmation or favorable-run
selection. Driver elapsed 49.69 seconds. The initial launch failed before timing
because its working directory did not locate the catalog corpus; the driver was
relaunched from the repository. Actual versions, DLL/input hashes, counts,
allocations and raw samples are retained in the JSON.

| Public Count workload | Before | After | Result / allocation |
| --- | ---: | ---: | --- |
| Ruff real, 32,514,627 bytes | 8,506 ms | 419 ms | 20 matches; 5,416 -> 3,600 B/op |
| Folded English literal family, 899,232 bytes | 13.906 ms | 1.707 ms | 725 matches; 0 B/op |
| Ruff 32 KiB, four finite limits plus live token | 7.255 ms | 7.647 ms | 0 matches; unchanged 3,712 B/op |
| Unchanged English literal control | 99.551 us | 100.680 us | 513 matches; 0 B/op |

These are complete public operations, including strict UTF-8 validation, not
isolated kernels. Ruff is 20.3x faster and the folded family 8.1x faster in this
comparison. The unchanged literal control moved +1.1%; the finite/live-token
control moved +5.4%. Neither result establishes parity on other workloads.

The folded-family change reuses the existing invariant ASCII literal route on
mixed UTF-8 subjects; Kelvin/boundary/capture eligibility remains checked. The
Ruff improvement recognizes a general leading greedy whitespace repeat followed
by a necessary ASCII literal. Checked span search discovers candidate windows;
the existing scalar VM verifies every scalar start within them. This preserves
cases where captures/references make the first start fail and a later one win.
Finite limits, deadlines, live tokens and checkpoint hooks retain their metered
path. No new engine, unchecked access, native dependency or allocation is added.

## Fairness and refresh preparation

- Independently calibrate comparison lanes. The former shared trip count left
  some producer samples at 2-5 ms while decoding ran about 100 ms. New Core V3
  records actual counts/raw durations, alternates order, pins a processor and
  refuses qualification below 20 ms. PCRE2 uses the same existing-host loop.
- Python/.NET result batches now retain the returned value, then hash it outside
  timing, as the producer and CPython already did. Required public result
  materialization stays timed. Global inline options are included in the .NET
  comparator. CPython startup/compilation remain excluded; strict decode and
  predecoded baselines stay distinct.
- Lexer loops consume native byte or UTF-16 lengths. Untimed checks now verify
  equivalent match ranges/text, then normalize each sink in its own units. No
  unrequested coordinate conversion is charged to either timed caller.
- Profile output files must not dirty another profile's source qualification.
  Only the exact known generated outputs are excluded; source, build inputs,
  corpora and unknown files remain gated. Original dirty flags are not edited.
- PCRE.NET comparisons retain validation, the standard NFA with JIT disabled,
  matching requested byte-range work and explicit unsupported/incompatible
  exclusions. Managed enumeration/MatchMany also supplies derived UTF-16
  coordinates; native results carry byte ranges. These are public API costs
  under those contracts, not identical kernel-only output shapes. The native
  wrapper validates the first match and reuses validation during enumeration
  (verified in its exact e5e5deaa source).
  Native retained heap is not exposed by PCRE.NET; managed allocation columns
  cannot establish equal total memory usage.

The .NET decode controls use `Encoding.UTF8.GetString` on verified-valid catalog
subjects. They do not measure malformed-input handling. Strict full-subject
validation remains required producer work. All timings use elapsed wall time,
not process/thread CPU time; native coordinate and runtime representation costs
remain explicit rather than being attributed solely to matching kernels.

## First bounded full-refresh attempt

[Step timings](first-attempt.json), [complete frozen checkpoints and logs (local archive)](../../../artifacts/experiments/net11-20261027/frozen-refresh.tar.gz)
(SHA256 16677352703316A5A22BAB1B8B3B5D48D2DCF5B7A1FABEC9278D09EB0D9C1C2B).
The archive contains relative repository paths and the original scripts, logs,
snapshots and generated pages. It is evidence, not current published figures.

Frozen source 427f6789, same VM/.NET 11, optimized CPython 3.13.2. Started
2026-10-27T16:49:36Z. **A complete sub-hour refresh was not achieved.** SDK/
interpreter provisioning was separate. Build 63.27 s, both-runtime tests 155.37 s,
disabled-dynamic-code checks 16.53 s, semantic preflight 14.58 s. Core stopped at
Ruff's 120-second child cap; checkpoints plus bounded continuation covered 75/89
unique cases. Python raw 90 rows took 300.95 s; seven scaling families 22.27 s.
PCRE2 raw 126 operation rows took 349.07 s; scaling reached its 600-second cap
after seven of sixteen families. The required-literal all-a miss family alone consumed roughly
nine minutes, with individual calls reaching about 28 seconds. Finite-policy
diagnostics hit a 90-second cap after most cases, retaining their raw samples.

Cross-profile output exclusions were incomplete: Python paired qualification
stopped before measuring, and later raw records report tracked-dirty despite
unchanged code. A lexer byte/UTF-16 sink mismatch also stopped continuation.
Those defects are corrected in later source; the original evidence/flags remain
unchanged. The original 60-minute deadline was not restarted for continuations.
The last continuation declined to run once the deadline had elapsed. The JSON
step ledger covers the main batch; individual continuation costs are in its
archived console/log files. Do not interpret its last saved elapsed value as a
successful complete refresh duration.

## Completed .NET 11 checkpoints and limits

[Main batch](current-refresh.json), [targeted completion](finish-current.json),
[remaining Python rows](python-last.json), [last warmup/placement checks](last-policy.json).
Complete scripts, raw logs and intermediate snapshots are retained in the
[local completed archive](../../../artifacts/experiments/net11-20261027/completed-net11-evidence.tar.gz),
SHA256 B0906FF1E704DB1071CA4BACF67A2F57EB52BB80AB7B8E2E4496A6E1B152C279.
Earlier local archives remain unchanged. The final source snapshots/pages are
the repository's existing benchmark files, not a second maintained scoreboard.

- Core: all 89 cases, ordinary and compiled, on .NET 11. The sole selective
  confirmation leaves four entries unqualified (Twain/symbol cases in both
  modes); raw sample durations remain visible. No further favorable reruns.
- PythonRe: all 90 operation rows, all seven scaling families, and paired
  CPython evidence for every operation. Eighteen rows are inconclusive and one
  is unqualified for removable harness-floor sensitivity. Old and new measured
  producer identities remain distinct. Qualification applies to those recorded
  builds; it does not assert freshness against later metadata-only rebuilds.
- PCRE2: all 126 operation rows, all sixteen scaling families and all 101
  supported native paired comparisons. Twenty-five exclusions retain reasons;
  fourteen paired rows remain inconclusive. One hundred Linux pairs used the
  scheduler default and report unavailable affinity. The later IPv4 pilot uses
  the existing single-processor Linux selector. Earlier pairs are not relabeled
  or remeasured simply to change placement metadata.
- Finite/live-token public IsMatch/Count sweep: all five cases across all three
  profiles completed in 101.37 s, including the four finite invocation controls,
  exact result agreement, allocations and decode/predecoded controls.
  [Raw public lanes](finite-public-lanes.txt).
- Release correctness covers both runtimes on Windows and Linux, including
  disabled dynamic code; focused new resource/candidate tests also pass.
  Generated-page/parity verification passes. Published 0.4.0 package/symbol
  [identities](published04-identities.json) are unchanged. No new package matrix
  or published version was created for these harness changes.

Delivery CI 40568979672 is green at c6660ea8 on Windows and Linux, including
both runtimes and disabled dynamic code. Later receipt-only commits do not
change the tested source or benchmark snapshots.

**The complete sub-hour target is not demonstrated.** The corrected main batch
started 17:59:34Z and took 52m46s, plus its prior 60.17 s build, but retained
unfinished work. Targeted completion took another 7m29s; subsequent bounded
confirmations/remaining-case checks are separately recorded. Source/tool fixes
occurred between checkpoints. This is not one uninterrupted frozen-revision
sub-hour run. SDK/interpreter provisioning was separate. Do not add up favorable
subsets or exclude failed phases to claim the target.

The identified costs are concrete: 64-call warmups on second-long operations,
100,000-call split floors, unbounded call-floor warmups on managed/CPython
corpus lanes, and the quadratic PCRE2 miss. Managed and CPython call floors now
stop after roughly 1.5 s when work is expensive, retaining minimum duration and
actual-call evidence. Cheap one-shot floors, result checks, alternating samples,
duration and stability gates remain. Native warmup keeps its 750 ms target and
64-call floor where feasible, with a 1.5 s call-floor limit. A single slow call
can exceed these soft warmup durations; outer command caps still apply.

The mail-corpus qualification ultimately completed in 74.99 s after fixing both
managed and CPython warmup floors. Its actual paired public Count costs are
2.508 s versus 358 ms predecoded CPython (7.05x); the 2.851 s independent figure
remains as originally measured. [Final qualification log](python-email-final.txt).

## PCRE2 required-literal Count

[Initial public comparison](pcre2-count-comparison.json),
[sole confirmation](pcre2-count-confirmation.json),
[driver](pcre2-compare.cs.txt), [project](pcre2-compare.csproj.txt),
[confirmation driver](pcre2-confirm.cs.txt). All exact DLL/input hashes,
versions, counts, allocations and raw samples are retained.

The existing candidate plan already knows a literal after a required leading
run. The slice Count route now borrows that admitted search before renting VM
scratch; a found literal leaves all match ordering/verification to the VM.
Validation/search share the original work, token and deadline authority.
Resource tests prove sticky work, zero VM scratch on impossible subjects,
admission on real attempts, retained captures, malformed tails and byte offsets.
This does not guarantee version-stable denial thresholds.

Baseline b70f1ff1 versus Windows-built candidate 0e9671b6, CPU2, three alternating
samples: the 128 KiB all-a miss fell from 27.137 s to 4.989 us; the finite/live
4 KiB miss fell from 36.571 ms to 1.664 us. Miss allocations fell from 1680/1792
to 0/112 B/op. However, matching input regressed 151.644 -> 493.925 us (3.26x),
while the unchanged literal control improved about 9%. The initial result stays
visible and is not discarded as noise.

One bounded confirmation used both VM-built .NET 11 assemblies, a 2.5-second
warmup through the timing loop, seven samples and reversed first lane. Matching
input was 122.742 -> 122.708 us; the unchanged control 49.794 -> 49.820 us.
Allocations were unchanged. These changes jointly alter build/layout and warmup;
the cause of the first discrepancy is not isolated. **Acceptance is scoped to
the VM-built Linux candidate. Package/Windows operating-cost equivalence remains
unproved and must be checked on actual candidate bytes at release freeze.**
No third comparison or further tuning was run.

Next work follows the existing plan: largest absolute/relative public gaps by
profile (AWS scans, Python mail Count, PCRE2 IPv4 Count), then one fresh complete
VM cycle at a meaningful delivery boundary. No unchecked-access campaign,
accounting framework, dormant engine or profile expansion was introduced.
