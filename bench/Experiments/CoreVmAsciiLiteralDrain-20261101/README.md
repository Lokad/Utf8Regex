# ASCII literal dispatch: closed and reverted

The adjacent-literal prototype removed repeated scalar setup inside the existing
VM Literal case, retaining one work/interruption charge per instruction. The
bounded public comparison found no useful captured-FindAll gain. Production and
prototype-only tests are fully restored/removed; no opcode, state, API, contract,
snapshot, package or new execution route remains. No confirmation or variants ran.

Parent `6592113a`, .NET 11 RC3, CPU2, isolated assembly contexts, changed Core and
identical PythonRe bytes. [One comparison](comparison.json) completed in84.595s:
14cases/four equal-result public lanes, independent60ms calibration, actual loop
warmup1s/min1000calls, five alternating samples, minimum42.813ms. Complete result
arrays are checked after timing; .NET lanes include required Python capture/null
projection, both predecoded and decode-then. This is not CPython qualification.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Captured FindAll,2048strings |799.377|795.115|−0.53%|
| Finite limits/live token |1009.547|1020.463|+1.08%|
| Deadline/live token |1546.247|1532.241|−0.91%|
| Mixed Unicode |439.186|435.867|−0.76%|
| Byte-offset FindAll |827.001|825.924|−0.13%|
| Unchanged literal control |23.080|22.858|−0.96%|

[All medians, spreads, paired observations and allocations](summary.json) remain.
Allocations are unchanged in every case. Useful target gain is required in
addition to the10% adverse allowance; the prototype fails that first criterion.

[FindAll controls](findall-phases.txt) first established the actual17408byte,
2048match route on matching parent binaries. Cumulative controls are not additive
phase costs. Trailing-capture replay remains benchmark-only and grants no
production eligibility. No codegen, phase estimate or parent comparison replaces
the fixed-original delivery gate or changes catalog Status.

Both runtimes pass1853focused Core checks,7096broader VM/lifetime checks and the
full4513PythonRe checks. Core selections overlap. Coverage includes scalar/tail
boundaries, captures/backtracking, exact per-literal work and every cutoff,
original-token cancellation, absolute deadlines and retained caller loans.
Restored source passes199Core checks/runtime; affected outputs are rebuilt.
The first fixture build used the wrong outcome property and failed; corrected
logs are retained. VM preparation's final display command failed on a trailing
carriage return after successful builds/preflights; no timing attempt was lost.

Immutable artifacts under `artifacts/experiments/vm-literal-drain-20261101-deferred/`:

- `evidence.tar.gz`, SHA256 `5054F1B88E4A8D952D29C0E32991984CE8BC508DAF0F487EAFF15D517A09F134`.
- `correctness-and-attribution.zip`, SHA256 `DC6D4F1C33D3E8EEC99B8A835F59FF08EBB2E109E2529CBA594311DD0A91151B`.

[VM capacity recovery](vm-capacity.json) restored ordinary affected-project builds.
Only16inactive generated bin/obj directories were retired after archiving780files
and checking process references. Historical source, packages and evidence remain.
The separate12GiB repository tmpfs was never the full system disk. Its raw archive
is `artifacts/experiments/vm-capacity-20261101/cold-build-outputs.tar.gz`, SHA256
`960726FB5F428B69A6DCF8985FECC91D8B954543030BD5D387C9F4069BF04EF3`.
No performance result is inferred from maintenance.
