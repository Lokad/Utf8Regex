# Fixed-baseline public operating cost

Compare **a40bda03 -> 0e24cece** on the quiet Ubuntu/Xeon VM, .NET 11 RC3.
Before uses the exact DLLs from the completed sub-hour baseline archive
`artifacts/experiments/net11-20261027/subhour-a40bda03.tar.gz`, SHA256
`333FF4E54FAED60D909F8DCF81D9091BAE35DD2B876837095ED9693D37BD7454`.
After uses the immutable Windows-produced 0.5.0-dev.1 package DLLs. All six loaded
assembly hashes and informational versions are recorded. Both versions resolve
the same typed public-call IL inside isolated assembly contexts.

The 21 fixed cases include Core ordinary/compiled Chinese catalog Count,
identifier finite/live Count, live Boolean, scalar NonBacktracking, short and
large-Unicode grouped text/native replacement; Python captured Count/FindAll,
possessive miss, lookbehind and grouped Subn; PCRE2 Unicode Count/enumeration and
grouped replacement. Literal Core/PCRE2 and unchanged predecoded BCL are controls.
The full Chinese catalog subject is used, with its original 30 expected matches.
New funded APIs have no funded historical baseline; their separate closed
feature-cost receipts remain authoritative for those additions.

One **120-second** outer-capped run after the full refresh finishes. Warm each
lane for 1.5s and at least 1000 calls; independently calibrate to 60ms, then take
five alternating-order samples. Every timed sample must be at least 20ms. Counts
and complete object outputs are retained in preallocated batches and checked
after timing; Count has no per-call boxing. Scalar enumeration consumes public
coordinate checksums inside its driver; Python Subn checks its replacement count
before returning the output. Construction and process startup are excluded.
Samples retain raw durations, iteration counts, allocations and processor affinity.

At most one selected confirmation if noise prevents a material decision, then
stop. Report individual operation costs and controls, not an unweighted average
or an invented overall parity verdict. Preserve established warm grouped-template
cost debt; the historical <=10% checked-access allowance does not turn unrelated
new feature costs into a pass. Full catalog/comparator rankings and platform/
package/actual-consumer checks remain separate acceptance evidence.

[Windows public preflight](preflight-windows.json) passes all 21 cases on both
frozen sources. [Lane source](lanes.cs.txt) and [host](host.cs.txt) are retained;
this is a disposable experiment using existing delivery evidence, not a new
qualification framework or benchmark mode.
