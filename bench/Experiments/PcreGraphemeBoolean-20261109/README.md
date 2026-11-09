# Boolean grapheme existence

Accepted against frozen dev61 PCRE2/Core (`9bbf54de`). After strict whole-input
validation and scalar-floor admission, one default `\X` succeeds iff input remains.
The existing character dispatch keeps its candidate charge and omits the unused
cluster endpoint. No new plan, metadata, executor or mode. Flags, finite/token/
deadline/funded invocations, hooks, multiple tokens and range-returning operations
retain established execution. This does not remove whole-input validation or
change pinned Unicode16 clustering, capture/lifetime contracts or resource units.

One **75.482s** quiet-VM .NET11 RC3 ordinary-process ABBA under180s. Same Core/
caller, one producer per process;19 public lanes and380 samples after warmup and
independent calibration. Every sample exceeds60ms; results and zero outstanding
loans are checked after timing. Preflight confirms the actual catalog uses the
character plan. No confirmation, full-catalog or package qualification.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Catalog IsMatch, unlimited |0.821|0.789|−4.0%|
| Catalog IsMatch, finite/live |0.899|0.944|+5.1%|
| Catalog IsMatch, funded/live |0.911|0.924|+1.4%|
| Catalog IsMatch, deadline/live |0.963|0.958|−0.5%|
| Long cluster IsMatch, unlimited |79.992|4.863|−93.9%|
| Long cluster, finite/live |79.979|79.660|−0.4%|
| Long cluster, funded/live |79.706|79.675|−0.0%|
| Empty IsMatch, unlimited |0.080|0.066|−18.0%|
| Unchanged Core Count |0.850|0.844|−0.7%|

Allocations and funded peaks are unchanged in all19 lanes. Long-cluster input is
one base plus4096 combining marks; it demonstrates a real public-call reduction,
not an isolated kernel saving. The catalog's248-byte mixed subject gains only4%:
the2.378x native gap remains open. Finite catalog+5.1%, Match+2.0%, match flags+2.3%
and other control movements are retained without normalization or sole-cause
attribution to unchanged paths. [Summary](summary.json) preserves every process,
range and allocation. No new native ratio or universal profile gain is claimed.

All6,635 PCRE2 Release checks pass per target on Windows/Linux, .NET10/11. Eight
new cases cover empty/combining/ZWJ/RI/CRLF/NUL, every scalar floor and offset span,
malformed suffixes, anchored-end/multiple-token counterexamples, original token
and returned cluster endpoints. Existing long-cluster cancellation, deadline and
finite refusal checks still execute inside the established controlled scanner;
working/result and retained-result checks remain. Basic candidate work is kept.

The first driver preflight passed null to the nonnull allowance overload for
ordinary finite lanes and was rejected before timing. Correcting only the driver
to use the cancellation overload passed both preflights; no measurement repeated.
That failed driver and its exception are preserved. Raw source, protocol, matching
graphs, Linux checks and public results are verified off VM at
`artifacts/experiments/pcre-grapheme-boolean-20261109/evidence.tar.gz`, SHA-256
`1A9B3F10B7668E3E9357294C9D9C8A418BBF401BF480CEB43606AAC52B1EC450`.
Windows checks/preflight refusal are alongside it. Dev61 packages/snapshots remain
immutable; source CI follows the scoped commit.
