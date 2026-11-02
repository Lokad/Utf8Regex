# Skip impossible nonempty boundary retries

**Accepted**, source `cb1d443e`. A complete capture-free `Boundary, Accept`
backtracking program cannot consume input on its nonempty retry after an empty
success. Reuse the existing failed-outcome path for that retry: scalar advancement,
EOF checks and workspace cleanup remain in one place. Actual candidates still
run the original VM. Captured, consuming, lookaround and NonBacktracking programs
retain their retries. No field, plan, matcher, mode or allocation was added.

Whole-subject strict UTF-8, boundary membership, scalar offsets, original
cancellation/deadlines, aliases and funded closure remain. A previous successful
attempt already initialized the reusable workspace; this impossible retry has no
fresh major reservation or capture state. SPEC documents lower work requirements
from eliminated instructions, with byte/depth/result admission preserved.

One quiet-VM .NET11 **14-case public comparison took60.066s**, under120s,
minimum timed lane **40.080ms**. Before uses frozen `ea01c3cd` Full-refresh
binaries; data-only parent `ef929276` has identical producer sources. Only PythonRe
was rebuilt with the archived patch; **Core DLL/PDB bytes match**. Normal8-CPU
startup then CPU2, managed1s/min128 warmup, BCL250ms/min64, independent60ms
calibration, five alternating samples and complete output batches checked after
timing follow the existing comparison machinery. Retained operation payload is
capped at64MiB per batch. .NET predecoded/decode-then controls use overlapping
semantics and explicit strict-ASCII boundary formulas where needed.

| Public operation | Before us | After us | Change | Allocated B, both |
| --- | ---: | ---: | ---: | ---: |
| ASCII boundary Split, catalog subject |239.630|194.866|-18.68%|40016|
| Finite limits/live token Split |307.207|240.956|-21.57%|44216|
| Deadline/live token Split |369.133|331.106|-10.30%|40176|
| ASCII boundary on mixed Unicode Split |128.206|115.935|-9.57%|15448|
| Neighboring2176B boundary Count |207.312|154.038|-25.70%|0|
| Captured-boundary control |325.755|340.940|+4.66%|64760|
| Non-boundary control |422.453|440.331|+4.23%|78808|

All allocation medians match. Largest adverse median **4.66%** fits the allowance;
unchanged Core ordinary/NB controls move+0.29/+0.15%. No equivalence claim is made
for controls with measured cost changes. **No noise confirmation or variant.**
[All observations](comparison.json) remain separate from cumulative acceptance
against fixed original `a40bda03`, which waits for the next coherent delivery batch.

Focused214/runtime, funded-close2/runtime and complete **4623/runtime** PythonRe
Release checks pass on .NET10/11. New witnesses cover independent scalar positions,
retained aliases, original interruption at EOF, parent-loan refunds and an
empty-then-consuming alternative that must retain its retry. The existing funded
cursor/host-wait/closure test now includes a boundary-only root. Two test compile
mistakes and a scratch driver variable-name collision were corrected before timing;
their logs are preserved. The runtime prototype remained unchanged throughout.

Existing selective raw refresh and nine-pair CPython qualification ran on clean
`cb1d443e` for exactly two catalog rows:

| Case | Previous recorded ratio | Fresh ratio [95% interval] | Status |
| --- | ---: | ---: | --- |
| split/zero-width |4.322|2.936 [2.912,2.953]|CPythonFaster|
| zero-width/count |1.741|1.101 [1.097,1.106]|CPythonFaster|

The catalog Count subject is17408B, larger than the parent comparison's2176B
neighbor. Previous/selected identities and uncertainty are retained in
[previous ratios](previous-ratios.json) and [selected ratios](selected-ratios.json).
Catalog movement includes comparator/context movement and is not the isolated
parent speedup. Other88 cases and catalog/corpus/lifecycle/scaling data were
checked unchanged. Two imported generated files match VM hashes; snapshot/API
guards pass on both runtimes with matching binaries.

[Verification and raw archive hashes](verification.json). Inputs, DLL/PDBs,
driver, complete measurements, source/tests and selected clean-source logs are
sealed under `artifacts/experiments/python-boundary-retry-20261102/`. The staged
dev.12 packages and42m04s frozen-source delivery evidence remain unchanged.
No package/platform matrix was repeated. Remaining N4 gaps and the next complete
fixed-original/delivery gates remain open.
