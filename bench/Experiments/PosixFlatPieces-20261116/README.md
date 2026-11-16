# Extend the existing POSIX flat-run proof

Accepted: admit single repeats and mandatory fixed ASCII-origin scalar pieces in
the existing greedy-run analysis. Required pieces must be disjoint from earlier
variable runs; fixed pieces introduce no rewind choices. Existing byte/scalar
execution, per-byte work, original interruption and preallocation admission remain.
No new executor, switch, descriptor or accounting mechanism. Folded pieces retain
scalar execution; first-atom hints refer to the first consuming piece. Repeated
public captures, required overlap, uncertain bodies and anchored nullable pieces
keep ordinary execution; LF-consuming runs retain multiline endpoint rewind.

Impossible optional exits, undo and repeat counters can disappear, reducing
numerical work/depth/working requirements. Matching, capture priorities/history,
scalar boundaries and cursor/result lifetimes are unchanged. A public8KiB capture
fits depth1/4KiB working admission; insufficient work still fails. Documentation:
[POSIX specification](../../../docs/SPEC-POSIX.md).

Windows/SAME-byte Linux pass526 POSIX Release cases per.NET10/11. Focused81 cases
pass per target before the full suite: all floors/output widths, BCL overlap,
production-general contrasts, Kelvin/folding, anchors, nullable/required-overlap
counterexamples, finite admission, original-token/deadline/refund checks and aliases.
Both actual DLL/PDB pairs and355 compiler documents/member inventories verify.
Adapter A6B02471→33B7248C; Core29CF2CC6, caller and.NET are identical.

One86.455s ordinary-process ABBA/180s cap,0 confirmations, normal8CPU startup then
CPU2. One-second warmup,75ms calibration plus one check, five150ms-target samples
per process/row, all>=20ms. Complete whole/group byte/UTF16 coordinates are consumed
after timing. Funded finite calls retain work/depth/working/result caps, original
token/deadline and the parent's17-byte seed after every call.

| Public Match | Before us | After us | Change | Allocated B before/after | Ranges |
|---|---:|---:|---:|---:|---|
| Single8KiB run | 283.247 | 23.320 | -91.77% | 512376 / 112 | separated |
| Fixed prefix | 1534.280 | 23.364 | -98.48% | 1931120 / 112 | separated |
| Fixed suffix | 2613.681 | 23.640 | -99.10% | 2491784 / 112 | separated |
| Funded finite prefix | 1557.867 | 30.943 | -98.01% | 1957320 / 160 | separated |
| Short prefix | 1.412 | 0.211 | -85.08% | 792 / 112 | separated |
| Unicode control | 1538.603 | 1574.647 | +2.34% | 1931120 / 1931120 | separated |
| Required-overlap control | 2461.552 | 2492.709 | +1.27% | 2491784 / 2491784 | overlapping |
| Existing multirun control | 0.223 | 0.223 | -0.11% | 112 / 112 | overlapping |
| Unchanged Core Count | 12.334 | 12.390 | +0.45% | 0 / 0 | overlapping |
| Unchanged.NET Count | 22.586 | 22.721 | +0.60% | 0 / 0 | overlapping |

Both process medians support the large gains. Keep Unicode's separated2.34%
movement although its production matching route is unchanged; controls need not
have identical timing. Do not normalize away adverse observations, compose earlier
percentages, or claim native parity. Unicode and required-overlap cases retain
their substantial general capture costs; no broader proof is claimed here.

All692 published/staged assets rehash unchanged. No package, Full/catalog/native
comparison or confirmation is repeated. Stop this trial. Raw is verified offVM;
original worker/keeper and collector are terminal0. Owned VM scratch retired after
accepted hash and zero process/cwd/FD/map references.

Artifacts: artifacts/experiments/posix-flat-pieces-20261116/.
Raw SHA256 A2C379F99911C1CE4387E58F3CA8F0C516DF3427CAF996DB2F0812392F2CF221.
Validation SHA256 725380BAA88B8DF0729F36CB154D9DBEEE240A169F11305A34D1A3DCA9544A26.
