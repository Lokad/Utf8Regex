# Combine common-validation vector predicates

**Rejected; production source restored.** Keeping the E1..EF range and ED
exclusion as vector predicates until one mask extraction removed two mask
transfers. It did not establish a useful public CJK Count gain. This decision
uses the benefit/cost evidence, not a universal 10% regression gate.

The trial changed four expressions in Utf8ValidationCore, covering 256/512-bit
common and mode-recovery paths. Algorithms, checked reads, continuation carry,
scalar/error offsets, allocations, work charges and interruption checkpoints
stayed identical. No helper, field, mode, engine or package was added.

Qualified dev.47 Core117CB3B6 is the baseline (source0c01d65f). Candidate
FAED9C60 is cec22747 plus patch052604BF; identical typed callerE11DA5B0 and
benchmark companions. Frozen DLL/PDB manifests are in the raw evidence.

[Untimed Release codegen](codegen-summary.json): default512 drops seven mask
transfers to five and 1414 to1409 bytes; actual256 drops seven transfers to
five but remains1402 bytes and introduces vector complement/and instructions.
The initial EnableAVX512F knob did not disable AVX-512. That ineffective check
is retained as such; only the corrected EnableAVX512=0 probe supplies narrower
coverage, explicitly reporting AVX512BW=false/Vector256=true. Tiering was disabled
only for these untimed FullOpts listings.

Before timing, Windows/Linux Release Core482/Python202/PCRE222 tests per target
pass on both .NET10/11. Forced256 adds103passing net11 checks. Existing golden
validation covers all256 byte tags, constrained continuations, offsets, vector
boundaries/tails, invalid offsets and mode recovery. Public whole-subject,
cancellation, deadline, resource and result-authority tests remain included.

ONE **65.869999055s** comparison under the120s cap: nine default-width public
rows in separate Default-process ABBA order, plus forced256 ABBA CJK/ASCII
controls. Normal eight-CPU startup then CPU2, default tiering/PGO, 1s/min128
warmup, independent60ms calibration, five samples per process. All220 producer
samples exceed20ms. Counts are checked after timing. Two equal-count BCL lanes
provide predecoded/strict decode-then diagnostics; finite controls have no BCL
resource equivalent. No confirmation or tuned variant.

[All allocations, hashes, spreads and process medians](cost-summary.json).

| Public Count | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual CJK |84.93|84.27|−0.78%|0/0|
| Actual CJK, compiled |84.10|84.95|+1.01%|0/0|
| Actual CJK alternation, compiled |102.72|101.68|−1.01%|0/0|
| Actual CJK, finite/live |378.79|397.32|+4.89%|112/112|
| Actual CJK, deadline/live |389.47|405.60|+4.14%|160/160|
| Dense three-byte neighbor |110.30|110.29|−0.01%|0/0|
| Late constrained/supplementary tail |10.30|10.10|−1.92%|0/0|
| ASCII control |2.59|2.60|+0.13%|0/0|
| Folded Russian control |235.33|229.27|−2.58%|0/0|
| Actual CJK, forced256 |141.36|144.97|+2.56%|0/0|
| ASCII control, forced256 |3.62|3.43|−5.15%|0/0|

Primary CJK ranges overlap. Finite candidate process medians417.94/379.40us
versus378.92/375.08 before, and deadline428.41/385.80 versus393.36/385.33, do
not establish a precise persistent4–5% cost. Forced256 CJK after141.45/147.20
versus141.26/141.46 before is also mixed. The tail improvement is small; the
unchanged Russian/narrow ASCII movement receives no causal credit or control
normalization. No substantial gain justifies keeping the trial.

Canonical artifacts/experiments/common-validation-mask-20261107/evidence.tar.gz,
3727303B, SHA256:
`2B3977134C4FC2C4C42BFA792F91B6686B5BC2F51B00265E56638013FFE34364`.
Exact bytes copied off the VM and verified before restoration. It retains the
source patch, caller/producer graphs, every raw sample/preflight, FullOpts
listings, both-platform tests and the failed width-knob check. Restoration
validation is recorded below. Dev.47 packages, all8Full benchmark DLL/PDB assets
and published/staged bytes remain immutable. No catalog or package refresh.

Restored source is clean on both hosts. Rebuilt Core and refreshed affected
adapter test graphs pass Core394/Python37/PCRE62 per target on both Windows and
Linux. Existing Full47 eight-asset manifest still passes. These are restoration
checks, not another package/platform matrix. Restoration archive SHA256:
`63072C49114AECA3300C0E7D377243EF1DB13D9DA744652A8AAE73D563A79CC3`.
Exact bytes are archived beside the evidence and copied off the VM.

All220 producer samples exceed57.945ms. The benchmark catalog and README tables
remain unchanged; this receipt establishes no new parity Status. Predicate-level
tuning is closed without a distinct larger saving. Further work should target a
measured operation cost or a different qualified public gap.
