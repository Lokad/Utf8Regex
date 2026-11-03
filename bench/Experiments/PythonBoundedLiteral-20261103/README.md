# Compare bounded literals at the required start

PythonRe's direct Match/FullMatch comparison admitted at most 16 UTF-8 bytes.
The 17-byte delivery control therefore performed complete candidate discovery
before checking endpoints that were already required. The same checked span
comparison now admits **1–256 bytes**, matching the profile's existing bounded
prefix-comparison block. This changes one length bound, with no new algorithm,
buffer, field, backend or mode. Longer literals and Search retain discovery.
Exact-kind, folding and boundary exclusions, whole-subject validation, original
interruption/publication and major admission remain. Avoided discovery reduces
route-dependent work; finite minima are not portable across versions.

One predeclared quiet-VM .NET 11 RC3 comparison ran **17 public cases in 79.432s**,
under 120s. Fixed original `a40bda03`, parent `f637b44a` and parent plus the patch
load in separate contexts; current Core DLL/PDB bytes are identical. Startup
sees eight processors before CPU2 affinity. Existing 500ms/minimum 128-call warmup,
independent 60ms calibration, five alternating batches, complete range/count
checksums after timing, allocations and equal-result predecoded/decode-then
compiled Regex diagnostics are retained. Minimum sample **32.572ms**.

| Public operation | Change vs parent | Change vs original |
| --- | ---: | ---: |
| 17-byte FullMatch hit | -48.67% | -44.23% |
| 17-byte FullMatch miss | -45.69% | -38.41% |
| 255/256-byte FullMatch hit | -46.47% / -48.70% | -41.69% / -43.33% |
| 17-byte Match hit/miss | -47.50% / -49.49% | -44.19% / -46.55% |
| Unicode literal FullMatch | -29.61% | -26.37% |
| Finite/live 17-byte FullMatch | -36.88% | -45.91% |
| Deadline/live 17-byte FullMatch | -36.50% | -48.27% |
| Unchanged native Count, largest adverse | +2.39% | +4.75% |

**Promote.** Every parent adverse median is within 2.39%; allocations are
unchanged. [All observations](summary.json) retain 16/17/255/256/257/4096-byte,
offset/Unicode, Match hit/miss, finite/live/deadline, captured and unchanged
Search/Count controls. No confirmation, variant, catalog or package run.
FullOpts inspection changes the helper from 1010 to 1013 bytes, exposing the new
255 unsigned upper comparison. It is code-generation evidence, not a timing lane.

Focused Release tests initially passed **111/runtime** on .NET 10 and 11.
The full PythonRe suite then passed 4645/4647 on each: two instances of the same
interruption fixture failed because it called FullMatch on an unequal-length
subject and expected a long search. FullMatch can now reject after validation.
The fixture now calls **Search**, matching its name and intended assertion, with
the original token/deadline injection and retained 13B loan checks unchanged.
Corrected affected/funded/work suites pass **183/runtime**. All other full-suite
observations are preserved; no locally repeated full pass is claimed. Production
code was unchanged after timing. Tests cover the new bounds, scalar offsets,
malformed tails, text/value agreement, work cutoffs, publication and funding.

This addresses the 17-byte cause; it does **not** replace the
[failed 189-case delivery gate](../DeliveryDev15-20261103/README.md).
Search miss is +8.80% versus original in this pilot, but its unchanged producer
code did not acquire a demonstrated fix. The previous stable +16.03% remains a
delivery failure; it was not replayed as noise or erased by this observation.
Core's +13.02% gap control also remains. Retain every existing 189 case and the
cooperative Boolean consumer at the next coherent gate; no new candidate is
qualified yet and earlier package bytes remain immutable.

Raw evidence under `artifacts/experiments/python-bounded-literal-20261103/`:

- `evidence.tar.gz`, SHA256
  `0811A670744323DF57E0BFBDD3AF504042C90611A0A9ACB883A100214F29AC63`:
  complete frozen source, exact three DLL/PDB sets, driver/project, patches,
  preflight/code generation, all timing/allocation samples, scripts and exits.
- `initial-archive-attempt.tar.gz`, SHA256
  `01F5EE272E6D53785CEF848EA337F4EAB4831F67869E6CF7692BC953CC607814`.
- `correctness-source.zip`, SHA256
  `9AC7ADDA41FB5F665163D857C7B8D20F3D146A0E29EAB387DF73CF098C118219`:
  final source/tests/contracts, initial full failures, corrected checks and setup logs.

Measurement exited zero. Initial tar creation changed its containing directory
and exited one before streaming; creating the final archive outside the task
folder fixed transport without rerunning measurements. An earlier setup used a
previous RAM scratch path cleared after logout; original inputs were recovered
from the hash-verified persistent original archive before any candidate edit or
timing. These setup/transport failures remain separate from performance evidence.
