# Bounded word runs before a positive boundary

Source `061ac4e5` extends the existing pure-atom repeat proof. For a nonempty run
of at most eight scalars, a bounded ASCII window uses the actual profile word
predicate. Every shorter endpoint then lies between word scalars, so a positive
boundary cannot succeed there. The existing repeat executor needs no rewind
frames. Zero minima, negative boundaries, RTL and uncertain windows retain their
original paths. Captures and required-end checks still execute normally.

No API, runner, ownership or storage mechanism was added. The proof is charged;
some operations need less working capacity/depth, and declined proofs can cost
more work. Strict whole-subject UTF-8, scalar predicates, retained results,
cancellation/deadlines and finite admission remain enforced.

One **83.492s**, sixteen-case public A/B used frozen dev.20 binaries before and
the Core-only prototype after. PythonRe/PCRE2/benchmark DLL/PDB bytes were identical.
Normal eight-CPU startup, then CPU2; managed1.5s/min128 warmup, BCL250ms/min64,
independent60ms calibration, five alternating samples, complete results consumed
after timing. All owned samples>=57.774ms. The unchanged native literal Count
control ran first and moved−0.07%. No confirmation or source variant.

| Public operation | Before us | After us | B/op before/after |
| --- | ---: | ---: | ---: |
| Python date miss |2.521|1.743|3568/80|
| Python date hit |1.015|0.535|3568/80|
| Python finite/live miss |2.916|2.175|3680/192|
| Python deadline/live miss |3.546|2.700|3728/240|
| Python dense date misses |212.512|180.801|3568/80|
| Core finite/live date miss |7.520|6.254|3680/192|
| Python captured Count |119.833|105.039|5744/5744|
| Dense finite word Count |158.045|203.478|3664/3664|

[All observations](cost-summary.json) retain allocation and sample spreads.
The dense finite word Count **+28.75% (~45.43us)** is an explicit accepted cost
for date hit/miss−47%/−31%, finite/deadline−25%/−24%, dense date−15%, Core finite
−17% and substantially lower scratch. Unicode digits+3.09%, zero-minimum+9.07%
and negative-boundary+9.37% remain observations; the latter spreads reach1.14/1.18.
Core native date+0.27%, PCRE2 date+0.43% and RTL−1.44% are not claimed gains.
The user accepts local regressions for relatively major gains; the old checked-
access10% rule is not a release ceiling. No guessed JIT attribution.

Release validation passes per .NET10/11: Core41304, PythonRe4718, PCRE26505.
Two added exhaustive work-cutoff cases then pass with the four existing cases;
each denial preserves the parent loan. The differential project initially had
stale Core copies; only that project was rebuilt and rerun,1503/runtime pass with
matching dependencies. Tests preserve captures, Unicode/default/UCP predicates,
zero-minimum rewinds, lookarounds, RTL and original interruption/retained results.
SPEC/CHANGELOG record revised resource requirements and the local cost.

Only `common/date-miss` was refreshed and qualified with the existing nine-pair
official CPython protocol. [Fresh observation](selective-summary.json): **3.600x
->2.051x [2.045,2.055]**,1.775us/80B, minimum49.240ms, CPU0x80. This independent
catalog movement is distinct from the CPU2 causal A/B. All other89 cases and
lifecycle/scaling records are unchanged; generated Markdown verification passes.
The whole-source verifier was not rerun against intentionally historical rows.

Dev.20 packages and their44m08s Full record remain immutable. No package matrix,
new qualification framework, unchecked access or reopening of the stopped
Unicode-class experiment. Follow up on the demonstrated dense-word cost through
the same repeat proof, with its own bounded investigation.

Immutable archives in `artifacts/experiments/word-boundary-repeat-20261104/`:

- Public A/B: `BDA2ADAA57FC1F22E73EC73BFF5ED8AF04FBF863B6CDDE0BE0A0900C9165D3F2`.
- Source/correctness: `62E1F4E1AB3B4E8C9DB50E74B997E4391A72431969D399577B60759BC6DF3F7A`.
- Selective/paired: `173233891667E40FCC90F94418F97E56A918C1ED0520A8BDB8FF2B81F907A47C`.
