# Common 0.5.0-dev.47 delivery

This batch combines [completed-reference journal reduction](../CompletedBackreferenceJournal-20261106/README.md)
and [pure-predicate repeat proofs](../RepeatProofPredicate-20261106/README.md).
Only two Core implementation files change from qualified dev.46; PythonRe,
PCRE2 and benchmark C# are unchanged. All three packages use47/exact Core pins
and both .NET10/11 targets. Strict UTF-8/scalars, captures/history, retained results,
original interruption and meaningful resource admission remain.

The frozen source is `e7a65967deeb4b974d748b4a936c80c3e63870fa`.
ONE [Full refresh](full-refresh.json) passed in **2776.4412912s (46m16.44s)**:
Full/Complete/exit0, all83expected steps, under3600s. One build69.89s,
both-runtime source tests272.59s, disabled dynamic code, complete Core/PythonRe/
PCRE2 catalogs, official CPython/private native comparisons, lifecycle/scaling,
resources101.16s and all final verifiers passed. No phase was substituted.
The original8GiB RAM/1GiB repo/8GiB shm/3600s guards were preserved.

Seven generated files were hash-imported once. Root README retains4generated
blocks,6descending tables and178ratios. Six matching-binary snapshot hosts/10checks
pass without rebuilding. All8measured DLL/PDBs remain preserved. Core119wins/
4ties/55gaps/0duration-unqualified; Python37managed/42CPython/2equivalent/
4inconclusive/5harness-floor exclusions; PCRE76managed/12native/13inconclusive/
25excluded. The90measured Python paired cases do not qualify floor-limited rows.

[Successive catalogs](catalog-movements.json) are observations, not isolated A/B:

- Named-reference miss is about0.47->0.37us,272->64B, ratio1.91 to CPython.
  Named-reference hit is about0.50->0.41us with the same208B reduction.
- Verbose FullMatch is about0.67->0.53us,0B, ratio1.80 to CPython.
- AWSquick Count is5050.58->6539.46us/+29.48% compiled and5044.19->6525.64us/
  +29.37% ordinary, still528B. Unicode FullMatch is17.86->21.34us/+19.46%,0B.
  These adverse observations remain visible; no normalization or causal explanation
  is asserted. They are included in the final bounded package comparison.
- Earlier construction/rewind uncertainty, slower capture controls and other local
  costs remain in the focused receipts. No universal slowdown gate applies.

Frozen [CI42310799518](source-ci.json) is GREEN on Windows/Linux, CLOSED.
Earlier own source CI42310386527 was superseded/cancelled after the freeze push
to avoid duplicate matrices. The final data/docs commit changes revision metadata;
its C#/build-input bridge is recorded separately from actual package hashes.

All three packages are qualified/staged in `artifacts/nuget/`, unpublished.
Final package source `0c01d65f14fdc5acbd555968011716c99ec763c7` changes only
generated data/docs/receipts after Full. Tracked C#/build inputs agree; original
Windows CRLF compiler documents, revision metadata and actual package hashes are
distinct from the Linux Full binaries. The [source bridge](source-bridge.json)
records this boundary rather than claiming byte-identical builds.

Windows qualification took307.5218432s: two selected builds, one pack per profile,
eight detached fixtures/109920checks, six assets and464compiler documents tied to
symbols. Linux consumed those EXACT Windows bytes: eight fixtures/109920checks,
24restore audits and12TRX checks pass, with no producer rebuild/repack. Supplied
Awk19/Lython118 per runtime, public probes and root loans0 pass on both platforms.
The1565original consumer files change only3exact pins8->47; original archive
CBD35E99 and repinned9FB64B08 remain. All246previous staged assets stay unchanged;
six new package/symbol files give252. [Final data-source CI](data-source-ci.json)
42317511458 is GREEN on Windows/Linux, CLOSED.

