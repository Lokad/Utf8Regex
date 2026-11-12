# Single captured class roots

Accepted on qualified dev77 producers (b1867c72), reused after metadata-only
c3bea6e1 alignment. Extend the existing complete capture-root helper to execute
CaptureStart/positive unfolded ASCII Set/CaptureEnd/Accept without VM cursor
setup or marker/Accept dispatch. Keep the original scalar decoder and predicate,
initialized workspace/slots, and metered entry/publication. One group has no
competing completion order. History, folding, complements, nesting, tails,
RTL/NonBacktracking and other roots retain their routes. No new prepared flag,
owner, buffer, opcode, mode or unchecked API.

Removed dispatch can reduce numerical work; Core/Python specifications document
the scope. Memory admission, returned captures/LastIndex, retained aliases,
validation policy and original cancellation/deadline authority remain.

## Public comparison

One 113.464s ordinary-process ABBA: 14 complete public operations, both input
policies, 560 samples, minimum 34.717ms, maximum 207.218MiB per retained timed batch;
140s outer cap. Linux, .NET 11.0.0, normal 8-CPU startup then CPU2, default tiering/PGO.
Distinct staged/loaded Core DLL/PDBs and identical Python/caller/.NET controls
verify. Initial complete-split warmup is at least 2s/1024 calls, then existing
300ms row warmup and 75ms calibration. Complete outputs are created during timing
and every returned value/range/LastIndex is checked afterward.

| Complete public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| Captured split strings |448.390 ->367.298|475.004 ->375.864|
| Captured split ranges |272.976 ->208.960|276.482 ->215.159|
| Finite/live split strings |591.599 ->524.760|595.185 ->491.667|
| Deadline/live split strings |999.474 ->988.562|998.293 ->969.474|
| Funded/deadline cursor and caller collection |1026.760 ->1015.009|1033.746 ->1018.892|
| Unicode-subject captured split |496.129 ->398.570|438.020 ->383.331|

Range split improves 23.45/22.18%, with disjoint sample ranges. String medians
improve 18.09/20.87%, but samples overlap: before-process medians 452.949/443.832
versus 333.994/400.603 (Validate), and 505.964/444.044 versus 384.684/367.043
(AssumeValid). Other changed rows overlap. Accept the range improvement and
consistent primary string medians; do not claim a settled exact string effect.
No confirmation, tuned variant, CPython ratio or catalog Status update.

Retain adverse/control observations: folded +11.15/+4.92%, uncaptured split
-1.60/+10.46%, tiny captured search+3.48/+6.09%, unchanged Core literal+.26/-4.74%
and unchanged .NET split+23.02/-10.36%, all overlapping. Controls are not
normalizers. Full 14-row data preserves negative/failed-branch/repeated captures.
Ordinary string allocations 274648B, finite 278848B, deadline 278896B and funded
cursor 197320B are unchanged. Range allocations vary by small incidental amounts
around 319.8kB; raw samples retain them. Shared modeled peak 177B includes 17B held
by the caller; final loans 0. These are admission observations, not heap/RSS bounds.

## Validation and preserved delivery

Windows/Linux focused Release pass per .NET 10/.NET 11: Core 4694 / PythonRe 681 / Pcre2 874.
Independent scalar predicates cover all 128 ASCII values, borrowed offsets,
NUL/EOF/tails and BMP/astral context; history-enabled execution checks eligible
and excluded roots. Every work cutoff clears refused slots and preserves the
caller reservation. Existing profile checks cover result/working limits, original
interruption, captures, aliases and input policies. Both 342-document Core PDB
inventories/PE identities verify; only the intended production file and generated
revision differ. Reuse adapters and test binaries; build only Core/Core tests.

Canonical raw artifacts/experiments/captured-atom-root-20261112/:

- Gate CBA9910C0BCD78082C101C940B9A2E552B8F5BDB06376F7A632BA8F6B6974243.
- Public 1D7BAD9180BE15C5B42F611A37DEFA6CA6FC2DEFE5A9C0563CD1E9D4ED4BF11F.
- Windows 8D5715572AAAC5A211F05FE4A97292AB24D2A325CE7229E582C4644FAAFD2345.

Linux archives verify offVM before keeper release. All 476 canonical package/symbol
bytes remain unchanged. Current catalogs/packages still describe accepted dev77;
this source change does not trigger a package or Full refresh. Material
Core/Python gaps and general POSIX history requirements remain active.

Implementation 20174326 CI43275206203 passed Windows/Linux. VM source is aligned; all13 measured DLL/PDB pairs remain unchanged.
