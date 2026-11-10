# Case-sensitive family ranking and16-byte gate — rejected

All production changes restored. The trial used memchr's case-sensitive ASCII
ranks in a checked constant-string lookup and extended the existing short-family
counter to16 bytes. No global folded ranks/thresholds, fields, unchecked API,
work charge or execution state changed. Independent preflight confirms the name
counter selects offsets0/2, fixing the earlier2/0 primary-filter weakness.
Fewer primary-byte occurrences alone did not establish a faster public operation.

One122.164s quiet VM.NET11 RC3 ordinary-process ABBA per input policy:
14 public lanes,5 samples/process,560 samples >=56.394ms. Frozen dev.67 Core
baseline, distinct staged Core DLL/PDB pairs, identical caller/adapter/control
files,8CPU startup then CPU2 affinity. Every result consumed after timing;
parent17 stays held per call and0 after release. One bounded75ms calibration/
confirmation,180s outer cap. Worker memory admission1GiB for retained corpora,
constructor batches and runtime headroom. No timed variant/confirmation follows.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| target name Count |3599.871->4219.494us,+17.21%|3107.828->3686.664us,+18.63%|
| uppercase corpus |+62.20%|+61.40%|
| lowercase corpus |-11.89%|-12.37%|
| dense short family |+71.16%|+72.98%|
| synthetic long family |-39.09%|-36.36%|
| PCRE2 name Count |+15.42%|+14.48%|

Those ranges are disjoint. Short-family tiny hits improve10.63/7.50%; constructor
+2.83/3.27% overlaps, with112B more allocation (62896->63008B). All execution
allocations remain unchanged. Governed rows overlap, including apparent trusted
finite-25.02%; unchanged predecoded/control rows overlap. Do not normalize away
these observations or infer exact phase costs from the source census. The target,
uppercase and dense penalties outweigh the synthetic win: reject the combined
strategy without threshold tuning or another gate-only trial. Existing8-byte
strategy and global heuristic remain intact; no memchr data is shipped.

The old tests duplicated the frequency formula. Retain their improved independent
oracle: verify unique-pair eligibility, ordered matching, funded retained payload
and mixed-case/width boundaries. No test requires a particular heuristic choice.
Initial candidate failures were stale offset/gate expectations, fixed before the
single comparison. Restored Windows/Linux268 focused checks per.NET10/11 pass.
No package/API/contract change or Full/package rerun; immutable dev.67 stays accepted.

Raw protocol/caller/patches/DLL/PDBs/results/license:
artifacts/experiments/case-sensitive-family-rank-20261110/evidence.tar.gz SHA256
8894AE26CAC5A6BE1C7284AB0C5113ECAAE7B7AA754ABBA70817A466BBABB4FB;
restored Linux checks93B88947658E88896EE80066AE9E2CE1E1F2852129CC9B6E27824554071166EE.
Copied offVM/hash verified; complete summary.json is alongside. All workers closed.
Next supported investigation is unobserved multi-group closing-order writes;
sole-group capture/split operations already omit them and remain controls.
