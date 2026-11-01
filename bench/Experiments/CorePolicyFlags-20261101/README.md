# Snapshot immutable policy dispatch once

Source `1db4c974`, frozen baseline `1f430e61`. Core's shared string/native
constructor now saves the existing four-HasValue finite-policy decision in a
readonly auto-property. ResourcePolicy is already a copied readonly value.
Every branch sees the same decision; invocation limits, work/admission,
cancellation, deadlines, algorithms and result lifetimes are unchanged.
No new execution mode or public API. The getter's IL shrinks97 ->7 bytes;
this is implementation evidence, not a public speedup claim.

One [public comparison](comparison.json), **69.299 seconds**, quiet Ubuntu VM,
.NET11RC3/CPU2, eight equal-result operations/four producer/predecoded/decode lanes.
Warm1.5s/min64, independent60ms calibration, five alternating samples, minimum
**58.769ms**. Result batches consumed after timing. [Driver](driver.cs.txt),
[summary](summary.json), [identities](identities.json).

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual FTP IsMatch, ordinary | 0.1948 | 0.1871 | -3.93% | 0 |
| Same, compiled | 0.1952 | 0.1872 | -4.10% | 0 |
| Finite limits/live token | 0.1719 | 0.1593 | -7.33%, one outlier | 120 |
| Deadline/live token | 0.2534 | 0.2592 | +2.31% | 168 |
| Shared common/backtracking | 0.2053 | 0.1962 | -4.46% | 0 |
| Shared literal | 0.5202 | 0.4706 | -9.53% | 0 |
| Shared Unicode scalar | 0.8812 | 0.8900 | +1.00%, noisy | 5744 |
| Unchanged native category Count | 4.2720 | 4.2793 | +0.17% | 0 |

FTP and backtracking gains are stable across every pair. Four finite pairs
improve6.62–7.99%; one regresses2.58%, so do not assert a uniform finite gain.
Unicode paired changes span-8.61 to+10.48%, lane spreads1.11/1.13; preserve that
uncertainty instead of asserting a blanket cost pass. No confirmation was needed.

One [clean-source selective refresh](selective-refresh.txt) qualifies FTP
compiled **4.21x ->3.76x**, ordinary **1.88x ->1.68x** (both Gap), and shared
backtracking compiled **4.54x ->4.34x** (Gap), ordinary0.23x (Win). All remain0B.
The independent FTP catalog producer/comparator point movement is much larger
than the paired4% source effect; do not attribute that entire movement to this
change. [Unselected rows/identities](snapshot-diff.txt) are unchanged, README
sorting/generated parity checks pass. No full catalog or package matrix.

[Release checks](verification.txt) pass on both runtimes: Core380 and674 focused
runs, PythonRe62 and PCRE288, plus snapshot/API/dependency16. Existing checks
cover all resource dimensions, native/text construction, funded owners, original
cancellation/deadline and managed/checked dependencies. The one shared constructor
initializes the decision before runtime preparation. No public contract/doc change.

Immutable `artifacts/experiments/core-policy-flags-20261101/` archives:

- `evidence.tar.gz`: exact before/after DLL/PDBs, patch/source bundle, driver,
  raw comparison, clean Linux build/refresh and preserved prior generated files,
  SHA256 `4FB58BE82E13855E430D1EC47B4A027A30A144F699FF0BB7748B00D2A75008DB`.
- `correctness-source.zip`: tests, snapshot guards, identities and patch,
  SHA256 `1FD2C5CCF5A1C9FD5E574F1E30AE2A39502CEB8AD0EA1A17977ACFBF2EE9A91B`.

`a40bda03` remains the final cumulative baseline. Preserve higher explicit
deferrals and genuine warm byte-template debt; this bounded win does not repay
that debt or complete the fresh full-refresh/package/consumer release gates.
