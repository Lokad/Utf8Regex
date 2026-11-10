# Controlled PCRE2 ASCII class-run Count

After `7ba82c92`, allow finite policies, original live tokens, deadlines and
working allowances through the existing complete ASCII class-run counter.
The algorithm and prepared byte set are unchanged. Every candidate and bounded
4096-byte scan block retain the shared work/interruption authority; no new engine,
buffer, cache, execution mode or accounting mechanism is introduced.

The admission audit found that budget construction alone observes no failure.
Controlled dispatch now calls existing `ReserveWorking(0)` before scanning, so
shared numeric/sticky denial and original interruption remain authoritative even
for empty input. The ledger deliberately does not contact external allowances
for zero storage. This Count counter acquires no scratch, frames or result roots:
working/depth/result requirements can fall, and a rejecting allowance can succeed
without receiving a loan request. Real allocations elsewhere retain admission.
The public [contract](../../../docs/SPEC-PCRE2.md) records this distinction.
CountCore still maps escaped deadline expiry, checks the original token before
and after the final absolute-deadline read, and synchronously refunds scratch.

Keep all former semantic exclusions: non-ASCII subjects, caseless/lazy/bounded/
nullable forms, compile anchoring/end anchoring/first-line/continuation skipping,
nondefault match flags and slice diagnostic hooks retain their existing runners.
Strict whole-input validation and scalar start checks precede this dispatch.

One **95.826s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
24 public lanes,480 samples, minimum71.147ms. Identical Core/caller, one PCRE2
producer per process,8 startup processors and post-startup affinity4. Actual
facade/program/IL preflight confirms controlled one-byte working/depth/result
admission changes from HeapLimit to the correct count. Full counts and zero
outstanding loans are checked outside timing. No confirmation, native comparison,
snapshot promotion, package/catalog/Full repeat or production edit after timing.

| Public Count | Before (µs) | After (µs) | Change | Allocation before → after |
|---|---:|---:|---:|---:|
| Catalog miss, finite/live token |1720.56|25.07|−98.54%|416 →112B|
| Catalog miss, funded/live token |1713.68|25.03|−98.54%|416 →112B|
| Catalog miss, deadline/live token |1886.20|25.14|−98.67%|464 →160B|
| Long-word hits, finite/live token |2304.70|38.57|−98.33%|3031152 →112B|
| Long-word hits, funded/live token |2456.51|38.42|−98.44%|3031152 →112B|
| Long-word hits, deadline/live token |2572.93|38.86|−98.49%|3031200 →160B|

Funded modeled peaks fall320→0B for misses and1504→0B for hits. Unlimited miss
24.75→24.81µs (+0.26%); unlimited hits38.26→38.19µs (−0.19%). Unchanged Core,
one-node and grapheme controls move+0.33/+0.23/+0.27%. Retain adverse bounded
88.82→93.26µs (+5.00%), lazy139.56→148.33µs (+6.28%) and non-ASCII+1.05%.
No normalization or attribution of unchanged-path movement. Anchored/flagged
controls improve−15.67/−11.77%; this is no separate claimed optimization.
[Summary](summary.json) preserves all lanes, process medians/ranges and costs.

All6660 PCRE2 Release checks pass per target/platform, Windows/Linux .NET10/11.
Thirteen new cases cover4095/4096/4097-byte boundaries/offsets, malformed tails,
storage-free low limits, long-run/dense-candidate work denial after validation,
existing shared denial on empty input, original-token/deadline interruption
between scan blocks and through publication. One old `[ab]+` heap-failure row
moves into the existing workspace-free success theory. Initial suites caught
that obsolete expectation before timing; failure logs are preserved.

Before PCRE2 `9866ED45EB55F46BAD53D5688DE64E8663DA7702647398AE1417DC83A9C15254`;
after `76DEA4DB1B7A1C2367DCACF8CA6C9BD1D8EDE3857110FA307FFC9726368BEA02`.
Common Core `4975406F71A3498C32564B5163F6AE475A99E81A4B6A43071820F5BED20BFA47`.
Raw graphs/PDBs, source patches, protocol, preflights, Linux checks and samples
verified offVM: `artifacts/experiments/pcre-class-run-controlled-20261110/evidence.tar.gz`,
SHA-256 `3A8B6DA7BCEF6A0F8497229F19DB08FE3C0E9620D24D76D15CADA403A204572D`.
Windows checks and initial failure logs are alongside it. Immutable dev63 bytes
and authoritative snapshots remain unchanged. Source CI42752086742 passes
Windows/Linux for90d18c2fe1907b4348947003c5308765c0cfccc7.
