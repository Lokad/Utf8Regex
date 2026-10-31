# Required one-byte rarity tie-break

**Rejected and fully reverted.** Baseline `0103b540`, plus the archived
production/pinned-test patch and two separately retained test files, all bound by
[identities](identities.json). It reused the existing ASCII frequency ranking only
when equal-length necessary families each contained one ASCII byte. No new field,
table, owner or matcher. It selected c instead of a for (?>ab+)c, improving an
absent-c miss but losing the earlier absent-a rejection when c was present.

[Focused Release checks](verification.txt) passed on both runtimes: Core344,
complete PythonRe4395 and PCRE2100 per runtime. They covered independent scalar VM
necessity, disjunctions/folding/RTL/NB, text/native construction, finite admission,
strict whole-subject validation, original search-phase token/deadline authority,
capture metadata and funded retained lifetimes. Three pinned first-byte choices
were intentionally updated for the prototype; those edits were restored too.
The initial old-choice failures remain in the correctness archive.

One [public comparison](comparison.json), **85.951s**, fifteen cases, minimum
sample **30.603ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Search/Count have four
equal-result lanes (before/after and diagnostic predecoded/decode-then compiled
Regex); construction has two producer lanes. Search range/complete Count outputs
are retained/checked after timing. Warm1s/min128 calls, independently calibrated
80ms lanes, three alternating samples. Python DLL bytes are identical; the change
is entirely in Core preparation. **No confirmation**: opposing misses decisively
reject the tradeoff. No source promotion, snapshot or package qualification.

| Public operation | Before us | After us | Allocated B before / after |
| --- | ---: | ---: | ---: |
| Actual Python atomic miss | 2.783 | 0.324 | 3552 / 0 |
| Actual Python atomic hit | 1.613 | 1.580 | 3552 / 3552 |
| Finite/live Python miss | 3.130 | 0.489 | 3672 / 120 |
| Finite/live Python hit | 1.976 | 1.834 | 3672 / 3672 |
| c present/a absent,16B | 0.219 | 1.944 | 0 / 1936 |
| c present/a absent,8192B | 0.405 | 690.667 | 0 / 1936 |
| Unicode captured hit | 1.206 | 1.149 | 5744 / 5744 |
| Core atomic miss Count | 1.751 | 0.177 | 3552 / 0 |
| Core atomic hit Count | 2.152 | 2.143 | 3552 / 3552 |
| Core c present/a absent Count,8192B | 1.819 | 1005.498 | 0 / 1936 |
| Folded control | 1.448 | 1.381 | 3552 / 3552 |
| Literal control | 0.191 | 0.194 | 0 / 0 |
| Python atomic construction/close | 120.553 | 280.379 | 66664 / 66664 |
| Core atomic construction | 46.393 | 46.245 | 51736 / 51736 |
| Core literal construction control | 32.067 | 32.281 | 40464 / 40464 |

The short opposing miss has spreads at most1.02x; Core's dense opposing miss at
most1.01x. Python's dense samples are noisier (up to1.48x), but the roughly three
orders of magnitude increase and new1936B allocation are unambiguous. A rare-byte
presence proves nothing; selecting it globally removed a valuable early rejection
and exposed repeated scalar attempts. Target gains do not earn that regression.
Core-hit and construction samples also vary; preserve them without precise
equivalence claims. [Summary](summary.json) retains every spread/allocation.

Both tracked prototype files and both new fixtures were restored/removed after
capture. Their exact source remains in the raw archive. No dormant selector,
switch or test-only executor was retained. The original first-on-tie policy is
still shipped; do not repeat this closed heuristic or tune from its favorable row.
The Python atomic gap remains visible; the next bounded investigation returns to
Core's Chinese literal gap. A different demonstrated cause is required to return.

[Driver](driver.cs.txt), preflights and raw console accompany immutable archives:

- [Raw/DLL/PDB/source patch and test files](../../../artifacts/experiments/required-byte-rarity-20261031/evidence.tar.gz):
  `EB35137CCFC808549D969A132833F9F23A8453AE3FEE18B4CE258F7C6C053902`.
- [Correctness logs and source/identity evidence](../../../artifacts/experiments/required-byte-rarity-20261031/correctness-source.zip):
  `FB86CD3DFCB50B3C682174DB6223C2D4BF9F50607166789C4DE3D70CCDF5DC47`.

Published/staged artifacts and every catalog snapshot remain unchanged. Final
cumulative/full-refresh/package/platform/consumer release gates remain open.
