# Combined PCRE2 VM state reset: rejected, restored

At exact parent e503a097, combined two adjacent repeat-state fills and five
capture-state fills into one fill per live buffer. The same storage was admitted
before use; candidate/instruction order, all work and interruption checks,
captures and ownership were unchanged. No helper, mode or allocation was added.
The prototype is fully reverted locally and on the VM.

The current date miss uses Pcre2Backtracking with no candidate search. Existing
diagnostic replay records 42 attempts, 111 VM steps and three workspace rents;
its cold timing is not public throughput. Previous candidate-filter, token-decode
and diagnostic-charge rejections remain closed.

One quiet-VM .NET11 RC3 public comparison took **63.765s**, covering 17 operations
with identical Core DLL/PDB bytes, 1.5s warmup, independent calibration and five
alternating batches. Minimum sample **35.477ms**; results agree, allocations are
unchanged everywhere. The existing driver consumes every result after timing.

| Public operation | Elapsed change |
| --- | ---: |
| Date hit / miss | +2.51% / +3.02% |
| Dense failed candidates | +3.66% |
| Finite/live miss | +2.93% |
| Captured Count | +0.01% |
| Unicode-heavy | -4.16% |
| Unchanged literal control | -0.31% |
| Bounded control, largest adverse | +4.41% |

[All pairs, spreads and allocations](summary.json) remain visible. Costs satisfy
the 10% ceiling, but the target date/dense operations get slower. This does not
meet the predeclared performance objective. No arbitrary minimum-gain floor,
noise confirmation, variant, package qualification or table refresh was added.

Actual default-Tier1 initialization has seven versus two inlined vector-fill
setup sites; method size 14,025 versus 13,609 bytes. Both untimed probes check
two million public results. [Codegen summary](codegen-summary.json) records
observations, not a speedup or cost attribution.

Focused 103 and full 6,486 PCRE2 Release checks pass per runtime on .NET10/11,
including repeat rollback, captures, aliases, strict validation and finite/live
interruption authority. After restoration, 103 focused checks pass per runtime;
the VM rebuild reproduces its exact original DLL/PDB bytes. No public contract
changed. No implementation-mirroring test was added.

Immutable archives under `artifacts/experiments/pcre2-state-reset-20261102-deferred/`:

- `evidence.tar.gz`: parent source, complete prototype, exact before/after assets,
  existing public driver, preflight, probes/full disassembly and raw comparison.
  SHA256 `78A42EE161AAF4BA7904426EC47F9911C07485C3654AF1CBF3EC518C64EFDB81`.
- `correctness.zip`: candidate source/patch, predeclared plan, codegen excerpts
  and Windows candidate/restoration test logs.
  SHA256 `F191222661836C6CCFB1456AB6270FED96D538F01D998D2977C7EE54374E83C6`.

Qualified dev.14 and historical package bytes are unchanged. N4 parity work
continues from the fresh catalog; this reset idea and prior rejected filters
stay closed without a distinct demonstrated cause.
