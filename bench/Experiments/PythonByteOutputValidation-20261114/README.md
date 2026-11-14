# Byte callback output: validity without unused coordinates

Source `ae463cb4` replaces discarded full validation metadata in
CompleteOutputBytes with the existing ThrowIfInvalidOnly helper. Strict validation
still covers the completed raw-byte callback stream, independently of the caller's
subject-text guarantee. Valid scalars may join across callback fragments; malformed
output retains absolute byte offsets. No new API, buffer, validation layer,
unchecked access or execution mode. No subject validation was restored.

All5162 PythonRe Release checks pass pertarget on Windows;116 focused checks
pertarget on Linux. Focused cases cover governed/unlimited cross-block fragments,
truncated-tail error2047 and final writer/result overlap admission with validity
enabled/disabled. Original interruption/refusal and result lifetimes remain.
UTF16 length/supplementary metadata is unused for byte-only output; the shared
validity kernel may still derive ASCII eligibility. Admission and buffers stay.

ONE public VM before/after pair, .NET11.0.0, caller-valid unchanged UTF8 and equal
patterns/options/output/limits. Prepared constant callbacks are timed, rich
capture replay and result consumption afterward. Separate ordinary producer
processes, same Core and caller DLL/PDB pairs, clean changing Python/source
identities;9 samples,250ms target, every sample>=20ms. No confirmation.

| Public operation | Before → after us | Before → after allocated bytes/call | Template control movement |
|---|---:|---:|---:|
| bytes/early-unicode | 693.814 → 77.818 | 525903 → 525881 | -5.21% |
| bytes/finite-live | 679.153 → 88.082 | 525982 → 525991 | -4.53% |
| text/early-unicode | 470.924 → 498.707 | 525953 → 525961 | +5.35% |

Raw-output primary ranges separate with a large improvement. Retain the unchanged
string-callback+5.90% observation and its+5.35% template movement. Controls span
−5.21..+5.35%; selected CPUs differ (CPU1/CPU7). Do not normalize them or claim
exact causal percentages/blanket equivalence. Allocation observations retain small
runtime variation; no major buffer/admission change is inferred from those totals.
This is a producer comparison, not a CPython ratio. Its reused before producer
has a different timing context from the preceding one-shot comparison; do not
compose gains or erase that comparison's adverse cases.

All532 staged package/symbol assets preserve hashes. No package/platform/Full
refresh or publication; this change is NOT in frozen dev84 packages/catalogs.
[Acceptance](acceptance.json). Both isolated comparisons are now CLOSED.
Raw verified offVM, keeper terminal0; SHA256 `BD8CA4C705F8772CF7480730FEF00A23B838C5DF32DC95B76F0489B3DE248BC5`.
Raw: `artifacts/experiments/python-byte-output-validation-20261114/`.
