# Wider checked ASCII drain for coordinate metadata

**Accepted with a dense-Unicode cost.** Eleven lines add64-byte checked
MemoryMarshal.Read<Vector512<byte>> ahead of existing32/16/scalar ASCII drains.
Metadata validation still counts identical scalar stops/UTF16 lengths and Kelvin/
supplementary flags. Existing2048-byte interruption/admission blocks, error
offsets, captures, aliases, resource controls and allocations remain. No new
state, table, mode, helper, accounting or API. This is the metadata drain, not the
already-wide validity-only common UTF8 drain or the rejected selector pilot.

[Untimed actual route](metadata-route.json): English folded-family Count validates
899232B/0D40805F, with895256B after the first non-ASCII byte;894266 suffix bytes are
ASCII. Metadata is produced before its selected Count kernel. The initial probe
also requested an industry case from a replica-only resolver; corrected to the
relevant actual family before any timing or producer change.

ONE **91.418-second** separate-Default A0/B0/B1/A1 compares frozen accepted Core
55924DE4 with663A4DCB. Caller and six adapter/benchmark DLL/PDB companions match.
Normal8CPU startup thenCPU2; producer warm1s/min16, independent60ms calibration,
five alternating batches. All160 owned batches exceed46.226ms. Typed results are
consumed after timing. BCL lanes have equal public results/UTF16 match ranges on
the shared valid subjects; byte coordinates remain covered by semantic tests.
No new CPython comparison, catalog Status, confirmation, source tuning or matrix.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| unchanged exact Count | 2.48 | 2.48 | -.21% | 0 |
| actual folded-family Count | 148.79 | 131.86 | -11.38%, uncertain | 0 |
| actual compiled family Count | 149.11 | 132.77 | -10.96%, uncertain | 0 |
| single validity-only Count control | 157.61 | 161.85 | +2.69% | 0 |
| actual compiled single Match | 72.05 | 62.42 | -13.36% | 0 |
| actual finite/live Match | 50.04 | 42.81 | -14.45% | 112 |
| actual deadline/live Match | 57.62 | 50.05 | -13.13% | 160 |
| dense Unicode Match neighbor | 1278.87 | 1374.33 | +7.46% | 0 |

[All observations](cost-summary.json) retain process/sample spreads and local
costs, without normalization. Match before72.16/71.89us exceeds after61.87/62.89;
finite before49.58/50.48 exceeds after42.47/43.06; deadline before57.35/57.84 exceeds
after49.78/50.58. These support acceptance. Family Count before160.18/137.29 versus
after122.67/140.70 overlaps; compiled Count similarly overlaps. Preserve pooled
Count movement without calling it a stable gain. Exact control is flat, validity
control overlaps. Dense Unicode before1284.65/1275.40 versus after1366.41/1376.65
shows a real local cost (~95us pooled); accept it for the broader metadata gains
under the user's trade-off policy. No universal10% gate or inferred phase sum.

Focused Release Windows/Linux per.NET10/11: Core1170/Python152/PCRE115 PASS;
69existing validation cases pertarget also pass with AVX512F disabled and SkipBuild.
Coverage includes borrowed offsets,31/32/63/64/65/tails, malformed offsets, exact
UTF16/scalar boundaries, Kelvin/supplementary metadata, caller loans, work denial,
cancellation/deadlines and retained results. No tests mirror the added width.
FullOpts DrainAscii351->456B emits the64-byte load/mask/first-bit stop, retaining
narrower paths; codegen is not timing evidence.

Immutable artifacts/experiments/metadata-ascii-wide-20261106:

- evidence.tar.gz: 15E03700DC317DF399C46D90D2EA4DAA8DAD17E5F561AB9355146DC2A4D76815.
- windows-correctness.zip: 93760DB88F0560E7CE5B5AE9ACB8116C9F17C6FF9B9545D5A2018B1736BA726D.

PredeclarationEC9C9CBB and runtime patch5B1F2DA7 were frozen before timing.
All historical packages, Full graphs and earlier evidence stay unchanged. Batch
this with the accepted early packed mask for the next delivery qualification.
