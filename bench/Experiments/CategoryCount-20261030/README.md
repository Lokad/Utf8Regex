# Metered single-category Count under scalar dispatch

Producer **0b5ad679678eddefcf80575d4dc632b39196914a**. Baseline production
**dab878a6d49247a127c8ac638c5b3476d3236773**; intervening commits only refreshed
snapshots/receipts. One bounded public comparison, then one selective refresh.

The existing classifier proves complete, case-sensitive single-category patterns,
including ordinary capture wrappers. Public Count's scalar selector bypassed the
existing Unicode-category counter. [Before](before-route.jsonl) and
[after](after-route.jsonl) probes show the same proof/results and changed dispatch.
Case-folded/compound patterns, RTL, NonBacktracking and ECMAScript retain their
existing routes. Supplementary scalars count once with their scalar category;
general UTF-16 comparator equivalence is not promised.

After strict full-subject validation, the same invocation admits one work unit
before each scalar-aligned block of at most 4096 bytes, plus publication. It reuses
the existing checked SIMD/category kernel, including for unlimited calls; no new
matching buffers, engines, switches or unchecked access. Original cancellation,
deadline and spent validation work remain authoritative. PCRE2 has a real consumer
of the shared kernel; its existing profile execution is unchanged.

The counter needs no capture workspace or nesting records. Tight working/depth/
result limits may now admit a constant-space Count that formerly needed VM
capacity. Work charges are route-dependent, not a portable CPU/byte minimum.
Denial remains typed failure, never partial/no-match success. See the
[resource contract](../../../docs/SPEC.md).

Release correctness passes on both .NET 10 and 11: full Core **40107/runtime**,
affected PythonRe **124/runtime**, PCRE2 **163/runtime**. The 53 new Core cases
cover all 30 categories against scalar enumeration, supplementary membership,
capture wrappers, offsets/tails at 4096-byte cuts, folding/other-route exclusions,
strict malformed tails, pre-cancellation, constant-space finite limits and actual
native-phase cancellation/deadline/work denial. Existing API/managed inventory
guards pass in the full Core suite. [Verification](verification.txt) retains the
summaries. The immutable logs include a corrected test compile error (value-match
coordinates, not a nonexistent Value property).

The [public A/B](comparison.json) took **74.99s**, minimum sample **79.36ms**, under
a 120s cap. No confirmation. Quiet Ubuntu 24.04.4 VM, Xeon Platinum 8573C,
.NET 11.0.0-rc.3.26522.148, CPU mask 4. Each of eight lanes gets 500ms warmup,
independent >=80ms calibration and three alternating samples. Results/checksums
are retained/checked after timing. Ordinary/compiled producers and predecoded/
decode .NET lanes remain distinct; finite/live producers enforce controls that
BCL Count does not emulate.

| Public Count workload | Before / after ordinary | Speedup | Before / after compiled | Allocated bytes, before / after |
| --- | --- | --- | --- | --- |
| Published RiverCorpus, 69 matches | 1074289 / 1759.45 us | 610.58x | 1074139.5 / 1773.48 us | 0 / 0 |
| Same corpus, finite limits/live token | 1084427.9 / 1839.66 us | 589.47x | 1085411.9 / 1838.10 us | 120 / 120 |
| Dense BMP math symbols, capture wrapper | 2410.68 / 168.79 us | 14.28x | 2413.22 / 170.28 us | 0 / 0 |

The finite/live candidate costs about 4.6% more than ordinary unlimited Count.
Unchanged folded-category controls measured -5.7%/-4.1%, literal +0.1%/+1.2%,
and word Count +0.9%/+1.7% (ordinary/compiled), with unchanged allocations.
These controls are not attributed producer gains. This pilot is not catalog Status
or a final cumulative comparison against a40bda03.

The corpus is exactly the existing benchmark's 16013977-byte RiverCorpus, SHA256
`F2AA28234E7A8212C9E009FA9C67D1960D2D063D076765DE46B0FAED5FE44AD8`.
All public producer/.NET lanes return 69. Preflight initially ran from the sibling
driver directory and failed to locate benchmark data; running from the repository
root corrected the driver setup. Failed and passing preflight logs are archived.

Existing `--refresh-readme-case industry/leipzig-symbol-count 5 5` then qualified
both affected rows with clean source and independently calibrated samples.
The ordinary ratio is **0.07824x**, down from **46.62x**; compiled is **1.47622x**,
down from **836.35x**. Generated README tables are reordered. No other snapshot
case changed; parity verification passed. [Raw refresh](refresh-symbol.txt).
No full catalog/platform/package requalification was repeated.

Immutable evidence under artifacts/experiments/parity-category-count-20261030/:

- category-count-0b5ad679.tar.gz: before/candidate DLL/PDB, all benchmark/profile
  binaries and identities, source bundle, driver/probes/raw measurements and final
  generated snapshots. SHA256
  `202777A63FA8188785AA6E2307C40A6B32F86075130AC3F34E8E0DF4B57830A4`.
- correctness-logs.zip: original local focused/full build/test logs, including
  failed test compilation and its passing correction. SHA256
  `015A863D1A8E9FFB34F07A05321E2A86E0295DE5F333A2BC694C784684D76248`.

Final fixed-baseline cost, a current complete sub-hour refresh, immutable packages,
Windows PCRE2 uncertainty and actual Awk/Lython acceptance remain release gates.
