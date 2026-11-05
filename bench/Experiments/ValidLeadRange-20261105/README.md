# Classify valid two-byte UTF-8 leads directly

**Measurement correction:** the dual-context figures below are historical.
[Identical-binary control and separate-process comparison](../ProducerProcessIsolation-20261105/README.md)
supersede their producer-effect attribution: Chinese−4.43/−4.78%, case-sensitive
Russian−5.78%, folded+3.31% and finite Chinese+3.12%. The classifier remains accepted.

**Accepted with a folded-Russian cost.** The existing common UTF-8 validator
now classifies C2–DF directly using unsigned vector subtraction/range comparison.
This removes the separate overlong-lead mask, its constants and its bitfield.
C0/C1 still fail on the error path, after any earlier missing continuation.
The 64/32-byte blocks, two-bit carry, mode selector/idle policy, checked reads,
scalar/error fallback, full validation and original resource/interruption scope
remain. No fusion, framework, owner, switch or unchecked operation was added.
Public semantics, numeric accounting and allocations are unchanged.

This is distinct from the closed fused validator/matcher, prefix selector and
first-scalar-pair experiments. The actual 813,478-byte Chinese corpus uses a
mixed-width 23-byte needle and whole-sequence search after validity-only
validation. It returns 30 matches. The all-three-byte fused gate declines it.

One **78.7758103-second**, 15-case public comparison used the exact accepted
433F388D Core and six byte-identical frozenfb309 companion DLL/PDBs against Core
built on parentf77e88d3 plus overlay
`C53E00D160FBDBAA5E607187636B3FCBC7832519775E3602A85846FFEEDB9D4F`.
[All initial observations](cost-summary.json) retain every result, allocation,
identity and spread; [one five-case confirmation](cost-confirmation-summary.json)
uses exactly the same binaries. No source variant, third run or tuning followed.

| Public Count | Before µs | After µs | Initial change | Confirmation |
| --- | ---: | ---: | ---: | ---: |
| ASCII control |2.52|2.52|+0.18%|−0.50%|
| Actual Chinese |94.25|90.55|−3.92%|−4.80%|
| Actual Chinese, compiled |94.76|90.54|−4.45%|−4.87%|
| Actual Chinese alternation, compiled |156.81|154.44|−1.51%|—|
| Actual Chinese, finite/live |1195.52|1161.85|−2.82%|—|
| Actual Chinese, deadline/live |2145.43|2158.30|+0.60%|—|
| Actual Russian, case-sensitive/compiled |185.86|174.25|−6.25%|−6.23%|
| Actual Russian, folded |230.33|256.63|+11.42%|+14.71%|
| Dense three-byte subject |108.31|107.62|−0.64%|—|
| Late constrained scalar |59.74|59.27|−0.79%|—|
| Mixed widths |58.26|57.89|−0.63%|—|
| Late supplementary scalar |92.58|88.18|−4.76%|—|
| Short mixed subject |0.31|0.31|−0.12%|—|
| Python mixed literal |58.85|58.17|−1.16%|—|
| PCRE2 ASCII control |160.23|164.92|+2.93%|—|

The confirmation took **26.3621951 seconds**, capped at60s. Chinese went
95.42→90.84µs and compiled94.89→90.26µs; case-sensitive Russian189.00→177.22µs.
Folded Russian went228.01→261.56µs: retain the extra26–34µs per call. The
primary improvements are modest, repeatable4–6% reductions with a simpler
validation invariant; the folded cost is an accepted local trade-off under the
user's clarified policy, not hidden behind a universal10% gate.

An untimed [route inspection](folded-route.json) confirms both folded calls finish
in the existing fused Cyrillic counter, before the changed canonical validator.
Its source is unchanged; raw IL hashes are retained without claiming equivalence.
This establishes reachability, not the cause of its repeated timing movement.
The cost remains part of this candidate's public observations. No new catalog
parity classification is inferred from these comparisons or BCL diagnostics.

Normal eight-CPU startup precedes CPU2 affinity. Producer warmup1.5s/min128,
BCL250ms/min64, independent60ms calibration and five alternating samples.
All150 initial producer samples exceed56.0921ms and all50 confirmation samples
exceed54.6236ms. Typed checksums are consumed after timing. Allocations stay0B
except finite112B and deadline160B. Initial spreads are below1.066/1.030.

Windows/Linux Release.NET10/11 pass focused Core801/Python176/PCRE165 checks.
The existing every-wide-lane/borrowed-offset corruption fixture adds C1/C2/DF
endpoints. Linux additionally passes78 forced256 and45 intrinsics-disabled checks
per runtime using matching binaries. Hardware probes verify default512, forced256
and scalar modes. These preserve malformed global offsets, continuation carry,
constrained/supplementary scalars, public strict validation, finite/funded work,
original cancellation and deadlines. No new forwarding executor or test framework.

Untimed Release disassembly retains both before/after listings and confirms the
candidate's `vpsubb`/`vpcmpleub` classification. Both listings use ZMM instructions;
[summary](disassembly-summary.json) is code-generation evidence, not kernel timing.
No full refresh, snapshot rewrite or package matrix was repeated. Staged dev30
packages, symbols, published bytes and the44m12.49s Full tables remain immutable.

Immutable artifacts/experiments/valid-lead-range-20261105:

- evidence.tar.gz: `84F8C0524E183538E8B2959C9CD094EF7D81A89491E510E3C95B0347D388E983`.
- confirmation-evidence.tar.gz: `403D1BA14D2BFC9C3506B4974EDA4D65156FC08EBFB22834382AFC16D25B58C1`.
- windows-evidence.zip: `D41BC7CE300F188D0469FDA4483F10836D33B2DDAE9CF0CEF16046598464F03D`.

Archives include exact graphs/overlay, declarations, unchanged measurement driver,
all samples, public preflight, hardware probes, disassembly, route inspection,
focused logs and terminal0. Candidate Core SHA256:
`B2C9D5A4CF8935E1AB68D26DCA4E8FDE07A8E49D7E2C4E28463279EE2F3D4B16`.
