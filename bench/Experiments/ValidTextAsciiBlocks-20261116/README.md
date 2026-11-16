# Valid-text sizing skips proved ASCII blocks

Accepted: inside each existing scalar-aligned4096-byte sizing block, use the
already-used checked BCL range search to find the first non-ASCII byte. Count
its proved ASCII prefix directly and apply existing checked unit masks only to
the remaining bytes. All-ASCII blocks omit masks entirely. This addresses the
[measured value-coordinate regression](../PythonValueUnitSizing-20261116/README.md)
with a distinct BCL-primitive change. No mode, cached classifier, decoder,
allocation, new validation pass or checkpoint/admission change. Scalar matching,
byte/UTF16 endpoints, generic decoder fallback and PCRE2 behavior stay unchanged.

One112.686s ordinary-process ABBA /180s cap,0 confirmations. Core2BAAF4AC→19C87916;
Python0220DAB3 DLL/PDB and common caller/.NET are identical. All393 actual compiler
documents, producer DLL/PDB/member and input identities verify. Normal8CPU startup
thenCPU2,1s warmup,75ms calibration plus one calibration check, five150ms-target
samples/process/row, all>=20ms. Full returned byte ranges/strings and finite/funded
root refunds are checked after timing. No public contract revision.

Windows and SAME-byte Linux per.NET10/11: affected Core847/full PythonRe5235 pass;
focused78/TFM cover borrowed/block cuts, pure ASCII plus2/3/4-byte tails and original
interruption. Source integration guard and intrinsics-disabled public preflight
pass. Core alone rebuilt; Python producer bytes remain unchanged. All684 immutable
feed files rehash unchanged; no package/Full/canonical refresh for this pilot.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| native-full-unicode | 43.742 | 45.970 | +5.09% | overlapping |
| native-full-ascii | 40.659 | 12.459 | -69.36% | separated |
| native-offset-unicode-prefix | 43.563 | 45.301 | +3.99% | separated |
| scalar-full-unicode | 7.236 | 7.179 | -0.79% | overlapping |
| funded-scalar-full-unicode | 8.505 | 8.650 | +1.71% | overlapping |
| scalar-search-prefix | 11.186 | 11.193 | +0.06% | overlapping |
| short-value-control | 0.284 | 0.284 | +0.18% | overlapping |
| ledger-ascii-string | 237.342 | 241.663 | +1.82% | overlapping |
| ledger-string-control | 835.523 | 830.623 | -0.59% | overlapping |
| coordinate-control | 15.460 | 15.779 | +2.06% | overlapping |
| unchanged-core-count | 12.602 | 12.457 | -1.15% | overlapping |
| unchanged-dotnet-count | 22.740 | 22.696 | -0.20% | overlapping |

Native ASCII FullMatch falls40.659→12.459us (−69.36%). Retain native Unicode+5.09%
(overlapping) and Unicode-prefix+3.99% (separated) costs; other ranges overlap.
All allocation medians agree. This substantially reduces the preceding about5x
ASCII cost; it does not establish equivalence to the original7.9us baseline.
Keep the two experiments' identities/ranges; do not compound percentages or claim
a qualified cumulative comparison. Stop this change without further variants.

The inherited raw label `ledger-string-control` is an affected consumer of the
same counter, not an unchanged control. Both string rows are affected workloads.
Their absolute costs differ from the preceding experiment; retain observations
and avoid cross-run causal claims. The coordinate/short/Core/.NET routes provide
unchanged controls. Raw labels and samples are preserved.

Original worker/collector terminal0; raw verified offVM. Artifacts:
artifacts/experiments/valid-text-ascii-blocks-20261116/.
Raw SHA256 AE9CD20DDB36D4A0418003F32669892DB1E87EE7C200BAECCE2103E1A5DE121E;
validation029D1067C8D3ADFBDA19A4F8C275E6442BDD83864291AEF4605F44F41DFADA6E.
