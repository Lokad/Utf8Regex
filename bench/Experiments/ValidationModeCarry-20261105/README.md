# Preserve the validation density hint across scalar resumptions

**Reverted; closed.** The prototype passed the existing caller-local hint by
reference and removed the drain's duplicate local. It preserved learned density
across constrained/supplementary scalar fall-throughs. No new owner, mode,
selector, wrapper, unchecked access or allocation was added. Public validation,
error offsets and resource/interruption rules remained unchanged.

An untimed source-matched replay found 7,797 drain calls and 3,949 lost learned
hints on the 6,839,410-byte email corpus. Those are replay counts, not instrumented
production branch counts or timing attribution. The current public family is
Utf8WordDelimitedTokenCount and returns92 matches. Chinese uses one drain and
has no such reentries. Replay archive SHA256:
`6396D04419F7BB5638847E1E337AA85F12EE925070868C619F0117F5BB41F397`.

One **80.1967319-second**, 15-case public comparison used exact accepted Core
B2C9D5A4 and six frozenfb309 companion DLL/PDBs against Core built on df8e62c8
plus overlay4BFFAF9A. Only Core changed. [All observations](cost-summary.json)
preserve every row, identity, allocation cost and sample spread.

| Public Count | Before µs | After µs | Change |
| --- | ---: | ---: | ---: |
| Actual email |1187.79|1168.20|−1.65%|
| Actual email, compiled |1177.13|1175.02|−0.18%|
| Actual email, finite/live |2787.17|2784.11|−0.11%|
| Actual email, deadline/live |2871.28|2859.33|−0.42%|
| Frequent constrained resumes |329.96|295.10|−10.56%|
| Supplementary resumes |167.69|151.41|−9.71%|
| Alternating widths |10.15|10.45|+2.92%|
| Mostly two-byte |28.53|28.61|+0.25%|
| Constrained-only control |138.84|140.61|+1.28%|
| Short mixed control |0.22|0.22|+3.75%|
| Chinese control |93.55|95.99|+2.61%|
| Folded Russian control |230.39|255.00|+10.68%|
| ASCII control |2.52|2.51|−0.52%|
| Python mixed control |518.04|517.92|−0.02%|
| PCRE2 ASCII control |142.78|143.84|+0.74%|

Synthetic gains do not establish a material actual-corpus improvement. With
compiled email essentially unchanged and no allocation savings, the benefit
does not justify retaining the internal signature and callsite changes. This
decision does not use a universal10% gate or attribute control movement to the
changed validator. No confirmation, local-copy tuning variant or new mode followed.

The same B2C9 binary previously measured about256µs in the candidate folded slot,
versus230µs here in the baseline slot. That observation motivates a separate
unchanged-binary harness control; it does not prove an implementation regression
or its cause. Frozen catalog parity remains unchanged.
The [completed control](../ProducerProcessIsolation-20261105/README.md) found9.81%
slot movement with identical DLLs. Rejection still rests on the negligible actual
email benefit and unchanged allocations, not on folded-control attribution.

Normal eight-CPU startup thenCPU2; producers warm1.5s/min128, BCL250ms/min64,
independent60ms calibration, five alternating samples. All150 producer samples
exceed54.9942ms; typed checksums are consumed after timing. Allocations are
unchanged:0B except finite112B, deadline160B and Python3568B.

Windows/Linux Release.NET10/11 pass focused Core803/Python176/PCRE165 checks,
plus Linux80 forced256/47 scalar checks per runtime with verified hardware modes.
New tests covered retained hints and malformed offsets after constrained and
supplementary resumptions; existing direct tests received local ref arguments.
No forwarding executor. Untimed Release disassembly retained before/after value-
and ref-hint signatures. Production and candidate-only test edits were reverted
together. Matching rebuilt Windows.NET10/11 Core801 checks pass after restore.
The VM retains all eight accepted benchmark files unchanged.

Immutable artifacts/experiments/validation-mode-carry-20261105:

- evidence.tar.gz: `EE0D8FF8875F2E9E5280C90750D02FD17BBFCBD7288710EBBF646917A299BB1D`.
- windows-evidence.zip: `ABA2478C91DF7543E4C05336BBF9454549C27938EDF53455E72C5ABB274E114F`.

Exact candidate Core:
`A317EF7D25CB988A93095563CA873DB817E65CCA2122F0056EF17FB2EE3E00EB`;
overlay:`4BFFAF9A7F146BF7BF675ACDC4D7D9E4EAB4294FEB08DF2B009BE7CD21AB3A05`.
Archives retain graphs, driver, declaration, every sample, preflight, ISA probes,
disassembly, focused logs and terminal0. No Full refresh, package qualification,
snapshot rewrite or changes to staged/published bytes. Restored Windows archive:
`240821F71B05425C348EFBD2FC430B7101B24F4E7B7915808EE4D1CEFCCFD01A`.
