# Scalar value outlining did not help native operations

**Closed; not promoted.** One `NoInlining` annotation on the existing
`MatchValueScalar` kept scalar cursor setup out of native value callers. It
reduced the optimized public FullMatch bridge from 2,894 to 1,266 code bytes and
its main frame from 2,056 to 280 bytes. The original `a40bda03` bridge had 2,203
code bytes and a 1,976-byte main frame. Smaller generated code did not produce
the predeclared useful improvement on the targeted native public operations.
The annotation and its import/comment were completely reverted. No tuning
variant, confirmation or replacement helper was added.

One quiet-VM .NET 11 RC3/CPU2 comparison completed 14 public cases in **65.879s**
under a 90-second cap. Separate contexts load original `a40bda03`, current
`a295ed84`, and current plus the archived annotation. Current Core DLL bytes
match. Other lanes are equal-result predecoded/decode-then compiled Regex
diagnostics. Runtime starts with normal processor visibility before in-process
affinity. Warmup is 500ms/minimum 128 calls per lane, independently calibrated
60ms batches, five alternating samples; minimum duration is **52.451ms**.
Full range/count digests are consumed after timing. No package/catalog/gate rerun.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Native Search hit |0.085|0.086|+0.70%|
| Native Search miss |0.077|0.078|+0.84%|
| Native FullMatch hit |0.082|0.082|+0.11%|
| Native FullMatch miss |0.079|0.080|+0.65%|
| Captured Unicode FullMatch |98.569|98.601|+0.03%|
| Lazy Unicode FullMatch |98.122|97.791|-0.34%|
| Branched Unicode FullMatch |101.099|98.644|-2.43%|
| Folded FullMatch |84.688|81.711|-3.52%|
| Suffixed FullMatch |36.475|36.324|-0.41%|
| Offset Unicode Search |114.285|111.321|-2.59%|
| Finite/live captured FullMatch |111.647|110.193|-1.30%|
| Deadline/live captured FullMatch |111.996|111.905|-0.08%|
| Accepted literal-repeat FullMatch |15.860|15.888|+0.18%|
| Unchanged native Count |0.114|0.113|-0.61%|

Allocation medians are identical in every case. The scalar movements do not meet
this experiment's primary native-improvement criterion. `summary.json` retains
simultaneous fixed-original ratios and all spreads; fixed-original Search miss
is still +12.08% and suffixed FullMatch +10.10%. These observations do not replace
the [failed cumulative gate](../DeliveryDev10-20261102/README.md) or establish
candidate acceptance. Stable negative evidence is not grounds for another run.

Complete PythonRe Release tests pass **4,583 per runtime** on .NET 10 and 11.
Only code generation was changed; no semantics, funding, lifetime or resource
contract was revised. The scratch codegen project initially included both driver
top-level programs. Its explicit compile item was corrected before preflight or
measurement; the initial failure and successful resumed build are retained.

Raw driver, protocol, codegen, patch, all three DLL/PDB sets and timing logs:
`artifacts/experiments/python-value-frame-20261102/evidence.tar.gz`, SHA256
`532EF3DB36EF136E14206CE8DA9E0EAE5D2A408A62B95D9CD50B9952B5125F6E`.
`correctness.zip`: SHA256 `48E4D381D2C77A6FAF1F12948FBA288DF27A9275E53C3170FFB0A3D1B173E5B4`.
Additional original/current native caller capture:
`artifacts/experiments/python-entry-codegen-20261102/native-entry.tar.gz`, SHA256
`407706ABE3333FD9B865006F5F09875522879EF5BB07CADCFE8C7657098D347F`.

The accepted endpoint-coordinate change remains. Staged/published bytes are
immutable; no fresh package boundary is claimed by this rejected experiment.
