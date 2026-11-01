# Finish the Python point runner's warm-up

The independent lookahead point samples at5c05ff8b reported~1.9us, while the
frozen public A/B and strong CPython pairs agreed around0.25us. The existing
point warm-up stopped at elapsed<1s **and** calls<65536; cheap lanes therefore
stopped around100–140ms. Remove only that call cutoff. The existing clock,
small batches and command cap remain; calibration, observations and protocols
are unchanged. No shipped library or engine change, new mode or framework.

First, one [frozen-method investigation](comparison.json), **13.696 seconds**,
uses the exact clean5c benchmark/Core/Python binaries. After1.2s warm-up both
existing batch methods agree: miss0.248/0.251us, hit0.278/0.276us. Count/FindAll
use the same fallback; checksums and semantic digests agree. Thus the different
batch bodies do not establish a persistent steady-state cost gap. Untimed
FullOpts preflight reports point/paired code9043/991bytes (IL2703/391); this is
diagnostic, not evidence of the default-tier cause. No loop unification needed.

Then one [actual point-runner comparison](warmup-summary.json), six fresh
processes, **6.403 seconds summed inside existing runners** (startup/orchestration
excluded), one120s outer cap, .NET11RC3/quiet Ubuntu VM/CPU2. Same catalog,
unchanged exact Core/Python/Pcre2 binaries, original calibration and five samples
per case. Samples are at least48.675ms. No confirmation or resampling.

| Point case | Before us | After us | Warm-up before ->after | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Lookahead miss | 1.442 | 0.239 | 103ms ->1000ms | 0 |
| Lookahead hit | 1.496 | 0.250 | 107ms ->1000ms | 0 |
| Slow Count control | 731.031 | 732.399 | 1008ms ->1006ms | 0 |

The cheap lanes formerly stopped at65551 calls; corrected lanes warm for the
full second. Count had already reached the time bound and moves+0.19%.
Corrected point values now agree with the independently qualified warm pairs.
This corrects measurement preparation; **it is not an engine speedup**. The exact
runtime tier transition was not traced, so do not attribute a particular tier
from timing or FullOpts alone. [Frozen driver](frozen-driver.cs.txt),
[point driver](warmup-driver.cs.txt), raw before/after JSON and
[verification](verification.txt) retain the evidence.

Release benchmark builds pass on net10 and net11, reusing matching library
binaries. Core remains SHA256
`FC24B4DE9E46467F5ED7CA689B696EB02CEC4AABA6124F31840FC0FE23E08E23`,
Python `8B65A5523BF43EFF9C67AAEBAE96112A572D3210B5A34648814F56C52296F202`.
No library tests/API/platform/package matrix was repeated for a timing-only fix.

Existing historical snapshots/paired receipts are preserved. Batch their fresh
points with the final complete refresh instead of repeating selective paired
qualification merely for provenance. The longer Python warm-up adds at most
about270s across90cases/three managed point lanes, relative to a zero warm-up;
the net increase should be lower. This estimate does not qualify the full
<=60min target; run the complete current-source scope at the delivery boundary.

Immutable `artifacts/experiments/python-point-path-20261101/` archives:

- `evidence.tar.gz`: exact before/after binaries/PDBs, raw probes, codegen,
  build log, patch and bundle, SHA256
  `02469444337A3992F8AABE18EB3818BD8B438817EB5992D827DFB8D3FB17D1F5`.
- `source-verification.zip`: driver source, patch/bundle, comparison summaries
  and build checks, SHA256
  `9354593063159A57C9242C229B0690875FA2A1ACF66EEFFBA4A8C4137C0483CD`.

Fresh cumulative fixed-baseline costs, complete current-source refresh and
immutable package/actual consumer acceptance remain required. No published or
staged package bytes changed.
