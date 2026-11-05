# Reject zero candidate vectors before extracting positions

**Accepted with explicit neighbor costs.** The existing three-mask AVX2 packed
family loop uses Vector256.EqualsAll zero to reject empty candidate vectors.
Original equality/movemask position extraction runs only for survivors. Release
FullOpts emits vptest on the common path; method2073->2069bytes. Method-wide
extraction count remains four; hot placement changes. Codegen is not throughput.
Retain32/16-byte widths,32-bit pending/cursor state, ordered verification, tails,
scalars, preparation/allocation, accounting, original cancellation/deadlines and
lifetimes. No helper, executor, mode, framework or unchecked API is added.

Actual Chinese alternation already has212candidates for207matches; improved
selectivity is not the target. This differs from the earlier metadata hoist.
Reuse matching016 source graph on data-only parent4c4ba294 without baseline
rebuild. Only Core changes; six adapter/benchmark companions and caller match.

ONE **107.324s** separate-Default-process ABBA public Count comparison, one Core
per process. Normal8CPU startup thenCPU2; producer1.5s/min16, BCL250ms/min4 warmup,
independent60ms calibration, five alternating samples/process. All160 samples
exceed **53.698ms**; typed checksums follow timing. Even medians average the middle
two of ten pooled observations/version. [All observations](cost-summary.json).

| Public Count | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual Chinese alternation |149.25|145.12|−2.77%|0/0|
| Actual compiled |148.45|144.76|−2.49%|0/0|
| Actual finite/live |53895.75|54110.95|+0.40%|1984/1984|
| Dense matches |62.11|54.88|−11.64%, unstable before|0/0|
| Dense false prefixes |19.97|19.62|−1.79%|0/0|
| ASCII family neighbor |140.19|142.71|+1.80%|0/0|
| Folded family neighbor |123.28|124.04|+0.62%|0/0|
| Single Chinese control |89.87|89.05|−0.91%|0/0|

Primary before-process medians150.24/148.22us versus after145.11/145.13 consistently
support the modest gain; compiled148.82/148.07 versus144.77/144.64 does likewise.
Retain ASCII's2.5us cost and folded movement. Finite and single-literal control
paths bypass this change: no causal credit or normalization. Dense matches move
69.48->54.70us between BEFORE processes, matching after54.84/55.11; its pooled
11.64% is not a stable dense-match speedup. Promotion rests on primary savings
with retained costs. No confirmation/tuning. BCL predecoded/decode-then counts
are equal-result diagnostics, not catalog Status or resource equivalents.

Windows/Linux focused Release per.NET10/11 pass Core2785/Python176/PCRE165.
Existing tests cover exact/folded families, borrowed offsets/tails, pending replay,
dense matches/false candidates, scalar/ordered boundaries, strict full malformed
tails and finite/original-token/deadline/lifetime controls. No forwarding or
implementation-existence fixture is added. Resource-policy contracts are unchanged.

Immutable artifacts/experiments/nibble-zero-test-20261105/evidence.tar.gz:
`F7DA7DCE1D0A5C9AA5049B155CB78D22C9E2E5C5775803C648DE57C1127AA9B4`.
DeclarationB05E2DEE/patch91345E66, preflights, before/after graphs/codegen, caller,
every sample and log remain. Windows/selected exports are recorded separately.
Existing packages/symbols and previous Full/classifications remain immutable.
No package, Full matrix or wider-filter/cursor design is introduced.

Windows correctness archive SHA256: 91C9B82583BE6F432EFCED5F4B8419FEB5EA79876C3D50608BBFBA1ECD7902AC.

One clean committed-source refresh changes ONLY Chinese alternation ordinary and
compiled rows. [Selected evidence](selected-refresh.json) retains all unselected,
lifecycle and scaling records; 30 six-lane samples exceed49.8804ms. Generated
Markdown has six descending tables/178ratios; both matching runtime snapshot
hosts pass six checks without rebuilding. No full or package qualification.
Selected archive SHA256:
`97BC4C9AC36651E2486E165F9C06C5B6A62326E0EA21458685C3EA09E753B093`.
