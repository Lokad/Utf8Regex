# Reuse the admitted UTF8 predecessor window

Accepted source `5d15e2a07d5f48ac0f895fafc82d7c11de24b331`, frozen Core dev94
`7f3895ca`. The lookup validator admits a complete vector/predecessor window,
then checks the same three predecessor bytes for incomplete incoming scalars.
Pass that window to the helper and use fixed positions2/1/0, replacing original-
subject indexing at offset-1/-2/-3. One fewer private argument; no new mechanism.

Validation algorithm, AVX2/AVX512, checked spans, scalar tails/error authority,
allocations, metadata and public policies are unchanged. Managed admission still
checks the complete window before accessing it. No metering or synchronous
original-token/deadline checkpoint is removed; bounded2048-byte validation and
covered work/depth/working/result requirements stay intact.

Release public-route Unix512 FullOpts code shrinks506→463 bytes, eliminating the
three repeated inner range guards; the admitted-window check remains. Windows256
FullOpts retains checked admission and has no inner range-failure helper,485 bytes.
These diagnostics establish generation, not public performance by themselves.

ONE **63.433s** public ordinary-process ABBA, eight rows/five150ms-target samples
per process,50ms calibration/20ms floor under120s. Default.NET11.0.0 tiers/PGO,
normal8CPU startup then existing placement. Full Count operations receive the
same input/options/output requirements, including whole-input Validate unless
explicitly trusted. Counts checked after timing. [Observations](cost-summary.json),
[acceptance](acceptance.json).

| Public Count | Before us | After us | Observed change | Allocation bytes |
|---|---:|---:|---:|---:|
| Actual6.8MB email |939.659|910.089|-3.15%, separated|0, unchanged|
| Email, finite/live token |1257.985|1234.954|-1.83%, overlapping|112, unchanged|
| ASCII-heavy mixed text |15.932|14.410|-9.55%, separated|0, unchanged|
| Mixed text, finite/live |16.822|15.132|-10.05%, separated|112, unchanged|
| Chinese literal |69.305|69.725|+0.61%, overlapping|0, unchanged|
| Unchanged wholly-ASCII route |388.412|391.520|+0.80%, overlapping|0, unchanged|
| Unchanged trusted email |276.625|279.968|+1.21%, overlapping|0, unchanged|
| Unchanged predecoded.NET email |456.429|467.761|+2.48%, separated|0, unchanged|

Accept the smaller checked path and intended-workload improvements. The unchanged
.NET image also moves with separated ranges: retain that uncertainty, full process
observations and adverse rows; do not normalize controls or claim an exact causal
percentage, whole-profile gain or refreshed canonical parity. No confirmation,
tuning variant, package/Full rerun or checked-access campaign follows.

Focused Release checks pass per runtime on Windows.NET10/11 and Linux.NET11:
Core1064, PythonRe142, PCRE2218, POSIX160. Reuse existing test hosts with the changed
Core pair; tests/adapters have no source changes. Existing exhaustive byte-pair
vector-edge cases, borrowed offsets, widths/constraints/tails, scalar first-error
oracles and public malformed suffixes cover both supported vector widths. Profile
pattern/output checks, native PCRE windows, text contracts, original cancellation/
deadlines and resource denials pass. Seven production-kernel cases also supply
Windows256 codegen; no test mirrors the indexing change.

Only Core and the private caller build. Distinct frozen Core DLL/PDB pairs, every
compiler-document checksum, common caller/.NET hashes and managed-member inventory
verify. All allocation medians and resource work remain unchanged. Published and
qualified dev94 packages/canonical tables remain immutable.

Raw verified offVM before keeper release:
`artifacts/experiments/lookup-predecessor-window-20261115/evidence.tar.gz`, SHA256
`5CD90E01FB5BF418E7FF08D3C9516580C4FD0BAFC0FF017279CF47C2E7E2F509`.
Windows evidence is alongside it; its hash is in acceptance. All workers,
collectors and keepers are terminal. This bounded experiment is closed.
