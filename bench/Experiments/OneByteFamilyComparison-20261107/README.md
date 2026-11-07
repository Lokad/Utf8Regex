# Reject the redundant one-byte family comparison shortcut

**Rejected; source and compiled test graphs restored on both hosts.** Parent
`9397905c5c701fc0ed639978d7c113bc358b986e`. Qualified dev.47 remains unchanged.

The limited Split audit found no missing native route. Core already lowers
`[,;]` to an exact two-literal family. The [untimed qualified47 probe](current-route.json)
confirms retained Core/native-result eligibility/candidate eligibility/group0,
2,944input bytes, four returned strings of5/4/5/2927characters and6,032warm bytes.
Those allocations equal the required strings and final reference array. No new
set matcher, selector, storage or coordinate-map work is justified by this audit.

The13-line prototype removed a full comparison after unfolded first-byte equality
had already proved a length-one literal. It retained the EXACT comparison input
charge/checkpoint, ordering, bounds, folded/RTL/confirmation behavior and allocations.
New production-helper tests compared every cutoff with the existing folded generic
comparison path; both directions/window edges preserve work and caller loans.
Windows/Linux focused Release checks pass per.NET10/11: Core1070/Python355/PCRE57.

ONE **70.718975647s** [public comparison](cost-summary.json), Default-process ABBA,
normal8CPU startup thenCPU2,1s/min128warmup, independent60ms targets/five samples:
12public rows/240samples, all above23ms (20ms floor), under120s. No confirmation.
Before is qualified47Core117CB3B6; after08718FBE. Python/PCRE DLL/PDBs and caller
are byte-identical. Complete arrays/ints are consumed after timing. Two separate
equal-result predecoded/decode-then-.NET Split diagnostics supplement the results.

| Public operation | Before us | After us | Pooled change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual limited Split |2.04|2.60|+27.56%|6032|
| Same finite/live |2.16|2.85|+31.99%|6296|
| Same deadline/live |2.47|2.96|+19.94%|6192|
| Dense unlimited Split |61.12|59.01|-3.44%|32848|
| Core finite family Count |29.93|28.10|-6.11%|112|
| Long-literal control |1.63|2.32|+42.31%|6528|

The pooled primary percentages are NOT established persistent regressions:
ordinary baseline process medians1.58/2.56us versus candidate2.59/2.61us are
bimodal, and finite/deadline candidate processes diverge. They establish no stable
primary gain. Long-literal baseline1.64/1.57us versus candidate2.55/2.06us retains
a cost in both candidate processes despite wide ranges. Dense Split and finite
Count improve in both candidate processes; finite Count ranges are disjoint.
Those modest gains do not justify the primary/control costs and larger code.
No universal slowdown gate or control normalization is applied.

[All observations](cost-summary.json) retain native+0.79%, Unicode Split+0.37%,
captured separators+0.07%, PCRE+4.94%, folded-0.27%, confirmed-byte+11.79%, all
process medians/ranges and unchanged allocations. No precise causal credit is
assigned to the interleaving controls. No tuned variant or further timing follows.

Untimed Release FullOpts grows4194->4433bytes (+239). Tiering is disabled only
for that separate code-generation probe, never for timing. Restore sanity passes
on Windows/Linux perTFM: Core152/Python58/PCRE18. Core is rebuilt and all matching
adapter/test graphs refreshed; source agrees with the original parent. All8Full47
assets and immutable staged packages remain intact. No package/full matrix ran.

An initial source-staging preflight found metadata at the checkout root and stopped
before Linux tests. Exact hash/path checks preceded moving those generated files
under tmp; no root scratch remains. This is unrelated to the performance decision.

Canonical off-VM `artifacts/experiments/limited-split-native-set-20261107/`:

- evidence.tar.gz: `342EF907776C985C06827D3612F8A32461608509BA25AE089DF0C89337A29314`.
- codegen-evidence.tar.gz: `CF3D1D27683E44F7B894C4A40605C1B17EFF56C994EEE5D34A2449815DAA998A`.
- restoration-evidence.tar.gz: `666366256F0DE7A074CDBE61977E21E0F402EFCE8457EEAE096E6ED473AD67A0`.
- windows-restoration.zip: `C6F58B2613F327D63BE57B1011163E12702E62A851F85604E519E8DF91E1D9E0`.

This path is closed without a distinct new cause. The existing managed checked
family implementation, metering and qualified47package evidence remain the baseline.
