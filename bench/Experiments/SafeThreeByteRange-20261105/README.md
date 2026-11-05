# Classify safe three-byte UTF-8 leads by range

**Accepted with a modest finite Count cost.** The existing common validator
classifies E1..EF except ED directly, using unsigned vector subtraction/range
comparison and one ED exclusion. E0 stays on its constrained scalar path; ED
keeps surrogate-range validation. Replace three extracted predicate masks with
two in both32/64-byte common and mode-recovery paths. Checked reads, continuation
carry, scalar/error offsets, density/idle policy, numeric accounting, allocations
and original cancellation/deadlines remain. No helper, matcher, mode, owner,
unchecked access or fused validator/search is added.

This differs from the earlier C2..DF classifier and the closed Chinese fusion,
prefix-selector and first-scalar-pair probes. First build/freeze one matching
net11 graph on clean7f0c7cf3; preserve original dev32Full bytes separately. Before
any timing, Windows/Linux focused Release Core443/Python176/PCRE165 per runtime
pass. A new golden test covers all256byte tags, four continuation limits and16
32/64-byte boundary offsets against BCL validity and scalar error offsets.
Forced256 validation adds79passing net11 checks, reusing binaries. Hardware
preflight confirms default512/BW and forced256 execution. All eight public counts
agree with independent predecoded/decode-then Regex diagnostics.

[Release codegen](codegen-summary.json), separate TieredCompilation=0 preflights,
shows one compiled validator listing:1446->1414bytes, vector comparisons8->6,
mask transfers9->7 across common/recovery code. This establishes generated-code
removal; it does not quantify public throughput. The measured run keeps default
runtime tiering/PGO, normal8CPU startup thenCPU2.

ONE **106.042s** separate-Default-process ABBA comparison, identical typed caller
and six adapter/benchmark companion DLL/PDB files; only Core changes. Producer
warmup1.5s/min16, BCL250ms/min4, independent60ms calibration, five alternating
samples/process. All160producer samples exceed **59.7095ms**. Typed Count results
are consumed after timing; even medians average their middle two observations.
[Every cost, graph hash, allocation, spread and process observation](cost-summary.json).

| Public Count | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual Chinese |91.20|88.55|−2.90%|0/0|
| Actual Chinese, compiled |91.15|88.60|−2.80%|0/0|
| Actual alternation, compiled |154.11|148.57|−3.60%|0/0|
| Actual Chinese, finite/live |1193.16|1207.77|+1.22%|112/112|
| Dense three-byte neighbor |114.28|113.55|−0.64%|0/0|
| Late constrained/supplementary tail |11.07|10.72|−3.15%|0/0|
| ASCII control |2.65|2.66|+0.17%|0/0|
| Folded Russian control |245.75|238.31|−3.03%|0/0|

Actual Chinese ordinary process medians91.19/91.20us before versus88.71/88.40us
after consistently support a modest gain; compiled does likewise. Retain the
finite cost of about14.6us and alternation's wider before spread. The unchanged
folded fused counter moves239.70/247.22 before versus245.37/227.32 after; its
variation is not causal credit or a normalization factor. Accept the simpler
predicate and consistent primary gain with these local costs/uncertainty. No
confirmation or tuned variant follows. BCL lanes diagnose equal-count overlapping
semantics; finite budgets have no BCL resource equivalent. A new catalog Status
requires a separate clean-source selected refresh.

Immutable artifacts/experiments/safe-three-byte-range-20261105/evidence.tar.gz:
`E576B47846603AD4BABC6F46E0ACCE9B3BD18FAED931832ED61C4ED6A5F9869F`.
Raw includes original/full/current graphs, declaration8491AA44, patch0DC0D941,
preflights, forced-width tests, codegen listings, caller and every sample/log.
Windows correctness archive SHA256: `6F33AA11F923B3264D02E7428A9186F132A5BCC714CEA6301C85D535D17F42DB`.
Selected-refresh evidence is recorded separately after clean-source qualification.

Existing staged/published packages, symbols and the dev32Full receipt remain
immutable. This is a small source change; no new package/Full matrix follows.
