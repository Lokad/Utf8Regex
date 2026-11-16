# Reuse bounded valid-text sizing for POSIX coordinates

Accepted: three ProjectRange calls reuse existing CountValidUtf16. POSIX already
requires valid unchanged UTF8; scalar ranges need coordinates, not decoder sizing.
The <=4096-byte BCL path and bounded checkpoints remain. No API, allocation
algorithm, matching/capture selection, cancellation, resource or lifetime change.
No new decoder, descriptor or execution mode.

Windows and SAME-byte Linux pass all503 POSIX Release tests per.NET10/11, including
four new long-range cases covering every scalar width, a borrowed offset, a scalar
crossing the4096-byte boundary, suffix/empty captures, finite controls and retained
cursor aliases. The new long-repeat fixture initially exceeds depth1024 as designed;
depth32768/8MiB admission suffices. Production limits are unchanged. A private caller
operator error and Git LF versus actual CRLF source-copy mismatch were corrected
before timing; actual loaded DLL/PDB/compiler checksums subsequently verify.

Only the adapter changes: AF3D7140→A6B02471; common Core5E490597, caller and.NET are
identical. Both producers'343 Core+12 POSIX compiler documents and managed inventories
verify. Test bundles are the same Windows-built bytes on Linux. One77.533s ABBA,
180s cap,0 confirmations; normal8CPU startup thenCPU2,1s warmup,75ms calibration
and one calibration check, five150ms-target samples/process/row, all>=20ms.
Whole/capture coordinates are checked after timing. These are public-operation
costs, not isolated counter or native POSIX parity measurements.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| Long ASCII prefix, Match | 1.781 | 2.641 | +48.23% | separated |
| Long Unicode prefix, Match | 268.386 | 12.025 | -95.52% | separated |
| Finite/token/deadline Unicode prefix | 269.925 | 12.414 | -95.40% | separated |
| Long Unicode prefix, cursor | 268.964 | 11.971 | -95.55% | separated |
| Short prefix control | 0.124 | 0.122 | -1.30% | overlapping |
| Long ASCII suffix captures | 1554.729 | 1539.039 | -1.01% | overlapping |
| Long Unicode suffix captures | 1577.501 | 1540.477 | -2.35% | overlapping |
| Unchanged Core Count | 12.357 | 12.308 | -0.39% | overlapping |
| Unchanged.NET Count | 22.678 | 22.559 | -0.52% | overlapping |

Keep the ASCII+0.860us cost for the large Unicode gains; this is an authorized
local trade-off, not a claim of universal improvement. Both process medians
support the separated effects. Prefix112B, finite160B and cursor480B are unchanged;
controls stay0B. Long suffix cases remain ~1.93MB/call, with small sample-dependent
pooled-allocation amortization differences; do not claim identical medians or a
precise causal suffix speedup. Their ~1.5ms capture work warrants a separate source
audit; this comparison does not attribute its cause.

All692 published/staged feed assets rehash unchanged. No package, catalog, Full,
native comparison or confirmation is repeated. Stop this sizing trial. Raw verified
offVM, original worker/keeper and collector terminal0; owned VM scratch retired
after accepted hash and zero process/cwd/FD/map references.

Artifacts: artifacts/experiments/posix-valid-text-sizing-20261116/.
Raw SHA256 C984E82BE3FF381F5C45A932F1DBB21E5E86D2523BF675B9C5E90882008364BC.
Validation SHA256 47B0FF1B54DF211479DC0B1D8521DBE8BED1A794AC73E9FF0A63DE5BBDC2A056.
