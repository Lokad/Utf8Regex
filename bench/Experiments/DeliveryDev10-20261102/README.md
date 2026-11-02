# Dev.10: cumulative cost rejected

Common `0.5.0-dev.10`, producer `3aa299000149e7e75c59e9512dee6e72711a7f8a`,
is immutable and staged under `artifacts/nuget/` and
`artifacts/package-candidates/0.5.0-dev.10/`. **Not published; not accepted for
delivery.** All three packages have .NET10/11 assets and exact adapter/Core pins.
Rejected Set/mask/prefilter/shared-runner prototypes are absent.

Windows package qualification passed in291.019s: two producer graph builds,
one pack per profile and107524 detached semantic/API/provenance/restore checks.
Original copied Awk19/Lython118 tests pass per runtime, along with native/funded
probes, matching loaded hashes and final root loans0. Only three copied project
pins changed; external repositories were untouched. All464 original Windows
compiler documents, including generated inputs, were captured. These results
are separate from Linux and performance acceptance.

The one fixed-original `a40bda03` gate completed147 public entries in691.432s:
original113 plus17atomic and17profileFullMatch cases, five alternating samples,
full outputs/allocations and finite/live/deadline controls. The existing100ms
decoded-BCL union correction was carried forward; other protocols were preserved.
**Not accepted:** seven stable FullMatch/Search controls regress10.95–16.13% versus
the original. Parent-only acceptance was insufficient. The capital+11.53% and
nullable-atomic+12.40% controls are noisier and cannot excuse the stable failures.
See `cumulative-acceptance.json`. No confirmation, variants or gate replay.

Linux package qualification and the complete refresh are held until a corrected
runtime passes cost. Keep these bytes; changed producers require fresh versions.
CI41402804478 passes Windows and fails Linux in the stale parser-hint coverage
test. The bounded investigation and test-contract repair are in
[CaseHintCoverage](../CaseHintCoverage-20261102/README.md).

Raw records under `artifacts/experiments/delivery-dev10-20261102/` include frozen
DLL/PDBs, all drivers/binaries, corpora, source, predeclaration and every sample/log:

- `cumulative-3aa29900-notaccepted-complete.tar.gz`: SHA256 `D542DA61B1B66461D0912A5F46A603A99F9958E3BBEA088AD875AADFF8BF260B`.
- Windows compiler-input ZIP: SHA256 `3C96E7002BF1198F9A4EEA6FBD7F98B4644D8570E1228C7D36CAC27CB5D6B393`.
- Original copied consumer-source archive: SHA256 `CBD35E993774B928D7D8C054F53A3BEABDFBE8C8024C138CF68253D5BA3C41AB`.

An initial archive command expanded log globs from the wrong directory. Its
partial archive remains; the complete archive above includes all logs. No
measurement was replayed. Next action is causal IR/work/call-path diagnosis of
the stable regressions, a bounded correction, then one fresh coherent gate.
