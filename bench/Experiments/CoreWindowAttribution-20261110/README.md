# River-window Count attribution and route correction

Frozen dev65 source `998c630f`, reused Release .NET11 RC3 graph. One existing
`--measure-compiled-microcost-case industry/leipzig-river-window-count 200`
command takes7.249s under120s, without a build, source sync or benchmark refresh.
Before reuse, all eight dirty generated Full outputs and Core/benchmark DLL/PDB
pairs match the accepted archive. The actual16,013,977-byte Leipzig subject
returns2 matches and3445 candidates/verifications.

Public compiled Count is3393.857µs; validity-only554.032µs. Internal
CompiledDirectCount is2795.843µs, but **executes a different scalar route** and
cannot directly attribute the public kernel. Full facade tracing shows the
existing CountCore scalarWindowCount/SupportsValidatedUtf8Input gate already
uses the scalar-aware bounded window invocation on this subject. A missing
diagnostic route marker produced `<none>`; that was not evidence of scalar
execution. Internal R05 router/runtime guards are not this public bottleneck.

Discard the attempted dispatch changes without a timing comparison. The final
production correction is two lines marking NativeOrderedLiteralWindow immediately
before the existing live facade call. It changes no eligibility, algorithm,
scalar/strict validation, capture priority, coordinates, work/admission or lifetime
contract. It prevents the same incorrect route inference in future diagnostics.

Nineteen independent baseline Count cases first pass against unchanged behavior.
An initially incorrect expected count was corrected: a greedy first branch spans
both nearby river suffixes, yielding1; LF separation requires2. Twenty final
cases retain scalar min/max/2048 edges, greedy nonoverlap, overlapping prefixes,
word boundaries, BMP/astral/NUL, malformed tails, original cancellation, finite
controls and actual route/declines. Marker-only1561 affected Release checks pass
per .NET10/11 target on Windows. The same cases also pass on both platforms in
the [descriptor-copy comparison](../WindowSearcherCopies-20261110/README.md).

Temporary dispatch prototypes, diagnostic failures and their corrections are
retained separately; neither their passing semantic checks nor an unmarked route
is a performance qualification. No new matcher, field, mode, accounting precision
or package qualification was introduced. Immutable dev65 bytes remain unchanged.

Raw command, original producer/input identities and per-file transfer hashes:
`artifacts/experiments/core-river-attribution-20261110/`.
Original attribution SHA-256
`8DB0D771278B6DE5D57D3351406CCCE43E302C66A2B0E1B0A57CFE0123BC3F5E`.
The subsequent public comparison measures the actual live window implementation;
phase timings here are neither a public speedup nor a catalog Status.
