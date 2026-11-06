# Common 0.5.0-dev.46 delivery

This batch combines the accepted [greedy empty-retry reduction](../GreedyEmptyRetry-20261106/README.md)
and [bounded word-interior skips](../BoundaryWordSpan-20261106/README.md).
Only PythonReScalarMatchState changes producer C# from dev.45; Core, PCRE2 and
benchmark C# are unchanged. All three packages share46 and exact Core pins.
Strict whole-subject UTF-8/scalars, captures, aliases, original cancellation/
deadlines and useful resource admission remain. All three .NET10/11 packages are
qualified and staged in `artifacts/nuget/`. They remain unpublished.

Final package source `6957b35aa51ee5730252011085e21669767ee1f9` changes only
data/docs/receipts after Full. Producer/benchmark C# and build inputs are identical;
revision metadata and actual binaries differ. Windows qualification took298.4366s:
two selected builds, one pack per profile, eight detached fixtures/109832checks,
six assets and464 compiler documents tied to symbols. Linux consumes those exact
Windows bytes: eight fixtures/109832checks,24restore audits and12TRX checks pass,
with no producer rebuild or repack. Supplied Awk19/Lython118 per runtime and public
probes/root loans0 pass on both platforms. All240 earlier staged assets remain
unchanged; six new package/symbol files give246. [Final-source CI](data-source-ci.json)
42297916134 is terminal GREEN on Windows/Linux, CLOSED.

ONE Full refresh, frozen source `41a751a3d8d9ba344b7ccdbfe9f0814d44571b00`,
completed in **2790.8441464 seconds (46m30.84s)**: terminal0, Full/Complete,
exact83expected passed steps, under3600s. One build68.62s, both-runtime source
tests292.74s/109872checks, disabled-dynamic8.35+8.70s, all .NET11 RC3 catalogs,
official CPython and private native PCRE comparisons, lifecycle/scaling,
resources101.29s and final verifiers passed. No phase was substituted.
Original8GiB RAM/1GiB repository/3600s guards stayed;8624588KiB RAM admitted.

Seven files were hash-imported once. Root README retains four generated blocks,
six descending tables and178ratios. Matching Full binaries passed six snapshot
hosts/10checks without rebuild. All eight measured DLL/PDBs remain preserved.
Core121wins/3ties/54gaps/0duration-unqualified; Python40managed/38CPython/
1equivalent/6inconclusive/5harness-floor exclusions; PCRE74managed/12native/
1equivalent/14inconclusive/25excluded. Preserve uncertainty and exclusion reasons.
The paired batch's90fresh cases do not make its five floor-limited rows qualified.

Successive [catalog observations](catalog-movements.json), not isolated source A/B:

- Empty-progression FindAll strings87.77->66.45us/-24.29%, ratio2.05->1.56,
  unchanged16464B. Empty-progression Count258.26->158.61us/-38.58%,0B.
- Zero-width Count669.97->524.76us/-21.67%,0B. Split without captures-10.04%.
- Actual date miss remains a gap: current2.22us/ratio2.58. The focused experiment's
  small pooled gain did not establish a precise persistent improvement here.
- Core AWS-full ordinary8731.11->10633.95us/+21.79%,0B, still Win. Core C# did not
  change; the catalog cannot identify a cause. Include this actual32MB operation
  in the exact-package comparison below instead of hiding or retiming it.
- Python one-template replacement+2.71%, many-capture strings+2.05%, evaluator
  replacement+2.23% and structural FindAll+7.52% remain, with unchanged allocations.

Focused receipts retain their own controls and costs. No universal slowdown gate,
normalization or new causal claim follows.

