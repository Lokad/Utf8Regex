# Visit live consuming cells before ordered epsilon cells

Packed NonBacktracking rows use successor bits to skip inactive consuming cells,
then evaluate their existing ordered epsilon cells. Accept/Fail no longer need
per-row dispatch. Two readonly masks replace no ownership contract; the compact
order uses the existing before-allocation construction admission. Wide programs
keep full ordered evaluation. All direct atom charges, <=64cell quanta, table
capacities, ordered captures, strict validation and cursor lifetimes remain.
No new mode, density switch, reverse-edge array or accounting framework. The
initial unmeasured reverse-frontier idea was simplified before timing.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json),
**95.902s**, quiet Ubuntu VM/.NET11RC3, CPUmask4. Frozenff018 binaries (production
code identical to parentee59) before, one archived two-file patch after. Fifteen
cases, four equal-result lanes, warm1s/min64, independent60ms calibration,
5alternating samples, minimum20.366ms. Complete result batches checked after timing.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Region Count ordinary | 3978.344 | 2157.384 | -45.77% |
| Region Count compiled | 4058.356 | 2225.569 | -45.16% |
| Region Count live | 4480.369 | 2435.087 | -45.65% |
| Region Count finite/live | 4770.463 | 2609.678 | -45.30% |
| Region Count deadline/live | 7277.850 | 5363.856 | -26.30% |
| Unicode Count | 740.909 | 493.818 | -33.35% |
| Wide Count | 2628.503 | 2450.316 | -6.78% |
| Nullable Count | 238.920 | 150.461 | -37.02% |
| Dense ASCII Count | 573.190 | 424.079 | -26.01% |
| Dense supplementary Count | 386.742 | 296.214 | -23.41% |
| Category Count deadline/live | 896.589 | 836.387 | -6.71% |
| Category IsMatch deadline/live control | 1168.445 | 1219.125 | +4.34% |
| Unchanged non-NB Count control | 4.668 | 4.681 | +0.28% |
| Unchanged literal Count control | 0.396 | 0.396 | -0.09% |

Invocation allocations are unchanged for every measured operation. Keep the
[raw pairs/spreads](summary.json), including the adverse IsMatch control. The
large region parity gap remains; these percentages are controlled A/B evidence,
not refreshed published ratios.

Initial cold creation samples were bimodal. The [sole selected confirmation](cold-confirmation.json)
used the same producer and typed-lane DLLs, only four cold producer lanes,
warm5s/min64, independent60ms calibration and5alternating samples under45s:
**23.309s**, minimum81.597ms, spreads<=1.05x. Native public UTF8Compile followed
by the first empty-subject Count costs49.098->48.773us(-0.66%),51128->51200B.
The before/after non-NB cold control costs40.356->40.698us(+0.85%),44144->44160B.
These lanes include fresh compilation/preparation each call; no reflection is
timed. They are explicitly labeled producer lanes, not .NET comparator results.
[Confirmation pairs](cold-summary.json) and the initial observation are retained;
no further confirmation or warm target resampling.

[Release checks](verification.json) pass on net10/net11. Eight new public cases
exercise shared epsilon dependencies, branch preference, last captures, replacement,
empty progress and byte/derived-UTF16 ranges on overlapping BCL semantics.
[371 complete production probes](state-summary.json) preserve every viability
bit, numeric invocation work, single-search range/capture/peak and13B loan refund.
Wide/packed-word boundaries, category first blocks, deadline/publication and aliases
remain covered. No basic metering was removed.

[Construction diagnostics](construction-after.jsonl) are internal accounting
observations, not public timing. Region order15->8, modeled retained/peak
37376->37344B, construction work2890->2913, cachecharge4297776->4297744B.
The compact array is admitted before copying/releasing its builder-owned full
scratch. Two masks fit the existing512B conservative carrier; no field collector
or new precision mechanism. Cold allocation traffic grows72B; retained modeled
capacity falls32B. Construction thresholds can shift, as [SPEC](../../../docs/SPEC.md)
now explains. Invocation work/capacity is unchanged.

- [Exact source/harness/binaries/raw evidence](../../../artifacts/experiments/nb-sparse-viability-20261101/evidence.tar.gz):
  `EE5CBE9ECEACB0D2005B63FE20F656BE06C37EA8DA671D294C77D378C854F81F`.
- [Correctness/source](../../../artifacts/experiments/nb-sparse-viability-20261101/correctness-source.zip):
  `65C2D978909DBCE56FB141DFAD96BF329B206FD53CCA4E756EB2D64503C3002E`.

The unchanged typed cold-lane DLL is SHA256
`EA31D008AC8DBE2A3F835DF392968909E0AA1EA57C406CC40E14AA9B9C3070F2`.
No complete package/platform/catalog qualification was repeated. Immutable dev.2
and8d full-refresh/consumer evidence remain that source's completed boundary.

Only the region row was selectively refreshed from clean source `570c9d4c`:
ordinary/compiled2176.461/2176.661us,557416B,**60.79/60.15x** predecoded.NET11,
both Gap. Previous published105.83/107.36x; this separate refreshed observation
is not the controlled A/B percentage above. All30 samples>=48.459ms. Other
rows/statuses/lifecycle/scaling retain their original identities. Existing
parity verification and both-runtime source snapshot/hash/ratio guards pass.
[Selective raw/generated/binary evidence](../../../artifacts/experiments/nb-sparse-viability-20261101/selective-evidence.tar.gz):
`3F444AF584A4667E3B178B522509CEE0C99DEDCC2BEEE26664F3F74348E7E0B9`.
Committed-source Core DLL SHA256:
`09D6CB2B17106DF51E687D991EFEA6B2FEB7F8DCC0C569AEB11D089C4E0EA10C`.
