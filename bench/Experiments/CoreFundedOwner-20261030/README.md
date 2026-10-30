# Core caller-funded compilation and scalar operations

Producer **1a7e0b90fb884210e4d432f15145a4d518c680f9**, baseline production
**157492e6f183696e27f21cdb90c26f65353f40b2**. First H2 embedding slice;
funded captures, persistent cursors, replacement and PCRE2 ownership remain open.

Funded Compile returns a disposable Utf8RegexProgramOwner, keeping its expression
private. IsMatch/Count use the program account or a distinct working account.
Close rejects new calls, drops its expression root and refunds compilation after
the last active call. Existing copied span cursors retain their contracts. Raw
Core IsMatch/Count also accept explicit shared working admission. Existing
construction/storage leases and metered scalar/native paths are reused; no new
matcher, accounting framework or GC-dependent reclamation was introduced.
Borrowed subject bytes and fixed incidental invocation metadata are outside the
modeled buffer account. Numeric work/depth/working/result limits remain distinct.
See the [contract](../../../docs/SPEC.md) and [example](../../../docs/MIGRATION.md).

Release verification passes on both .NET 10 and .NET 11: complete Core
**40134/runtime**, including **19 new owner cases/runtime**; affected PythonRe
**58/runtime** and PCRE2 **13/runtime**. Public consumers cover separate child
accounts under one root, terminal denial, failed/canceled compilation, concurrent
calls/close, live call-token/deadline interruption, numeric guards, RTL/NB,
strict malformed tails and synchronous exclusive refunds. API snapshot and
managed-access inventory checks pass. [Logs](verification.txt).

The one [public comparison](comparison.json) took **47.26s** under a 120s cap,
on Ubuntu 24.04.4/Xeon Platinum 8573C, .NET 11 RC3, CPU mask 4. Each lane warms
750ms, calibrates independently toward 80ms and records three alternating samples.
The shortest actual sample is **32.18ms** (unfunded literal compilation); all
matching samples exceed 84ms. Returned results/checksums are checked after timing;
working loans and held program balances are checked between samples and final
close. No confirmation, catalog refresh or package matrix was performed.

| Public operation | Before / after unfunded (us) | Working admission (us) | Program default / separate working (us) |
| --- | --- | --- | --- |
| Literal Count, 8198 B | 0.419 / 0.419 | 0.420 | 0.481 / 0.482 |
| Category Count, 8196 B | 0.685 / 0.705 | 0.823 | 0.874 / 0.869 |
| Word Count, 9728 B | 9.162 / 8.327 | 8.392 | 8.384 / 8.375 |
| Scalar Count, 1472 B | 89.390 / 90.720 | 89.959 | 90.148 / 90.255 |
| Literal IsMatch, 8198 B | 0.463 / 0.440 | 0.329 | 0.381 / 0.379 |
| Scalar IsMatch, 1472 B | 2.211 / 2.130 | 1.319 | 1.347 / 1.363 |

Unfunded controls range from -9.12% to +2.91%, with unchanged allocations.
New funded APIs have different invocation/publication paths and extra guarantees;
their costs are not evidence of equivalence or a comparator benchmark Status.
Working admission adds **120 B/op**; a program-use lease adds another **24 B/op**.
Scalar matching already allocated 3872 B/op; other unfunded matching cases are
allocation free. These are managed allocations, distinct from modeled major-buffer
admission. Count retains the measured literal/category/word counters.

Eager literal compilation costs 31.94 / 32.44 us before/after unfunded and
33.66 us funded including close; scalar compilation 50.69 / 50.89 / 54.77 us.
Funded compilation adds **112 allocated bytes** in both cases. Compile controls
vary +1.59% / +0.39%; the new funded feature costs +3.76% / +7.63% versus current
unfunded compilation. Caller balances return to zero after close.

Exact source, driver, DLL/PDB, failed/full-temp then successful task-TMPDIR build,
raw timing and correctness evidence are preserved under
artifacts/experiments/core-funded-owner-20261030/:

- core-funded-owner-1a7e0b90.tar.gz SHA256
  `E54CECD5BAA9C47FD40E0B48D0388FAC660FA82DF062FFBA588BC20EED502FA1`.
- correctness-logs.zip SHA256
  `7330C9D8CAD7997250ECB8D3110833EF60263E5BCE7AAF62985DFB831AEE418B`.

Continue H2 with explicit persistent cursor/result ownership and PCRE2 flows.
The fixed-a40bda03 cumulative comparison, current complete sub-hour refresh,
immutable final packages and actual downstream consumer acceptance remain open.