ONE **112.658756760s** [exact-package45-to46 comparison](package-cost.json) passed:
ordinary Default-process ABBA,18public rows/360samples, minimum51.3256ms, under
the120s cap. No confirmation. Exact package DLL/PDB graphs and identical caller/
benchmark bytes are [bound](package-cost-declaration.json); complete results are
consumed after timing. Before execution, actual32MB AWS replaced the small captured
date row without increasing the fixed scope.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Empty FindAll strings | 81.50 | 58.15 | -28.66% | 16464/16464 |
| Empty FindAll finite/live | 114.83 | 91.06 | -20.70% | 20664/20664 |
| Empty FindAll deadline/live | 156.95 | 122.24 | -22.12% | 16624/16624 |
| Long word interiors | 109.50 | 0.94 | -99.15% | 0/0 |
| Boundary-only EOF | 130.29 | 1.01 | -99.22% | 0/0 |
| Dense almost-dates | 563.11 | 581.39 | +3.25% | 0/0 |
| Actual date miss | 1.49 | 1.49 | +0.42% | 0/0 |

Dense almost-date process medians570.64/561.41us before and585.49/572.51us after
retain a local cost; accept it with the large iteration gains. Date finite/live
+0.91% and deadline/live-0.62% remain essentially flat. One-capture+0.96% and
two-capture-2.26% have interleaving process medians, limiting attribution. No row
increases allocations. AWS7414.53->7378.97us/-0.48% also interleaves: the Full
catalog's+21.79% movement did not reproduce here and is not erased or explained.
Core history-1.09%, rollback-0.90%, PCRE long backreference-1.58%, captured tail
-2.04% and native control-1.26% remain observations, not attributed Python gains.
The earlier targeted captured-tail+25.04% observation remains intact. Four separate
equal-result predecoded/decode-then-.NET diagnostics are in the JSON; these do not
replace the full catalogs' official-CPython/native/.NET comparisons.

Source CI42289147358 is terminal GREEN on Windows/Linux, CLOSED. It covers the
accepted source plus the common46 metadata freeze in one pushed matrix. Final
generated data/docs change revision metadata; producer and benchmark C#/build
inputs must remain identical to Full. Actual package and Full DLLs may differ;
the final [source bridge](source-bridge.json) records their distinction.

Original Awk/Lython handoffs are staged from archiveCBD35E99:1565files, only three
exact pins8->46 change, archive640FF1F5. No live consumer repository is modified.
All package/consumer/cost gates are complete. Eight measured Full DLL/PDBs remain
unchanged after Linux acceptance and package-cost measurement.

Fresh45Linux scratch was retired after full18,374-entry/content comparison and
canonical off-VM EDED0EEC proof. Eight current-batch scratch targets were retired
after246-entry/content comparison and off-VM retirement archive22E6B99B/84237967B.
All those deletion scopes are closed. Prior protected targets, packages and eight
Full45 binaries stayed unchanged through cleanup. Compiler workers expired
naturally; no server shutdown, kill or admission reduction was used.

Canonical off-VM `artifacts/experiments/delivery-dev46-20261106/` evidence:

- full-evidence.tar.gz: `7888F2093A1DFF5A31B8242D3FFEBC02BFF1FE0A859E2F2835B3D128F8F49B7E`,34342703B.
- post-refresh-guards.tar.gz: `F1AA68FEE5DDCCB16EF23F85505C4F347F67D97E716AD19818A30EDA574710F3`.
- windows-evidence.zip: `ABEDF22411A9A08FF0396C055C0841D2CE63479497FBEDDC64832E1BD94C9E09`.
- windows-restore-inputs.zip: `0EF6F19564D7E7669716A4594AB3B3F1735FC7D88E360F3A0C4E1FCC4AB7B2CF`.
- linux-evidence.tar.gz: `AD55CA95A60DBBD52F16C5D698ADB670A7AE351DDEF281B10F7C00AF73A51D5E`,808663481B.
- package-cost-evidence.tar.gz: `CE08E99BD7F9B7A0C8FB95D81227897DCB3AB2E999AFB7AE14CAA54771236212`.

Immutable staged/published assets remain unchanged. Broader profile completion,
optional POSIX and remaining parity gaps retain separate plan items.
