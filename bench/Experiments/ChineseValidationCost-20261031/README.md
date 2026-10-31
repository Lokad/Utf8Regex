# Chinese literal cost attribution

Read-only diagnosis using matching VM `04e75512` Release/.NET11 RC3 binaries,
813478-byte published Sherlock Chinese input,23-byte mixed two/three-byte needle,
30 matches. No source prototype, A/B, snapshot refresh, test rerun or package change.
One <=120s outer cap, CPU2, existing [commands](diagnose.ps1),2000 operations per
measured lane with the CLI's normal warmup. Full [output](diagnosis.txt) binds
DLL identities and preserves raw total durations. This single-sample diagnostic
does not establish a new public benchmark Status or exact effect size.

| Phase / public operation | us/op | Observation |
| --- | ---: | --- |
| Owned validity-only | 217.052 | Existing checked ValidationCore route |
| BCL Utf8.IsValid | 1018.057 | Does not support replacing the current selector |
| Validation with UTF-16 metadata | 1286.769 | Count does not request this work |
| Prepared literal search | 52.297 | No whole-input validity scan |
| Ordinary / compiled direct Count | 54.178 /54.069 | No whole-input validity scan |
| Public ordinary / compiled Count | 212.595 /213.196 | Whole-input validity included |
| Predecoded Regex ordinary / compiled | 41.827 /29.064 | UTF-8 validation omitted |
| Decode-then Regex ordinary / compiled | 2291.620 /2373.949 | Separate baseline |

Do not add/subtract these independently warmed observations, derive an exact
validation percentage or claim a hard lower bound: validity alone even exceeds
the separately measured whole operation. They establish cost scale and reject
the proposed BCL replacement, not an optimization gain or stable regression.
The previous qualified6.247x ordinary/5.186x compiled snapshot stays unchanged.

The untimed census reports236720 safe three-byte scalars,102638ASCII and340
two-byte scalars; no constrained-three/four-byte scalars. The current cost
selector correctly uses the owned dense-three-byte validator. Its fixed-grid
replay is not an executed production-branch counter. The full literal already
uses checked BCL sequence search after validation. Prior sequence-search and
controlled-validity experiments remain closed.

The existing fused literal counter is scalar, admits only all-three-byte needles,
and correctly declines this mixed-width literal. Simply widening that gate does
not supply a vectorized combined validator/search. A new combined SIMD design
would require separate boundaries, malformed-tail, nonoverlap and public-cost
proofs; it is a larger investigation, not the current bounded delivery change.

Defer this gap with the evidence above and preserve strict whole-subject
validation. Continue the next Core ratio, lexer identifier6.13x, sharing the
recently improved bounded value-prefix mechanism. This is not parity, completed
release performance or permission to weaken metering/validation. Final-source
cumulative performance, current full refresh and fresh package/consumer gates remain.
