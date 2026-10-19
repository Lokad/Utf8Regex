# Frozen actual114 .NET10/11 Count comparison

Actual immutable Core114, producer01968d0f, SHA256
1AB068EDB637A94222191820A0C5AAE461701216239FEF67F650F1C89810B6E6.
This reuses the existing four-case/three-cell fixture from Runtime11-Scalar110,
changing only the exact product-hash guard. Original110/59 inputs remain frozen.
[All samples, order, identities, allocations and ratios](measurement.json).

Installed WSL SDK10.0.401/net10 and SDK11RC1/net11 build the real generated Regex
comparators off the quiet VM (1.85s/2.55s). Four runner factories per SDK; no runtime
Regex fallback. All actual runtime preflights pass Count/range/subject digests,
scalar/code-unit exclusion, malformed rejection and original pre-cancel identity.
Environment.Version is numeric11.0.0; full Framework/Regex informational identities
bind11.0.0-rc.1.26425.128. The collector setup mistake is preserved; no build or
verification rerun followed its correction.

One fixed15-process batch: five declared three-cell rotations, CPU2/FullOpts /
ReadyToRun off. Runtime10.0.12/SDK10; identical SDK10 harness on11RC1; fresh
SDK11/net11 on11RC1. Each cell loads the same actual Core114 DLL. Ordinary/compiled
unlimited,all-four finite and live-token Core lanes accompany ordinary/compiled /
generated/NonBacktracking BCL predecoded and strict-decode controls.120ms warmup,
80ms calibration target,60s child/480s batch caps. All840 samples exceed65.453ms,
above the50ms floor. One batch is enough; no confirmation or further tuning.

Median ordinary Core Count, unlimited / finite / finite+token, microseconds:

| Case | R10/G10 | R11/G10 | R11/G11 |
| --- | ---: | ---: | ---: |
| literal-late |4.921 /3.814 /3.841|3.948 /2.787 /2.801|3.792 /2.755 /2.804|
| fixed-width |7.830 /19.724 /19.330|8.530 /21.039 /20.729|7.365 /19.703 /19.800|
| optional-prefix |138.814 /539.587 /535.357|134.942 /1143.013 /1138.786|134.062 /1139.508 /1136.508|
| shadowed-family |114.704 /123.407 /123.021|116.167 /128.449 /125.534|116.934 /130.441 /126.094|

Same SDK10 IL on runtime11 improves literal medians roughly20-27%, but finite
optional-prefix costs about2.1x runtime10; the fresh SDK11 cell retains that gap.
This is a runtime/JIT-path lead, not proof of a specific runtime defect. Preserve
basic metering. The optional-prefix generated baseline stays49us while current
ordinary Core is135-139us unlimited /0.54-1.14ms finite. Its historical Core110
1.35-1.79ms observation is not current114 cost or a causal before/after experiment.

Some fixed-width and shadowed-family controls are noisier (overall maxspread1.541x).
Do not infer precise tiny differences or universal runtime/generator wins. Fresh
TFM/compiler/harness effects are combined in R11/G11; ordinary/compiled controls
prevent generator-only attribution. Default-tier actual114 handoff costs, full
semantic/resource/runtime qualification and CPython Status remain separate/open.
No product retarget, package requalification, old remeasurement or consumer change.

Downloaded archive SHA256:
5A68F43697DDCE7F2DFE12F8B1C0318D5A1F958129B3382C8F3845D36E937509.
Quiet gate passes; all processes exit0. Private build/source/JIT-generation/preflight
and journals:artifacts/runtime11-scalar114-preflight-20261020 and
artifacts/runtime11-scalar114-comparison-20261020. Source build places the actual
DLL at ../product/Lokad.Utf8Regex.dll; net11 changes only TargetFramework. Exact
generator hashes are injected as build metadata. --verify creates an untimed
journal; --sample ordinal output creates one sample per lane. CreateNew protects
receipts. This is a frozen experiment, not a new qualification framework or an
instruction to repeat the closed batch. Full original goal remains open.
