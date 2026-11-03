# Accept whole ASCII input before Unicode selection

**Accepted**, source `dd492ee0`. Existing `TryValidateOnly` now accepts a complete
checked `Ascii.IsValid` proof before selecting/draining Unicode validation.
Non-ASCII inputs retain the original selector and first-error path. No new field,
mode, matcher, unchecked API or resource-accounting mechanism.

The public owned Boolean path validates under its original invocation before
suffix discovery. The new return avoids repeated scanning of ASCII there;
controlled block validation retains its existing admission/interruption points.
Strict whole-subject validity, captures/scalar boundaries, original cancellation/
deadlines and useful limits remain. No API or resource contract changes.

One **80.714s**, 13-case public A/B against frozen `87db1ced` Full DLL/PDBs.
Only Core was rebuilt; benchmark/PythonRe/PCRE2 callers stayed identical. Normal
eight-CPU startup then CPU2, existing2s/min512 managed and250ms/min128 comparator
warmup, independent60ms calibration, five alternating samples and complete outputs
consumed after timing. Minimum sample27.16ms; allocations unchanged in every lane.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual47B IsMatch, ordinary |0.1719|0.0997|-41.96%|0|
| Actual47B IsMatch, compiled |0.1721|0.0998|-41.99%|0|
| Finite/live token |0.1817|0.1514|-16.66%|112|
| Deadline/live token |0.2864|0.2455|-14.27%|160|
| Large ASCII subject |1.9394|0.6325|-67.39%|0|
| Common three-byte subject |0.6213|0.6682|+7.56%|0|
| 64KiB ASCII prefix then Unicode |4.0404|4.8564|+20.20%|0|

[All observations](summary.json) retain two/constrained-three/four-byte subjects
and metadata Count/Python/PCRE2 controls. The late-Unicode cost is about0.82us;
it is an explicit accepted trade-off alongside the much larger ASCII gains.
Core metadata Count control moves+0.25%, Python-1.92%, PCRE2+1.06%; these are
observations, not isolated causal speedups. Target spreads are at most1.021;
no selected confirmation or variant was warranted. The user accepts documented
local regressions for relatively major gains; no universal10% gate is applied.

Focused Release checks pass per .NET10/11: Core 439, PythonRe 239, PCRE2 97. They
cover every ASCII byte/control/NUL with sliced offsets/tails, late scalar widths,
malformed whole-subject offsets, original cancellation/deadlines and numeric
admission. All old malformed/non-ASCII selector tests remain. The earlier rejected
prefix-range selector and Chinese fusion are unchanged and closed.

The first selective command used an unknown flag and exited2 before measuring
any case. Its completed build was reused with the existing `--refresh-readme-case`
command; the before/after run was not repeated. Selective common/backtracking
publication uses its own current-source rows, separately from this causal A/B.
On clean `dd492ee0`, the corrected selective refresh changes only the ordinary/
compiled `common/backtracking` records: **0.111x/2.042x** against matching
predecoded .NET, both about0.093us/0B. The previous compiled4.39x point remains
historical. All other Core rows retain their identities; three generated files
were hash-imported and the existing parity verifier passes. The43m08s Full receipt
still applies to frozen `87db1ced`, not this later selective update.
Dev.19 packages and frozen43m08s Full evidence remain immutable. Complete matrices
stay at the next coherent boundary; nothing was published to NuGet.

Immutable evidence under `artifacts/experiments/ascii-validity-fast-return-20261103/`:

- `evidence.tar.gz`: `1B815AFE7706EF0348D4FB2448E6B4D4C5C3BC084E041428ECB41E7B6E97AEF4`.
- `correctness-source.zip`: `11C920FF9368C6A599BFBD63D6F16D67E94F07F8C33CC093FAFBDFAC331F9EFE`.
- `failed-selective.tar.gz`: `ED80C8B617A6005C2A96FE0149BA09E0C91069733C1B3689DD9C36D9E8536B1F`.
- `selective.tar.gz`: `D12EE72D45D0859ECFE2A6CC9C7EAA956E7FC2BB13553DB7D92B60751AE8D4F8`.
