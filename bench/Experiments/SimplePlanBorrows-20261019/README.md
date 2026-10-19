# Borrow immutable interpreter plans

Parent5e7d1510,source111-dev. Actual Unsafe.SizeOf reports SearchPlan2744B and
SimplePatternPlan536B; both are readonly structs. Eight existing interpreter
helpers accepted them by value, repeatedly copying their reference-bearing data
per candidate/branch. Change eleven parameters to `in`; bodies, algorithms,
SIMD/search, preparation, allocations and numeric metering stay identical.
No field, framework, engine, classifier, policy switch or public API is added.
Original scalar/strict validation, captures, NonBacktracking, cancellation /
absolute deadline and result/cursor lifetimes remain.

## Caller and Release code-generation proof

Public unrestricted ASCII Count reaches CountSimplePatternFrom, then the same
finder/verifier per candidate. Existing controlled Count/cursor paths already
borrow these plans. Untimed verification uses the unchanged allocation-preflight
driver and actual same product bytes on each platform. No JitDisasm is present
in timed processes. Windows.NET10.0.13 and Linux.NET10.0.12 are separate within-
platform comparisons, not a runtime-performance comparison.

| Stack reservation | Windows before -> after | Linux before -> after |
| --- | --- | --- |
| FindNextSimplePattern |3832->552B|3752->520B|
| Fixed-length candidate helper |664->120B|3352->56B|
| TryMatchSimplePatternAt |600->64B|552->24B|

Windows finder has ten bulk write-barrier copy sites before, one after; branch
matcher loses its536B copy. Linux's expanded struct-copy sequences likewise
disappear. The remaining copy belongs to prepared literal-search value handling.
These are JIT frame reservations, not managed-allocation or exact heap claims.
Actual SizeOf stays; no unchecked-access campaign is reopened.

## One bounded public comparison

Reuse exact [previous Count fixture](../AsciiRejection-20261019/Program.cs),driver
45F6D773...,swap only Core. Quiet Xeon8573C CPU2/.NET10.0.12,FullOpts/R2R-off,
AB order,5 alternating samples per lane,120ms warmup/80ms calibration target,
60s child/140s batch cap. Two untimed codegen processes precede the pair. All
120 samples>=74.91ms,maxspread1.046x;20.19s including codegen,17.35s timed-child
wall. No confirmation, tuning or further timing. Constructors/startup excluded.

Public **Count** medians,microseconds (ordinary/compiled):

| Subject | Unlimited before -> after | Finite control before -> after | Bytes U/F |
| --- | --- | --- | --- |
| optional hit,13KB/2000matches |1736.56->142.01 /1737.41->142.16|559.01->554.25 /556.31->556.16|1792/112 unchanged|
| optional miss,13KB/0matches |842.69->171.57 /846.41->171.44|649.09->648.24 /650.96->652.57|1792/112 unchanged|
| fixed control,14KB/2000matches |7.924->8.091 /7.808->8.083|19.884->21.159 /20.038->21.197|0/112 unchanged|

Finite50M work/depth1024/64MiB working/128MiB result are experimental allowances,
not safe defaults. Hit~12.2x/miss~4.9x faster in this scoped public observation.
Finite controls within0.9%; fixed controls move+2.1..+6.4%,retained in raw evidence.
No fixed-cost equivalence, default-PGO, PythonRe, immutable110, whole-engine parity
or package/Status qualification claim. The existing generated comparator's49-53us
is a separate historical source/product experiment, not a paired timing here.

## Validation and identities

Release Corefocused7026/full39746,differential1504,Python165,PCRE80 pass without
skips. Existing public Match/IsMatch/Count/offset/cursor, scalar/capture/NB and
resource/interruption tests cover the affected implementation; no test mirrors
parameter spelling. All final test-output Core DLLs match the measured candidate.

BaselineB1F47A32B63963388085B70436522142A605A84036AB8B39F1905D1FED63B306;
candidate1D1A5003A446C089594E48EA0D8326F119B2CA6A81B06FB60FCF88A3BAA985A9.
[Raw samples, identities, both codegen summaries](measurement.json). Source/full
JIT receipt:artifacts/simple-plan-borrows-20261019/RESULT.md. VM archive downloaded /
SHA256-verified2B9138CA35C16978FC6743590CA8AEBDC3EED21B84BE9CE5E2CB96EB2F763DC4.
Quiet gateCPU2=0%,other observed dotnet CPU tick deltas0. Both cells terminal0.
Immutable110/older packages and all closed experiment receipts are unchanged.
