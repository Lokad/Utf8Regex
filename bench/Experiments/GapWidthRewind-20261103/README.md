# Use scalar width alone for finite-gap rewind

The existing conservative candidate window rewound a finite scalar gap by
finding each preceding width, decoding the Rune again and discarding its value.
The loop now calls the existing checked `TryGetScalarWidthEndingAt` directly.
Whole-subject strict validation and scalar endpoints remain prerequisites.
Whitespace/class membership still decodes complete values; every gap step keeps
the same work charge, cancellation and deadline check. No candidate, matching,
admission, allocation or lifetime contract changes.

This is a width-only simplification, separate from the two rejected class-filter
designs and the unnecessary ASCII decoder shortcut. No new helper, field,
threshold, mode or framework was added. The main Count caller is unchanged:
FullOpts 4,014 code bytes/696-byte frame before and after. The rewind helper
falls 2,036 -> 2,005 code bytes and 232 -> 216 frame bytes; public results below
govern acceptance, rather than these diagnostic sizes.

One quiet-VM .NET 11 RC3 comparison completed **20 public workloads in 59.680s**,
under 120s, minimum sample **55.371ms**. Exact parent `fbd25bd7` versus its Core
patch, with identical Python DLL/PDB bytes. The same twenty Count workloads and
driver/settings from the closed class-filter trials were retained: normal
eight-CPU startup, then CPU2 affinity, 500ms warmup/minimum128 calls except the
full mail corpus, independent 60ms calibration and five alternating >=20ms batches.
Complete Count checksums are consumed after timing; allocations are measured.
Compatible predecoded/decode-then .NET diagnostics remain separate.

| Public Count | Parent us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Dense Core gap | 69.550 | 68.103 | -2.08% |
| Finite/live gap | 79.628 | 77.575 | -2.58% |
| Deadline/live gap | 90.474 | 88.179 | -2.54% |
| Class/whitespace | 44.829 | 43.349 | -3.30% |
| ASCII-range control | 22.089 | 23.498 | **+6.38%** |

**Accept:** actual gap work improves, all public controls remain within 10%,
allocations unchanged. No noise confirmation or tuning variant. The range
control's adverse cost remains explicit; no exact causal attribution is made.
[All results](summary.json) preserve every workload and paired sample.

Existing focused Release checks pass **Core 992, PythonRe 109 and PCRE2 694 per runtime**
on .NET 10/11. They cover independent scalar enumeration, every scalar floor,
0/1/99/100/101 gap boundaries with ASCII/Unicode/supplementary input, malformed
tails, captures/references, all work cutoffs, original interruption and funded
ownership/refunds. No mirrored test or additional package/platform matrix.

Immutable raw evidence under `artifacts/experiments/gap-width-rewind-20261103/`:

- `evidence.tar.gz`, SHA256
  `5199E4747DFE54907AE6D7448E3A98E172564C55F1C04872C613C71B3A5CC0A6`:
  frozen source/assets, patch, driver/project, preflight, code generation,
  every timing/allocation sample, scripts and exits.
- `correctness-source.zip`, SHA256
  `86F80AA586C5FEA9BA3C16CDE32A9EFBC1CAFA6E72DC7467D66A2EDF17E5FBB1`:
  exact prototype source, all focused checks and setup evidence. The initial freeze guard
  stopped before building/timing because the VM still held the accepted Match
  patch. Exact patch verification, restoration and fast-forward to its committed
  parent resolved that setup state. No timing run was repeated.

Prior dev.15 fixed-original failures remain recorded. The accepted Search,
bounded-literal, Core Match and width changes now justify one fresh coherent
common-candidate delivery gate; parent acceptance does not establish release
acceptance. Existing staged packages and full-refresh evidence remain immutable.
