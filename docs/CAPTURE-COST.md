# Historical capture-cost assessment

This is the **2026-10-13 scalar.33** observation, not a current-engine or POSIX
qualification. Core still uses ordered first success, without POSIX longest-match
or subcapture priority.

The assessed pattern was `^([\p{L}]+)([0-9]+)$` on `sku007`.
MatchDetailed returned whole 0/6, group 1 `sku` at 0/3 and group 2 `007`
at 3/3; 4,096 records produced checksum 40,960. This uniquely anchored example
cannot qualify ambiguous POSIX selection.

| Policy | Median ms / 4,096 calls | B/call |
| --- | ---: | ---: |
| Unlimited, infinite timeout | 8.238 | 6,416 |
| Unlimited, 5-second timeout | 10.096 | 6,576 |
| Finite limits, 5-second timeout | 10.286 | 6,576 |
| Finite limits, timeout and live token | 10.590 | 6,576 |

Finite allowances were work 1M, depth 8,192, working/result 64 MiB each.
These focused Linux/.NET 10.0.12 FullOpts results excluded compilation, consumer
script/I/O and publication. Clock/checkpoint counts did not establish CPU
attribution or justify removing metering. The supplementary/empty capture control
also exposed an oracle-coordinate disagreement; broad equivalence was not claimed.

Source 894fa641c291bbb36b395c714d4e252f62f118f5; Core DLL SHA256
CAF67325DF31E10BE3179FB8988A3489292B1F17645D26BE16AA0F3D82BCA600.
Detailed protocol, ranges, failed initial tiering observation and local receipt
paths remain in the
[original assessment](https://github.com/Lokad/Utf8Regex/blob/91973e3a/CAPTURE-COST.md).
No consumer integration or dependency adoption is established.
See [current evidence](CURRENT-PERFORMANCE.md) for release status.
