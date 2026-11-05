# Count validation without unused coordinates

**Accepted.** Finite structural/simple Count consumed only `IsAscii` from a full
validation result. Return the ASCII proof already computed by the existing
validity check through its bounded walk, and remove a redundant `SupportsInput`
gate behind that proof. No extra scan, validator, mode, cached trust or partial
coordinate result. Coordinate-producing operations keep complete metadata.

Strict whole-subject UTF-8/scalars, error offsets, captures, RTL/NonBacktracking,
aliases, original cancellation/deadlines and admission remain. Validation uses
the same complete-scalar 2048-byte blocks and identical work charges. Allocations
are unchanged on every measured row. [Contract](../../../docs/SPEC.md).

One **101.630-second** separate Default-process A0/B0/B1/A1 compares the frozen
dev35 BC Full Core against parent `10c17575` plus the archived runtime/tests patch.
The caller and six adapter/benchmark DLL/PDB companions are identical. Normal
eight-CPU startup then CPU2; producer warm1s/min16, BCL250ms/min4, independent60ms
calibration, five alternating samples. All180 producer batches exceed57.7014ms.
Counts are consumed after timing. Equal-result BCL predecoded/decode-then lanes
are diagnostics, not CPython comparisons or new catalog verdicts.

| Public Count workload | Before us | After us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core literal control | 2.463 | 2.465 | +0.09% | 0 |
| Actual finite declarations | 43.978 | 43.399 | -1.32% | 112 |
| Actual finite compiled/live declarations | 45.029 | 44.029 | -2.22% | 112 |
| Actual declarations with Unicode comment | 43.851 | 42.631 | -2.78% | 1168 |
| Russian declaration-query sibling | 3903.338 | 915.696 | -76.54% | 112 |
| Chinese fixed-query sibling | 1286.538 | 376.245 | -70.76% | 176 |
| Mixed-Unicode sparse fixed query | 51.777 | 42.790 | -17.36% | 176 |
| Dense ASCII finite fixed query | 12.215 | 12.160 | -0.45% | 112 |
| Finite Chinese exact-literal control | 1209.390 | 1176.012 | -2.76% | 112 |

The Russian/Chinese query siblings use real frozen corpora with different ASCII
patterns; they are explicitly novel workloads, not speedups for their catalog
literal rows. Both after-process medians beat both before on the three larger
Unicode improvements. The smaller declaration results retain sample/process
variation; dense ASCII process observations overlap. The unchanged Core control
is flat. The finite literal control shares the validity-only helper, whose former
duplicate ASCII probe is also removed. No normalization or additive claim.
[Raw observations and process spreads](cost-summary.json).

Focused Release Windows/Linux per .NET10/11: Core1116/Python1143/PCRE202 PASS.
New22 Core cases cover borrowed offsets, block/scalar boundaries, complete
coordinate metadata, identical work charges, malformed absolute offsets, public
compiled/RTL/NonBacktracking routes, original-token cancellation, deadlines and
work denial. Two initial interruption-fixture failures reflected an undersized
input that never reached the diagnostic callback; corrected before measurement,
with initial logs retained. FullOpts preserves the existing bounded walk and
validator calls (2219->2009 code bytes). No confirmation, source tuning, package
qualification or Full refresh for this pilot. Dev35 bytes stay immutable.

Immutable raw under artifacts/experiments/count-validation-metadata-20261105,
verified off VM:

- evidence.tar.gz: `A3E0B12983B356B119AA2836AE919EB8122A10BCF6EC0CC12E6E8E888940928C`.
- windows-correctness.zip: `FFF4B33D492EEE036A8E9D6DBD74E46B062E39F302C9F6D51AE5823C00E2914D`.
- public-routes.zip: `AB94C6103378FD6298E810EC9866F848D8058881426DC16140CCD797EB748306`.

Declaration `62AB22A3638B3B71DB436EBC52C873D562229D5188478519692BEA171589D53D`
and runtime/tests patch
`BD5BA4FC041E6C60B3D452BEA5E2DF7975C525E80F1D3D3523489A1A58F8BCD3`
were frozen before timing. Documentation follows measurement; runtime is unchanged.
