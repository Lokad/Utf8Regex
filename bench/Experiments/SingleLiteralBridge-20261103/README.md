# Single-literal result bridge: rejected and restored

The Core bridge admits only single exact ASCII/UTF-8 literals. All three native
creators pass the same complete byte array to literal analysis and the prepared
searcher, with no alternate family. The trial replaced the general
literal/family wrapper and PreparedSearchMatch reconstruction with the existing
single-literal finder and proven literal length. Guards, whole-subject validation,
confirmation, work charges and original interruption remained unchanged.

One quiet-VM .NET 11 RC3 comparison completed **22 public cases in 97.801s**,
under 120s, minimum sample **44.532ms**. Exact original `a40bda03`, parent
`36f00956` and its Core patch; Python DLL/PDB bytes are identical across
parent/candidate. The same 22 definitions, selection and settings as
[publication ordering](../PythonValuePublication-20261103/README.md) were retained:
normal startup before CPU2 affinity, 500ms/minimum128 warmup, independent 60ms
calibration, five alternating >=20ms batches, complete results consumed after
timing and allocations. Compatible .NET diagnostics remain separate.

| Public operation | Change against parent |
| --- | ---: |
| Search hit | **+4.98%** |
| Search miss | **+5.97%** |
| Finite/live Search hit / miss | +2.31% / +2.56% |
| Trailing assertion | -3.77% |
| Python Match hit / miss | -1.87% / -1.03% |

**Reject:** the targeted actual Search operation gets slower. Although parent
penalties fit 10%, they do not help the remaining delivery blocker. No
confirmation, tuning variant or favorable replay. [All results](summary.json)
preserve every row/sample, allocation and original ratio. In particular nullable
Count is +11.08% against original; the parent is already above 10% in this pilot.
Keep that control at delivery, without attributing it to this bridge edit or
erasing earlier results. A favorable original Search ratio does not fix dev.16.

FullOpts bridge code shrinks 668 -> 612 bytes and its frame 136 -> 104 bytes;
smaller code/frame did not yield the target gain. Prototype Release checks pass
Core998/PythonRe253 per runtime on .NET 10/11, including original authority,
whole validation, budgets and ownership. The full patch is restored. Restored
Core62/PythonRe65 per runtime pass, and the VM recovers byte-identical frozen
Core/Python DLLs/PDBs. No contract change survives.

Immutable evidence under `artifacts/experiments/single-literal-bridge-20261103/`:

- `evidence.tar.gz`, SHA256
  `3D7D5B2C0EDE799EE66013CC10A3B36DEC539C1D60B075F0FBE73FBF087252BC`:
  frozen source/assets, patch, driver/project/selection, preflight, codegen,
  all timing/allocation samples, scripts and exits.
- `correctness-source.zip`, SHA256
  `0ACC81A906D9D060BD3D3ACCE64B28D911113FBC198D03E7904151A4AFC41390`:
  prototype source, all focused checks and normalized source hash, sealed before
  restoration.
- `restoration.zip`, SHA256
  `CE2ADD39084704BD4E09EAB6D4EE483BD92CAC487EB1E866DBE8AED8CB6054B1`:
  restored tests and exact recovered asset hashes.

The direct bridge stays closed. The accepted publication-order change remains;
its CI is green. No packages, complete platform matrix, Full refresh or cumulative
gate were repeated. Further work needs a distinct demonstrated cause.
