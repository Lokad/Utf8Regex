# Boundary-disjoint structural prefix families

4a8037bb admits the existing structural route when all literal prefixes are ASCII
words behind a leading boundary. A proper-start overlap inside another prefix
cannot satisfy that boundary. Same-start ambiguity, unresolved overlaps with
punctuation or without boundaries, and mixed UTF-8 retain scalar verification. No matcher,
execution mode or public API was added.

Boundary tests exposed defects in the previously prepared native plans. Tail
extraction subtracted the separate initial atom from the tail's bounds; upper-word
kernels ignored those bounds. Both ordinary and emitted kernels now honor them.
Emission declines narrower whitespace classes. The non-emitted counter advances
by its selected anchor offset, replacing a hard-coded three-byte offset. Existing
scalar, capture, interruption and admission contracts are unchanged. Native route
diagnostics now identify the actual borrowed kernel.

[Raw comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt)
and [project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2: bcfc318c versus
4a8037bb. The later c1b7d422 baseline commit only changed reporting/evidence.
Four equal-result public Count lanes, 500 ms warmup, three alternating samples
around 50 ms. One comparison took **20.51 seconds**, capped at 120 seconds; no
confirmation. a40bda03 remains the fixed cumulative release baseline.

| Public Count | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Keyword family, ordinary | 314.14 | 75.36 | 3552 / 0 |
| Keyword family, compiled | 310.78 | 75.48 | 3552 / 0 |
| Finite limits and live token, 32 KiB | 242.51 | 205.59 | 3664 / 3664 |
| Dense ASCII | 849.78 | 71.63 | 3552 / 0 |
| Dense Unicode scalar control | 817.17 | 859.30 | 3552 / 3552 |
| Existing native star-tail control | 27.86 | 28.11 | 0 / 0 |
| Sherlock literal control | 71.64 | 75.21 | 0 / 0 |

The Unicode/control variation is +5.2%, +0.9% and +5.0%; do not describe those
lanes as improvements. The finite lane's measured reduction is retained without
attributing it to a new finite executor.

One [five-sample refresh](keyword-boundary-refresh.txt) changed only this case's
ordinary/compiled rows. The generated predecoded ratios are **0.34x ordinary**
(previously 1.44x) and **9.67x compiled** (previously 40.96x), with zero allocation.
The compiled lane remains a qualified gap. Other cases and lifecycle/scaling
evidence are unchanged. [Report verification](keyword-boundary-verify.txt) passed;
README tables remain generated and sorted by decreasing ratio.

[Focused Core checks](core-both.txt) passed on both runtimes, including public
Count/IsMatch/Match/cursors, UTF-8 errors, tail bounds, overlap retries, NB/RTL,
finite controls and interruption/lifetime fixtures. [Disabled emission](dynamic-disabled.txt),
[PythonRe](python-net11.txt) and [PCRE2](pcre-net11.txt) checks also passed. Failed
intermediate correctness attempts are preserved in the local archive below.
No package or full-catalog qualification was repeated.

Exact binaries, PDBs, benchmark data, driver and raw refresh/comparison evidence:
`artifacts/experiments/structural-boundary-prefixes-20261028/parity-keyword-boundary-4a8037bb.tar.gz`,
SHA256 `5896A620731D2C271967B1BB02A2E1FC5E6908B9A99AC0185DBF57DCDD4421D2`.
The same directory contains `correctness-attempts.zip` with the initial failures
and read-only route probe. Preserve both archives and the frozen release baseline.

This bounded experiment is closed. Next priority is the qualified PythonRe date
miss (37.57x CPython); remaining Core/PCRE2 gaps and release-boundary qualification
stay visible in PLAN.md.

CI 40727672645 found that the manual upper-word construction-admission fixture
still supplied a space-only separator to the now correctly restricted emitter.
Its family uses all six ASCII whitespace bytes now; cutoff, rollback, original
token/deadline and zero-allocation assertions are retained. The production
change's narrow-separator public tests remain. [Focused both-runtime checks](ci-fixture-fixed.txt)
pass. Raw failed CI is preserved beside the archives as
`ci-40727672645-failed.txt`; no performance or package campaign was repeated.
