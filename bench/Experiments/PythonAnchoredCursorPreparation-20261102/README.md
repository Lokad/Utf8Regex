# Skip unused anchored cursor search preparation

Parent `2598d904`, dedicated Linux VM/.NET11RC3/CPU2. Match and FullMatch already
pass `search=false` during execution. Their constructor now borrows the existing
operation intent and skips candidate-hint eligibility and the leading-ASCII proof
that anchored execution never reads. The bool is not stored. Global/durable
discovery keeps its hints; strict full-input validation, required-literal
rejection, captures, slots, cleanup, publication and all resource/token/deadline
authority remain. No public API/accounting contract change, new state or mode.

Full PythonRe Release tests pass4530 per runtime on .NET10/11. Existing public
anchored/offset/lookbehind/capture/strict-input/resource/cancellation checks cover
the affected entry points. Core bytes are identical in the performance lanes.

One16-case public comparison took99.079seconds, no confirmation or variants.
Exact FullMatch hit/miss improve6.24%/6.03%, finite5.99%, deadline4.93%, offset4.11%;
Unicode +0.73%. Every allocation is unchanged. Captured-body **+0.51% against
parent**, so this is not a demonstrated primary captured-body repair. Its finite
and deadline variants improve1.88%/0.50%. Unchanged Core/NB/literal controls are
-3.10%/-2.91%/-0.32%; preserve that variation when interpreting small deltas.

The same batch adds a frozen `a40bda03` fifth lane on the three original FullMatch
rows and actual-hit/literal controls: variable +3.85%, lazy +6.36%, captured-body
+5.82%, exact hit -34.30%, literal +0.21%. This selected original coverage passes,
but does **not** replace the coherent79-workload final gate. The earlier rejected
2598 gate and its sole confirmation remain intact; do not select its better run
or infer cumulative acceptance from parent gains.

Controlled actual captured FullMatch warmup produces Tier1 constructor3930->3712B
with unchanged0x688 local stack. Candidate checks the constructor bool before
the hint helpers; anchored false falls through to RentSlots. Both listings report
Synthesized PGO. [Codegen](codegen-summary.json) explains this observed code shape;
it is not throughput or universal production-PGO evidence.

[Parent costs](summary.json), [direct original costs](original-summary.json).
Reused typed public driver, isolated producers, full coordinate checksums consumed
after timing, warm1s/min1000calls, independent60ms calibration, five alternating
samples with20ms guard, finite/live/deadline and unchanged controls. Four normal
lanes include predecoded and decode-then .NET, with the selected original fifth.
No CPython, package, catalog, README/Status or platform qualification was repeated.

Immutable raw under `artifacts/experiments/python-anchored-cursor-preparation-20261102/`:
`evidence.tar.gz` SHA256 `2413F01F77B4B70BC38F4272AF8F02A43E8BD4BE57C061657CE0F36FC5B46A32`;
`correctness.zip` SHA256 `9798468A1F89C02F9D54038B4B3B2C0E86C9BA95B1D662F750422542599B42DC`.
Drivers, all outcomes, identities, patch, predeclaration, codegen and test logs are
retained. Accept the bounded simplification/useful exact-case gains; final delivery
still requires a fresh coherent fixed-original gate and immutable candidate.
