# Valid result sizing reuses coordinate arithmetic

Accepted: share the existing checked 32-byte UTF8 lead/continuation arithmetic
between Python scalar/UTF16 coordinates and unknown-length result-ledger strings
larger than 4096 bytes. Ledger callers supply valid scalar ranges/completed text;
Core's one production ledger caller supplies an exact length. General BCL sizing,
short strings, actual decoding and PCRE2's independent native-window/output rules
are unchanged. No new validation policy, descriptor, mode, cache or public API.

Exact long-string capacity admission, allocations, borrowed offsets and scalar
boundaries remain. Coordinates retain 32-byte checkpoints; sizing retains its
scalar-aligned 4096-byte checkpoints and original token/deadline/work authority.
Mixed-width/vector/block/tail capacities and pre-admission interruption tests pass.
Windows and SAME-byte Linux per target: affected Core 2060, full PythonRe 5226.
Windows PCRE2 validation/boundary/projection 46 per target also pass. Software
intrinsics-disabled public preflight passes. No public contract revision.

One 105.972s ordinary-process ABBA, 180s cap, zero confirmations. Twelve complete
public operations, normal 8CPU startup then CPU2, 1s warmup, 75ms calibration plus
one calibration check, five 150ms-target samples/process/row, all >=20ms. Producer
DLL/PDB pairs differ, all 393 actual compiler documents and managed member
inventories verify; common caller/.NET and complete outputs are checked. Python
uses caller-valid unchanged UTF8; funded finite results preserve parent refunds.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| native-full-unicode | 1628.470 | 544.882 | -66.54% | separated |
| native-full-ascii | 157.768 | 189.790 | +20.30% | overlapping |
| scalar-full-unicode | 189.558 | 63.327 | -66.59% | separated |
| funded-scalar-full-unicode | 191.759 | 63.375 | -66.95% | separated |
| replacement-large-text | 1611.296 | 549.769 | -65.88% | separated |
| replacement-dense-text | 69.201 | 53.986 | -21.99% | separated |
| unchanged-short-text | 0.327 | 0.315 | -3.72% | overlapping |
| unchanged-byte-output | 105.389 | 103.600 | -1.70% | overlapping |
| captured-large-strings | 190.102 | 63.461 | -66.62% | separated |
| coordinate-counter-control | 14.600 | 15.497 | +6.14% | separated |
| unchanged-core-count | 12.377 | 12.264 | -0.91% | overlapping |
| unchanged-dotnet-text | 20.568 | 20.536 | -0.16% | overlapping |

Retain the coordinate +6.14% separated cost and large ASCII +20.30% median with
overlapping ranges for the substantial Unicode/output savings. No universal 10%
gate applies. All ordinary allocation medians agree; .NET's tiny amortized values
remain in raw. Do not normalize control movement or compose earlier gains. Stop
this trial without tuning, package repetition or canonical/native-parity claims.

Core 94F3F6E3 -> 12F4F00E, Python B2D9D946 -> CE02A36D. A later comment only
clarifies checkpoint ownership. All 676 immutable feed files rehash unchanged.
Original worker/collector terminal 0; evidence verified off VM. Artifacts:
artifacts/experiments/valid-text-sizing-20261116/.
Raw SHA256 EEB4C72976F5C9B2516FA6245A6B56AC04C6ABDB653EE8C47FE3ADAD64CBE63B;
validation 38B4BF766586574CB36DC489225607DC1F5896D8AA75D87C31065DC0962E3524.
