# Reuse the repeat proof through capture openings

Correctness/source checkpoint against `a0823313`; performance delivery remains
open because two affected cases regress. The existing bounded no-rewind proof
looks through CaptureStart as well as CaptureEnd. All capture writes, journals,
completion order and subsequent verification still execute at the selected
endpoint. No capture is elided; no opcode, field, cache, executor or mode is added.
The32-marker cap, scalar/RTL/NonBacktracking behavior, strict/AssumeValid contracts,
original cancellation/deadlines, admission and retained results remain.

An untimed loaded-pair public audit establishes actual engagement before timing:
32 adjacent-capture FindAllToStrings tuples require1223→1104 work steps, while
the already eligible nested control remains1104. The8192+8192 capture case with
1000work/depth1 refuses baselineDepth and returns complete correct strings after
the change, under both input policies. Removed exploration can reduce numerical
work, depth and workspace; SPEC records that intentional contract revision.

One **129.985s** quiet .NET11 RC3 VM ordinary-process ABBA per input policy:
14 complete public rows,560 samples, minimum47.276ms,75ms calibration target,
150s cap. Common adapters/caller, explicit distinct staged/loaded Core DLL/PDBs,
eight startup CPUs thenCPU2. All identities, input/result checksums and samples
verify independently. Maximum observed sample allocation437.029MiB fits the
declared512MiB check; capacity records retain actual RAM. No confirmation.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Adjacent-capture strings |1008.867 →833.374|985.401 →851.538|
| Adjacent-capture UTF8 output |1100.969 →872.529|1071.165 →864.905|
| Adjacent-capture Count |705.012 →538.283|690.254 →537.881|
| Strings, finite/live |1338.695 →1150.527|1321.738 →1166.361|
| Complete funded range cursor |1330.895 →1216.343|1323.610 →1176.039|
| Strings, deadline/live |1870.300 →1729.857|1852.485 →1774.873|
| Long detailed match |301.028 →15.469|302.354 →14.199|
| Complement/Unicode strings |117.176 →132.295|112.365 →132.019|
| Overlapping classes |1043.485 →1420.934|1022.670 →1417.535|
| Folded classes |1351.454 →1963.199|1347.289 →1951.990|

Count's22–24% and long output's94.9–95.3% gains have disjoint ranges, as does
strict funded-cursor−8.61%. Ordinary strings/bytes, finite/deadline, trusted
cursor and complement ranges overlap; their medians alone are not settled gains
or causal regressions. Preserve complement+12.90/+17.49% and all process modes.
The overlapping-class+36.17/+38.61% and folded+45.27/+44.88% costs have disjoint
ranges and must be addressed explicitly before the next delivery decision.

Allocations drop1056B per ordinary/governed/cursor operation. Long detailed output
falls429288→about66000B; retain66001.051B strict and66003.168/66001.002B trusted
observations alongside66000B. Shared-account peak1537→417B includes parent17;
final loans are zero. Overlap243064B and one-capture94344B stay unchanged.
Current hyphenated-string−6.31/−7.15%, one-capture−0.04/−0.84%, unchanged Core
+0.36/+0.33% and predecoded.NET+0.79/−1.22% controls overlap, without normalization
or a new native/CPython/catalog ratio.

Full Release Windows/Linux pass **Core42372, Python5119, Pcre6671 per.NET10/11**;
Linux noHW adds183 Core checks/target. Required assertion ends, opening/ending
marker limits, nullable/overlap/fold/duplicate/balancing captures, scalar coordinates,
live interruption and cutoff/refund guards remain. Old disjoint tails no longer
need their historical frames: exact denial tests now use genuinely overlapping
supplementary tails, retaining their precise cutoff assertions. A captured-window
Count explicitly succeeds with1working byte; a later backreference retains real
slot admission/refusal. No fake state or weakened basic metering repairs tests.

The initial Windows Core93 and Python52 failures per target were obsolete
resource thresholds, not result differences. Corrected focused Core371 and full
Core pass, followed by corrected full Python/Pcre. Initial Linux fullCore passed;
its corresponding Python fixture failed before timing. Preserve that archive,
correct only those Python tests and reuse the successful Core suite. Continuation
checks prove the candidate DLL/PDB unchanged across those repairs.

Verified canonical offVM evidence under
artifacts/experiments/capture-boundary-repeat-20261111:

- Final raw: `AAC163AB352FFFB9ED1C7EA6558AF90937C3E1A360127E39D9E484113DD8894D`.
- Windows: `D955F184BFF8BDD8B3BD5EC34264267235FE7DEBD1DBB3B53E99DC3A2C86C1EA`.
- Initial failed checks: `0A468423DA568E06D87EA58FFA49B241C3C0D1B59C40FCEDEA2A8EE4CB8C177B`.

All workers are terminal. Keep this structural/state saving as an intermediate
source step; broad performance acceptance remains unfinished. Investigate the
demonstrated extra proof cost at newly visible folded/overlapping tails, with a
fresh bounded design and the fixed original baseline. No further timing of this
unchanged candidate, new packages/Full or mutation of immutable dev.70 artifacts.
