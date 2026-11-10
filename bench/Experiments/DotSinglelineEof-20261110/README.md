# Complete greedy Singleline EOF tail

Accepted after the LF scan. Its unchanged Singleline control still built one
rewind alternative per scalar before accepting EOF. The existing endpoint proof
now recognizes an unbounded greedy DotAll with minimum0/1 and a complete end
anchor/capture-end/Accept tail. At EOF that tail always succeeds. Required
assertion endpoints, lazy/bounded/RTL runs and consuming tails stay opaque.

Reuse the same runner and2048-byte work/interruption loop, without a stop search
for committed DotAll. No new engine, metadata, cache, buffer, mode or ownership
contract. Capture instructions still execute. Whole-input strict validation,
scalar endpoints, original token/deadline, useful admission and synchronous
refunds remain. Removing useless rewinds lowers work/depth/working requirements;
exact former cutoffs are not promised. [Resource contract](../../../docs/SPEC.md#cancellation-deadlines-and-resource-limits).

One **92.094s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
23 public lanes,460 samples, minimum67.216ms. Reuse the accepted LF candidate's
exact Core DLL/PDB and adapters as the before graph (Core source compiled from
`c6de14e1` plus the subsequently committed LF patch). The after graph changes
Core only; common adapters/caller and distinct loaded Core hashes are checked.
Actual Singleline code shape, changed runner IL, complete results and zero loans
are verified. No confirmation, new native ratio or package/catalog/Full repeat;
dev63 packages and tables stay immutable.

| Public operation | Before (µs) | After (µs) | Change | CLR B/op before→after |
|---|---:|---:|---:|---:|
| Singleline long Count |2844.669|2.574|-99.9%|2966936.0→0.0|
| Singleline short, finite |94.009|0.463|-99.5%|93976.0→112.0|
| Singleline short, funded |97.709|0.393|-99.6%|93976.0→112.0|
| Singleline short, deadline |102.810|0.603|-99.4%|94024.0→160.0|
| Python Singleline strings |3049.353|18.845|-99.4%|3098363.9→131235.1|
| PCRE2 Singleline Count |2808.576|2.381|-99.9%|2967208.0→64.0|
| Imports control |9.311|9.301|-0.1%|0.0→0.0|
| LF detailed control |182.381|192.878|+5.8%|271290.0→271279.5|
| Python many-string control |735.955|771.208|+4.8%|242008.0→242008.0|
| Core literal control |0.886|0.877|-1.1%|0.0→0.0|

The long subject has about65KiB; controlled Singleline subjects use about2KiB,
fitting the existing4096-frame policy before the change. Compare each lane with
its own before producer. Long Count/PCRE2 Count eliminate roughly2.97MB per call;
controlled Count drops93976→112B (deadline94024→160B), and funded working peak
93248→0B. Python retains the actual returned string storage. These are removed
rewind buffers, without additional accounting precision.

Accept the major Singleline benefit with unchanged LF detailed captures+5.8%,
Python many strings+4.8%, funded LF Count+3.1% and short LF Count+3.0% movements
retained. No attribution or normalization by controls. The imports gain from the
previous change remains in this comparison. [Summary](summary.json) retains every
lane, process and range; pooled figures are not composed into fresh native ratios.

Core 4,684 focused and complete PythonRe 5,012/PCRE2 6,647 Release checks pass per
target/platform, Windows/Linux .NET10/11. New public cases cover complete Unicode/
NUL/newline captures under depth1, retained values and lazy/consuming-tail choices.
Original-token/deadline interruption now explicitly covers Singleline as well as
LF scans. Existing required-lookbehind endpoints, bounded/overlapping/RTL and
every-work-cutoff/refund checks pass. The23-lane local and both VM preflights
passed before the sole timing; no production edit followed it.

Before Core `A244CC778683660166D54D44B84EF1EA490EA4ED4CE7A6352E55D7BDA7742DB0`;
after `DBC0B099BD54D0FCE9F4608D4B0C27BDD365FE8D53426C99A3E465E92F0800F8`.
Raw patch/protocol, graphs, preflights, Linux checks and samples verified offVM:
`artifacts/experiments/dot-singleline-eof-20261110/evidence.tar.gz`, SHA-256
`0106E451A61E5883343BEEA011A855E58AEA730816D0D21C6BFC561F35C5EF64`.
Windows checks/preflight are alongside it. Source CI42749510788 passes
Windows/Linux for81d1066d89a5f0ecbcbdcab31d2bf406eef25e94.
