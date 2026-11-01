# Reuse validated simple-pattern template discovery

The final-source cost check first reproduced the genuine warm-template debt
against original `a40bda03`: text bytes +15.84%, string output +15.08%, despite
lower allocation. Its [26-case result](cumulative-before-fix.json) took109.819s.
This is not a cumulative performance pass. Other covered routes improved or
remained close to baseline; keep the raw uncertainty and allocation differences.

The correction reuses the existing compiled enumerator factory after complete
subject validation, for the existing `AsciiSimplePattern` template worker only.
The public enumeration round-trip repeated validation and offset dispatch with
large value-cursor returns. The factory still owns its anchored/scalar decisions.
Controlled calls, other execution kinds and diagnostic hooks keep their routes.
No new matcher, resource exemption, public contract or allocation behavior.

One [eleven-case public comparison](comparison.json), **95.244s**, quiet Ubuntu
VM, .NET11RC3/CPU2. Frozen6c73821a before; same source plus the archived patch
after. Warm1.5s/min64, independent60ms calibration, five alternating samples,
minimum51.6814ms; complete outputs consumed after timing. No confirmation.
[Route preflight](preflight.txt) proves the grouped-map worker is reached; mixed
Unicode remains a scalar control. [Driver](driver.cs.txt), [summary](summary.json).

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Warm text template | 4.635 | 1.986 | -57.15% | 400 |
| Warm native template | 4.678 | 2.018 | -56.87% | 400 |
| String result | 4.749 | 2.072 | -56.38% | 416 |
| Native destination | 4.680 | 2.028 | -56.67% | 344 |
| Compiled grouped | 4.607 | 1.977 | -57.08% | 400 |
| Cold native /32 templates | 6.319 | 3.879 | -38.61% | 2520 |
| Native no match | 4.132 | 1.482 | -64.14% | 40 |

Count control -1.89%; scalar Unicode -0.82%, mixed subject +2.64%, finite/live
+1.50%. All allocations unchanged; control ranges are retained, not declared
exact timing equivalence. The two .NET comparator lanes produce equal results;
byte results include encoding and destination includes copy. These are operating
costs, not equivalent resource policies or a refreshed catalog benchmark Status.

Focused Release checks pass on both runtimes: final Core replacement/resource
5053, Core capture/detailed/RTL/NonBacktracking9150, PythonRe230, PCRE21188.
Added hardcoded full-subject/capture/anchor output checks also reject malformed
tails before publication. Existing tests retain controlled and output-failure
coverage. The earlier5069 pass preceded a test-only consolidation into those
hardcoded cases. No repeated full package matrix for this pilot.

The complete candidate must still be compared with the original fixed baseline,
then receive one full refresh and immutable package/consumer qualification.
Do not multiply independent point gains into a cumulative claim. Stageddev.1
and published0.4.0 bytes are unchanged.

Immutable `artifacts/experiments/template-factory-20261101/`:

- `evidence.tar.gz`: DLL/PDBs, driver, patch, builds and raw observations;
  SHA256 `F615946D2E408998C7EC30E5DE461F751B63A6DCC71F2289A55A682D0E649033`.
- `correctness-source.zip`: source, driver and complete focused logs;
  SHA256 `DC63BE327179D1944AE65BB7E917A52F3E8D818F8DA6DF3C549DCBEDD8C54565`.
  The final5053 rerun is retained separately in the delivery evidence.
