# Bind funded PythonRe execution and result staging

**Accepted, source 451abf14.** Existing facade invocation roots now use the
program's account for covered execution capacity and result staging by default.
Construction/program/cache charges share that same allowance; numeric limits,
original tokens/deadlines, strict UTF8 and capture/scalar behavior remain.
No implicit result-traffic quota or private working cap is added. The explicit
four-argument cursor can still select a different working account. This batch
adds no public overloads, graph accounting or alternate engine.

Canonical literal FindAll now uses the bound invocation. Native collection uses
the existing controlled Core kernels and owns all its private enumerator copies;
its exclusive operation boundary refunds remaining map/table reservations after
range/output staging, including failed setup. Public copied Core cursor ownership
is unchanged. Exact literal bool/Count keep storage-free native routes. Bound
timeouts omit diagnostic input and map the nested Core translated pattern back
to the original Python pattern.

Covered working/result capacity is funded until synchronous completion, failure
or cursor disposal/publication. Caller-retained output and borrowed input are
separate; explicit retained-output transfer remains unfinished. Metadata owners
are incidental overhead, not exact heap/RSS admission. Retained program/cache
charges survive temporary releases and remain until their real lifetime ends.

[Public comparison](comparison.json), [console](comparison.txt),
[preflight](preflight.txt), [driver](driver.cs.txt), [project](driver.csproj.txt):
exact 59638981 versus 451abf14 DLL/PDBs, quiet VM CPU2, .NET11 RC3, **42.61s**.
500ms warmup per lane, independent calibration and three alternating samples;
minimum batch **29.90ms**, 120s outer cap, no confirmation. Baseline lacks only
the subsequent cold diagnostic fix as well as this execution binding; equal
successful outputs are verified. No package/catalog matrix or benchmark Status
refresh. Each measured collection includes public result shaping, with all batch
results held until the clock stops. Checksums/equality consume every result after
timing. Batch arrays are outside measured producer allocations.

| Public operation on funded expression | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Scalar Count | 7.827 | 8.083 | 1704 / 1824 |
| Unicode/capture Count | 13.269 | 13.549 | 5768 / 5888 |
| Already finite/live Count | 8.420 | 8.329 | 1824 / 1824 |
| Captured substitution | 13.158 | 14.462 | 5696 / 5816 |
| Already finite/live substitution | 16.235 | 16.901 | 5824 / 5824 |
| Canonical literal FindAll strings | 1.497 | 1.262 | 304 / 424 |
| Literal-family FindAll strings | 11.660 | 10.132 | 1448 / 2264 |
| Mapped dot FindAll strings | 15.084 | 14.797 | 2232 / 3048 |
| Default cursor, owned group-zero text collection + disposal | 15.701 | 19.389 | 7928 / 8048 |

Existing unfunded Count/substitution and native Count/Core Count controls changed
at most **+0.4%**, with unchanged allocations. Negative variation is not a gain.
Funded scalar Counts add **2.1-3.3%**, captured substitution **9.9%**, already
finite/live substitution **4.1%**, default cursor **23.5%**. The old account did
not fund those working/result loans; these are extra authority costs, not
identical-authority regressions or a global <=10% claim. Numeric finite/live
cases already had shared invocation metadata and keep their allocation cost;
other bound roots add 120 B, native collection adds 816 B for its controlled
state. All retained balances remain stable and final disposal refunds every
program/cache account.

Both .NET diagnostic lanes use equal-result public operations: Count, Replace,
or EnumerateMatches plus owned string collection. Producer cursor measurement
collects owned Current group-zero text into a caller list/array; producer bulk
returns ScalarValues directly. All characters matched by dot in this comparison
are BMP; supplementary characters outside those matches do not create a
scalar/code-unit disagreement. CPython catalogs are not refreshed by this pilot.
Lower native bulk timings reflect the changed controlled route/validation;
they are not promoted to a catalog speedup or parity claim.

[Release checks](verification.txt): complete PythonRe 4134/runtime, affected Core
cursor/resource/invocation/inventory 2768/runtime, PCRE2 resource/timeout/cancel
157/runtime, .NET10/11. Public tests exercise 27 operation families over scalar,
native, mapped and Unicode subjects; refusal/partial growth/success balances;
original post-grant cancellation/concurrent reuse; callback output admission;
numeric precedence; strict malformed-tail rejection; retained owned/range results;
and 500000 discarded ranges with only 16KiB available beyond the program charge.
Literal bool/Count make no extra reservations. Former program/cache-only balance
assertions now include live execution loans without weakening final balance checks.

The new timeout witness reached mapped Core execution and first exposed its
translated diagnostic pattern; bound bulk now maps it to Python's original text.
The failing evidence and a fixture property-name compile error are retained.

Immutable archives in artifacts/experiments/python-funded-execution-20261029/:

- python-funded-execution-451abf14.tar.gz: exact producer DLL/PDBs, source bundle,
  driver/build/preflight/raw results. SHA256
  `4CA5A0218E5886E59DF1EFB58C6D156879BFBA87CA1184E23D96C5C48E481FF7`.
- correctness-logs.zip: SHA256
  `5B1774D0C986E04362A23244882BB07E4B2FAB2DA74D310040026806FDD0D4D8`.

The archive was streamed to the workspace and fully inventoried. Published/staged
assets remain immutable. H1 output ownership, scalar coordinates and structured
diagnostics, final cumulative performance and release/consumer gates remain open.
