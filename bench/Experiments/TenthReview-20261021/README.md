# Tenth review: bounded partial preparation and less replacement/runtime state

Development **0.4.0-rc.2-dev**. PLAN.md was rewritten before implementation
around M1–M5; earlier completed work remains in ignored PLAN.HISTORY.md.
Both planning files and the five original TODO handoffs remain intact.
Final source `8840fd00` has the same shipped C# as measured producer `1603f2dd`;
the later change repairs test thresholds. This is source qualification, not a
new release candidate or permission to publish frozen RC1.

- PCRE2 partial preparation checks fixed portions of the actual semantic tree.
  Whole-tree diagnostic serialization and ordinal-string classification are
  removed. Unrelated large trees decline without traversal/staging. The existing
  curated profile, options, captures and full/soft/hard Probe behavior remain.
  Character literal operands preflight tokens, admit exact storage and encode
  with bounded original-authority checkpoints. A fixed coarse tree-recognition
  work allowance and one traversal frame replace unmetered preparation; accepted
  operand arrays and descriptors share the original construction owner. Covered
  construction can now refuse earlier. No new partial VM or recognition framework.
- Replacement plans retain only their real instructions. Their unused positive-
  capture map/vector, capture placeholders, stride/snapshot/lookup machinery,
  range Tag/IsSet and lowered brace flag are deleted. The existing instruction
  eligibility scan already proves group-zero native templates, so no replacement
  flag is needed. Real numeric/named/sparse/unknown capture expansion and the
  parser's live syntax flags remain. Major arrays, output, overlap, cumulative
  result capacity and original interruption stay; removed metadata and smaller
  ranges intentionally reduce some construction/instruction/working/cache units.
- Literal execution returns its implementation directly. Two identical runtime
  forwarding classes, one actual object per applicable construction and benchmark
  unwrap cases are gone. Split retains its existing compiled-kind eligibility
  proof; SIMD, emitted paths, validation and scalar/empty/capture/RTL/controlled
  behavior remain. Actual construction allocation falls 24 B on the selected
  literal/family instances; this is distinct from the removed 128-byte admission.
- Removed unused native destination TryReplace/exclusive TryEmit and no-count
  cursor Replace forwards. Current engine-inventory/translation prose now names
  the owned implementation. Real public destination paths and the strict BCL
  dependency checks remain. No unsafe API, compatibility switch, accounting
  framework or dormant execution engine is introduced; actual Unsafe.SizeOf stays.

[Untimed preparation evidence](preparation.json) isolates the phase after parsing.
The original 4,096-repeat unrelated tree allocated **448,600 B** while recording
zero work/admission/checkpoints. It now declines in **104 B** with a coarse work
charge and no major payload. Under the original one-byte/one-step allowance it
refuses before growth; the canceled phase observes the original token. A
4,096-byte character operand previously acquired **12,368 B** without admission;
the admitted phase now acquires **4,328 B**, publishes the correct operand and
records its covered capacity. A one-byte ceiling refuses before acquisition.
The additional successful/decline lanes use a sufficient work allowance under
the revised units. These are isolated allocation/admission observations, not a
whole-Compile one-byte acceptance claim, live-capacity total, RSS or latency bound.
The backtracking parser's separate coarse storage admission is retained.

[Validation](validation.json) records focused Release checks for Core replacement,
literal/compiled/Split/empty paths, independent conservative cache coverage,
construction/work/result admission, original interruption and retained captures/
cursors across PythonRe and PCRE2. Filters overlap; counts are not additive.
The complete affected PCRE2 suite also passes. Initial builds/integration exposed
stale range constructor, reflection-layout and fixed byte-threshold assertions.
They are repaired against the smaller representation; denial/success boundary
checks remain. The formerly exact capture-list assertion now checks actual public
replacement values. No semantic fixture is disabled or skipped.

Windows/Linux source CI 39594045850
is terminal green at exact `8840fd0057fe5a3fc0233c8909f79f89689d20e7`.
All four Release suites pass on both platforms, without failed/skipped results;
existing compiler/analyzer warnings remain. Initial integration failures are
recorded in `validation.json`. Existing PE/PDB/source preflight accepts matching library and
benchmark binaries after the test-only final fix. Normal incremental build/server
reuse remains; no repeated solution builds or package matrices during experiments.

[One bounded cost comparison](measurement.json) uses a fixed original producer
`61429e69`, final measured producer `1603f2dd`, **identical driver DLL bytes**,
default tiers and one common logical CPU on Windows/.NET 10.0.13. Ten selected
public lanes warm through the timed loop for at least 1.5 seconds and a fixed
call floor; five samples alternate lane order. Calibration targets 45 ms with a
35 ms floor; actual samples can fall below that floor as throughput changes.
Each child has a 60-second outer cap. Construction uses separate retained
16-instance batches, or four for the Compiled-option family. Its raw identifier
`emitted-family` denotes that option, not proof that emission was selected.

The first observation is **inconclusive**: unchanged Core Count and predecoded
Regex controls slow 14.20% and 12.26%, alongside larger public increases. The
**single allowed confirmation** reverses candidate/original host order using the
same driver/protocol and frozen libraries. Confirmation public medians in µs:

| Public operation | Original → candidate | Change | Candidate B/op |
| --- | --- | --- | --- |
| Core literal Match, 896 B | 0.137 → 0.112 | −17.82% | 0 |
| Core family Count, 736 B | 0.765 → 0.791 | +3.45% | 0 |
| Core family EnumerateSplits, 736 B | 11.668 → 9.989 | −14.39% | 0 |
| Core native template Replace, 768 B | 20.513 → 15.780 | −23.07% | 5,144 |
| Core finite-four Replace, 768 B | 31.917 → 26.905 | −15.70% | 9,568 |
| PythonRe FindAllToUtf8, 768 B | 19.950 → 17.312 | −13.22% | 5,256 |
| PCRE2 curated soft Probe, 8 B | 0.159 → 0.127 | −20.19% | 80 |

All affected public allocations are unchanged. Finite allowances are 1M work,
depth 2,048 and 16 MiB working/result each, with infinite timeout and no token;
they are experimental allowances, not defaults. Correctness checks cover live
tokens/deadlines separately. Core control changes −9.29%; both overlapping Regex
controls materialize UTF-8 output and change −12.97%/−20.97%. One predecoded control
sample allocates an extra 976 bytes total; its allocation median is unchanged.

No persistent public regression above the 10% allowance is demonstrated; the
confirmation's largest selected increase is +3.45%. Moving controls and sample
spreads up to **3.12x** prevent an attributed speedup, universal equivalence or a
precise 10% bound. Construction is separate: confirmation medians range −9.14%
to +7.39%; initial medians differ substantially. Selected unrelated-PCRE2 and
character construction allocations fall 454,312 → 445,160 / 87,584 → 85,736 B;
curated construction falls 51,936 → 51,488 B and construction-plus-first-captured
Replace falls 134,872 → 134,480 B. No stable causal construction timing claim.
Timing is **closed**; no further confirmation or benchmark Status update follows.

[Immutable-file checks](immutable-files.json) preserve all four benchmark/parity
snapshots and nine original RC1 hashes. Existing read-only page/parity verifiers
pass. No package, symbol, zip, manifest, handoff or published artifact is rebuilt
or relabeled. Frozen common RC1 remains unpublished with known PCRE2 defects.
Fresh coherent package/downstream qualification, actual-package/host costs,
broader profiles/defaults and publication remain deferred; historical evidence
keeps its original scope. The completed checked-access and earlier cost campaigns
remain closed.
