# POSIX leaf capture histories

Omit fictive tags inside a public capture with no nested public captures. Equal
occurrence boundaries leave no observable choice inside it. Keep that capture's
full history, nested public histories, and the uncaptured prefix/sibling tags
that determine POSIX priority. Construction traversal remains admitted and
interruptible; no executor, mode or Core algorithm changed.

One quiet-VM .NET 11 RC3 separate-process ABBA completed in **44.665 seconds**:
11 public rows, 220 samples, independent 75 ms calibration, 500 ms warmup and
five samples per process/row, with a 20 ms floor. The same worker and Core DLL
were used throughout; before is measured source `053ec0de`, after adds only the
PosixProgram change. Exact DLL/worker hashes and the applied patch remain in the
ordinary ignored raw evidence. Inputs, compiled regexes and capture destinations
are prepared before timing. Full result and loan checks occur after timing.

| Public operation | Before µs | After µs | Change |
| --- | ---: | ---: | ---: |
| Nullable captures | 2.209 | 1.590 | -28.0% |
| SKU captures × 4096 | 8308.975 | 5458.344 | -34.3% |
| Ambiguous captures | 1.690 | 1.079 | -36.2% |
| Repeated captures | 4.132 | 2.123 | -48.6% |
| Unicode captures | 1.033 | 0.859 | -16.8% |
| SKU finite/live × 4096 | 10911.632 | 7777.590 | -28.7% |
| SKU funded finite/live × 4096 | 11916.221 | 8409.817 | -29.4% |
| SKU finite/deadline/live × 4096 | 13133.475 | 9501.494 | -27.7% |

All eight changed rows improve in both processes, with disjoint sample ranges
and fewer allocations. Unlimited SKU allocation falls from 1,114,112 to 983,040
bytes per batch; finite/funded from 9,601,024 to 4,980,736. Funded working peak
falls from 1,776 to 1,008 bytes; final outstanding loans are zero. These are
modeled working charges and CLR allocation traffic, not total retained heap.

Unchanged POSIX IsMatch +1.29%, ambiguous Count -0.80% and Core literal Count
-0.71% have overlapping ranges; no normalization or causal claim. All process
medians, ranges, allocation medians and working peaks remain in
[summary.json](summary.json). No confirmation was needed. Do not compose these
changes with the earlier native catalog to manufacture a new parity ratio.

Windows POSIX 92 and Linux 91 checks per target pass (Linux lacked the new
source-only summary test), including independent selection fixtures, nested and
repeated captures, strict UTF-8, interruption, admission/refund and aliases.
The corrected local summary test passes on both targets; its initial failure
was a table-cell index error, fixed before qualification. No package repacking.

Raw data: `artifacts/experiments/posix-leaf-tags-20261108/vm-evidence.tar.gz`, SHA-256
`EC036E8A6DD1072DE61D87907DC5DB00558874AF5F2D8052F5134B2F604BE5BE`.
This is a local evidence location, with no upload or durable archive prerequisite.
Further tuning is deferred to delivery. The final four-profile Full refresh and
fresh immutable package qualification remain separate acceptance steps.
