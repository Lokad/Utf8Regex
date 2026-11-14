# Python capture-prefix coordinates

Accepted source `8ec2f992`, against frozen dev89 `e8b20aa5`. PythonRe already
requires valid unchanged UTF-8 text. Move its existing sparse scalar index into
shared discovery and store scalar/UTF-16 prefix counts together. Remove the full
bidirectional coordinate map, whole-subject description, one-shot finished-state
shortcut, separate cursor coordinate owner and test-only map forwarders. Short
and ASCII projections acquire no index; one-shot value allocation behavior stays.
[Acceptance and all rows](acceptance.json).

One **84.133s**, eleven-row ordinary-process ABBA; five150ms-target samples per
process/row, all>=20ms,180s cap, **zero confirmations**. Different staged Python
DLL/PDB pairs and every compiler-source checksum verify; Core/caller/.NET controls
are unchanged. Complete outputs, including lookaround byte/UTF-16 captures, are
checked after timing. Cursor costs include construction, first advance and disposal.
This is a producer comparison, not a new CPython parity ratio.

| Public operation | Before µs | After µs | Observation |
|---|---:|---:|---|
| Early ASCII detailed cursor,600002B |1135.277|0.623|826298→896B/op |
| Early lookahead detailed cursor,600007B |1076.912|0.852|826774→1368B/op |
| Distant detailed cursor,616385B |1150.988|3.591|859801→1144B/op |
| Dense ASCII details,1024 results |407.215|426.963|+4.85%, same allocations |
| Dense Unicode details,1024 results |1021.078|1033.785|+1.24%, overlapping ranges |
| Finite dense Unicode details |1244.481|1330.017|+6.87%, saves26520B/op |
| Distant scalar-only cursor |3.003|3.512|+16.96%,888→1088B/op |

Keep the measured regressions for the major early-result improvement and simpler
ownership. The scalar-only cost is about0.51µs; the former10% checked-access gate
does not apply universally. One-shot value+5.17% overlaps with the same64B/op.
String-only-2.63% separates, but this does not establish coordinate-removal causation
on that unchanged path. Core/.NET controls+0.95/+0.33% overlap and are not normalizers.

The existing4KiB cursor/bulk early-detail refusal now succeeds with equal outputs.
Index capacity is working scratch, not cumulative result traffic; returned buffers
and strings still spend result capacity. Two counts per256B can raise scalar-only
working admission. Growth admits old/new overlap before allocation and refunds on
failure/EOF/disposal. Numerical requirements can change; no heap/RSS cap is implied.
Matching, captures/closing order, aliases, original cancellation/deadline authority
and useful finite work/depth/working/result limits remain.

Windows Release PythonRe5191 checks pass per.NET10/11, followed by42 focused
coordinate/boundary cases per target (including3 new lookaround/index-cut cases).
The42 also pass with intrinsics disabled on.NET11. Linux.NET11 passes all5194.
Coverage includes widths/slices/vector and index cuts, absent/empty/lookaround
captures, range/detail/scalar mixing, retained snapshots, growth refusal/refunds,
result traffic and original tokens/deadlines. Initial numerical-test/caller
preflight corrections and the collector-directory recovery are disclosed in JSON.

Raw copied and hashed offVM before keeper release:
`artifacts/experiments/python-cursor-prefix-audit-20261114/comparison/evidence.tar.gz`,
SHA256 `529974A8A0337FF3F54C84C0EC37C641F4AE32E2DF5DB643D1E51383911EF77E`.
Windows checks are archived alongside it. VM worker/collector/keeper terminal0;
all572 staged package/symbol files unchanged. No package/Full/canonical refresh.
Thedev89 corrected-protocol46m40s Full remains the latest milestone qualification.

This preparation correction is closed. Next investigate demonstrated capture,
search and output costs; preserve adverse dense/scalar observations. No second
index, new execution mode or capture compiler is justified by this comparison.
