# Use the existing ASCII class representation

POSIX brackets previously had an empty ASCII mask and a scalar table, even for
fixed ASCII named classes. Populate the existing mask, retain only non-ASCII
ranges, and omit the category-table descriptor when no such ranges remain.
No matcher or Core primitive changes. Range merging, negation, newline behavior,
strict scalar decoding and construction admission/interruption remain.

One quiet-VM .NET 11 RC3 ordinary-process ABBA completed in **36.533 seconds**
under a declared 180-second cap. Fixed before is the accepted whole-output
candidate; after changes only PosixParser. Same Core and worker DLLs, normal
startup before affinity, 500 ms row warmup, independent 75 ms calibration and
five samples per process with a 20 ms floor. Check complete results/loans outside
timing. SKU remains a historical adapter control, not a new Core headline row.

| Public operation | Before µs | After µs | Change |
|---|---:|---:|---:|
| SKU captures × 4096, unlimited | 5593.20 | 5132.57 | -8.2% |
| Same, finite/live | 6901.15 | 6492.79 | -5.9% |
| Same, funded finite/live | 7588.69 | 7131.93 | -6.0% |
| Same, finite/deadline/live | 8161.93 | 7622.55 | -6.6% |
| Construct SKU regex | 10.91 | 10.96 | +0.4% |

Both after-process medians improve in every matching lane. Finite-lane sample
ranges are disjoint; unlimited ranges overlap, so its exact improvement remains
less certain. Matching allocation/working peaks are unchanged. Construction falls
13,616→13,072 allocated bytes, with overlapping timings and mixed process movement;
do not claim a construction speedup or demonstrated regression. Mixed Unicode/ASCII
class capture -0.3%, unchanged empty -1.1%, nested -1.7%, Core Count +0.1% are controls,
not causal gains. No confirmation was needed.

[summary.json](summary.json) retains process medians, ranges, allocations and
working peaks. Release POSIX checks pass Windows/Linux, both runtimes, including
mixed classes, the U+007F/U+0080 boundary, named-class membership, negation/LF,
capture ranking, construction refusal, interruption and refunds. No broad refresh
or package qualification; native parity ratios are unchanged historical evidence.

Raw worker, patch, fixed binaries and samples:
`artifacts/experiments/posix-ascii-classes-20261109/vm-evidence.tar.gz`, SHA-256
`16216F3F2A3A294D64D6E3E1FEADFF728C55986B9F02351A5B6CF16CBE312CFE`.
The later Linux check log is alongside it as `tests-linux.txt`.
