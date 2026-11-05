# Controlled boundary-family Count dispatch

**Reverted; closed.** This follow-up tested the separate live-token/deadline
Count dispatcher after [finite Count proof reuse](../FiniteBoundaryFamily-20261105/README.md).
It reused the family overlap proof while preserving generic overlap restrictions
for ordered windows. It added no executor, owner, metadata or public switch.
Finite-policy and ordinary unlimited Count remained logical control paths.

One **78.9212862-second**, 15-case public comparison used the exact accepted
Core433F388D built on39d377e9 against Core built on parentdf9413b6 with the
controlled-dispatch overlay. All six frozenfb309 companion DLL/PDBs were identical.
The actual 53,352-byte declaration corpus still returned 73 matches. No public
semantics, validation, numeric admission or original interruption was removed.
See [all observations and identities](cost-summary.json).

| Public Count | Before µs | After µs | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Actual corpus, deadline |53.03|66.25|+24.92%|3712 / 160|
| Actual corpus, deadline/live token |55.48|66.90|+20.58%|3712 / 160|
| Actual corpus, deadline/compiled/live |54.98|65.91|+19.88%|3712 / 160|
| Actual corpus, token only |46.95|47.18|+0.47%|3664 / 112|
| Dense deadline hits |2504.73|3625.99|+44.77%|3712 / 160|
| Sparse deadline miss |7.64|8.36|+9.44%|160 / 160|
| Deadline word-interior prefixes |1164.27|1170.24|+0.51%|224 / 160|
| Deadline long identifiers |275.28|165.59|−39.85%|3712 / 160|

The 3,552-byte savings and long-identifier improvement do not justify the actual
deadline operation's additional 11–13 µs or dense matches' additional 1,121 µs.
This is a workload trade-off decision, not a universal 10% regression gate.
The accepted finite-policy change remains useful and unchanged. No token-only
variant or tuning campaign followed this rejection.

Keep all control movement: finite +7.45%, unlimited +0.31%, same-start −6.74%,
Unicode −1.71%, NonBacktracking +0.43%, Python +1.32%, PCRE2 −1.66%. Those
logical paths are unchanged; the experiment does not establish the cause of
their timing movement. Their allocations are unchanged. These observations do
not revise frozen catalog parity classifications.

Eight-CPU startup followed by CPU2 affinity; producer warmup 1.5 seconds/min128
calls, BCL250ms/min64, independent60ms calibration, five alternating samples.
All150 producer samples exceed56.8511ms; spreads below1.054 before/1.068 after.
Typed public checksums are consumed after timing. Same-result BCL predecoded
and decode-then lanes are diagnostics on overlapping semantics. One comparison,
no confirmation, retuning, full refresh or package matrix.

The prototype passed Windows/Linux Release.NET10/11 focused Core1369/Python176/
PCRE165 checks, including ordered-window guards and original cancellation and
deadlines during discovery and verification. The first run caught an incorrect
new test assumption: finite memory limits redact timeout Input, while unlimited
numeric policies preserve it. Correcting that assertion changed no runtime code;
both failed and final successful logs remain. Production, test and documentation
prototype edits were then reverted together. Restored-source Windows.NET10/11
Core1365 checks pass with matching rebuilt binaries; the VM retains all eight
accepted benchmark DLL/PDBs unchanged.

Immutable artifacts/experiments/controlled-boundary-family-20261105:

- evidence.tar.gz: `53690821924035CAB7D23E8349981A3C10A76C4B278F18F8EED571974A763DBA`.
- windows-evidence.zip: `F7540144D2DCB7DA6DA773D3C61FEDF42937CDADB6126ABD4BBE750491444502`.

Raw evidence includes both graphs, predeclaration, driver, every sample,
preflight, original and corrected test overlays, both correctness runs and
terminal0. Final overlay SHA256:
`679E8752D16019202C015765E816F973DF7A7D786207B580F1477D134A4092A3`;
the initial A50360C8 overlay has identical production code. Staged dev30 packages,
symbols and Full tables remain immutable. Restored Windows checks archive:
`328FD5C9331918967B8D4E0F68302767A334DF798B717FE237F1D4DCAA1EC8EB`. Production df9413b6 CI41953150478
passed Windows/Linux; this receipt retains a rejected investigation only.
