# Readonly token storage: closed and reverted

The fixed-original delivery gate found large captured-FindAll regressions.
One candidate restored read-only token storage with an init-only property and
value-copy exchange, preserving all current counters, limits and deadline.
The [metadata census](actual-metadata.json) confirms InitOnly at the same field
ordinal and unchanged actual sizes: Budget80/Invocation128/Ledger24bytes.
Metadata alone did not explain or repair public costs. Production is restored;
no token-storage variant or confirmation ran.

[One bounded batch](summary.json),96.115timed seconds,18selected public cases:
6FindAll,7legacy/control/durable-token,5repeat controls. Frozen `2ff58324` Core
versus its archived candidate patch, identical PythonRe/PCRE2 bytes. Existing
typed drivers, isolated contexts, warmed lanes, independent calibration and five
alternating samples≥20ms. Complete results are consumed after timing.

| Public operation | Change |
| --- | ---: |
| Captured FindAll, ordinary/byte offset |−1.81%/+0.16%|
| Finite/deadline FindAll |−5.04%/−2.22%|
| Core grouped Unicode replacement |+2.59%|
| Finite Python captured Count |−1.68%|
| Core durable cursor, None/live advance token |+4.40%/+4.47%|
| Python durable cursor/live advance token |+4.03%|

Allocations are unchanged throughout. These parent observations do not resolve
the53–89% fixed-original FindAll regressions. No causal token-mutability claim,
package qualification, full catalogs or snapshot Status changes follow.

Both runtimes pass Core41160, PythonRe4515 and focused PCRE2resource/deadline/
advance146checks. The new semantic budget test is retained on restored production:
token borrow/restore preserves counters, first denial, limits and the original
absolute deadline. It tests the existing contract independently of the candidate.
Prototype-only compiler/metadata/benchmark code remains in ignored raw evidence.

Immutable artifacts under `artifacts/experiments/readonly-budget-token-20261101-deferred/`:

- `evidence.tar.gz`, SHA256 `A8B72971F1F9A555C46DCC0701D34544DC2CFB11CFF9567F54C00EDF923C213C`.
- `correctness.zip`, SHA256 `6189318B78370AAA45412A74E08FB1F56DB040AD4B4E62B663111464E809A938`.

All source/binary/PDB/driver identities, preflights, metadata sources and raw
parts remain. Stageddev.7 stays immutable and correctness-qualified; its failed
cumulative performance gate still holds the full refresh. Next compare actual
program/route shapes againsta40 before another accounting or dispatch change.
