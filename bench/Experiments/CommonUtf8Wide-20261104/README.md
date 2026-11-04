# Wider common UTF-8 validity blocks

**Accepted**, source `3a8cf7e2`. The existing common UTF-8 validity mask algorithm
uses 64-byte blocks when Vector512/AVX512BW is available, retaining its 32-byte and
scalar fallbacks. Checked MemoryMarshal reads, explicit two-bit continuation carry,
scalar/error handling, dense-lead threshold and byte-scale idle selection remain.
No fused matcher, new execution mode, retained state or unchecked access.

One **89.7599872s**, 17-case public comparison changes only Core DLL/PDB; the six
PythonRe/PCRE2/benchmark companions remain byte-identical. The frozen baseline has
Core `baae83e6`, PythonRe `8af18a5e` and the dev.23 PCRE2/benchmark assets. Normal
eight-CPU startup precedes CPU2 affinity. Existing 1.5s/minimum128 managed and
250ms/minimum64 diagnostic warmup, independent60ms calibration and five alternating
samples are retained. All170 owned samples qualify, minimum **56.2685ms**. Counts
are consumed after timing, and allocations are unchanged in every case.

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| ASCII control first | 2.476 | 2.495 | +0.79% |
| Actual Chinese, ordinary | 133.222 | 95.034 | -28.67% |
| Actual Chinese, compiled | 133.622 | 94.082 | -29.59% |
| Chinese alternation, ordinary | 250.072 | 202.322 | -19.09% |
| Chinese alternation, compiled | 244.628 | 201.248 | -17.73% |
| Chinese, finite/live | 1220.413 | 1199.804 | -1.69% |
| Chinese, deadline/live | 2199.107 | 2218.964 | +0.90% |
| Actual Russian, compiled | 248.942 | 199.927 | -19.69% |
| Actual Russian, folded | 229.543 | 259.883 | +13.22% |
| Dense three-byte | 76.042 | 76.288 | +0.32% |
| Mixed two/three-byte/ASCII | 65.056 | 67.180 | +3.27% |
| Late constrained scalar | 65.929 | 65.334 | -0.90% |
| Four-byte control | 12.297 | 12.209 | -0.72% |
| Late four-byte scalar | 65.617 | 65.662 | +0.07% |
| Short offset/tail | 0.237 | 0.225 | -4.87% |
| PythonRe control | 9.625 | 9.585 | -0.42% |
| PCRE2 control | 17.210 | 16.771 | -2.55% |

The folded Russian cost is retained: +13.22%, about30.3us, without assigning a
speculative cause. Owned spreads are tight; no confirmation or tuning variant.
Promotion follows the user's acceptance of local costs for major gains. The
checked-access campaign's allowance is not a universal delivery gate.
[All observations](cost-summary.json) retain spreads and qualification flags.
Compatible .NET same-options predecoded/decode-then lanes are separate diagnostics;
this comparison establishes neither catalog Status nor cumulative improvement.

Strict complete-input validity, scalar/error offsets, captures, cancellation,
deadlines, resource admission and allocation behavior remain. Windows Release
checks pass102 focused,41344 Core,1503 Diff,4736 PythonRe and6507 PCRE2 per target.
The existing validation fixture adds offset/tail/corruption checks against scalar
metadata validation. Linux passes459 default-wide and52 forced-256 focused checks
per target. Hardware probes verify both paths on .NET10/11; Windows exercises256.
Untimed Release disassembly confirms512-bit instructions in the admitted wide
method. No package matrix or Full refresh was repeated.

Eight directly affected catalog rows were selectively refreshed once, including
both folded Russian rows. The exact measured Core and unchanged companions were
reused with **no benchmark graph build**. [Selected observations](selected-ratios.json)
retain statuses and ratios. Chinese ordinary/compiled now4.97/3.93x predecoded;
Chinese alternation2.96/5.88x. The other170 rows and section/context/lifecycle data
remain unchanged. Three generated files were hash-imported; four blocks, six
descending tables and178 ratio rows verify. Snapshot guards pass3 checks per
target using matching binaries. Independent catalog movement is not the causal A/B.

Immutable evidence under artifacts/experiments/common-utf8-wide-20261104/:

- evidence.tar.gz: `86DDB3FC8AFB0BB1C0F3A42471D0F0A376618BB013304D392A5A6586DC5EDB95`.
- correctness-source.zip: `694FBC39EC84E3A42EC44E6A3DB1DA71341701FEC50484C87D89BEBE1D8097A4`.
- selective-evidence.tar.gz: `3A455EF61CD44075A579EB73E6156B824A8C26DDEDD6E7AD2187B1D933B4F7EA`.
- import-guards.zip: `8A95C91FDDAC69F687F49EF733D687340AB0994AAE842EE2721CCB4691D94E5F`.

The 20695-byte incremental source bundle, frozen assets, corpora, predeclared driver,
raw timings, hardware probes, disassembly and checks are retained. Earlier staged
packages and Full evidence are immutable. This is distinct from the closed fused
validation/count experiment; no NuGet publication.
