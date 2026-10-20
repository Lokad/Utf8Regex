# One bounded first-byte scan for controlled exact pairs

Parent `e6208014`, source 119-dev. Raw candidate discovery has no prepared first-byte
union. Exact LTR pairs formerly scanned each window of at most 2,048 bytes once per
literal. One inline checked `Span.IndexOfAny(first0, first1)` now finds the earliest
possible start. The existing ordered loop still checks both complete literals,
including shared first bytes and long or cross-window candidates. Confirmation,
reverse, folded, empty and other-arity paths keep their existing selections.
No helper, field, owner, cache, preparation object, engine mode or public API is added.

**Accounting revision:** one scan removes one input-work charge per window.
Each scan still starts with `TryChargeInputStep`, checking the original synchronous
token and absolute deadline. Full comparisons, branch/result/terminal work, sticky
denial and caller loans remain. Depth, working/result capacity, defaults and
retention keep their existing scope. Scalar dispatch and complete-input validation
are unchanged. Actual `Unsafe.SizeOf` and completed checked52 stay intact.

## One public pair — accepted

Quiet Xeon 8573C CPU 2, .NET 10.0.12, FullOpts/R2R off; only the Core DLL differs.
Reuse the existing 62 public lanes and protocol, adding two controlled folded and
three-branch neighbors before timing (64 total). Warmup 120 ms, target 80 ms, five
alternating-order samples, 60-second phase and 180-second batch caps. One 81.305 s
pair, 640 samples of at least 64.702 ms. No confirmation or retiming. All 25 range /
subject and 64 operation preflights agree.

Ordinary public medians in microseconds; finite normal/live Core rows retain 112 B/op:

| Subject | Finite Count before → after | Finite value enumeration before → after | Live Count before → after |
| --- | --- | --- | --- |
| Hit, 13 KB / 2,000 matches | 283.137 → 262.544 | 389.154 → 370.666 | 276.876 → 256.222 |
| Miss, 13 KB | 366.975 → 337.360 | 374.651 → 342.541 | 360.189 → 328.415 |
| Dense false prefix, 4,099 B | 267.750 → 141.810 | 292.535 → 144.018 | 294.450 → 138.364 |
| Short, 8 B / 2 matches | 0.526 → 0.491 | 2.654 → 2.641 | 0.513 → 0.483 |
| Shared first byte, 15 KB / 2,000 matches | 312.627 → 289.348 | 416.707 → 400.204 | 307.164 → 283.188 |

Compiled rows agree. Deadline Count falls 557.616 → 518.190 us / 160 B, enumeration
693.411 → 647.172 us / 272 B. Ordinary hit/miss finite Count improves 7.3–8.8%; dense
false-prefix Count/enumeration roughly halve. Most affected spreads stay below
1.044x; baseline dense rows reach 1.136x, so dense percentages are point estimates.
No measured public regression exceeds 3.3%. All Core allocation medians are unchanged.

True .NET controls move −0.58..+0.40%; Python/PCRE neighbors stay within 0.6%.
The controlled three-branch neighbor moves +3.25%, folded pair −0.54%.
Unrestricted short-pair routes move up to +3.26%. Unrestricted three-branch Count
unexpectedly improves 18.4%; retain that neighbor/codegen observation without
attributing it to a raw-pair union or treating it as an unchanged timing control.
PCRE's pooled unlimited allocation median shifts 16.087 → 15.914 B/op; that does
not establish an allocation gain or exact parity. No additional timing is needed
for the bounded acceptance decision.

This supports the measured source change, not universal finite overhead, cumulative
checked-access acceptance, CPython Status, .NET 11 or immutable118 performance.
118 remains READY and unchanged; no package was built or requalified for this helper.

## Correctness and identities

Focused Release checks: Core 210 plus 970 resource/copy/deadline checks, Python 858,
PCRE 91; no failures or skips. New tests call the production finder directly and
cover 2,047/2,048/2,049 offsets, borrowed tails and start floors, shared prefixes and
ordinals, non-ASCII and long literals, every work cutoff and sticky denial, original
cancellation/expiry during scanning and full comparison, and caller reservations.
Existing public scalar/RTL/NonBacktracking/folded/context/capture/alias cases pass.
Actual public plans prove changed optional/shared-head two-literal routes and
controlled three-literal/folded declines. No forwarding executor was added.

Baseline Core `72B1A1C99C700F7477830621364EA5507BDBF58A033E8A6479D0B4C3ED1FB617`;
candidate `6053493C7B76CD05FB7135195AA3FC9729932ABED5B46B4E8A0F3DF36A380F38`.
[Raw samples, preflights, identities and summary](measurement.json).
Archive `655AA0DA3ADE54EEBCD5D7EB1B035F9A3FDA32A97E5D9B1669A7700A756BCDA6`.
Private source/predeclaration/logs: `artifacts/controlled-pair-candidate-20261020`.
Source `d0bb4cb6` [CI39385691193](ci.json) is terminal green on Windows/Linux,
closed; do not repoll. The original versioned R09 comparison lanes are complete,
while a fresh source review identifies ordinal backreference comparison as the
next applicability investigation. Broader R06/R07/R08/R09/R10 remains open.
