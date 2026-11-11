# dev.72: stable .NET 11 baseline

Frozen producer `f66205321f29c9bec01118b5257283af8b699f10`; all four packages at
0.5.0-dev.72, exact Core dependencies, .NET 10/11. The SDK is 11.0.100 and the
benchmark runtime is 11.0.0. Historical RC3 packages and measurements remain
unchanged. No packages were published.

One complete quiet-VM refresh passed in **47m40s** (2860.057s/3600s), all 85
existing steps: Release build, both-target tests and disabled dynamic code,
four catalogs, paired comparators, lifecycle/scaling, resource checks and
verification. The [Full receipt](full-refresh.json) records the scope. This is
the stable-runtime baseline for subsequent experiments, not a causal comparison
with RC3: the SDK/runtime and POSIX scoped-state implementation changed.

Windows and Linux each pass 111972 detached checks across ten fixtures. Linux
consumes the identical Windows-produced packages without repacking. Original
copied Awk/Lython handoffs pass 19/118 checks per target/OS, including public
producer probes and zero final shared-root loans. This establishes compatibility,
not downstream adoption or POSIX consumer acceptance. Nine generated-table checks
per target/OS pass. Source CI43078894599 is green on Windows/Linux. All 428 earlier
canonical packages/symbols retain their hashes; the eight new files match their
candidate feed. Detailed identities are in [acceptance.json](acceptance.json).

Ordinary catalogs use **Validate**. Ratios below are managed/reference execution
time; lower is better. AssumeValid remains a separately labeled caller contract.

| Public operation | GA ratio | Managed time |
|---|---:|---:|
| Core bounded English repeat, compiled reference | 0.602 | 1115.943µs |
| Core name-family Count, compiled reference | 0.531 | 1597.617µs |
| Core river-window Count, compiled reference | 1.444 | 2258.409µs |
| Core symbol Count, compiled reference | 1.525 | 1900.963µs |
| Python many-capture strings | 2.273 | 783.795µs |
| Python one-capture strings | 2.534 | 539.638µs |
| Python captured split | 1.632 | 416.313µs |
| POSIX SKU, 4096 calls | 3.887 | 2720.972µs |

Core retains 54 gap rows; strict email is 2.596×, Chinese literal 3.455× and
AWSquick 1.059×. Python warm output remains material. Seven tiny Python rows are
unqualified because removable harness floors are too large; their observations
do not establish parity. PCRE2 retains nine native-faster, fourteen inconclusive
and twenty-five excluded rows. POSIX retains six native-faster, one equivalent
and one managed-faster comparable observations; eight history rows remain
excluded from native capture equivalence. Preserve lifecycle uncertainty and
rejected scaling fits too.

The [scoped-state causal receipt](../PosixScopedState-20261111/README.md) establishes
the small synchronous POSIX allocation savings and its overlapping +3–6% cursor
medians. Current SKU allocation is 22.345B per 4096-call batch, retaining pool-fill
observations. The [preceding delivery](../DeliveryDev71-20261111/README.md) links
older causal gains and accepted costs; these new observations do not erase them.

Raw Full and Linux acceptance archives are verified off the VM under
artifacts/experiments/delivery-dev72-20261111. SHA-256:

- Full: `4395FCC707ABCE9A1A91AE5999A83D4766B54D959C0C5BDBA44EF6E5FCEA6083`
- Linux: `31B42EFD2013FE6C4F9AC478D3EF88DB46BFD0C6A02511765D545EDBEEE16CAF`

All qualification, collection and snapshot workers completed successfully.
Next investigate removing supplementary-only metadata scans from ordinary/funded
category and letter Count, retaining strict validation and the existing scalar
counters. Keep warm Python output and general POSIX history work active. No new
Full or package qualification follows a small source change.
