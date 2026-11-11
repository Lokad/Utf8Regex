# Borrowed literal Count validity — accepted

Baseline5cdae5c1. `CountForResult` now always requests the existing CheckSubject
authority, removing a branch that computed and discarded complete UTF-16 subject
metadata for unmetered Validate. Python's offset/family Count and canonical
FindAll strings consume this helper. Coordinate-producing APIs retain their facts.
No new state, validator, kernel, owner, execution mode or public API.

One **85.389s** .NET11 RC3 quiet-VM ABBA per policy,14 public rows/336 samples,
minimum45.841ms. Distinct Core DLL/PDBs, identical Python/caller/input bytes;
one producer per ordinary process,8CPU startup thenCPU2, normal tiering/PGO.
At least2s/1024 real canonical-corpus warmups,75ms calibration target. Complete
strings/counts are checked after timing. No confirmation or tuned variant.

| Public operation | Validate before → after (µs) | AssumeValid before → after (µs) |
|---|---:|---:|
| Canonical FindAll, actual Chinese corpus |1263.920 →83.951|23.670 →23.603|
| Offset Count, actual Chinese corpus |1268.453 →83.463|23.214 →23.105|
| Canonical FindAll,1MiB ASCII |36.747 →28.332|16.701 →16.684|
| Canonical FindAll,1MiB BMP |666.352 →76.477|16.654 →16.699|
| Canonical FindAll,1MiB astral |2608.626 →1137.877|16.588 →16.767|
| Mixed UTF8 family Count from byte floor |126.389 →116.911|107.377 →106.948|
| Canonical FindAll,1MiB BMP miss |661.347 →76.415|16.424 →16.757|

All strict improvements above have disjoint sample ranges. Trusted corpus offset
Count−0.47% and trusted BMP miss+2.03%(+0.333µs) are also disjoint; retain the latter
cost explicitly. Strict ASCII family Count+0.36%(+0.382µs) is disjoint. Other trusted
targets/tiny cases overlap. Allocations are unchanged in every row: canonical
corpus304B, large hits80B, tiny72B, counts/misses0B. No native-parity claim.

Unchanged captured output+0.09/+0.83%, actual Core Chinese−0.14/−0.17%, finite/live
canonical−11.85/+0.04% all overlap. Unchanged predecoded.NET−0.64% strict has
disjoint ranges, trusted−0.11% overlaps; no normalization or causal credit.

Focused Release **Core222/Python130 checks per target** pass Windows/Linux on
.NET10/11: whole-input rejection even before a byte floor, native ordered counts,
captures, policy propagation/at-most-one validation, canonical identity, finite
admission, original cancellation/deadlines, publication and refunds. Reused tests
gain two Unicode offset rows, one literal policy case and malformed-prefix coverage.
Final Linux Core DLL/PDB exactly match the measured pair:
1ABB591368123C6491D5FE7C81D2CD170CFD06150A8BA723F422DB95141395AE /
9DD5EF03564E3D6311588E915F7C010B5F7A0314AC0C8F995B3B656B6A8A3857.

Hash-verified offVM evidence, `artifacts/experiments/borrowed-count-validity-20261111/`:

- Primary/source/pairs/Linux: `7254F6BA204B494AA44714E69832D2CB6EBE5B1BAAAF05FCAC54046517D3DE01`.
- Final patch/Windows/identities: `B892F519E0381A38470F15A60D50EDBE54746A8499D49B7995EDF94EDCADE3D8`.

Strict validity, Unicode/byte results, borrowed-input contracts, original authority
and meaningful admission stay. Specifications clarify fact selection. Immutable
packages and generated catalog identities stay unchanged; no package/Full repeat.
