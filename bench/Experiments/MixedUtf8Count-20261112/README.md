# Mixed-width validated Count — rejected

Do not promote the full-block prototype on4328691d. It improves important strict
Count calls, but adds a116-line primitive and dispatch/fallback integration while
making several other Unicode Count workloads materially slower. This is a
trade-off decision, not a universal10% regression veto. Restore all nine prototype
source/test inputs; preserve the existing validator and measured literal kernels.
No threshold variant, second comparison, package or Full refresh was run.

The proposed algorithm shares common UTF-8 classification and exact three-byte
candidate discovery in checked32/64-byte blocks, carrying unfinished scalars
between complete blocks. It shares the validator's safe-three-byte predicate and
hands unsupported scalars to the existing validator with absolute error offsets.
It keeps counts private until whole-subject validation completes and uses the
original invocation for finite/funded/borrowed operations. No public API, retained
state, cache, mode or ownership mechanism was proposed.

This differs from the earlier rejected
[scalar-aligned overlap design](../ChineseValidatedCount-20261103/README.md).
The independent strict-decode/nonoverlapping-count model passes94,746 inputs and
1,705,432 comparisons in3.452s, including the real813478-byte Chinese corpus,
vector edges, pending-lead rewinds and malformed tails. That is a design gate,
not public performance evidence. Actual public probes engage the new counter on
ordinary/finite/funded/borrowed roots and exclude already-validated, trusted,
RTL and NonBacktracking roots. Strict/trusted validity counts remain1/0.

Final Release Tier1 uses64-byte blocks, five mask transfers per block and one
combined candidate mask; the counter is5812 bytes. The shared predicate inlines
in the existing validator, whose final code changes1266→1271 bytes. Smaller work
or comparable code size does not imply a public-operation improvement.

One **125.412-second** fourteen-row, two-policy ordinary-process ABBA comparison
retains560 samples, minimum69.248ms, maximum0.436MiB retained batch. Runtime is
.NET11.0.0 with eight startup CPUs then CPU2 affinity and default tiering/PGO.
Each row warms separately; calibration targets75ms with a20ms qualification floor.
Complete results/checksums are consumed after timing. Baseline is the exact
qualified dev.73 Core/Python pair: Core production source is unchanged from EAE
through4328691d. Python, caller and .NET comparator bytes are common; Core DLL/PDB
pairs are distinct and loaded hashes match staged bytes. Both PE/PDB identities
and342/344 Core documents verify, including six generated observations and seven
changed production inputs. Windows raw source and Linux LF patch manifests are
separate to preserve their actual source/PDB identities.

| Public Count, Validate | Before → after | Change |
|---|---:|---:|
| Real Chinese corpus, compiled |80.078 →66.433µs|−17.04%|
| Same, finite/live |112.452 →67.677µs|−39.82%|
| Same, funded/deadline/live |118.171 →69.704µs|−41.01%|
| PythonRe nonzero offset |74.803 →67.641µs|−9.57%|
| Dense mixed-width hits |49.896 →14.599µs|−70.74%|
| Existing three-byte hit family |18.716 →21.912µs|+17.08%|
| Dense first-prefix misses |9.824 →16.893µs|+71.96%|
| Leading supplementary fallback |80.398 →94.400µs|+17.42%|
| RTL control, new counter excluded |77.465 →93.365µs|+20.53%|
| NonBacktracking control, excluded |78.112 →93.787µs|+20.07%|

Every row above has disjoint observed ranges. The RTL/NB costs establish a public
producer-level penalty despite private exclusion; they do not identify which
shared source/JIT change caused it. Do not attribute those costs solely to the
new primitive or infer stable equivalence from its gate. Unrelated strict category
+2.12%, email+3.26% and .NET+2.17% overlap. Trusted primary/control rows overlap,
except the tiny ASCII Count control (−6.44%); strict ASCII−16.99% is also retained.
No normalization, causal speedup claim for unchanged controls or native-parity
claim. [All observations and per-process medians](cost-summary.json).

Allocations stay0B ordinary,112B finite and160B funded/deadline. Aggregate modeled
shared peak stays17B (the held parent reservation), final0; this is not heap/RSS.
Windows/Linux focused Release checks pass per.NET10/.NET11 target: Core591,
Python187, PCRE2124, POSIX145, Core software591 and32-lane178. They cover offsets,
tails, malformed-first-error coordinates, nonoverlap, strict/trusted propagation,
finite admission and original cancellation/deadlines. Initial two test failures
assumed a separate old search phase; the diagnostic hook was corrected to observe
the real combined scan before qualification. Namespace/property/private-probe
compile mistakes were fixed before timing, with original logs retained.

Restoration verifies all frozen inputs before restoring six tracked files and
removing three archived prototype-only sources. Matching four-profile test projects
are rebuilt for both targets/OS; Core581 and Python187 checks pass per target/OS.
Both tracked trees return to4328691d, measured producer pairs remain unchanged.
An initial Windows clean-tree assertion saw two CRLF checkout stat entries with
no canonical diff; refreshing those index entries left the cached diff empty.
No production contract or release documentation change remains.

Canonical hash-verified raw evidence is under
artifacts/experiments/mixed-utf8-count-20261112:

- Comparison/source:DC6D1A2E857238C357A1B675C606214F3E760A0C403B6B7D6B080DCA8FB3BBAA
- Code generation:70EE1964123AFA914CB8A16F88E17516A5769B2F3C66C5E51BDD5C80FA372501
- Windows candidate:B924FE017A054E8CCBFCBEA0A8434833BF6291A1CBB4DBA44B5B6A40C97E95BA
- Linux restoration:93E693CF463160389B612BA2F871CBBAE0FE5B70D8693D1D6D72CE5D5C608B18
- Windows restoration:3D30EBD8E7CCE91AACED41ECBE1C28A9B89E08C251C729C72085AC05B151A635

All evidence is offVM, collectors are terminal and keepers released. Historical
packages/tables remain immutable. Do not repeat this design unchanged or add
shape thresholds to rescue its controls. Resume a distinct source-supported
shared Python/Core cause; broader P0–P5 performance work remains open.
