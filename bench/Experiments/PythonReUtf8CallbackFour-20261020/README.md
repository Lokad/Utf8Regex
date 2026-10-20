# Frozen117 UTF-8 results and callback costs

One bounded existing-runner batch on immutable Core/PythonRe scalar.117,
producer04b7a3ed1273, quiet VM .NET10.0.12/default tiers and official
CPython3.12.3. [Identities](identities.json), [summary](summary.json).
No library build, restore, pack, source change or package requalification.

| Public operation | Managed / CPython us | Paired ratio, 95% interval | Allocation B/op |
| --- | --- | --- | --- |
| FindAll UTF-8 values |692.210 /441.985|1.590 [1.410,1.631]|186120|
| FindAll UTF-8 capture tuples |820.310 /1170.990|0.704 [0.660,0.727]|262016|
| Callback Subn string |651.184 /16229.139|0.0408 [0.0397,0.0416]|239328|
| Callback Subn UTF-8 |650.465 /15978.811|0.0409 [0.0394,0.0416]|230112|

**All four results are Inconclusive.** Each received the existing predeclared
9-to-17-pair extension and stopped. CPython interquartile spreads respectively
1.211/1.183/1.146/1.135 exceed1.10; managed spreads are1.036/1.021/1.030/1.012.
Order effects also remain in the summary; no ratio overrides the failed spread
gate. No reversed run, rescue, timing retuning or extra campaign follows.
The68final pairs have minimum managed/CPython samples49.139/50.503ms.
The batch completed161.111s under300s total/120s command caps.

These are composed public result costs, not native engine comparisons:

- UTF-8 FindAll includes corresponding CPython string-to-UTF-8 value/tuple
  projection. Results are retained and checked after the timed batch.
- Both callback lanes materialize detailed match data and traverse metadata for
  checksum/semantic-digest work inside each callback before returning a fixed
  replacement. These figures do not describe a minimal constant-return callback.
- Every row retains Not engine-comparable / NotApplicable. The callback ratio
  is not a pure owned-VM/SRE speedup. Historical source/product observations
  do not provide a causal before/after comparison for these current allocations.

Existing117 full91 semantic-digest proof is reused; each row's semantic preflight
still executes. The original exact04b producer snapshot was verified first,
then current snapshot/page data installed as permitted qualification inputs.
Shipping source and host hashes remained unchanged. Raw source/runtime/CPython/
CPU-affinity/protocol identities and samples remain in the snapshot rows.
Product SHA256 AFC83D37793129107B36326224B74E8FAB6B375D0854CF599D175BD3128ABB0F.

Exactly four case objects changed; the other86 include the previous four117 rows
and are unchanged. Frozen117 freshness now reports current8/product-changed82;
current identity does not mean all eight have qualified performance conclusions.
Generated markdown verifies on the VM and locally. The matching focused Release
PythonReBenchmarkSnapshotTests passes with SkipBuild. No unchanged source suite,
platform/package matrix, terminal CI poll, immutable-byte change or adoption claim.

Raw predeclaration, setup, journals, checkpoints, hashes and transport:
artifacts/pythonre-utf8-callback-four-20261020. Evidence archive SHA256
F31DF74FB0755F03888E29F90E9F521C1F76E715685BCF2FADD7788CCC0D44D9.
One RAM checkout streams evidence over its original SSH session; unrelated VM
data is untouched. Full90-rowR10 and broaderR09 remain open. Further catalog
refresh is deferred in favor of a concrete current-engine cost investigation;
this does not revise the existing qualification protocol or rescue these rows.
