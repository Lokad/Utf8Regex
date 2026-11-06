# Common 0.5.0-dev.45 delivery

This candidate combines the accepted [single-group rollback reduction](../SingleGroupCaptureTrail-20261106/README.md)
and [multi-group start-entry reduction](../MultiGroupStartTrail-20261106/README.md).
Strict UTF-8/scalars, captures/history, retained results, original cancellation/
deadlines and useful resource admission remain. No public API or unchecked access
is added. All three common-version packages, targeting .NET10/11 with exact Core
pins, are qualified and staged in `artifacts/nuget/`. They remain unpublished.

Package source `6c50b01b72f9385f233b367a8df4aa177cf4eec9` differs from the Full
source only in generated data, documentation and receipts. C#/build inputs are
identical; generated revision metadata and actual binaries differ. The
[source bridge](source-bridge.json) records these identities.

Windows qualification took296.4733s: two selected builds, one pack per profile,
eight detached fixtures/109792checks and464 compiler documents tied to symbols.
Linux consumed those exact Windows packages without a producer rebuild or repack:
eight fixtures/109792checks, sixassets,24restore audits and12TRX checks pass.
Original copied Awk19/Lython118 checks per runtime and public probes/root loans0
pass on both platforms. Only three version pins change in1565 supplied files.
All234 prior staged assets are unchanged; six new package/symbol files give240.
Source CI42279995071 is terminal GREEN on Windows/Linux, CLOSED.

ONE final-source Full refresh, source7c5f60bd27604f48bdf85b4318373e46c6acbf5d,
completed in **2768.7323673 seconds (46m08.73s)**, under3600s: terminal0, Complete/
Full, exact83 expected passed steps. Both-runtime correctness and disabled-dynamic
checks, .NET11 RC3 catalogs, official CPython, private native PCRE comparator,
lifecycle/scaling, resources and verifiers are included. No phase was substituted.
Admission was8659280KiB available RAM; the original8GiB/1GiB/3600s guards stayed.
Idle build nodes expired normally; two fresh archived scratch trees were retired,
with no server shutdown or old cleanup retry.

Seven generated files were hash-imported once. Root README retains4 generated
blocks,6 descending tables and178 ratios. Matching frozen binaries pass six
snapshot hosts/10 checks without rebuild. Core122wins/2ties/54gaps/0duration-
unqualified; Python40managed/37CPython/2equivalent/6inconclusive/5floor exclusions;
PCRE77managed/11native/13inconclusive/25excluded. Preserve all uncertainty/reasons.

Successive paired Python observations: one-capture strings483.47→438.22µs,
ratio2.26→2.08; two-capture strings707.92→683.35µs, ratio2.02→1.97. These are
catalog movements, not isolated causal effects. The largest Core gaps remain;
AWS Count compiled9303.74→10178.84µs/+9.41% is retained as an adverse observation.
The separate exact-package comparison below measures the combined delivery.

ONE **112.434842744s** [Windows-package44-to45 comparison](package-cost.json)
completed under120s: ordinary Default-process ABBA,18public rows/360samples,
minimum31.6661ms. Caller/benchmark bytes are fixed, each process loads one producer
graph, and complete results are consumed after timing. No confirmation,
normalization, source-effect decomposition or new catalog Status follows.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| One-capture strings | 428.79 | 405.44 | -5.45% | 94552/94344 |
| Two-capture strings | 649.04 | 651.54 | +0.39% | 242008/242008 |
| Two-capture UTF-8 | 711.83 | 716.12 | +0.60% | 242008/242008 |
| Two-capture finite/live | 1068.87 | 1058.16 | -1.00% | 246208/246208 |
| Two-capture detailed | 1062.68 | 932.57 | -12.24% | 545112/545112 |
| Twelve-group strings | 113.25 | 107.65 | -4.95% | 53928/53776 |

The twelve-group process medians are106.52/108.54us after versus113.06/113.45us
before. One-capture medians overlap. Detailed before medians diverge924.67/1198.81us
versus914.89/950.84us after: preserve the pooled observation without claiming a
precise persistent12% gain. The combined package comparison does not reproduce
the isolated multi-group experiment's ordinary string/UTF-8 gains; both receipts
remain intact. No observed row increases allocations.

Named FullMatch miss+0.55%/hit+2.62%, finite/live-2.69% and deadline/live-3.68%
retain272/384/432B costs. Core history+0.02%, Unicode rollback+0.15%, PCRE long
backreference+2.64%, captured tail+1.01%, AWS full+0.48% and native control+0.83%
remain in the evidence, with process ranges. Window finite/live-0.94% and Python
class-run-3.84% are observations, not attributed capture gains. In particular,
the near-flat tail observation does not erase the earlier targeted+25.04% cost.
Separate equal-result BCL FullMatch diagnostics are in the JSON; they do not
replace the full catalogs' .NET/official-CPython/native comparisons.

Accept the capture allocation/working-storage reductions with these documented
costs and uncertainty. The completed checked-access allowance is not a universal
delivery gate. Matching semantics, original interruption authority and resource
admission remain covered by both-runtime source/package/consumer checks.

Canonical off-VM `artifacts/experiments/delivery-dev45-20261106/` archives:

- full-evidence.tar.gz: `47B5FF3D13479291B9BA722BC93818909BE8BBDE59B35135D984269EA1F89E9C`,34339874B.
- post-refresh-guards.tar.gz: `58569AC6E62A20D452A230F335302809079D94715C35503AC542F0CD4A194EA1`.
- windows-evidence.zip: `502387184EF6D392768571849C0D83A5E959C6BAFC916D8422A6C8AF29FDFA52`.
- windows-restore-inputs.zip: `D6F263CA9B4CFD106164C565790523F6D3CD92A803F5C03F77A1AA06583B2CB1`.
- linux-evidence.tar.gz: `EDED0EEC0206F0CF4BA4FDA448B096E30BD77554B6980E65DAA43DC212878D1B`,808603190B.
- package-cost-evidence.tar.gz: `AF890C09B1B748EA2CEA50D9FB4F03117604C733F7CE372497D61ED1453639F7`,3902597B.

Previous staged234assets and the dev.44 Full graph remain preserved. Original
Awk/Lython source handoffs stage1565files with only3 exact-version pins changed;
archive84723791 derives from originalCBD35E99. All eight Full DLL/PDBs remain
unchanged through Linux qualification. An initial copied Linux wrapper rejected
already-staged scratch before any tests; its orchestration repair reused verified
inputs without repeating qualification or weakening admission. The raw evidence
and [preparation note](linux-preparation-note.json) retain the distinction.

Broader profiles, optional POSIX and remaining performance gaps are separate plan
items. No package regeneration, publication or repeat unchanged matrix follows
this receipt update.
