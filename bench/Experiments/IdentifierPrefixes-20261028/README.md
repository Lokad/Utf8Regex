# Structural prefixes in prepared scalar Count

a5ca07b6 certifies the already-admitted identifier-family literal prefixes as
possible starts and passes the original search plan into prepared scalar Count.
It reuses the existing CountFromBytes candidate search. The scalar VM still
verifies Unicode boundaries/whitespace, suffixes and captures. Native dispatch,
NB/RTL handling, validation and the original invocation authority remain intact.

[Comparison](comparison.json), [console](comparison.txt), [driver](compare.cs.txt),
[project](compare.csproj.txt), [selective refresh](readme-refresh.txt).
Same .NET 11 RC3 quiet VM and CPU2 as the preceding experiment. VM-built a40bda03
and a5ca07b6 load in one process. The preceding simple-prefix change does not
classify this native identifier-family shape. Results agree with .NET before and
during measurement. One <=120s comparison, completed in **9.64 seconds**: four
independently calibrated public lanes, three alternating samples after warmup,
raw times/allocations retained, no confirmation or favorable rerun.

| Public Count | Before | After | Speedup | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Keyword/identifier family, ordinary | 4025.72 us | 348.47 us | 11.6x | 3552 / 3552 |
| Compiled | 3856.94 us | 317.17 us | 12.2x | 3552 / 3552 |
| 32 KiB, finite limits and live token | 2515.40 us | 258.25 us | 9.7x | 3664 / 3664 |
| Mixed-scalar dense hits | 2203.73 us | 844.40 us | 2.6x | 3552 / 3552 |
| Unchanged compiled literal | 71.970 us | 73.758 us | +2.5% elapsed | 0 / 0 |

The paired compiled ratio against predecoded .NET was 39.62x. The separate
five-sample README protocol reports **40.96x**, versus 495.80x before; ordinary
is **1.44x**. Remaining gaps stay visible. Attribution/private-kernel timings
are not substituted for these public costs.

[Core .NET 10](core-net10.txt), [Core .NET 11](core-net11.txt),
[PythonRe](python-net11.txt) and [PCRE2](pcre-net11.txt) affected Release checks
passed. They cover scalar/finite count routes, false prefixes, Unicode boundary
and whitespace context, retained captures, working/work admission, cancellation,
NB and RTL. The prepared-count test proves an absent prefix can finish within
the original bounded working allowance and rejects exhausted work.

Exact measured candidate/benchmark binaries, PDBs, driver and outputs are in the
[local archive](../../../artifacts/experiments/identifier-prefixes-20261028/parity-identifier-a5ca07b6.tar.gz),
SHA256 `DE287B77BE297637E571CCBA4D89E4D81B3238A14AE61D948A68B53CAC3FFA54`.
No new engine, resource switch, package matrix or full benchmark refresh was added.
