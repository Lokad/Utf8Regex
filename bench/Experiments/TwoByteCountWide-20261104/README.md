# Widen the existing correlated two-byte literal Count scan

**Accepted with explicit public costs.** On hardware with 512-bit acceleration,
the existing large exact-UTF8 literal counter processes 64 candidate starts before
its retained 32-start and scalar tails. Correlation still checks literal bytes at
offsets 0, 1 and 3, then verifies the entire literal and suppresses overlapping
starts. Checked `MemoryMarshal.Read` uses one admitted span per block. The same
128KiB eligibility, strict complete-subject validation and allocation behavior
remain. Finite/live-token/deadline calls retain their existing controlled routes.
No engine, preparation, retained storage, owner, mode, framework or unchecked API
is introduced. Public semantics and resource-policy contracts are unchanged.

Untimed inspection identifies the actual 1570556-byte Russian corpus, a 23-byte
literal and 724 Count matches in ordinary/compiled modes. The invariant-Cyrillic
strategy is absent; dispatch predicates select the existing correlated-prefix
helper. Public route diagnostics return `<none>` here, so this is source/control-
flow evidence, not an invented executed-branch counter. Inspection archive:
`3E6AF5AB5E16D7D8242AC17252729A0D6E30A5ABC7EC0537781BEF06E266E43F`.

One **79.9200865-second**, 15-case public A/B compares accepted `69548b89` Core
with overlay `BCE837EA` built on test-only `e0b4bfa7` (identical runtime source
before the overlay). All six frozen `ba457088` companion DLL/PDBs are identical;
only Core changes. [All observations](cost-summary.json) retain exact hashes,
allocations, every case and sample spreads.

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual Russian |202.01|180.57|-10.61%|
| Actual Russian, compiled |201.70|180.31|-10.61%|
| Actual Russian, finite/live control |6880.62|6892.32|+0.17%|
| Actual Russian, deadline/live control |14011.86|15185.08|+8.37%|
| Dense short two-byte literals |180.51|158.73|-12.07%|
| Dense false correlations |122.94|149.64|+21.71%|
| Short mixed-width literal |98.41|85.72|-12.89%|
| Long needle with overlapping internal candidates |24.23|26.47|+9.26%|
| Supplementary tail |261.62|278.81|+6.57%|
| 131071-byte threshold control |11.62|11.15|-4.08%|
| 131073-byte subject |15.09|12.47|-17.36%|
| Folded Russian control |244.52|259.76|+6.23%|
| Python exact Russian Count |203.19|180.07|-11.38%|
| ASCII control |2.48|2.47|-0.61%|
| PCRE2 control |144.16|148.31|+2.88%|

Allocations are unchanged: zero except finite/live 112 B/op and deadline/live
160 B/op. Preserve all local costs, including dense false-prefix +26.69us and
the long/supplementary cases, for the actual Russian/Python gains. Control
movement, especially deadline +8.37% and folded +6.23%, remains visible without
assigning an unsupported cause or claiming causal gains on unchanged paths.
All spreads are below 1.068. No confirmation or tuned variant is needed for this
bounded trade-off decision. This is not a universal 10% gate or new parity Status.

Normal eight-CPU startup precedes CPU2 affinity. Managed warmup is at least 1.5s/
128 calls, BCL diagnostics 250ms/64 calls, calibration independently targets 60ms,
then five alternating samples. All **150 owned samples exceed 53.528ms**. Typed
public results are consumed after timing; same-options predecoded/decode-then BCL
lanes agree on these overlapping subjects. No CPython qualification is claimed.

Windows/Linux .NET 10/11 each pass focused Core 777, PythonRe 176 and PCRE2 165
checks. Fifteen new public tests cover threshold boundaries, borrowed offsets,
vector/scalar tails, dense and false correlations, mixed/supplementary literals,
long matches that consume several masks, malformed complete input, original
cancellation and finite work/depth/working/result controls. Linux also passes all
15 per target with 512-bit acceleration disabled and 256-bit acceleration retained.

The initial `DOTNET_EnableAVX512F=0` setting was ignored by the pinned runtime:
the hardware assertion stopped before fallback tests or timing. Runtime source
and actual JIT binary symbols identify the grouped `DOTNET_EnableAVX512=0` setting.
Its observed hardware flags are Vector512 false / Vector256 true; both runtime
versions expose that knob. The original output is retained. The remote host lacks
rg, so a Python text assertion checks the disassembly. No production source or
benchmark variant was changed during preparation recovery.

One untimed FullOpts disassembly shows ZMM operations in the candidate and the
retained 256-bit loop. Native method size is 587 -> 963 bytes. Its explicit
tiering/disassembly settings were confined to that diagnostic process; timing
uses normal runtime defaults. Full original validation remains inside public
measurements, and unchanged controls do not earn causal speedup credit.

Immutable archives under `artifacts/experiments/two-byte-count-wide-20261104/`:

- `evidence.tar.gz`: `81E889741815A77E4D7B5B0773F97BE421FC0DE23A85D62BA2767A4777AF1204`.
- `windows-correctness.zip`: `35FB685C6FE8F648A8F8370AF842F358C6D03ED8BE286CC6F89AB5D5A292A020`.

Raw evidence includes frozen graphs, source overlay, declaration, driver, failed
hardware assumption, passing checks, codegen, all samples and terminal status.
Published/staged packages and dev.28 Full tables are unchanged. No package/Full
refresh or completed checked-access campaign was repeated for this experiment.
