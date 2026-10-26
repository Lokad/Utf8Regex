# RC2 package operating cost

Measured final package-extracted Core/PythonRe/PCRE2 DLLs against fixed,
correctness-equivalent baseline 844dcbad on the dedicated Xeon 8573C VM,
.NET 10.0.12, CPU 6, with default runtime/tiering. The existing same-process
alternating host warms each version/lane for at least 2 s and 10,000 calls
(5 million for short replacement), calibrates toward 40 ms, and takes 9 samples.
No concurrent builds/tests/measurements; sampled CPU activity was zero beforehand.

One 109.138 s comparison and the sole 23.568 s reversed-first, 17-pair confirmation.
Fifteen of sixteen public rows initially range from -18.79% to +4.99%;
allocations are unchanged. Finite Python captured Count initially measures
+10.38% with about 3% sample spread; its sole confirmation measures +0.06% with
about 1% spreads and controls from -1.81% to +1.47%. Absolute Count times change
from 18.6/20.5 us to 12.0/12.0 us between runs. Both observations remain visible;
do not pool them, conceal the initial exceedance, or claim stable absolute cost.
The declared representative cumulative gate is accepted: no repeatable regression
above 10% remains. This does not establish a guarantee for other workloads.

| Final-package public operation | Unlimited us | Four finite controls us | Allocations unlimited / finite B |
| --- | ---: | ---: | ---: |
| Core compiled identifier Count / 512 B, 16 hits | 0.825 | 9.092 | 0 / 112 |
| Python captured Count / 3 KiB, 1,024 hits | 37.892 | 12.047–20.512 observed | 0 / 112 |
| Python eager UTF-8 capture tuples / 1,024 matches | 410.090 | 393.732 | 129,280 / 133,480 |
| PCRE2 Unicode backtracking Count / 64 B, 8 matches | 6.163 | 6.686 | 0 / 1,592 |
| PCRE2 Unicode enumeration / 64 B, 8 matches | 8.801 | 10.340 | 0 / 11,952 |
| PCRE2 dense identity replacement / 1 KiB, 1,024 matches | 104.541 | 171.347 | 13,384 / 17,584 |

Core finite identifier Count with a live token is 9.261 us; a 10 s per-call
absolute deadline gives 18.320 us / 160 B. Finite scalar NonBacktracking Count
is 24.470 us / 4,832 B for 384 scalars. All finite policies use 50 million work
steps, depth 1,024, working 64 MiB and result 128 MiB. These are experiment
allowances, not recommended defaults.

The Core finite/unlimited identifier ratio is 11.0x; the deadline ratio is 22.2x.
Python finite routes can be faster. These are public-operation observations,
including policy-dependent routing, rather than pure accounting microcosts.
Predecoded and strict-decode .NET controls use overlapping ASCII semantics.
Eager tuple/replacement outputs are retained; full values/ranges are checked
before timing, with per-call checksums checked during timing.

Raw observations, actual loaded identities, warm/calibration details, the
predeclaration and unchanged controls remain in the adjacent JSON and source
receipts. Construction, complete catalogs, native PCRE attribution and historical
benchmark Status are outside this measurement. There was no further run.