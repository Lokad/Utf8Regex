# POSIX alphabet endpoint ceiling — accepted

The existing history pruning no longer requires physical EOF when the program's
consuming atoms prove an earlier endpoint bound. After the first general
acceptance, a walk of at most 64 instructions admits exact ASCII literals and
positive ASCII-only class masks. A conservative interval containing every admitted
scalar cannot cross its first excluded byte. One checked search of at most 2048
following bytes finds that bound; a proof/search miss keeps physical EOF.

The existing first-root-child prefix comparison and whole-only longest-match exit
reuse this bound. Actual input is retained for anchors, strict validation and
coordinates. There is no new executor, field, cached fact, copied program, option,
allocation or ownership contract. Original work/interruption checkpoints meter
the bounded proof/search; numerical work and workspace costs can decrease.
Unclosed parents, wider/complemented/dot consumers and unproved bounds retain
general competition. This does not establish general linear-time matching.

One **114.139s** quiet .NET 11 RC3 ordinary-process ABBA, both input policies,
14 public rows/560 samples, minimum **68.580ms**. Baseline `b64aba0b`, candidate
plus archived source patch; identical Core DLL/PDB, caller and inputs, distinct
POSIX DLL/PDB pairs. Each process starts with eight visible CPUs then selects
CPU2, using default tiering/PGO. Construction is excluded. Every returned whole
match and capture coordinate is checked after timing; Count and cursor progression
are checked too. No confirmation or further timing follows.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Captured `(a\|aa)*`, 12 a's then `!` |61.246 →1.404|61.292 →1.425|
| Same, 16 a's |424.418 →2.093|418.119 →2.111|
| Captured `(a\|a[ab])*`, 12 a's then `!` |62.034 →1.449|63.136 →1.490|
| Finite/live captured match |76.571 →1.910|78.087 →1.920|
| Funded/live captured match |77.141 →2.043|78.298 →2.068|
| Whole-only match |44.418 →0.508|43.151 →0.511|
| Complete Count |43.951 →0.643|43.079 →0.629|
| Complete cursor with retained capture |62.284 →1.812|62.377 →1.850|

All target ranges are disjoint: **97.0–99.5% less execution time**. Finite/funded
allocations fall 1712→1024B/call. Other targets retain 32/48/352B. Modeled shared
peak falls 1521→977B including the held 17B parent; final outstanding is zero.

Preserve adverse unchanged-control movements: wide/complemented history
**+5.17/+4.52%** (3.184/2.805µs), disjoint; Unicode history +0.39% overlaps and
**+1.56%** (0.146µs) is disjoint. EOF history −4.35/−1.84%, SKU +1.06/+4.28%,
common Core −1.76/+0.85% and predecoded .NET −0.08/+0.39% overlap. SKU shows
small pool-fill allocation observations, 96→96.014/96B and 96→96.021/96B across
candidate processes; no blanket unchanged-allocation claim. Large target savings
and lower governed storage justify these explicit local costs. The .NET Count
control supplies equal counts, not POSIX capture parity or a native benchmark Status.

Full POSIX Release tests pass on Windows/Linux and .NET 10/11, 268 per target;
Linux hardware-disabled 268 per target also pass. Existing independent occurrence
oracles cover non-EOF suffixes, branch order, nested/nullable captures and longer
whole/parent counterexamples. New finite 32-scalar cases cover both policies,
ASCII/astral/NUL separators, byte floors, whole/short/full output, cursor aliases,
retained values and strict malformed tails. Four cases fail on the explicit frozen
baseline at 100001/100000 work and pass after candidate restoration. Longer fixed
alternatives across the 2048-byte probe boundary still win correctly.

Initial fixtures were corrected before timing: the general-history interruption
control now uses a genuinely wider complemented class, and long branch tests use
nested intervals within the existing ERE 255 cap. Initial untimed worker build
output loaded the candidate on both sides; explicit staged pairs corrected this.
The actual comparison verifies every loaded DLL/PDB against its staged file before
timing, as well as the common caller/control identities. No invalid timing is used.

Canonical raw evidence is copied off VM and hash verified under
`artifacts/experiments/posix-alphabet-ceiling-20261111/`:

- `evidence.tar.gz`: E56B1E08CD6BDDBF3B481ABB1876E05C77AF5F163AF6B161244A3B6D83C5EF1D.
- `windows-checks.tar.gz`: CA648410C595441A70901F8112C7CB928ABB0352B397D3D0DF0CE71DCB1D94DB.

Summary, offline identity/result verification, initial failures and final checks
remain alongside the archives. No Full/package matrix, catalog refresh or mutation
of immutable dev.69 artifacts. General parent-history architecture, wider scalar
bounds and Core/Python parity remain separate work.
