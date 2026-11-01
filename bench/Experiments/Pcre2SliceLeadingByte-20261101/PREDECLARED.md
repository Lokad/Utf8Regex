# Reuse the required leading byte for PCRE2 Boolean slice search

Baseline runtime a1986746, parent7a99e749 data/receipt only. Frozen baseline Pcre2
DLL SHA2564F5EE13373E74A9C0F637972997C8BA57EEF155B2FE428DC80FA7E1BEB17709E.
Candidate is parent plus the archived four-file adapter patch. Core is unchanged.

Use the existing AST-required leading ASCII byte, through the current IsMatch
slice cursor. Scan only when the current byte is impossible; dense candidates
retain normal matching. Every scan admits at most2048bytes with direct original
token/deadline checks, including unlimited calls. No empty/contiguous/compile-
anchored filtering, no changes to non-None match options. All actual candidates
use existing VM/capture workspace. No compiled metadata, arrays, engine, selector,
framework or unmanaged/unchecked operation. No exhaustive overload symmetry.

Numeric work/admission costs can decrease by avoiding impossible VM attempts;
the scan adds its own bounded units. Preserve meaningful work/depth/working/result
controls, complete strict validation, alias/result validity and interruption.
Focused Release net10/net11 full PCRE2 checks plus deterministic2047/2048/2049,
unlimited basic metering, refusal, first-scan cancellation/deadline and13B loans.

One <=120s public comparison on the quiet VM/.NET11RC3/CPU mask4: fourteen cases,
four equal-result lanes (before/after/predecoded .NET/decode-then-.NET), warm1s/
min64calls, independent60ms calibration, five alternating samples >=20ms, batches
checked after timing. Actual assertion-capture default/live/finite/deadline/miss;
dense hit/false starts, failed candidates, Unicode prefix, caseless, Unicode
leading, nullable, anchored and an unchanged literal control. One selected noise
confirmation at most, then stop. Accept useful primary gains with unchanged or
improved matching allocations and no >10% demonstrated adverse public control.
No cold/package/fullcatalog repetition; preserve immutable dev.3/published bytes.
