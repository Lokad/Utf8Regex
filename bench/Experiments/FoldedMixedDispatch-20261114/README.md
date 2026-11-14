# Invariant folded literals on mixed UTF8

Accept Core `8b9b0a69`, before `70eaf3d2`. Capture-free invariant folded single
literals reuse the scalar-aligned byte search already admitted for families.
Keep capture, culture, Kelvin, RTL/NonBacktracking and controlled-operation
fences. No new kernel, mode, retained descriptor or unchecked access.

Remove the special Kelvin Count metadata branch and its selector completely.
CountViaCompiledEngine already discarded this literal metadata. Reuse common
validity-only Count preparation and the existing Kelvin scalar fallback.
Strict whole-input validation remains; AssumeValid omits the validity pass.
This supersedes the [preparation-only patch](../TrustedFoldPreparation-20261114/README.md).

Initial broad admission exposed captured split fallback validating twice:
593/594 checks pertarget, preserved. Keep captured single folded literals on
their original owned route. Final594 focused Release checks/target on Windows/
Linux pass, including independent overlapping .NET literal results for IsMatch,
Count, Match, enumeration, replacement, split and byte/UTF16 coordinates, both
policies, supplementary prefixes, aliases, culture, malformed strict tails,
original interruption/work admission and existing alias/lifetime cases.

ONE public .NET11GA VM A/B plus its sole reverse-order confirmation, same frozen
DLL/PDBs and caller, no confirmation build. The prior caller is reused by hash;
separate ordinary producer processes, internal affinity, equal valid unchanged
text/options/full Count outputs consumed after timing.9 samples/lane,250ms
target,1s warmup,227ms minimum observed. Compare each policy with itself.

| Public Count | First strict / trusted movement | Confirmation strict before → after us | Confirmation trusted before → after us |
|---|---:|---:|---:|
| english-corpus | -10.22% / -12.53% | 173.146 → 154.154 (-10.97%) | 139.546 → 119.768 (-14.17%) |
| mixed-text | -0.88% / -3.07% | 21.244 → 21.078 (-0.78%) | 14.149 → 13.991 (-1.12%) |
| dense-ascii | +0.16% / -2.86% | 51.877 → 51.667 (-0.41%) | 52.567 → 50.896 (-3.18%) |
| unaffected-family | +28.65% / -30.78% | 137.371 → 173.521 (+26.32%) | 200.729 → 134.964 (-32.76%) |
| unaffected-no-k | +0.02% / +0.37% | 588.711 → 597.958 (+1.57%) | 554.674 → 562.920 (+1.49%) |
| unaffected-finite-live | -14.10% / -15.33% | 1191.962 → 1043.670 (-12.44%) | 1163.697 → 1010.481 (-13.17%) |
| unaffected-kelvin | -3.08% / -3.29% | 2560.913 → 2478.731 (-3.21%) | 2555.218 → 2474.272 (-3.17%) |

**Retain the strict-family regression:28.65% first,26.32% confirmation.** English
improves10.22/10.97% strict and12.53/14.17% trusted; trusted family30.78/32.76%.
Accept these measured trade-offs and removal of duplicate preparation. No-k
search shows no useful gain. Allocations remain0B except112B finite/live.
Finite/live controls move -12..-15%, Kelvin controls about-3%; keep those shifts
without normalizing other results or claiming their exact cause. No new .NET
parity ratio, composed gain, or improvement on every workload is asserted.

Case IDs inherit the unchanged caller. Despite their `unaffected-` spelling,
family/no-k are affected routes here; finite/live and Kelvin remain controls.
The earlier source-only cost attribution was corrected before this experiment.
No further timing or tuning for this candidate.

Raw verified offVM before both keepers45334/49471 closed terminal0. Initial
SHA256 `6CF96F6D75B0606FB0D0E1666CC7BEF3C8C4567CFB76A49F9C67F3D6D3CF28E8`; confirmation
`925B9C321AA430A89A27BFB29DD700FE023F3A0501AB6608E3E1361E8CF8C1E8`. Local raw:
`artifacts/experiments/core-folded-mixed-dispatch-20261114/`.
[Acceptance](acceptance.json) preserves both observations, ranges, failed-check
receipt and route audit. No package/Full refresh; all540 historical package/
symbol hashes are preserved. This source is after qualified immutable dev85;
canonical tables retain their earlier identities until a delivery refresh.
