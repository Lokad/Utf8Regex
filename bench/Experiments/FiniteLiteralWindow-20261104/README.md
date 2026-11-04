# Finite literal prepared-window cutoff

**Deferred; production change reverted.** Candidate `f3997fd1` raised the existing
bounded exact prepared-search cutoff from16 to32 bytes. Its concrete target is the
23-byte Chinese literal: the previous route separately verifies frequent first
UTF-8 lead-byte candidates. The existing17–32-byte last-byte primitive needs no
new preparation, storage or allocation. The2048-byte total window, owned invocation,
long/folded paths and interruption/publication checks remain in the experiment.

ONE **89.698204s**,17-case public Count comparison uses accepted wide Core
`3a8cf7e2` as baseline. Only Core DLL/PDB changes; six PythonRe/PCRE2/benchmark
companions remain byte-identical. Normal eight-CPU startup thenCPU2 affinity,
1.5s/minimum128 managed and250ms/minimum64 diagnostic warmup, independent60ms
calibration and five alternating samples. All170 owned samples qualify, minimum
**56.1083ms**. Every Count is consumed after timing. Allocations are unchanged:
0B for unlimited controls,112B for finite/live and160B for deadline/live.

| Public Count | Before us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| ASCII control first | 2.50 | 2.50 | -0.05% |
| Actual Chinese finite/live | 1214.49 | 605.08 | -50.18% |
| Actual Chinese deadline/live | 2142.42 | 617.12 | -71.20% |
| Actual Chinese live only | 1189.99 | 603.88 | -49.25% |
| Actual Chinese unlimited control | 110.89 | 108.54 | -2.12% |
| 16-byte finite control | 306.54 | 301.51 | -1.64% |
| 17-byte finite | 444.86 | 0.91 | -99.79% |
| 23-byte finite | 444.70 | 0.91 | -99.80% |
| 32-byte finite | 424.60 | 0.91 | -99.79% |
| 33-byte finite control | 438.48 | 503.16 | +14.75% |
| 23-byte dense-first miss | 445.06 | 0.89 | -99.80% |
| 23-byte dense-last adverse | 1.18 | 308.47 | +26017.84% |
| 23-byte dense matches | 18.11 | 15.05 | -16.88% |
| 23-byte reverse | 2004.30 | 2049.25 | +2.24% |
| 23-byte supplementary prefix | 447.31 | 1.42 | -99.68% |
| 23-byte captured Count | 444.64 | 0.93 | -99.79% |
| 23-byte folded control | 1473.16 | 1162.82 | -21.07% |

The actual Chinese improvements are substantial. The adverse case is equally
explicit: needle22`a`+`b`, subject32768`b` followed by the needle, one match.
Switching candidate orientation makes this simple32KiB ASCII input about261x
slower, adding307us. Mirrored dense-first cases improve dramatically. A blanket
cutoff extension therefore moves a large data-dependent cliff rather than
providing a balanced general improvement. Defer this proposal; do not silently
promote it, add a policy switch or tune another variant in this campaign.

This decision does not reinstate a universal10% gate. Smaller local regressions
and major gains remain acceptable delivery trade-offs. Tight owned spreads leave
no noise question requiring confirmation. The unchanged-route33-byte/folded
observations are retained without assigning speculative causes. Separate
same-options .NET predecoded/decode-then diagnostics establish no catalog Status.
[All observations](cost-summary.json) retain spreads and qualification flags.

Windows/Linux focused Release checks pass926 per .NET10/11 for the candidate.
They cover Count, value/Boolean matches, reverse ranges, detailed captures, borrowed
slices,16/17/23/32/33-byte boundaries, every-work-cutoff denial without partial
publication, malformed input and original cancellation/deadlines. Useful additional
boundary/capture/interruption coverage is retained after restoring the16-byte
cutoff and existing resource contract. No catalog refresh, package qualification,
Full run, confirmation or further timing variant. Staged artifacts stay immutable.

Immutable evidence under artifacts/experiments/finite-literal-window-20261104/:

- evidence.tar.gz: `794747E6F5528BFF568DF83C6755163A4B6959D088284D9CEC38DDCC223A9FB8`.
- windows-correctness.zip: `66E9FC9347491F6D81B71741A2E58BE9DE40D2356657916D1EE28980145C928A`.
- restored-checks.zip: `B87B6DDE26FA595F4BBCCE916EA75991AEADEAFE96C77B0F8D7F627EA49CED48`.

The archive retains the predeclared driver,17 cases, frozen before/after assets,
source patch, exact corpus, raw timings, result checks, source identities and Linux
checks. The reverted candidate must not be used as the next accepted baseline.
