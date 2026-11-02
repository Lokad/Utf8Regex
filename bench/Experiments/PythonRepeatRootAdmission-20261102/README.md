# Early repeat-root admission did not improve the suffix control

**Closed; fully reverted.** The trial moved the necessary complete-root exit
check from the private literal-repeat proof into its existing FullMatch caller.
Suffixed repeats could then avoid entering the helper and declining its proof.
No state, helper, mode, algorithm or contract was added. The required useful
suffixed FullMatch gain did not occur: **+0.26%**, with tight spreads. No variant
or confirmation was attempted. The condition and accompanying test-fixture change
were restored completely.

One quiet-VM .NET 11 RC3/CPU2 comparison completed the same 23 public cases in
**107.537s**, under a 120-second cap, minimum sample **40.069ms**. Fixed original
`a40bda03`, parent `b757befb` and parent plus the archived two-file patch load in
separate contexts. Current Core bytes match. Equal-output compiled Regex
diagnostics use predecoded/decode-then inputs. Warmup is 500ms/minimum 128 calls,
independent 60ms calibrated batches and five alternating samples; full outputs
are consumed after timing. Runtime startup precedes in-process affinity.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Suffixed FullMatch target |36.073|36.166|+0.26%|
| Native Search hit |0.076|0.076|+0.17%|
| Native FullMatch hit |0.060|0.060|+0.09%|
| Eligible literal-repeat FullMatch |15.623|14.643|-6.27%|
| Nullable atomic Search |1.027|1.054|+2.67%|
| Unchanged native Count |0.117|0.114|-2.64%|

All allocation medians match. The unrelated eligible-repeat movement does not
satisfy the predeclared suffix objective. Every control, original ratio and
spread remains in `summary.json`; original suffix +10.73% and nullable atomic
+12.37% remain visible. This does not replace the cumulative delivery gate.

Full PythonRe Release passes **4,602 per runtime** on .NET 10 and 11. Cold
preparation authority was checked for both the suffixed fallback and a complete
root whose literal-prefix/class body declines the remaining proof. Source,
tests and binaries from this trial remain in the raw records; production changes
are not kept. Existing accepted short-position and checkpoint improvements stay.

Raw driver/protocol, three DLL/PDB sets, patch, codegen, all samples/logs:
`artifacts/experiments/python-repeat-root-admission-20261102/evidence.tar.gz`,
SHA256 `B75B00245A02F170510C4A5AAF6E60E7014F4A7E22F3FCEC0F42D24AE291CF64`.
`correctness-source.zip`: SHA256 `4E5A6B24320ED51C71740ABBBBC2DD81CC8CF6AF1B72F193572C0140DE98B84F`.

Separate read-only canonical `b757befb` route facts disprove another potential
shortcut for the nullable atomic control: Core is present but neither complete
native discovery nor candidate guidance nor a leading ASCII range is available.
Suffixed FullMatch also lacks native/candidate guidance. Folded Search can use a
candidate; anchored FullMatch does not prepare that search hint. The class-body
fixture has an actual retained candidate backend. These are route facts, not
timing attribution. No new production instrumentation or package run was added.
