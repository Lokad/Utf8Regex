# Detailed snapshot entry checkpoint

Source `84c42fdd96ed5fc940d05b8c554dd47da894b9cd`, frozen dev94 producer
`7f3895ca`. Remove SnapshotDetailed's Checkpoint immediately before
PrepareCaptureProjection, whose first statement checks the same invocation.
The helper's entry check still precedes coordinate work and allocation. Keep
inner, decoder, admission and final publication checks, captures and independent
mutable callback/result arrays. No API, allocation, algorithm or framework change.
One modeled work step less per owned detailed snapshot is intentional; useful
work/depth/working/result controls and original cancellation/deadlines remain.

ONE **78.548s** ordinary-process ABBA comparison, eight rows/five150ms-target
samples per process, 50ms calibration and20ms floor under120s. .NET11.0.0 default
tiers/PGO, normal8CPU startup followed by existing placement. Prepared-constant
callbacks receive complete owned snapshots during timing; rich argument/output
verification happens outside timing. Both producers receive identical caller-valid
unchanged UTF8, options and output requirements. [Observations](cost-summary.json),
[acceptance](acceptance.json).

| Public operation | Before us | After us | Observed change | Allocation bytes |
|---|---:|---:|---:|---:|
| String callback |75.007|74.221|-1.05%, overlapping|49568, unchanged|
| UTF8 callback |76.271|76.529|+0.34%, overlapping|48800, unchanged|
| Finite/live string callback |146.803|138.405|-5.72%, separated|49680, unchanged|
| Funded/deadline/live callback |286.918|278.125|-3.06%, overlapping|49752, unchanged|
| ASCII detailed iteration |69.688|69.036|-0.94%, overlapping|67696, unchanged|
| Unicode detailed iteration |213.793|209.112|-2.19%, separated|80480, unchanged|
| Unchanged Core / .NET controls |12.314 /22.709|12.245 /22.356|-0.56% /-1.55%, overlapping|0, unchanged|

Accept the smaller entry path and separated intended-workload improvements.
Controls also move; do not normalize results or claim a general callback speedup.
Retain the adverse UTF8 observation and full per-process ranges. No confirmation,
new CPython/native ratio, canonical refresh, package or Full qualification.

Windows.NET10/11 and Linux.NET11 each pass5223 affected Python tests. Existing
checks cover owned/retained/mutable results, Unicode coordinates, every detailed
work cutoff, original cancellation/deadlines, decode interruption, admission and
loan restoration. No test duplicates the removed implementation detail. Only
the affected adapter/tests/private caller build; Core, caller and .NET images
are identical across producers. Distinct Python DLL/PDB pairs and every compiler
document checksum verify. Funded operations restore held amounts, disposal zero
loans. Immutable dev94 packages remain unchanged.

An initial source-diff assertion included README changes after the dev94 freeze;
it failed before builds or timings. Correcting the code-change parent reused the
same frozen baseline. This setup-only failure is preserved, not a discarded run:
`artifacts/experiments/python-snapshot-entry-20261115/evidence.tar.gz`, SHA256
`84C6F2E7ADF0F42026FA30B3175E5A9E7C54BDF5772A9E893E72414BE0C606FC`.
Successful raw verified offVM before keeper release:
`artifacts/experiments/python-snapshot-entry-measure-20261115/evidence.tar.gz`, SHA256
`4629632123DBD35A80C142AD9F551D83EAC0F2AE62353B575344FD3A7EFAF165`.
Windows evidence is alongside it; its hash is in acceptance. All workers,
collectors and keepers are terminal. This trial is closed.
