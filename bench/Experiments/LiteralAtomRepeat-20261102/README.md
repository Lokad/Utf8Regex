# Literal atom fusion: rejected and restored

At parent a80260ec, an eight-line Core prototype let the existing no-rewind
pure-atom repeat proof admit unfolded Literal instructions. The same atom loop
used exact scalar equality. Existing targets, required ends, disjoint tails,
captures, folding/lazy/RTL/NonBacktracking exclusions and per-scalar metering
remained. No state, mode, helper, engine or unchecked operation was added.

**Rejected.** Target improvements do not compensate for stable public regressions
above 10%. Production and experimental test changes are fully restored locally
and on the VM. No confirmation, tuning variant, package or catalog refresh ran.

One quiet-VM .NET11 RC3 comparison completed **66.454s**, 17 public workloads,
minimum sample **22.737ms**. Exact before/after Core assets, identical PythonRe
DLL/PDB bytes, normal runtime startup then CPU2, independently calibrated batches
and five alternating samples. Complete result batches are checked after timing.
Predecoded/decode-then Regex cover overlapping semantics. Unicode comparisons
omit empty matches inside surrogate pairs; the Python empty-then-consuming
alternative has explicit expected outputs and no incompatible .NET lane.

| Public operation | Elapsed change | Bytes before / after |
| --- | ---: | ---: |
| Catalog empty-progression FindAll strings | -24.18% | 20016 / 16528 |
| Finite limits/live token | -17.76% | 24216 / 20728 |
| Finite/deadline/live token | -7.94% | 24264 / 20776 |
| Mixed supplementary scalars | -25.65% | 14872 / 11384 |
| Captured complete repeat | -27.35% | 16040 / 12552 |
| Finite Count | -27.68% | 3664 / 176 |
| Capture inside repeat | **+13.04%** | 15016 / 15016 |
| Overlapping tail | **+21.99%** | 8696 / 8696 |
| Existing Core class Count | **+10.33%** | 0 / 0 |
| Core RTL Count | **+25.99%** | 3552 / 3552 |
| Unchanged native literal FindAll | +0.17% | 2112 / 2112 |

[All observations](summary.json) retain the other controls, paired results and
spreads. Capture-inside, overlap and RTL penalties persist in all five pairs,
with spreads at most 1.017x. No noise confirmation is justified. RTL remains
excluded from the changed proof; its measured penalty is retained without an
unproven attribution to a particular instruction or JIT decision.

Initial Python checks found two depth witnesses whose pure literal runs no longer
needed frames. Fixtures were changed to actual capture-inside/overlapping-tail
pressure, retaining denial and snapshot assertions. Full PythonRe then passed
**4641/runtime** on .NET10/11, including 18 added cases. Initial Core checks
passed 817/818 per runtime; its frame-chunk witness similarly needed an overlapping
tail. Corrected Core checks passed 107/runtime, including literal/capture/scalar
and all-cutoff parent-loan coverage. PCRE2 checks passed 720/724 per runtime;
four exact-depth expectations remained incompatible with the reduced depth cost.
**The prototype never completed PCRE2 correctness qualification.** Those failures
were preserved; no further fixture edits were warranted after performance rejection.

After complete restoration, focused Release checks pass on both runtimes:
Core806, PythonRe119, PCRE2724. The VM rebuild reproduces exact parent Core DLL/PDB
bytes. No runtime, test, contract, public API, package or benchmark-table change
survives. The existing dev.14 delivery remains intact.

Immutable evidence under `artifacts/experiments/literal-atom-repeat-20261102-deferred/`:

- `evidence.tar.gz`: parent source, complete runtime patch, before/after assets,
  reused comparison machinery, predeclared scope, preflight and all raw results.
  SHA256 `CCE37796AF0F00840AA3AD68ECEC9115B794D822E47F3F894F63E32214CC358C`.
- `correctness.zip`: complete prototype/test source and initial, corrected and
  restored Windows logs; hash is recorded in verification.json.

Keep this shared-kernel extension closed. Any later design needs a distinct
demonstrated cause and consumer-cost evidence, preserving the fixed-original
cumulative gate and all managed scalar/resource contracts.
