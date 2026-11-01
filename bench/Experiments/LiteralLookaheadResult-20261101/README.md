# Reuse complete metered literal lookahead discovery

Source `5c05ff8b`. The existing prepared literal plan and metered confirmer
already prove a literal prefix followed by an exact positive lookahead. Allow
that proof through complete-result eligibility instead of excluding every
lookaround/trailing requirement. Other semantic fences remain. No new matcher,
state, switch or unchecked access; adapters borrow the original invocation.
The suffix is verified before publication and excluded from the match span.

The Python miss `foo(?=bar)` on `xxfoobazzz` previously used an11-instruction
scalar program and1872B. Its Core matcher already used0B, but complete native
result eligibility was false. The [preflight](preflight.txt) proves that gate
opens for exact/Unicode/overlapping families; negative and captured controls
retain scalar verification. A newly useful optional family Core can now be
retained where it was previously constructed then discarded; funded lifetime
and conservative admission tests remain green.

One [public comparison](comparison.json), **78.006 seconds**, twelve equal-result
cases/four lanes, .NET11RC3/quiet Ubuntu VM/CPU2. Warm1s/min64, independent60ms
calibration, five alternating samples, minimum **62.302ms**, output checksums
after timing. No confirmation. [Driver](driver.cs.txt), [summary](summary.json).
Source-equivalent baseline1fb3bea0 binaries are retained explicitly; candidate
Core is a2dfbd6b plus the eligibility patch, with the exact same Python DLL.

| Public operation | Before us | After us | Change | B/op before ->after |
| --- | ---: | ---: | ---: | ---: |
| Python actual miss | 0.688 | 0.250 | -63.61% | 1872 ->0 |
| Python hit | 0.564 | 0.279 | -50.52% | 1872 ->0 |
| Python finite/live | 0.824 | 0.357 | -56.71% | 1992 ->120 |
| Python deadline/live | 0.826 | 0.522 | -36.79% | 2040 ->168 |
| Overlapping family | 0.654 | 0.332 | -49.27% | 1872 ->0 |
| Unicode | 0.631 | 0.339 | -46.36% | 1872 ->0 |
| 4098-byte assertion tail | 21.921 | 0.383 | -98.25% | 1872 ->0 |
| Negative assertion control | 0.538 | 0.526 | -2.24% | 1872 ->1872 |
| Captured control | 0.726 | 0.713 | -1.84% | 4064 ->4064 |
| Shared Core Match | 0.095 | 0.097 | +2.67% | 0 ->0 |
| Core finite/live | 0.622 | 0.365 | -41.36% | 1992 ->120 |
| Unchanged Count | 0.403 | 0.413 | +2.56% | 0 ->0 |

Every actual-miss pair improves60.85–64.59%. Shared Core Match regresses2.12–4.76%
per pair, within the allowance. These .NET experiment lanes consume equal byte
ranges and include UTF-16-to-byte coordinate conversion; their resource semantics
and costs are distinct from CPython qualification.

One [clean selected CPython qualification](selective-refresh.txt), nine alternating
pairs per case, official CPython3.13.2, matching loaded DLL/PDB/source checks:

- Miss: qualified ratio **4.227 ->1.296 [1.259,1.322]**, still CPythonFaster;
  managed paired0.251us,0B.
- Hit: **2.288 ->0.847 [0.837,0.858]**, ManagedFaster; paired0.257us,0B.

The independent point lanes disagree: miss1.894us (spread1.21), hit1.976us
(spread1.42), versus paired~0.25us and the frozen A/B. Preserve these raw values;
do not assert a cause, rewrite them as warm evidence, or claim all timing paths
agree. Audit existing point/paired execution and warm-up before the final refresh.
No resampling campaign was run. [Only the two selected cases](snapshot-diff.json)
changed; unselected identities, corpus, lifecycle and scaling are unchanged.
Generated Markdown and snapshot/API/dependency checks pass.

[Release checks](verification.txt) pass per runtime: Core874+7615, complete
Python4493, PCRE2 1427, and snapshot/API/dependency3. Tests cover same-start branch
priority, Unicode offsets, retained aliases, finite working/result cap1/depth1,
all25 work-policy operations, all helper cutoffs and original cancellation/
deadline checks. Two internal assertions intentionally change: complete positive
lookahead is eligible, and single-literal FullMatch can check suffix/endpoints
without preparing a second scalar program. Explicit Compile remains eager.
[Core](../../../docs/SPEC.md) and [PythonRe](../../../docs/SPEC-PYTHONRE.md)
document the same-invocation proof and revised work/state use.

Immutable `artifacts/experiments/literal-lookahead-result-20261101/` archives:

- `evidence.tar.gz`: exact before/after and clean qualified DLL/PDBs, bundles,
  patches, raw comparison, paired logs and selected snapshots, SHA256
  `30B6D97E9DEF2276AE07B3B544134BC555591AA7CF24EFA3D66F69408E20F219`.
- `correctness-source.zip`: source/tests, driver/probe, bundles and complete
  verification logs, SHA256
  `9ECA9BA225F1B35AC771C3A889531076A3CD11A6C5239BB31B56092971C2B389`.

No package or complete catalog qualification. Final fixed-a40bda03 cumulative
costs, current complete sub-hour refresh and a fresh immutable package/consumer
boundary remain required; published/staged bytes are unchanged.
