# Unfolded negated-set repeat pilot

**Closed and reverted.** This prototype extended the existing positive ASCII-mask
loop to unfolded negative Set atoms. It preserved per-scalar charging and the
Unicode/folded path, but did not materially improve actual imports Count.

Before: `13ff4c5f`. Candidate: `fe975000` plus the archived
[source/test patch](../../../artifacts/experiments/negated-set-repeat-20261031/candidate.patch), SHA256
`CF9812BBAD7B224C5E5745652889B4624C9E3F5E7E6429AA50465671099D28F6`.
The candidate was not committed or packaged. Both production/test files were
restored afterward; its experimental tests remain in the archived patch/logs.

[One bounded public comparison](comparison.json) completed in **87.885s**,
12 cases/four lanes, minimum timed sample27.129ms. Complete results agree against
predecoded and decode-then-.NET operations. Warm1.5s/min1000 calls, independently
calibrated50ms lanes, five alternating samples; every result batch is consumed
after timing. No confirmation: the stable target rows already show no useful
gain. Allocations and instruction counts are unchanged throughout.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual imports, ordinary | 50.725 | 50.681 | -0.09% |
| Actual imports, compiled | 50.494 | 49.707 | -1.56% |
| Actual imports, finite/live | 62.787 | 62.827 | +0.06% |
| Dense ASCII | 119.983 | 114.157 | -4.86% |
| Dense mixed Unicode | 126.997 | 122.057 | -3.89% |
| Positive-set control | 66.169 | 67.712 | +2.33% |
| Folded control | 87.315 | 107.213 | +22.79% |
| Unchanged literal control | 95.143 | 95.222 | +0.08% |

Actual imports spreads are within4.5%; dense rows within0.7%. Positive/folded
controls have1.82–1.93x spreads, so their medians do not establish a stable
regression magnitude. Dense gains alone do not justify accepting this as the
imports fix. See [summary](summary.json) for every row, including boundary,
finite misses and detailed matching. No snapshot or package refresh follows.

Focused Release checks passed on both targets: Core183, PythonRe149, PCRE2114.
They covered scalar/capture boundaries, borrowed views, malformed tails, folded
Kelvin, work cutoffs/parent loans and original cancellation/deadlines. Initial
preflight rejected a stale copied Core DLL, then missing AppContext-relative
data; both diagnostics are retained. Final preflight used an explicitly verified
Core DLL/PDB and launched next to the copied corpus. No timing used stale bytes.

The existing imports IR/proof remains45 instructions. The separately bounded
[Dot-only follow-up](../AsciiDotRepeat-20261031/README.md) also showed no useful
target gain and was reverted. This family is deferred; do not resample these
closed prototypes or reopen required-prefix trials.

[Immutable raw/source/DLL evidence](../../../artifacts/experiments/negated-set-repeat-20261031/evidence.tar.gz),
SHA256 `663412EC2E55C201FA90563FC042769A609027894B03DEE497E1FA831D9B0F7F`.
