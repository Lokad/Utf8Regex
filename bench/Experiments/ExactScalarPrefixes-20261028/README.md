# Exact scalar prefixes: AWS quick

The simple-pattern analyzer retained covering literal prefixes but omitted their
start-guidance proof. Mixed UTF-8 Count consequently tried the scalar VM at every
scalar. b119df56 enables the existing exact-prefix candidate search; the VM still
verifies candidates and decides alternation, captures and progression. Later
literals and folded prefixes are not promoted. No new executor, allocation
mechanism, unchecked operation or resource switch was added.

[Comparison](comparison.json), [console](comparison.txt), [driver](compare.cs.txt),
[project](compare.csproj.txt), [selective five-sample refresh](readme-refresh.txt).
Ubuntu 24.04.4, Xeon Platinum 8573C, .NET 11 RC3, CPU2. VM-built baseline a40bda03
and candidate b119df56 load in one process. The frozen catalog supplies patterns,
options and input. Public results are checked against .NET before and during
timing. Four lanes retain independent counts, raw samples and allocations;
three samples alternate order after warmup. One 120-second-capped comparison,
completed in **56.24 seconds**, with no confirmation or favorable rerun.

| Public Count | Before | After | Speedup | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| AWS quick, 32.5 MB, ordinary | 3,906 ms | 11.783 ms | 331.5x | 3552 / 3552 |
| AWS quick, compiled | 3,898 ms | 11.721 ms | 332.5x | 3552 / 3552 |
| 32 KiB slice, finite limits and live token | 4.512 ms | 7.995 us | 564.3x | 2048 / 112 |
| Mixed-scalar dense hits, 1024 matches | 3.111 ms | 1.079 ms | 2.9x | 3552 / 3552 |
| Unchanged compiled English literal | 69.865 us | 70.242 us | +0.5% elapsed | 0 / 0 |

The paired experiment's compiled ratio against predecoded .NET was 3.00x; the
separate five-sample README protocol measured 19.537 ms and **4.87x**, versus the
previous 1070.25x. Its ordinary ratio is **1.19x**, versus 391.40x. Both receipts
remain: different timing loops yielded different candidate costs, and the README
uses its own measurement. These data establish a large improvement, not exact
cross-harness equivalence or parity on every workload. No additional campaign
was used to select the faster number.

[Affected .NET 11 checks](affected-net11.txt) and [.NET 10 checks](affected-net10.txt)
pass, covering prefix overlaps, byte offsets/window boundaries/tails, false
candidates, captures retained across Count, strict malformed-tail rejection,
working/work admission and original-token cancellation. The existing publication
tests also retain their final cancellation/deadline authority. A missing prefix
can finish without VM workspace; a surviving candidate still needs admitted
buffers. Resource-denial thresholds are not version-stable API values.

Exact measured candidate/benchmark binaries, PDBs, driver, raw outputs and
selective snapshots are retained in the
[local archive](../../../artifacts/experiments/exact-scalar-prefixes-20261028/parity-aws-b119df56.tar.gz),
SHA256 `2FA8315A63BB67369D299297412DAE8CF8E1D41BFBE22923DDDB95CF01ED1C33`.
The immutable a40bda03 baseline is also in the previous sub-hour archive.
No package qualification or complete catalog refresh was repeated.
