# Reject widening the short-family counter solely by comparator extent

**REJECTED; eight-byte strategy gate restored.** Baseline
`f32a946c6870ff3d09ec77f4fbb56f253a6891da`. The trial changed only the existing
2–4-literal counter's maximum needle length8→16, reusing the same checked
comparator, fields, pair selection and counting loop. The comparator supports16
bytes, but this does not establish that the search strategy is appropriate.
A source comment now explains that distinction; no widened counter remains.

One **133.776s** two-policy ordinary-process ABBA under180s,12 public lanes,
**480 samples >=126.332ms**. Same caller/adapters/control graph, distinct Core
pairs;8 CPUs then CPU2 affinity, default runtime settings. Read-only runtime
proof verifies that name/long-family counters engage only in the candidate;
dense short-family eligibility stays true. Every public Count result is consumed
after timing;2598 corpus matches agree with .NET. Parent17/peak17/refund0 stay.

| Public operation | Validate before → candidate µs | AssumeValid before → candidate µs |
|---|---:|---:|
| core/name-compiled | 3668.698 → 34811.488 | 3122.830 → 34485.875 |
| core/name-ordinary | 3622.522 → 34259.898 | 3136.178 → 34037.863 |
| pcre2/name-count | 3764.550 → 34460.113 | 4440.189 → 35517.188 |
| core/long-four | 43.393 → 27.927 | 43.283 → 28.050 |

The real mixed-length corpus slows about8–11×, disjoint in both modes; the
synthetic9/11/13/16-byte family improves35%, also disjoint. The latter cannot
justify the realistic regression. Execution allocations remain0B (governed112B).
Finite/funded, Python and most unchanged controls overlap; the full summary
retains trusted funded+24.98%, .NET+9.57% and other adverse/uncertain medians.
No normalization, confirmation or tuned variants follow.

An untimed read-only probe explains the selection failure. Candidate primary
index2 searches lowercase m/w/c/n, with1,668,192 raw byte occurrences in this
corpus; the existing first-byte family search uses uppercase F/H/S/T,103,198
occurrences. That is16.2× greater raw population, not a measured candidate count
or phase time. Secondary index0 later rejects many hits, after their search and
loop cost. The generic primitive is ineligible here (shortest needle3<5); the
baseline uses prepared family search. Static frequency ranking currently treats
uppercase/lowercase letters equally. Widening only the length gate bypassed a
more selective existing route. The failed first probe assumed that primitive
existed; final probe handles its absence. No timing sample was repeated.

A future distinct design would need case-sensitive selectivity evidence and an
explicit strategy choice, then realistic sparse/dense and mixed-length controls.
Do not relift this cap using the same scorer, add arbitrary thresholds/switches,
or infer performance from comparator correctness alone. Keep this trial closed
while moving to the next supported larger gap/accepted-result refresh.

Candidate focused370 Release checks/target passed Windows. Restored362 per
.NET10/11 pass Windows and Linux. The retained public tests cover3–17-byte
families, every incomplete tail, offsets, near misses, supplementary prefixes,
RTL, NonBacktracking and captured Count; they do not require the rejected counter
to exist. Three long/mixed/rejected constructor families also retain independent
admission/oracle coverage. Original source arithmetic/resource behavior is back;
no public contract or API change, no dormant strategy or compatibility mode.
The earlier accepted4/5-byte BCL search remains intact.

Raw graphs/PDBs/caller/protocol/patches/proofs/observations:
`artifacts/experiments/short-family-sixteen-20261110/evidence.tar.gz`, SHA256
`71CC73B31FBE1A46A15A362CC1B5F991337F97916A14CB16F50A10EEEB2B2355`.
Derived summary and final untimed probe source/outputs are beside the archive.
Restored Linux source patch/checks archive SHA256
`672662B1467E6C0D08EF955F7516D40DCD50CEF47E561CE191C314575CBD9C2F`.
All evidence is offVM/hash verified; all workers closed. No package/full refresh,
publication, new owner/framework or altered immutable artifact. Timing closed.
