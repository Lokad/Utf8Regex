# Reuse the POSIX whole-match coordinate origin

Before `7d5b4b89d353fc10d38581e27a0291bcaf409203`; after
`a90739883a568c9a98db9fe8a8aa27db0813bce5`. POSIX ERE captures stay inside
the selected whole match. Derive their UTF-16 positions relative to its already
counted prefix; reuse the whole coordinates for identical nested ranges.
No map, new allocation, ownership state or execution mode. Selection, byte
coordinates, checked access, per-slot/bounded-decoder interruption and admission
remain. Eliminated scans consume less work. Overlapping ranges **inside** a long
match can still require repeated local scans; this is not a linear-time claim.

One **37.187s** separate-process public `Match` ABBA on the quiet Linux VM,
.NET11 RC3. Eight startup processors, then CPU2 affinity; no tiering overrides.
Both producers use default **Validate**, with identical valid immutable inputs.
Both loaded Core/Posix DLL/PDB identities/revisions and staged graphs are checked.
Preflight verifies every capture independently. Timed results retain whole and
first/middle/last capture coordinates in preallocated arrays and consume them
afterward; the unchanged capture reads are included in both producers.
Each row warms500ms, calibrates independently to75ms, keeps five samples per
process;180samples, minimum72.995ms. Outer180s/worker45s caps. No confirmation,
native retiming, existing table refresh or package replay.

| Public workload | Before µs | After µs | Change | Before / after | Sample ranges overlap |
|---|---:|---:|---:|---:|:---:|
| Short, 1 capture |0.231|0.229|-1.21%|1.01|Yes|
| Short, 32 captures |4.107|3.879|-5.57%|1.06|Yes|
| 128KiB ASCII prefix, 32 captures |60.974|7.244|-88.12%|8.42|No|
| Unicode prefix, 1 capture |407.041|271.484|-33.30%|1.50|No|
| Unicode prefix, 8 captures |1354.075|271.892|-79.92%|4.98|No|
| Unicode prefix, 32 captures |4607.364|275.230|-94.03%|16.74|No|
| Unicode prefix, finite limits, 8 captures |1356.993|272.756|-79.90%|4.98|No|
| Unchanged whole-only control |271.533|271.056|-0.18%|1.00|Yes|
| Unchanged ambiguous-selection control |1.066|1.045|-1.94%|1.02|Yes|

Unicode prefix is16,384 copies of `é😀`:98,304bytes/49,152UTF-16 units.
Captures are nested around the single trailing `a`; the start floor skips the
prefix for selection. Finite policy:1,000,000 work steps/1MiB working capacity.
Affected allocations are unchanged:32/88/280B for1/8/32captures,416B for the finite
eight-capture lane. Whole-only stays0B. Selection control56.0046/56B includes a
small observed allocation variation; do not round it into a claim of improvement.
Short/control changes overlap and are inconclusive. Exact observations,
per-process medians, ranges, hashes and allocations: [summary.json](summary.json).

All239 POSIX Release tests pass on each of .NET10/11. New tests independently
cover long ASCII/BMP/astral prefixes, overlapping/empty/absent captures, partial
and excess output, cursor aliases and retained coordinates under both policies.
Existing work-refusal, synchronous-token/deadline publication cleanup and working
admission/refund tests remain. Four one/four-capture projection scaling cases
use the existing catalog/result shape; all16 catalog result preflights pass,
including equal native coordinates for the new cases. Native performance rows
remain pending the next coherent refresh; frozen tables retain their old scope.

Raw evidence: `artifacts/experiments/posix-prefix-20261110/evidence.tar.gz`, SHA256
`3EFD36A533A251DB6ACD6ADFD77F6099862FB70392023141D97AB8DB3A7DEE56`.
The adjacent `driver-validation.tar.gz` retains the exact driver/project,
bounded shell runner, summarizer, preflight and Release logs. No published or
staged package bytes were changed.
