# Complete exactly filled Python result vectors without copying

**Promoted for allocation/working-capacity savings, with small public time costs.**
The existing result ledger adopts an exactly full staging array and its live
working reservation before the next interruption checkpoint. The source buffer
becomes empty, so cleanup neither clears the published array nor returns it to
the scratch pool. Partial buffers retain the existing exact final copy and
overlap. No new owner, pool, cache, mode, framework or unchecked API.

All completion consumers were traced to terminal Python materializers. The private
range collector reads staging and does not complete it. Capture history is already
last-slot only; string-only operations already avoid maps and borrow ASCII sizing.
Those mechanisms were not added again. Full original validation, scalar matching,
captures, retained arrays/children, caller mutation and synchronous original
interruption remain. Major allocation admission stays before acquisition. Removed
copies/acquisitions can reduce work, peak working and cumulative result traffic;
SPEC-PYTHONRE/changelog record this version-dependent change.

One **80.7737598s**,15-case public A/B on the exact frozen dev.28 Full graph,
source `ba457088`. Six companion DLL/PDBs are identical; only Core changes, from
four-file overlay `25F2752A` built on data-only `a55cdb16`. Normal8CPU startup then
CPU2, managed1.5s/min128 and diagnostic250ms/min64 warmup, independent60ms calibration,
five alternating samples. **150 owned samples, minimum39.1689ms**. Typed returned
strings/tuples/bytes are consumed after timing; no timed reflection invocation or
result boxing. BCL same-options lanes compare equal composed results on these
overlapping subjects; no CPython/parity qualification. [Observations](cost-summary.json)
retain every case, identity, allocation and spread.

| Public Python operation | Before us | After us | Change | B/op before -> after |
| --- | ---: | ---: | ---: | ---: |
| Catalog one-capture strings,2048 results |417.86|423.90|+1.45%|112944 ->96536|
| Same finite/live |615.47|616.38|+0.15%|117144 ->100736|
| Same deadline/live |820.34|837.10|+2.04%|113104 ->96696|
| Catalog two-capture tuples |644.94|639.79|-0.80%|260400 ->243992|
| Supplementary prefix,2048 results |442.72|446.89|+0.94%|112944 ->96536|
| Exactly full16 |4.31|4.19|-2.75%|2920 ->2920|
| Partial17 |4.74|4.67|-1.53%|2960 ->2960|
| Exactly full256 |53.67|54.35|+1.25%|12520 ->12520|
| Exactly full512 |104.76|106.20|+1.38%|26880 ->22760|
| Partial2049 |409.79|422.71|+3.15%|145776 ->145776|
| Catalog one-capture UTF8 arrays |431.13|433.85|+0.63%|112944 ->96536|

Savings apply to exactly filled buffers. Unpooled2048 roots save16408 actual bytes
(14.53% for the single-capture row), and512 saves4120B. Small pooled roots do not
reduce steady allocation; partial roots keep their copy. This is not a general
FindAll execution-time speedup. Mixed miss +3.49%(0.25us,0B); unchanged native Count
-1.58%, Python Count +1.64%, PCRE Count -0.17%, with no causal control credit.
Spreads are at most1.05 before/1.03 after. These bounded small costs are accepted
for the allocation/peak-working savings and a single clear ownership transfer.
No confirmation, tuned variant, repeat campaign or universal10% gate.

Windows/Linux .NET10/11 each pass Core74 and Python777 focused checks. They cover
zero/full/partial/pooled/unpooled buffers, strings/tuples/UTF8 shapes, later calls,
caller mutation, transfer interruption and loan cleanup, finite/funded controls,
malformed tails and original cancellation/deadlines. The initial new work-limit
test exhausted its manual budget during staging; failure retained and corrected
by observing preparation work. Windows before-probe identities match qualified
packages. Its after probe rebuilt Python too, and inherited a stale frozen label;
raw hashes and inspection-notes identify those untimed observations. The actual
timed Linux comparison keeps all six companion files unchanged.

Immutable artifacts/experiments/python-result-array-20261104:

- evidence.tar.gz: `AA4EBDC515F2CA31F8CD401A80DAD2FE379AB04F2CECDECBB6309996EE17E7A4`.
- windows-correctness.zip: `24CB035D8FD07A5E4BC10620326D2524A7C036EFA94C2CF7A37576EF46F6A9F2`.

Staged dev.28 packages and its44m24.30s Full tables remain immutable and describe
`ba457088`, before this change. Complete platform/package matrices stay at the
next coherent delivery boundary; no new package qualification or publication.