ONE **106.554773438s** [exact-package comparison](package-cost.json) passed:
18public/4diagnostic rows, Default-process ABBA, independently calibrated60ms
targets/five samples,360public samples/minimum53.3229ms, under120s. No confirmation.
Before any timing, AWSquick replaced the uncertain hot-construction row and actual
Unicode FullMatch replaced Unicode-whitespace, retaining the bounded scope.
Focused construction/finite-Unicode evidence remains. [Exact graphs and unchanged
caller/benchmark bytes](package-cost-declaration.json) are bound before timing;
complete result projections/arrays are checked afterward. Separate BCL diagnostics
do not replace the Full profile comparators.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Named FullMatch miss |0.43|0.35|-18.05%|272/64|
| Named FullMatch hit |0.46|0.40|-14.00%|272/64|
| Named miss finite/live |0.64|0.54|-15.86%|384/176|
| Named miss deadline/live |0.81|0.67|-17.42%|432/224|
| Verbose FullMatch |0.69|0.56|-19.69%|0/0|
| Verbose finite/live |0.88|0.76|-13.74%|112/112|
| Verbose deadline/live |1.00|0.88|-12.17%|160/160|
| Completed-reference FindAll |159.18|147.02|-7.64%|45376/45168|
| Core long reference history |28.45|24.48|-13.96%|40312/28512|
| PCRE long backreference Count |22.60|20.22|-10.57%|35784/22784|
| Actual Unicode FullMatch |18.15|18.79|+3.51%|0/0|
| Actual AWSquick ordinary Count |4788.67|4803.34|+0.31%|528/528|

Target ordinary/finite/deadline, Unicode-reference, FindAll and history/PCRE ranges
are disjoint and improve in both candidate processes. Unicode-reference-13.68%
also saves208B. Unicode FullMatch is slower in both candidate processes, with
disjoint ranges; accept this3.51% local cost for the larger target gains. No row
increases allocations. Native+1.50%, shorthand+0.46%, one-capture+0.42%,
two-capture+0.49% and rewind-0.54% remain observations with interleaving process
medians/ranges, not attributed gains or a normalization factor.

AWSquick before4790.34/4787.00us versus after4825.43/4772.50us does not reproduce
the Full catalog's29% movement. The final driver measures ordinary Count; its
different process history and package binaries do not explain or erase either
catalog mode. Unicode's final3.51% likewise does not supersede the catalog19.46%.
All earlier focused costs and uncertainty remain intact; no blanket speedup or
universal slowdown gate is asserted.

Canonical off-VM evidence under `artifacts/experiments/delivery-dev47-20261107/`:

- full-evidence.tar.gz: `ED55D5879DDCC84FCC5FC58D7A488CD7DA3040D6E82110A681D4E58DEA51AC12`,34283479B.
- post-refresh-guards.tar.gz: `06FD021ADE1A8D8C25DE81ACBE50AA07E643F05E528FAB42F45325340E02C524`.
- retired-source-outputs.tar.gz: `CCFB3A9E4F089D2AD7CEC89980DDA97FCC4D713756CA37F6BE01B517D6F1FC81`,135159502B.
- windows-evidence.zip: `EBEB60C78D28C9E2944AC4794C60AEA58CA6DD0804371B20D8F276FC18E94B72`.
- windows-restore-inputs.zip: `EEAFADCCB4D254A3766BDEECF37933BA0E687B60AE0522B353A5314D48027346`.
- linux-evidence.tar.gz: `73F04313E430719F287E80CA7E8876DF6A6DD48B0BA58265050959FD62F1ECC8`,808690256B.
- package-cost-evidence.tar.gz: `87D9E17C4BDAFC960C6FA9125A796503A6F80A3EBB63978278EAFB974615AD8E`,3546939B.

Fresh46Linux scratch and15disposable source-output targets were retired after
full inventory/content and off-VM archive verification. All8prior Full46 assets
were copied/verified separately. These cleanup scopes are closed; protected and
previously rejected scopes remain untouched. Compiler workers expired naturally;
no server shutdown/kill or admission reduction was used.

The fresh flat VM duplicate of Full46 was subsequently retired after7888F209/
34342703B canonical/hash/path proof. Its first relative-manifest preflight stopped
before deletion; a read-only audit and corrected cwd preceded the single removal.
This scope is closed. Both eight-asset graphs and all package bytes remain intact.
Eight measured Full47 DLL/PDBs are verified unchanged after Linux and final costs.
All package/consumer/cost gates for this candidate are complete.

Immutable staged/published bytes remain unchanged. Broader profile work, optional
POSIX and remaining parity gaps retain separate plan items.
