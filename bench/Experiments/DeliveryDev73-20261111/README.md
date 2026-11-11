# dev.73: coherent managed-engine delivery

Frozen producer `eae880c88f7a54ed4331715c2fd8f4b0fc6e6d63`; all four packages at
0.5.0-dev.73, exact Core dependencies, .NET 10/11. SDK 11.0.100 and benchmark
runtime 11.0.0 are unchanged from [dev.72](../DeliveryDev72-20261111/README.md).
No packages were published or downstream checkouts changed.

One complete quiet-VM refresh passed in **47m17s** (2836.549s/3600s), all 85
existing steps: Release build, both-target tests and disabled dynamic code,
four catalogs, paired comparators, lifecycle/scaling, resource checks and
verification. The [Full receipt](full-refresh.json) records the scope.

Windows and Linux each pass 112,028 detached checks across ten fixtures. Linux
consumes the identical Windows-produced packages without rebuilding or repacking
producers. Original copied Awk/Lython handoffs pass 19/118 checks per target/OS,
including public producer probes and zero final shared-root loans. This establishes
compatibility, not downstream adoption or POSIX consumer acceptance. Nine generated
table checks per target/OS pass. Source CI 43107172937 is green on Windows/Linux.
All 436 earlier staged packages/symbols retain their hashes; eight new files match
their canonical candidate feed. Detailed identities are in [acceptance.json](acceptance.json).

The batch includes [category/letter Count fact removal](../CategoryCountFacts-20261111/README.md),
[Python logical failure boundaries](../PythonResultBoundaries-20261111/README.md),
[POSIX fixed-scalar parent bounds](../PosixScalarSuffix-20261111/README.md) and
[validator predicate reduction](../ValidationMaskReduction-20261111/README.md).
Those causal receipts retain their gains, adverse controls, allocations and
uncertainty. The separate Full observations below cannot be composed into a
causal cumulative percentage; fixed66 also used the earlier RC3 runtime.

Ordinary catalogs use **Validate**. Ratios are managed/reference execution time;
lower is better. AssumeValid remains a separately labeled caller contract.

| Public operation | Fixed66 time | GA72 time | Current time | Current ratio |
|---|---:|---:|---:|---:|
| Core name-family Count, compiled reference |4048.463µs|1597.617µs|1615.196µs|0.527|
| Core river-window Count, compiled reference |3263.030µs|2258.409µs|2220.326µs|1.460|
| Core symbol Count, compiled reference |1840.222µs|1900.963µs|1875.893µs|1.501|
| Core email Count, compiled reference |1161.738µs|1182.234µs|1169.228µs|2.521|
| Core Chinese literal, compiled reference |83.002µs|84.001µs|79.753µs|3.383|
| Python many-capture strings |778.585µs|783.795µs|785.660µs|2.229|
| Python one-capture strings |474.299µs|539.638µs|499.578µs|2.340|
| Python named-capture strings |169.995µs|157.591µs|144.269µs|1.527|
| Python captured split |440.259µs|416.313µs|388.132µs|1.548|
| POSIX SKU, 4096 calls |3251.532µs|2720.972µs|2567.145µs|3.880 observed|

The full refresh **does not reproduce the focused many-string median gain**.
One-capture strings remain slower than fixed66. Core retains 53 gap rows and six
tie candidates; email, river and Chinese validity-sensitive gaps remain material.
Current AWSquick is 1.034×/3711.281µs; URI is 1.279×/1407.919µs. No parity-complete
claim follows from the package acceptance.

Python retains 45 managed-faster, 29 CPython-faster, nine inconclusive, two equivalent
and five tiny harness-floor-unqualified rows. PCRE2 retains 79 managed-faster,
12 native-faster, nine inconclusive, one equivalent and 25 excluded rows. Its largest
current absolute deficit is about 0.150µs, including enumeration rows. Preserve
lifecycle uncertainty and rejected scaling fits too.

POSIX SKU is **Inconclusive** because lane-order median ratios differ by 2.26%,
despite the observed 3.880 ratio. Its current batch is about 0.627µs/call and 22.345B
per 4096 calls, retaining pool-fill observations. Do not selectively retime it to
change that verdict. Five other comparable rows are native-faster, one equivalent
and one managed-faster; eight history rows remain excluded from native capture
equivalence. Nine finite-policy measurements retain their operating costs.

Raw Full and Linux acceptance archives are verified off the VM under
artifacts/experiments/delivery-dev73-20261111. SHA-256:

- Full: `3BB704F6C120FE8B7D8EA9BF4A35607D26A46FA0B559A22CB27231241DE7779A`
- Linux: `C45BD446CAF9CB18E56E6578F6CB24E94647627BF2404D79C69C17607FCD75A5`

Every qualification, collection and snapshot worker completed successfully.
Next investigate reusing the existing alphabet ceiling for once-entered POSIX
parents with variable tails, requiring a dominance proof and independent history
oracles before one bounded public comparison. Current dev.73 packages demonstrate
fourteen 100001/100000 work refusals and two fixed-tail successes on that untimed
probe; this is an opportunity, not a qualified optimization. Keep warm Python
output, default validation costs and unresolved general history competition open.
No new Full/package qualification follows a small source step.
