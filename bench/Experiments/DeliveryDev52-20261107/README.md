# Common dev.52 delivery checkpoint

Common **0.5.0-dev.52** freezes validated native-value coordinate counting and
the common-UTF8 density census at `6f9b661ae5bce1259039e4e379a2a12b71a0686c`.
All three packages support .NET 10/11 with exact Core pins. No NuGet publication.

The coordinate change reuses the existing bounded UTF-16 sizing counter after
strict whole-input validation and scalar-complete literal discovery. The census
uses checked SIMD to count the same lead bytes in the same bounded sample; its
choice and strict validation are unchanged. Neither adds fields, allocation,
execution modes or an engine. [Coordinate evidence](../NativeValueCoordinates-20261107/README.md)
and [census evidence](../CommonUtf8Density-20261107/README.md) retain the separate
source comparisons, interruption/resource checks and adverse observations.

## Final package costs

ONE exact-package dev.51-to-dev.52 Default-process ABBA completed in **114.109s**:
22 public workloads, 440 samples, minimum 70.0828 ms, unchanged allocation medians
throughout. One producer is loaded per ordinary process. The fixed caller uses
750 ms/minimum 32-call warmup, independent 75 ms calibration, five samples and
120-second outer/35-second producer caps. Results are consumed after timing.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Unicode miss Search | 0.290 | 0.135 | -53.53% | 0 |
| Same finite/live | 0.425 | 0.222 | -47.65% | 112 |
| Same deadline/live | 0.523 | 0.317 | -39.27% | 160 |
| Short Unicode hit | 0.173 | 0.159 | -8.18% | 0 |
| Same finite/live | 0.351 | 0.297 | -15.20% | 112 |
| Same deadline/live | 0.512 | 0.418 | -18.36% | 160 |
| Core Unicode miss Count | 0.474 | 0.254 | -46.34% | 0 |
| Whole-subject Unicode Search, finite/live | 1.811 | 0.677 | -62.60% | 112 |
| Two-byte prefix value Search | 1.588 | 0.579 | -63.51% | 0 |
| Three-byte prefix value Search | 2.148 | 1.100 | -48.79% | 0 |
| Supplementary prefix value Search | 13.703 | 8.230 | -39.94% | 0 |
| Mixed prefix, finite/live | 50.849 | 33.884 | -33.36% | 112 |

All 12 intended gains persist in both processes with disjoint sample ranges.
Effect sizes vary: Unicode finite before-process medians are 0.3704/0.4769 us;
deadline 0.4708/0.5766 us; full Unicode finite 1.6415/1.8379 us. No confirmation
or producer tuning followed. [All observations](package-cost-observations.json),
[summary](package-cost-summary.json) and [acceptance](package-cost-acceptance.json)
retain every process and sample.

Accept persistent ordinary mail **+2.87%/+64.22 us**, with disjoint ranges, alongside
the substantial target gains. Detailed +1.57%/+15.26 ns and anchored FullMatch
+0.40%/+1.17 ns overlap. Finite mail -21.27%/-711.27 us is also disjoint, but was a
neighbor rather than an intended optimization target; retain it without an
unmeasured causal explanation. The PCRE ASCII control -7.14%/-24.11 ns is disjoint
and bypasses the edited routes; give it no causal credit. Other controls and all
uncertainty remain in the summary. No normalization or uniform-speedup claim.

Unicode miss finite extra cost falls 0.1349 to 0.0876 us while relative overhead
rises 46.56% to 65.09%. Short-hit finite extra falls 0.1774 to 0.1383 us. These are
direct package observations; keep earlier source regressions and gains separate.
The completed checked-access campaign's 10% allowance is not a universal gate.

## Platform and refresh acceptance

ONE Full passed all 83 steps in **2,819.1771038 seconds (46m59s)** under the
original 8 GiB RAM/1 GiB repository/8 GiB tmpfs admission and 3,600-second hard cap.
It includes one Release build, both-runtime correctness, disabled dynamic code,
complete .NET 11 catalogs, paired comparators, lifecycle/scaling, resources and
final verifiers. Raw evidence is copied off VM and hash-verified. Seven files were
imported once; README keeps four generated blocks, six descending tables and 178
ratios. Six matching-binary snapshot hosts pass 10 checks.

Core records 122 wins/1 tie candidate/55 gaps and no unqualified rows. PythonRe
records 45 managed faster/32 CPython faster/3 equivalent/7 inconclusive/3 floor
exclusions. PCRE2 records 78 managed faster/13 native faster/1 equivalent/
9 inconclusive/25 excluded. The Unicode literal miss's raw diagnostic ratio is
2.64x predecoded .NET, compared with 9.03x in the previous catalog. Its raw managed
0.1333 us and paired managed 0.1437 us are separate observations. Preserve
uncertainty and comparator scopes; do not infer an isolated causal A/B from
successive catalogs. Catalog adverse movement includes AWS +18.89%/+1,659.44 us;
[all movements](catalog-movements.json) remain visible without attribution.

Windows qualification completed once in **279.934953 seconds**, with two builds
and one pack per profile. Windows and exact-Windows-byte Linux consumption each
pass eight detached fixtures/**110,246 checks**, original Awk 19/Lython 118 tests
per target and public API probes with final root loans zero. All 464 compiler
documents, 12 Windows/24 Linux restore audits and 12 Linux TRX identities are
retained. Linux neither rebuilds nor repacks the producer. The source handoff audit
changes only three exact package pins among 1,565 original files; the original
source archive remains immutable.

All 276 older staged files remain unchanged; six new assets bring
`artifacts/nuget/` to 282. Both frozen Full51/Full52 eight-asset graphs were verified
after Linux and costs. [Preservation](frozen-graphs.json),
[Windows](windows-acceptance.json), [Linux](linux-archive-acceptance.json) and
[inputs](boundary-inputs.json) record the boundary. Source CI passed on Windows
and Linux. Platform/package compatibility is distinct
from downstream adoption and the later POSIX work.

All seven raw archives under `artifacts/experiments/delivery-dev52-20261107/` are
verified off VM and immutable; [hashes](canonical-archives.json) identify the bytes.
No NuGet publication.
