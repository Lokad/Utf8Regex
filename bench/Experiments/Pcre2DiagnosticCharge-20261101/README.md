# Diagnostic classification extraction: deferred, fully reverted

At committed48df5d6c/engineecfd28a1, moved the unchanged diagnostic switch from
ChargeBacktracking to its existing collector. Kept the exact numeric increment,
TryChargeStep and profile failure conversion, with no new mode, accounting,
algorithm or inlining attribute. No production change from this experiment remains.

Actual .NET11RC3 Tier1 inlined the smaller charging body into the VM, removing
its call; VM code17512->16649B. Both probes consumed two million correct public
results; instruction/request sizes stayed40/72B. Code size is diagnostic evidence,
not a performance result. Existing focused247 and full6486PCRE2 Release checks
pass per runtime on .NET10/11; restored source was rebuilt and247focused checks
pass again per runtime.

The [one bounded public comparison](comparison.json) completed63.809s,17cases,
minimum35.939ms samples. Warm1.5s/min1000, independent60ms calibration, five
alternating samples with complete result consumption. All allocations unchanged.
Datehit-3.25%, miss+2.16%, densemiss+3.68%, nonwordCount+6.31%, Unicode-heavy
+4.56%, subroutine-2.71%, finitehit/miss+.56/+.57%, deadlinehit/miss-2.14/+.32%.

The unchanged literal control was+15.50%, with1.006/1.003before/after spreads and
all five paired observations+14.93..15.82%. Its cause is unresolved; this does not
establish that its execution traverses the edited helper. Retain the adverse
observation. Mixed changed-route costs and that stable adverse control do not
support promotion. No noise confirmation justified, none ran; no variants,
package qualification or benchmark table refresh. See [all observations](summary.json).

Sealed archives under
`artifacts/experiments/pcre2-diagnostic-charge-20261101-deferred/`:

- `evidence.tar.gz`: exact before/after DLL/PDBs, source patch/full candidate,
  public driver, codegen probes/disassembly, builds and raw measurements.
  SHA256 `91463B55FC44C288C70F58284D3DC0B4917B03597768784ED24ED98741BDD639`.
- `correctness.zip`: exact Windows candidate source/patch, both-runtime
  libraries/PDBs/test binaries and focused/full logs.
  SHA256 `64CA719286320C4161EA3009F87B70E1CB31EEBB19919644143F7E5BE16D2C7B`.

Published/staged artifacts remain immutable. The fixed-original cumulative
performance gate remains at the next coherent delivery boundary.
