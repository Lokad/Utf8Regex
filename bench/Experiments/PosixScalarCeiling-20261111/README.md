# POSIX scalar endpoint ceiling — accepted

The [ASCII alphabet proof](../PosixAlphabetCeiling-20261111/README.md) now also
admits exact non-ASCII literals. Its original bounded instruction walk derives
a conservative scalar interval. ASCII keeps the checked span search; a wider
literal alphabet decodes complete scalars within the same 2048-byte probe,
charging work every 64 scalars. A probe cut inside a scalar keeps physical EOF.
The same local bound, pruning, whole-only exit and actual input remain; no new
field, cache, executor, option, allocation or validation pass is introduced.
Positive ASCII classes remain eligible; general wider classes, complements,
dots and unresolved-parent histories keep their existing execution.

One **114.543s** quiet .NET 11 RC3 ordinary-process ABBA under both input policies,
14 public rows/560 samples, minimum **70.659ms**. Baseline `42d098d6` plus the
archived candidate patch; distinct staged POSIX DLL/PDBs, identical Core and caller.
Loaded producer identities are checked against staged bytes before timing.
Eight visible startup CPUs then CPU2, default tiering/PGO. Construction is excluded;
complete public calls include their selected validation and live policy costs.
Full byte/UTF-16 coordinates and retained capture values are checked after timing.
No confirmation or further timing follows.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Captured `(é\|éé)*`, 12 scalars then `!` |62.392 →1.563|61.945 →1.439|
| Same, 16 scalars |422.668 →2.248|418.779 →2.137|
| Captured `(😀\|😀😀)*`, 12 scalars then `!` |63.689 →1.672|63.247 →1.540|
| BMP finite/live captured match |77.885 →2.057|77.444 →1.901|
| Astral funded/live captured match |80.091 →2.303|79.548 →2.188|
| BMP whole-only match |44.229 →0.619|43.811 →0.524|
| Complete BMP Count |44.137 →0.741|43.805 →0.651|
| Complete astral cursor with retained capture |64.574 →2.088|64.154 →1.982|

All target ranges are disjoint: **96.8–99.5% less time**. Finite/funded allocations
fall 1712→about1024B/call; the funded after-process observations include small
pool-fill medians1024.002B. Other targets keep32/48/352B. Modeled shared peak
falls1521→977B including the held17B parent; final outstanding is zero.

All unchanged controls overlap: ASCII non-EOF +0.63/−2.93%, wide non-EOF
+1.57/+2.51%, SKU +1.83/−0.87%, common Core −1.74/−0.82%, predecoded .NET Count
−10.69/−0.87% and Unicode EOF +0.19/−2.15%. Retain every process/range rather than
normalize by controls or claim exact equivalence. The preceding ASCII pilot's
disjoint wide-control costs remain recorded; this comparison does not erase them.
The equal-result .NET Count is a control, not a POSIX capture-parity comparator.

Full POSIX Release277 checks per target pass on Windows/Linux .NET10/11; Linux
hardware-disabled277 per target pass too. Existing occurrence oracles and
longer-whole/parent counterexamples remain. New BMP/astral non-EOF cases cover
finite budgets, byte floors, capture coordinates, whole/Count/cursor aliases,
retained values and malformed strict tails. Four new Unicode cases fail the
explicit frozen baseline at100001/100000 work, while four prior ASCII cases pass;
all eight pass after candidate restoration. Longer alternatives across probe
boundaries include cut three-byte scalars and keep their true longer endpoints.
No grammar limits, input policies or interruption/ownership contracts change.

Canonical raw evidence is copied off VM and hash verified under
`artifacts/experiments/posix-scalar-ceiling-20261111/`:

- `evidence.tar.gz`: 9DFBA588D116C257D5FC28A485F0EBC32DBFCA0230CD46A955B5F3FD1FE461A1.
- `windows-checks.tar.gz`: E929A7429076D2440A7E24FD69D279FB67B43888D866FBC45C3D0AE7796325DB.

Summary, offline identity/result verification and frozen producers accompany the
archives. The comparison completed without a failed timing or caller repair.
No Full/package/catalog repeat or mutation of dev.69 artifacts. Broader POSIX
history architecture and Core/Python parity remain active.
