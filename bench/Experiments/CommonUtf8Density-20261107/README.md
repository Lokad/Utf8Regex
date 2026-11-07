# Vectorize the existing common-UTF8 density census

The validity-only validator still counts C2..DF and E1..EC/EE..EF leads in the
first256bytes and selects the initial common-mask mode with the same strict
comparison. Checked Vector256 reads and popcount now handle complete32byte blocks;
the original scalar tail and hardware fallback remain. No new helper, field,
mode, allocation or API. Validation algorithms, error offsets, work charges,
interruption, admission, scalar matching and result lifetimes are unchanged.

This is separate from the front-door E0/ED/F0+ BCL selector and previous closed
main-mask/fusion trials. A read-only probe confirms that the actual837byte Python
Unicode miss, input SHA05D2E466, reaches this census. Release FullOpts code changes
1433 to1474bytes and shows guarded vector loads, comparisons and popcounts with
no hot per-load range check. Code size is diagnostic, not a throughput claim.

ONE **96.991972721s** public source comparison, Default-process ABBA,19workloads/
380samples/min48.9243ms. Frozen clean source `9cdb09a0` plus only this census:
Core `6C4F1850` to `C3CF9C42`; Python `9B88CA5C`, PCRE2 `10360E34` and caller
`BC59E127` stay identical. Existing750ms/min32warmup, independent75ms calibration,
five samples,120souter/35sproducer caps and post-timing result checks remain.
No confirmation, retuning, Full or package qualification followed.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual Python Unicode Search miss | 0.307 | 0.135 | -56.00% | 0 |
| Same finite/live | 0.485 | 0.221 | -54.46% | 112 |
| Same deadline/live | 0.583 | 0.317 | -45.57% | 160 |
| Core Unicode-miss Count | 0.576 | 0.256 | -55.58% | 0 |
| Long two-byte miss | 0.691 | 0.521 | -24.55% | 0 |
| Long three-byte miss | 1.266 | 0.935 | -26.10% | 0 |
| Mixed common-width miss | 0.591 | 0.316 | -46.58% | 0 |
| Short Unicode hit | 0.148 | 0.162 | +9.62% | 0 |

**Promote, accepting the short-hit cost of +14.22ns for the larger gains.** Target
gains and that cost persist in both processes with disjoint overall ranges.
Core Count before-process medians0.689/0.442us vary; after0.252/0.259us improve
against both. Preserve that uncertainty rather than claiming a precise universal
55.58% effect. All19allocation medians are unchanged.

Other adverse ranges overlap: PCRE Unicode miss+1.47%,captured metadata+2.20%,
actual Chinese Count+0.165%,four-byte selector control+0.10%. BCLcontrol-4.37%,
CoreASCII-0.80%,PCREASCII-1.08%,short/largeASCII-2.53%,constrained selector-1.93%
and family metadata-1.47% remain without normalization or causal credit. No broad
PCRE/Chinese speedup or composed .NET/CPython ratio is claimed.
[All observations](process-observations.json),[summary](cost-summary.json) and
[acceptance](acceptance.json) retain scope and historical trade-offs.

Windows/Linux each pass Core519/Python112/PCRE71 focused Release checks per
.NET10/11, plus85validation checks per target with hardware intrinsics disabled.
Existing fixtures add255/256/257 boundaries; every byte tag, strict malformed
offsets, scalar endpoints, finite work, parent loans, original cancellation and
absolute deadlines remain covered. No failed build/test/preflight/timing attempt.
The Chinese control uses frozen Full51 LF813478byte input F129E819/count30;
the inspected Windows external clone's843478CRLFbytes were not used.

Canonical immutable evidence, verified off VM:

- `artifacts/experiments/common-utf8-density-20261107/evidence.tar.gz`:
  `EE90AE2C48A1D3FF1B97DAC199840667A19368C274ACCAB1DC0248933BA42AE4`,5637255B.
- `windows-checks.zip`: `A456BEE2E33C1EBBD01DD26E82AB137CC2D22BB8010AC6866960E715BBD41C6D`.

Dev.51 packages/symbols and qualified catalogs remain immutable. This source A/B
does not replace the [dev.51 checkpoint](../DeliveryDev51-20261107/README.md) or
erase the [coordinate pilot's](../NativeValueCoordinates-20261107/README.md)
miss/family costs. Final package costs and fresh catalog qualification stay separate.
