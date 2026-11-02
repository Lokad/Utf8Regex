# One numeric working-memory ceiling

**Accepted.** `Utf8SliceInvocation.TryReserveWorking` already checks the budget's
numeric ceiling before entering the storage ledger. Remove the ledger's duplicate
ceiling field, constructor argument and comparison. The ledger still owns live
bytes, overflow checks, caller-funded reservation/refund and transfer; the budget
still owns numeric policy, peaks, original cancellation/deadlines and sticky
denial. Production has exactly one ledger-reservation caller, behind that budget
check. No execution algorithm, SIMD, checkpoints, owner or public API changed.

The five workspace/preparation consumers retain the same numeric-cap-and-funding
predicate, now derived from the existing invocation. PCRE2 replacement preparation
subtracts held storage from the budget's ceiling. It keeps cold construction
admission, overlapping loans and its original error mapping. Direct fixture cap
checks now exercise production invocation admission, including sticky failure
across aliases and after refund. Standalone ledger overflow/refund checks remain.

Actual `Unsafe.SizeOf<T>()` queries on the captured assemblies show ledger
**24 -> 16 bytes** and invocation **128 -> 120 bytes**; budget stays 80 and limits
32. Original `a40bda03` sizes were ledger 16/invocation 120, without caller funding.
No manual size estimate or hard-coded replacement charge was introduced.

One quiet-VM .NET 11 RC3 **15-case public comparison took 70.536s**, under its
120-second cap; minimum timed sample **46.212ms**. Original `a40bda03`, parent
`78f07c53` and parent plus the archived seven-source-file patch load separately.
The existing protocol retains normal startup then CPU2 affinity, 500ms/minimum
128-call warmup, independent 60ms calibration, five alternating samples, complete
public outputs consumed after timing, allocations and equal-output compiled Regex
predecoded/decode-then diagnostics. No confirmation or variant was needed.

| PythonRe public operation | Before us | After us | Change | Before/after allocated B |
| --- | ---: | ---: | ---: | ---: |
| Native Search hit |0.076|0.078|+2.61%|0 / 0|
| Native FullMatch hit |0.060|0.061|+0.78%|0 / 0|
| Captured Unicode FullMatch |98.738|97.753|-1.00%|79616 / 79616|
| Folded FullMatch |80.969|81.665|+0.86%|85848 / 85848|
| Suffixed FullMatch |37.761|36.458|-3.45%|43752 / 43752|
| Finite/live captured FullMatch |109.461|109.528|+0.06%|79736 / 79728|
| Deadline/live captured FullMatch |110.653|110.333|-0.29%|79784 / 79776|
| Nullable atomic Search |1.078|1.088|+0.88%|3552 / 3552|
| Unchanged native Count |0.110|0.108|-2.11%|0 / 0|

Every allocation median is unchanged or lower. Largest adverse current median
is **2.61%**, within the predeclared allowance for this verified simplification;
no throughput win was required or inferred from object sizes. Original ratios
remain explicit in the complete summary: suffix **+10.87%**, nullable atomic
**+18.25%** still fail delivery's cumulative allowance. No package/cumulative
acceptance or benchmark Status update follows from this focused experiment.

Release correctness passes both .NET 10 and 11: focused Core **1043/runtime**,
complete PythonRe **4600/runtime**, affected PCRE2 **310/runtime**. Coverage includes
numeric denial before host funding, exact finite cutoffs, overflow, shared aliases,
parent loans, deterministic close/refund, retained results, original interruption
and PCRE2 preparation. Funded tests are correctness evidence, not funded-throughput
measurements. An initial Core run exposed a fixture still testing numerical
denial directly on the ledger. That and the analogous string-getter fixture were
migrated; initial failure, focused repair pass and final combined pass are saved.
Production runtime was unchanged after its one performance run.

Source CI on `cb4320f5` caught eight benchmark-only references to the removed
ledger property in two replay/portfolio tools. The initial audit covered shipped
source and tests but missed those consumers. They now use the identical invocation
predicate too; the affected benchmark project is built for both targets. This
repair changes no shipped runtime and does not require another performance run.

Raw driver/protocol, all DLL/PDB sets and hashes, samples, actual-size probe,
complete patch and test logs:
`artifacts/experiments/working-ceiling-20261102/evidence.tar.gz`, SHA256
`E76A545C6B3CCAA89DA405CF721ED4463BE081126955D21B8E94B856A47C77E9`.
Changed source, fixtures and validation logs: `correctness-source.zip`, SHA256
`8D86C7137BF3F1135AD6D7E9C701FFFAC1840148B0A6173B38950D1E1674813F`.
