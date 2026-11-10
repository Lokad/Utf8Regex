# Python trusted discovery defers unused subject facts

Accepted source `acb1e6f32e860dbea535a8387f1ce0c177254806`. The shared scalar
cursor retains strict entry validation and defers its trusted-input description
until string, mapped-coordinate or necessary ASCII-hint consumers request it.
One-shot values count their requested prefix/range; ranges and discovery need no
whole-subject coordinates. The existing description is cached once requested.
No public API, engine, global cache, ownership mechanism or unchecked read.

One **136.183s** Linux .NET11 RC3 separate-process ABBA per input policy, under
180s; 12 public lanes/policy, five samples/process, **480 samples >=43.684ms**.
The caller/Core DLL/PDBs are identical across producers; only Python differs.
Eight processors remain visible at startup before CPU2 affinity. Read-only
route inspection confirms two captures and no native value fast path. Result
checks occur after timing; returned borrowed values copy coordinates during the
same caller operation. Finite/live-token/funded cursor loans return to zero.

| Public operation | AssumeValid before µs | After µs | Change |
|---|---:|---:|---:|
| captured Search, 4 KiB ASCII tail | 0.439 | 0.429 | -2.4%, overlapping |
| captured Search, 1 MiB ASCII tail | 10.923 | 0.427 | -96.1% |
| captured Search, 64 KiB BMP tail | 13.197 | 0.420 | -96.8% |
| captured Search, 64 KiB supplementary tail | 68.423 | 0.419 | -99.4% |
| captured Search, mixed prefix/64 KiB BMP tail | 11.677 | 0.455 | -96.1% |
| anchored miss, 1 MiB ASCII tail | 27.377 | 16.831 | -38.5% |
| finite/live/funded range cursor | 14.077 | 1.301 | -90.8% |

Except the 4 KiB hit, these ranges are disjoint. The anchored miss retains a
whole-input required-literal absence check; this change removes its description,
not that separate search. Default Validate still rejects malformed suffixes and
retains its whole-input cost. Its early-search movements mostly overlap, with
only the 4 KiB case disjoint at -2.5%.

Dense results retain uncertainty: trusted many-capture strings +0.14%, bytes
+5.51%, captured split +9.73%; default strings -18.54%, bytes +12.81%, split
+6.03%, funded cursor +15.85%. Every one of these ranges overlaps. Preserve these
observations rather than claim equivalence or a dense-output speedup. The strict
Core control moves -0.47%/-0.65%, overlapping. Allocation baselines stay 272 bytes
for Search, 968 for the funded cursor, 242008 for dense tuples and 274648 for
split; intermittent process/runtime allocation noise remains in the raw samples.
These measurements do not resolve the capture-heavy CPython gap or establish a
new native-parity ratio.

The initial run is preserved as incomplete: Validate ABBA finished, but the first
changed trusted-search sample fell below20ms after calibration. One confirmation
used the same production binaries with a common revised caller (500ms warmup,
125ms calibration). No further campaign or source tuning. Initial archive:
`artifacts/experiments/python-subject-facts-20261110/initial-incomplete.tar.gz`,
SHA256 `C614F6163A7448986CDE05F20CD0578C132D5332A5A271C9ADC60A117A25E54F`.
Completed raw samples, caller, graphs/PDBs and identities:
`artifacts/experiments/python-subject-facts-20261110/evidence.tar.gz`, SHA256
`7D6CDC2E88753B6BB4AC771180B25A64F6E4A0D4630FC24F1E1243EB68005270`.
The full derived summary is alongside the archive. Baseline Python is9df5eb7c;
both graphs use the same Core3dc2e65a; changed Python isacb1e6f3.

Python Release suite5035/target passes on .NET10/11, plus final focused37/target:
malformed early-failure suffixes, EOF/mixed UTF16 coordinates, range aliases,
retained bytes, late scalar projections, original-token cancellation/deadline
failure and synchronous refunds. Shipped package bytes, ordinary generated
benchmark tables and accepted dev66 qualification remain unchanged. Timing is
closed. Continue with construction duplication and shared capture/output costs.
