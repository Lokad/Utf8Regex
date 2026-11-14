# POSIX forward cursor coordinate reuse

Accepted source `29d64145`, against frozen dev90 `70ea16b2`. The existing exclusive
cursor reuses its preceding published whole-match byte/UTF-16 end. Count only the
unseen gap and selected content when projecting the next whole result. Empty-match
scalar advancement stays in that next gap; capture origins retain their existing
whole-match reuse. Two integer fields, no index, map, owner or public API. One-shot
Match keeps its zero origin. [Acceptance/all observations](acceptance.json).

One **68.468s** nine-row ordinary-process ABBA, five150ms-target samples per
process/row, all>=20ms,150s measurement cap, **zero confirmations**. Distinct staged
POSIX DLL/PDB pairs; every Core/POSIX compiler-source checksum verifies. Core,
caller and.NET images are identical across lanes. Caller-valid unchanged UTF-8,
matching options and complete byte/UTF-16 capture values. Time the entire public
cursor lifetime and all capture slots into identical reused caller-owned buffers;
verify complete results after timing. No isolated-kernel or native-parity claim.

| Public cursor operation | Before µs | After µs | Change | Allocated B/op |
|---|---:|---:|---:|---:|
| Short ambiguous cursor | 1.767 | 1.770 | +0.14% | 1040→1048 |
| Dense ASCII captures | 521.926 | 364.712 | -30.12% | 376→384 |
| Unicode gaps and captures | 1727.590 | 131.396 | -92.39% | 376→384 |
| Funded/deadline Unicode gaps | 1823.931 | 226.622 | -87.58% | 536→544 |
| Empty Unicode progression | 2283.682 | 87.889 | -96.15% | 360→368 |
| Nonzero Unicode floor | 19153.179 | 165.736 | -99.13% | 376→384 |

The five material rows have separated observed ranges. Short ambiguous+0.14%
overlaps. One-shot control+4.83%, identical Core+0.09% and.NET−0.10% overlap;
retain the process medians/ranges rather than normalizing. These are observed
ranges, not formal confidence intervals. **Every complete cursor operation adds
8B of managed traffic**, with the same allocation count and scratch acquisition.
That fixed cost is retained in exchange for eliminating repeated prefixes.

POSIX Release396 checks pass per Windows.NET10/11 and Linux.NET11, including
nonzero borrowed-memory slices with invalid bytes outside the slice, skipped
Unicode gaps, empty progression, aliases, partial/excess output spans, retained
values, finite limits, original cancellation/deadlines and closure/refunds.
Coordinate work can fall; no total heap/RSS guarantee or budget precision claim.
No implementation/preflight/timing failure or confirmation was needed.

Raw copied/hashed offVM before keeper release:
`artifacts/experiments/posix-cursor-coordinate-20261114/evidence.tar.gz`,
SHA256 `1455C75D021B594B9095DD57023D69CC567DF1D23AA6B4EE8FBAE1CDFA2F2BC0`. Windows checks archived alongside it;
worker/collector/keeper terminal0. All580 package/symbol assets are unchanged.
No package/Full/canonical refresh; dev90 remains the last usable staged dependency.
POSIX native comparisons still use PCRE.NET ordered NFA only on verified overlap,
not as a general POSIX oracle. Broader grammar and remaining capture/output costs
are separate. This bounded simplification is closed.
