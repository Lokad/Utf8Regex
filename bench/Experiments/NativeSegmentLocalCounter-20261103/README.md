# Rejected: make the repeated-segment counter local

The ASCII structural executor has one caller of `TryConsumeRepeatedSegment`.
It discards the helper's `out separatorCount`. A seven-line prototype made that
counter local, preserving its minimum check and committing the match end only
after the segment succeeds. No algorithm, allocation, checked access, public
contract or resource-authority change was intended.

**Rejected and fully reverted.** One permitted confirmation measured eight
public Core Count cases in **61.854s**, minimum sample **59.134ms**, five
alternating samples per lane. Original `a40bda03`, frozen dev.17 parent
`347b7845`, and prototype `cd9f24ae` plus the archived patch used isolated
assemblies; compatible predecoded/decode-then .NET diagnostics stayed separate.
Allocations are unchanged. [Complete results](summary.json).

| Public Count | Original us | Parent us | Prototype us | vs parent | vs original |
| --- | ---: | ---: | ---: | ---: | ---: |
| Compiled ASCII native | 13.855742 | 14.187518 | 15.660928 | **+10.39%** | **+13.03%** |
| Ordinary ASCII native | 13.816990 | 14.241645 | 15.674970 | **+10.06%** | **+13.45%** |
| ASCII short failed runs | 79.996000 | 87.539164 | 88.130383 | +0.68% | +10.17% |
| Unchanged literal | 6.466198 | 6.426189 | 6.425416 | -0.01% | -0.63% |

The target has no gain and exceeds the fixed-original 10% allowance. No further
confirmation or variant. Unicode-class/captured/folded/literal-prefix controls
are retained, without attributing their variation to this helper.

The initial 18-case attempt stopped at the second candidate ASCII sample below
20ms after preflighting all outputs. Its completed first ten corpus/Unicode
medians are partial evidence, not a qualified 18-case result. They were not
repeated. The sole confirmation covered the failed ASCII case and seven
previously unexecuted controls, increasing warmup 500 -> 1,000ms and independent
producer calibration 60 -> 90ms. Diagnostic targets stayed 60ms; the minimum
20ms and five samples remained. Exact frozen assets were hash-verified; no
product rebuild, invalid-sample pooling or source variant occurred.

Read-only warm Tier1 inspection confirms the actual repeated-segment route.
The prototype removes the counter's memory increment, but helper code grows
405 -> 409 bytes, with the same saved-register frame. Neither observation is
proof of public performance. An initial reflection accessor error was corrected
before that inspection; a bundle-ref error stopped setup before any trial timing.
Both are preserved.

Focused Release checks passed 695 per runtime on .NET 10/11. After reverting,
nine existing public endpoint/scalar/RTL/NonBacktracking/alias/authority proof
cases passed per runtime. VM source is restored and its exact dev.17 DLL/PDB
hashes are `627A6767...` / `B8FBAE0C...`. No package or catalog was refreshed.

The unchanged parent is faster here than in the [failed delivery gate](../DeliveryDev17-20261103/README.md):
14.187518 versus 17.145111us, while the original is 13.855742 versus 13.989679us.
Warmup, lane order and process profile differ. This warrants bounded read-only
tiering/code-generation investigation; it does not overturn the failed gate or
establish which difference caused it. Core gap and PythonRe nullable Count
remain separate recorded delivery blockers.

Immutable raw under `artifacts/experiments/native-segment-local-counter-20261103/`:

- `initial-evidence.tar.gz`: `C791FD95AE636B1271025BD0D8EE29C19A11219698E3F4ABFF18C73E93B60223`.
- `confirmation-evidence.tar.gz`: `F01D189D0EA3975F6A800DFDFD22F11BBD1AFB142599319C003699206C6B2D2B`.
- `correctness-source.zip`: `219763A7D02B60389CA4C9092D3DA128943CE716CA17F6C3B11942B3D1616AAC`.
- `restoration-source.zip`: `A6B5B5AB0BF6524AB8685E9E7DF0A54EB7913BA51018197384384D6B628500A4`.

Original/current read-only inspection including its initial failure is separately
sealed at `artifacts/experiments/native-segment-inspection-20261103/evidence.tar.gz`,
SHA256 `A93F285D472E1D62E7916A1986C1E9EC55A38D100A589CD01D5B013A5E491DF3`.
