# Two-byte span search: rejected

The candidate replaced the forward `AsciiSearch.IndexOfExact2` loop with the
checked BCL `ReadOnlySpan<byte>.IndexOf(literal)`, removing 24 production lines.
Reverse/folded search, validation, caller windows and resource charges were kept.
**Decision: revert the production change.** Several affected public operations
exceed the 10% allowance with stable unchanged controls. No confirmation or tuning.

One quiet-VM .NET 10.0.12 FullOpts/R2R-off comparison, Xeon 8573C CPU 2. Same
[driver](Program.cs), before then after, 120 ms warmup, 80 ms calibration and five
alternating lane-order samples. Fourteen public Count lanes; finite means all four
invocation controls. Constructors and preflights are outside timing. The complete
pair took 19.12 seconds within 60-second child / 180-second batch caps.

Public Count medians, microseconds:

| Workload | Unlimited before → after | Finite before → after |
| --- | --- | --- |
| Captured `(a)(b)`, 1K matches | 10.050 → 11.635 (+15.8%) | 24.888 → 23.945 (−3.8%) |
| Captured `(a)(b)`, 5K matches | 49.292 → 56.725 (+15.1%) | 127.794 → 121.591 (−4.9%) |
| Sparse `ab`, 64 KiB prefix | 2.704 → 4.073 (+50.6%) | 3.885 → 5.549 (+42.8%) |
| Absent `ab`, 64 KiB | 3.385 → 5.039 (+48.9%) | 3.809 → 5.526 (+45.1%) |
| False candidates, 64 KiB `a` | 575.131 → 5.062 (−99.1%) | 566.206 → 5.171 (−99.1%) |
| Two-byte Unicode `é`, 1K | 10.867 → 12.528 (+15.3%) | 26.816 → 25.088 (−6.4%) |
| Unchanged three-byte control | 12.009 → 11.816 (−1.6%) | 25.481 → 25.874 (+1.5%) |

All 140 samples reach at least 74.963 ms; maximum within-lane spread is 1.067x. Allocations
remain 0 bytes unlimited / 112 bytes finite. Public counts, IsMatch, first-match
byte ranges, subject hashes and fourteen declared short-unit minimum-work
boundaries agree. These threshold witnesses do not establish every pattern's
quantitative boundary. CPU quiet gate: 0% busy, other observed dotnet CPU ticks 0.

The gain on many false candidates does not offset demonstrated regressions on
other representative public inputs. Keep the existing algorithm and new borrowed
view / boundary tests. No selector, compatibility mode or additional optimization.
This source experiment does not explain the earlier actual-package 1K/5K cost
anomaly, qualify default-tier behavior, update a snapshot/Status, or reopen the
completed checked-access campaign.

[Raw samples, identities, preflights and summary](measurement.json).
Baseline actual scalar.115 Core:
`30CFD1A459205C2D5B6B10BCEEB775F64D11FFEAE0BB5D06E76D9695C6B5E804`.
Rejected candidate:
`F9A71464164423DF273F55E949D0A1624744DA5982FDA71E45E48E39C57C490D`.
Driver: `FD184F119DF3B1BC1CBDE72DBCCBEB9111150B9B55378D01D5E6C1AA3799729B`.
Complete evidence archive:
`0DFCBD4B8D65AB26FD7917238A320AE58432F23A4DDA185411F55A6F2044B5B4`.

The rejected implementation passed 1,400 focused Release Core checks. Production
is restored before final checks. No package was rebuilt or requalified; published
and staged immutable bytes remain unchanged.
