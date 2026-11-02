# Reuse validated endpoint coordinates

PythonRe scalar value shaping revalidated a Unicode match to derive UTF-16
coordinates even when its endpoints were byte zero and subject end. Original
strict input validation already supplies those coordinates. Reuse them; keep
the existing coordinate-map/interior-span path. No buffer, state, execution mode
or unchecked access is added. Captures, retained values, owner lifetime and
publication checks are unchanged. Removing redundant scans intentionally lowers
work charges; allocation admission and result charges are unchanged.

One predeclared quiet-VM .NET 11 RC3/CPU2 comparison completed nine public cases
in **43.159 seconds**, under a 60-second cap. Separate assembly contexts load
fixed original `a40bda03`, current `b63c8151`, and current plus the archived patch.
Current before/after Core DLLs are byte-identical. Two compiled Regex diagnostics
compare predecoded and decode-then inputs on overlapping semantics. Warmup is
500ms/minimum 128 calls per lane; independently calibrated 60ms batches alternate
five samples, all at least **47.276ms**. Full UTF-16 range digests are consumed
after timing. Normal runtime startup precedes in-process affinity. No confirmation,
variant, catalog refresh or package qualification was run.

| Public operation | Before us | After us | Change | After vs original |
| --- | ---: | ---: | ---: | ---: |
| Captured Unicode FullMatch |110.952|99.056|-10.72%|-6.73%|
| Lazy Unicode FullMatch |111.940|98.680|-11.85%|-8.33%|
| Branched Unicode FullMatch |112.890|97.988|-13.20%|-8.22%|
| Terminal Unicode Search |111.905|100.351|-10.32%|-6.01%|
| Interior Unicode Search control |111.820|111.154|-0.60%|+4.26%|
| Finite/live captured FullMatch |125.652|110.873|-11.76%|-5.57%|
| Deadline/live captured FullMatch |127.264|112.424|-11.66%|-5.52%|
| Empty value at EOF |0.609|0.551|-9.53%|-21.20%|
| Unchanged native Search |0.110|0.111|+1.02%|+15.77%|

**Promote this correction.** Every allocation median is unchanged. The unchanged
native Search still exceeds the fixed-original allowance, consistent with the
[failed dev.10 gate](../DeliveryDev10-20261102/README.md). This subset does not
establish cumulative release acceptance; all previous stable failures remain
required controls at the next coherent gate. Current medians also differ from
the earlier gate, so these are direct simultaneous comparisons, not stitched
absolute timings. No new CPython Status or whole-profile speedup is claimed.

Focused Release projection/context/retention/resource/cancellation tests pass
86 cases per runtime; the complete PythonRe suite passes **4,583 per runtime**
on .NET 10 and 11. Coverage includes BMP/astral offsets, whole/end/interior/empty
values, detailed contexts and final-publication original-token/deadline injection.
Existing malformed-prefix/tail and finite-budget checks remain in the suite.
Source and correctness logs: `correctness-source.zip` under the raw directory,
SHA256 `17B434F10A645FCEE9DC5CF66E2AC6C67C8890995D3D93C6B3F093187AE95EEE`.

Raw protocol, driver, source patch, all samples/logs and three DLL/PDB sets:
`artifacts/experiments/python-known-end-coordinates-20261102/evidence.tar.gz`,
SHA256 `1917E91774DEDAD200D8C1C2CAAB795884383DBDF47113634DFE001EAD03C33D`.
`summary.json` retains medians, spreads and allocation results.

Separate read-only JIT capture of original/current entry paths shows scalar
`MoveNextCore` Tier1 code growth from 5,030 to 5,669 bytes and stack frames from
408 to 488 bytes. Both exact-literal entry paths inline; this does not demonstrate
that owner-entry exception handling prevents inlining or that frame growth causes
the regression. The capture did not time operations or change source. Raw inputs
and disassembly are in `artifacts/experiments/python-entry-codegen-20261102/evidence.tar.gz`,
SHA256 `129C8661BBABE34200D101987383C87A8F8B7D6C5613F37BD7B96669DBC5B45C`.

Published/staged packages are immutable. Further producer corrections precede a
fresh version and one complete cost/platform/package/consumer qualification.
