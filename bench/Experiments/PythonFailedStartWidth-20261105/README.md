# Failed Python search-start advancement: reverted

**Closed without promotion.** The prototype used the validated lead byte solely
to advance after failure, replacing a repeat Rune decode whose scalar was discarded.
It preserved complete-subject validation, scalar candidate/match boundaries,
captures, empty retry/EOF, unchanged-subject lifetimes, all numeric work/admission
and original-token/deadline checkpoints. No helper, selector, mode or allocation.
This does not reopen the closed small-class native-rendering experiment.

[Release FullOpts](codegen-summary.json) removes one DecodeFromUtf8 call/status
path and shrinks MoveNextCore1665->1570bytes. This is codegen evidence, not a
public speedup. Windows/Linux each.NET10/11 pass927focused candidate checks,
including four added empty-progression/captured-separator cases spanning all four
UTF8 widths, a nonzero offset and EOF. Existing malformed-tail, finite resource,
original interruption and cursor/retained-result checks remain.

ONE **110.204s** ordinary-Default-process ABBA, unchanged Core Count FIRST;
matching0dc graph on data-only parentde4d0d37, one producer graph per process.
Only Python DLL/PDB differs; six Core/PCRE/benchmark companions and typed caller
are identical. Normal8CPU startup thenCPU2; producer1.5s/min16, BCL250ms/min4,
independent60ms calibration, five alternating samples. All160producer samples
exceed52.1436ms; typed results are retained during timing and digested afterward.
[All samples, process medians, allocations and identities](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core Count |2.520|2.485|−1.36%|0/0|
| Actual Unicode Split |124.362|126.713|+1.89%|19512/19512|
| Same finite/live |158.131|159.079|+0.60%|23712/23712|
| Same deadline/live |210.506|214.820|+2.05%|23760/23760|
| Captured Unicode FindAll |222.420|228.277|+2.63%|25248/25248|
| Long astral Split |1741.020|1683.317|−3.31%|49272/49272|
| Empty-retry Split |93.646|93.201|−0.48%|8064/8064|
| ASCII Split neighbor |71.197|78.679|+10.51%, unstable|43088/43088|

Actual Split's after-process medians121.327/132.232us straddle both before
124.443/124.306. ASCII's after71.130/87.073us likewise has substantial process
movement; its path bypasses the changed advance, so the pooled10.51% is not an
established causal regression. Do not normalize these costs using controls.
Captured FindAll's two after medians consistently cost about5.9us more; long
astral Split saves about57.7us consistently. These do not establish a useful gain
on the targeted large catalog gap. Code removal alone does not justify promotion
or another timing trial. No confirmation, tuning, selected refresh or catalog
Status; rejection uses no universal10% gate.

The initial candidate build reused Core references; focused tests rebuilt them
and changed Python bytes. A pre-timing identity guard stopped before A0. Preserve
the initial graph/codegen, freeze the tested Python bytes and repeat only untimed
preflight/codegen: same1570bytes/no decode call, all eight public digests agree.
No source change, performance restart or extra build followed that correction.

Immutable artifacts/experiments/python-failed-start-width-20261105:

- evidence.tar.gz: `7A9460E642CEB07023CF5AF8F685CA9B58A0E956527827AA6EF26B9F1E13BECA`.
- windows-correctness.zip: `BE27964B5E9B1E80218DB519832868DA8C65088028F0A90481E88496A177D209`.

DeclarationA7BC0B4F/patchC622701A, source/tests, before/after and initial graphs,
worker, preflights, original codegen, every observation and terminal0 remain.
Production and candidate-only test edits are restored together; Windows/Linux
each.NET10/11 pass923focused restored checks. The eight measured benchmark
DLL/PDBs stay unchanged. Linux restoration.tar.gz SHA256:
`D99F9D77EA13C0058B7B227639939285867E077087CA9C6860130883CA0A9F0B`.
Windows-restored.zip SHA256:
`9451E530D8EF01D72FBA3C74F11CA504FBA13D6730DA34059B8FD045A37481CB`.
Packages/symbols, snapshots, old observations and
the43m07.82s Full remain immutable; no package/platform matrix is repeated.
