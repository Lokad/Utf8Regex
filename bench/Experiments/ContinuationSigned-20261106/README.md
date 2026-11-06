# Signed continuation-byte classification

Parent `741326b7af2b4db883fc10cffc90db05e39e393f`; before is the immutable qualified
[dev38 Full graph](../DeliveryDev38-20261106/README.md), without a rebuild. Core
changes alone; the caller and six PythonRe/PCRE2/benchmark DLL/PDB companions are
identical in separate ordinary Default-context processes.

UTF-8 continuation bytes80..BF are exactly signed bytes below-64. Replace vector
mask-plus-equality with register-only AsSByte and a signed comparison at the
existing256/512 widths. Checked MemoryMarshal.Read, selector/carry, scalar fallback,
first malformed offset, whole-subject validation and existing interruption/work/
working/depth/result authority stay unchanged. No new helper, retained state,
mode, API, allocation or unchecked access; actual Unsafe.SizeOf queries remain.

Existing boundary/malformed tests now include7F/BF at every tested vector lane and
slice offset, comparing common validation against the independent semantic path.
Focused Release checks pass on Windows/Linux and .NET10/11: Core525, Python185,
PCRE105 per runtime, plus87 narrower-vector and37 scalar-fallback checks. Fresh
FullOpts codegen replaces VPANDD+VPCMPEQB with VPCMPLTB; complete method code stays
1414B. The before listing was captured twice during untimed preparation; both
identical listings and the after listing are preserved. No codegen timing claim.

ONE **91.422-second** A0/B0/B1/A1 comparison: normal eight-CPU startup thenCPU2,
.NET11RC3, producer1s/min16 and BCL250ms/min4 warmup, independent60ms calibration,
five alternating samples per lane/process. All160 owned samples exceed59.1187ms.
Complete typed Count results are consumed after timing. Actual catalog inputs/
patterns and digests match; equal-result predecoded/decode-then BCL lanes remain
diagnostics, not a new catalog Status or equivalent resource policy.

| Public Count | Before us | After us | Change | Before / after B/op |
| --- | ---: | ---: | ---: | ---: |
| Unchanged exact ASCII control | 2.47 | 2.48 | +0.10% | 0 / 0 |
| Actual Chinese | 89.34 | 87.53 | -2.03% | 0 / 0 |
| Actual Chinese compiled | 89.19 | 87.98 | -1.36% | 0 / 0 |
| Actual Chinese family compiled | 107.81 | 105.69 | -1.97% | 0 / 0 |
| Actual Russian | 178.23 | 173.78 | -2.50% | 0 / 0 |
| Actual AWS quick | 4892.19 | 4901.58 | +0.19% | 592 / 592 |
| Actual Chinese finite/live | 1213.36 | 1193.05 | -1.67% | 112 / 112 |
| Actual Chinese finite/deadline/live | 2365.49 | 2391.14 | +1.08% | 160 / 160 |

Both after-process medians beat both before medians on all three Chinese target
rows. Russian and finite ranges overlap; their pooled gains are uncertain.
Deadline after medians2476.32/2294.69us overlap before2329.82/2413.96us; retain the
pooled+1.08% cost and raw spread. AWS ranges also overlap: both versions here stay
near4.9ms. This does not explain or replace the dev38 catalog's larger AWS movement
or reopen its closed investigation. No control normalization or causal invention.

Accept the simpler predicate and modest supported public gains, preserving all
local costs. No confirmation, tuned variant, full refresh or package cycle.
[Raw process samples, costs and exact graphs](cost-summary.json) remain authoritative.
This source change is outside the immutable dev38 packages; a future coherent
package boundary needs a new common version. No NuGet publication.

Immutable artifacts/experiments/continuation-signed-20261106, verified off VM:

- evidence.tar.gz: `6F09DFCA9FDAF2BC4EF0BB6E6BBFECC5835D9D36F236D06CEC2A5EEB5D36BE59`.
- windows-evidence.zip: `1857275C0C0761B42EDAD429BC4DA5D851420467C86986E9653E3EA4E5CADA4C`.
- Declaration: `16FF26D8531331CD3207A2B6E176D44B17D467F26F33E7B15DF90EF22743E53E`.
- Source/test patch: `A861ABE3A809ACD8A448EA8B21A1A571EA0068EB6982A589C83770C1AE4E2787`.
- Before Core: `1C97F232A7CF32CFDCBC71F910C43FDBB2DB0D7E4B04E1EAA0B7D0617A5EB452`.
- After Core: `B187664C06CC2C5138A03AF85464923C1225F9A24D8F62486811BFC19BD4771D`.

Frozen dev38 Full graph identities, old raw graphs, and all staged/published
packages and symbols remain unchanged. Source CI is checked after committing.