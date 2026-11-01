# Borrow immutable tokens in PCRE2 execution

TryMatchToken now takes its existing immutable token by readonly reference.
The instruction's value-returning auto-property becomes a readonly field at its
original declaration position, so the shared VM passes the stored token directly.
No new storage, ownership annotation, engine, algorithm, candidate, selector,
allocation or numeric quota change. All existing interruption/resource checks
and accepted instruction/request/boundary improvements remain. Internal friend
consumers must be rebuilt together; public APIs and retained lifetimes are unchanged.

Baseline committedba9c810a, engineecfd28a1. Actual SizeOf is token16B,
instruction40B, request72B in both lanes. Observed Linux Tier1 passes the token's
address instead of staging/copying its fields; VM code17526->15433B. Both probes
consume two million correct public results. This explains the removed copy,
not a universal code-generation or speedup guarantee. Focused261 and full6486
affected PCRE2 Release checks pass per runtime on .NET10/11.

The [one public comparison](comparison.json) completed63.735s,17workloads,
minimum36.240ms samples. Same actual Core DLL/PDBs; typed delegates in isolated
contexts, warm1.5s/min1000, independent60ms calibration, five alternating samples,
complete result consumption. Allocations unchanged in every lane; no confirmation.

| Public operation | Change |
| --- | ---: |
| Date hit/miss |−2.18% / −2.83%|
| Finite/live hit/miss |+0.55% / −1.39%|
| Deadline/live hit/miss |−0.61% / −3.01%|
| Dense failed candidates |−1.12%|
| Dense Count |−2.85%|
| Unicode / UCP |−3.85% / −1.83%|
| Nonword Count |−2.88%|
| Captured Count |−0.84%|
| Unicode-heavy |+0.87%|
| Subroutine |−0.78%|
| Bounded control |+0.66%|
| Literal control |+4.79%|

Retain [all observations](summary.json), including adverse unchanged controls.
Largest adverse median4.79% is within10%; small gains do not imply parity or
universal improvement. The fixed-original cumulative gate remains at the next
coherent delivery boundary. Rejected diagnostic extraction is fully absent.

Sealed archives under `artifacts/experiments/pcre2-token-borrow-20261101/`:

- `evidence.tar.gz`: exact before/after DLL/PDBs, source/patch, public driver,
  codegen probes/disassembly, builds and raw measurements. SHA256
  `883DCC90E5EB7811644B82E586E82480D11894FF22EE9F134C7124FAE634282E`.
- `correctness.zip`: exact Windows source/patch, both-runtime libraries/PDBs/
  test binaries and focused/full logs. SHA256
  `39BABFB08AF51EA122C14A6A3015399D88873C0730E86AFCD5DCE320B0CA0D4D`.

Refresh only the existing affected date/native rows at committed source next.
No package, complete platform/catalog matrix or immutable artifact changes.
