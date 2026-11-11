# dev.70: Count and POSIX endpoint improvements

Frozen producer `c69edb3e83321860945827f90a72f8e92c5bfba2`; all four profiles at
0.5.0-dev.70, .NET 10/11, exact Core pins. Immutable packages and symbols are in
artifacts/nuget. Windows and Linux each pass 111,768 detached checks across ten
fixtures. Linux consumed the Windows-produced bytes without rebuilding or
repacking. Original copied Awk/Lython probes pass 19/118 checks per target/OS,
including public API probes and final shared-root loans of zero. This establishes
package compatibility, not downstream adoption or POSIX consumer acceptance.
Nothing is published; downstream checkouts are unchanged.

One complete quiet-VM refresh passed **47m05s** (2825.205s/3600s), all 85 existing
steps: one Release build, both-runtime correctness/disabled dynamic code, four
catalogs, paired comparators, lifecycle/scaling, resource checks and verification.
Nine matching-source snapshot checks per target pass on each OS. Frozen-source
CI43015263562 is green on Windows/Linux. The [Full receipt](full-refresh.json)
and [acceptance](acceptance.json) preserve identities and off-VM archive hashes;
all 412 earlier canonical NuGet files remain unchanged. A completed Full does
not qualify every individual timing or scaling fit.

The batch restores the existing complete-class counter's precedence and lets
Count ignore unobserved plain capture markers around complete positive greedy
scalar repeats in Core/PythonRe/Pcre2. POSIX reuses history pruning at proved
non-EOF ASCII/scalar alphabet ceilings. These retain actual input, captures,
validation policy, interruption, resource admission and result/cursor ownership.
Focused causal evidence and costs remain in the [precedence](../TerminalClassPrecedence-20261111/README.md),
[captured Count](../CapturedRepeatCount-20261111/README.md),
[ASCII ceiling](../PosixAlphabetCeiling-20261111/README.md) and
[scalar ceiling](../PosixScalarCeiling-20261111/README.md) receipts.

Ordinary catalogs use **Validate**. Ratios are managed/reference execution time;
lower is better. These separate Full observations include comparator movement
and are not causal A/B results. Acceptance also compares the fixed dev.66 imported
baseline `7b0cc1c3`, producer `b478f853`.

| Public operation | dev.69 → dev.70 ratio | Current managed cost |
|---|---:|---:|
| Core bounded English repeat, compiled reference | 4.033 → 0.602 | 1110.312 µs |
| Core name-family Count, compiled reference | 0.552 → 0.559 | 1694.196 µs |
| Core river-window Count, compiled reference | 1.426 → 1.416 | 2218.932 µs |
| Python many-capture strings | 2.288 → 2.242 | 786.987 µs |
| Python one-capture strings | 2.458 → 2.499 | 534.457 µs |
| Python captured split | 1.749 → 1.636 | 415.278 µs |
| POSIX SKU, 4096 calls | 4.001 → 4.021 | 2754.996 µs |

Warm Python strings remain over 2×; earlier selective lower timings are not the
current catalog. Strict Core email (2.534×) and Chinese literal (3.446×) remain
gaps; AssumeValid's distinct input contract must stay labeled. POSIX SKU remains
NativeFaster (~0.673 µs/call). General unresolved-parent histories and opaque wide
consumers still need architectural work. Five tiny Python rows are unqualified;
PCRE2 retains 17 inconclusive and 25 excluded rows. Preserve lifecycle uncertainty,
all exclusions and the focused receipts' adverse controls; no universal parity or
performance-completion claim follows this checkpoint.

Raw outputs are in artifacts/benchmark-refresh/current; immutable Full/Linux
archives and snapshot logs are verified off the VM under
artifacts/experiments/delivery-dev70-20261111. Resume supported shared execution
and history work without repeating closed trials or qualifying unchanged packages.
