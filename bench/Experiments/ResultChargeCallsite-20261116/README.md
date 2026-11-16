# Inline existing result-charge gates

Accepted: add AggressiveInlining to the existing ChargeResultBytes wrapper and
TryChargeResultBytes budget method. The unlimited-result gate already skipped
accounting; no policy, counter, peak, sticky breach, helper object, ownership or
interruption semantics change. Numeric admission still precedes acquisition.
Original token/deadline, finite result/working/depth/work limits and aliases remain.
No new tests mirror the implementation; use existing resource/capture coverage.

A dev.103 FullOpts preflight locates separate344-byte wrapper/217-byte budget
bodies. Before/after actual-package/source-verified codegen shows both charge
calls removed from Reserve, whose native body grows327→833 bytes. DecodeUtf8
remains520 bytes. These untimed Windows FullOpts bodies are not sampled PGO
attribution or a public speedup; retain the native code-size cost.

Windows and SAME-byte Linux per.NET10/11: focused Core resource/work/result1158
and full PythonRe5235 pass. Intrinsics-disabled public preflight passes. Core
alone rebuilds; Python D64A6B4C DLL/PDB and caller/.NET are identical. Core
05FB278A→5E490597; actual393 compiler documents, producer DLL/PDB/member and input
identities verify. All692 old/dev.103 feed files rehash unchanged; no package,
Full/catalog or canonical/native-parity qualification is repeated.

One104.577s ordinary-process ABBA/180s cap,0 confirmations. Twelve public operations,
normal8CPU startup thenCPU2,1s warmup,75ms calibration plus one calibration check,
five150ms-target samples/process/row, all>=20ms. Complete tuple strings/bytes,
split, replacement counts and output are checked after timing. Funded tuples
keep finite work/depth/working/result limits, original token/deadline and refunds.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| tuple-strings | 376.039 | 361.050 | -3.99% | overlapping |
| single-strings | 179.454 | 175.640 | -2.13% | separated |
| unicode-tuples | 582.510 | 570.777 | -2.01% | overlapping |
| funded-finite-tuples | 1138.202 | 1140.760 | +0.22% | overlapping |
| tuple-bytes | 398.854 | 395.124 | -0.94% | overlapping |
| captured-split | 254.076 | 243.218 | -4.27% | overlapping |
| captured-replacement | 152.676 | 147.024 | -3.70% | overlapping |
| one-string | 109.912 | 105.308 | -4.19% | overlapping |
| core-string-replacement | 131.732 | 133.296 | +1.19% | overlapping |
| unchanged-core-count | 12.342 | 12.316 | -0.21% | overlapping |
| unchanged-dotnet-tuples | 871.469 | 860.295 | -1.28% | overlapping |
| unchanged-dotnet-count | 23.070 | 23.204 | +0.58% | overlapping |

Single-capture strings improve2.13% with separated ranges. Both tuple and split
process medians improve; their sample ranges overlap, so do not claim a precise
causal4% gain. Captured replacement has opposing process movement despite its
favorable aggregate median. All allocation medians agree. Finite funded+0.22%,
Core replacement+1.19% and unchanged controls overlap. Keep those observations;
no normalization or cross-trial multiplication. Two annotations and a supported
single-capture gain justify keeping the change despite native-code growth. Stop
without confirmation, new helpers or another inlining campaign.

Original Windows/VM worker/collector terminal0; raw verified offVM. Artifacts:
artifacts/experiments/result-charge-callsite-20261116/.
Raw SHA25653A1F9B498C94878F43235AC18E5786D6A79F754BD068CBFE1D18C9B4851D3E2;
validationCB1FE505E6CA46B218CF5F40AA5C80405610D988E5F1DF271BF98A6F365FE796.
