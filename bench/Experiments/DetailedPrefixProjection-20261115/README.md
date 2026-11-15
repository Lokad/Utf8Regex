# Detailed results map only their capture prefix

Accepted: extend the existing maximum-capture-endpoint map from ASCII to Unicode
prefixes. Reuse the bounded descriptor/map helpers; remove the custom ASCII proof
loop and whole-subject Unicode fallback. Trusted discovery defers metadata until
actual output. An optional existing description is represented explicitly by the
nullable value already used by its caller. Four net production lines added.

The helper has one production consumer. Returned match/group/history boundaries
are covered by the largest whole/history endpoint, including lookahead captures
past the whole match and lookbehind captures before it. Views still borrow all
original bytes. Explicit offset maps bypass this helper; callback maps retain
separate full scope/reuse. No new map, owner, framework, selector state or mode.

Five public baseline failures establish actual operating cost: four early valid
Unicode detailed cases exceed128 work steps in unused description; an early
Unicode capture requests34192 modeled result bytes under a4096 cap. They pass
afterward, with complete oracle coordinates/history. The corresponding strict
128-step cases still deny required whole-subject validation. Pattern/replacement
checks, scalar semantics, managed bounds and original interruption remain.

ONE102.584s ordinary-process ABBA,13 public rows, five150ms-target samples per row/
process,>=20ms floor,180s cap,0 confirmations. Every group/history byte andUTF16
range is consumed into a checked checksum during the public MatchDetailed action.
Extra decoded strings are not requested; independent correctness tests check full
capture text. Russian/Chinese are the existing full valid unchanged document
fixtures (1570556/813478B); patterns capture their first Cyrillic/CJK word. No
canonical Count catalog or native parity ratio is changed.

| Public detailed workload | Before µs | After µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| russian-document-strict | 9079.169 | 3506.375 | -61.38% | Separated |
| russian-document-trusted | 7751.696 | 0.754 | -99.99% | Separated |
| chinese-document-strict | 3682.396 | 1247.988 | -66.11% | Separated |
| chinese-document-trusted | 3198.474 | 0.942 | -99.97% | Separated |
| russian-document-finite-live | 7719.546 | 1.465 | -99.98% | Separated |
| lookahead-trusted-early | 3110.547 | 1.183 | -99.96% | Separated |
| history-trusted-early | 3229.073 | 1.492 | -99.95% | Separated |
| late-unicode-strict | 2485.344 | 2476.004 | -0.38% | Overlap |
| far-history-strict | 1783.617 | 1781.328 | -0.13% | Overlap |
| tiny-ascii-strict | 0.720 | 0.719 | -0.06% | Overlap |
| anchored-trusted-miss | 66.531 | 0.292 | -99.56% | Separated |
| unchanged-core-count | 24.342 | 24.346 | +0.02% | Overlap |
| unchanged-dotnet-detailed | 0.182 | 0.184 | +0.98% | Overlap |

Allocations fall from3674256→808B on ordinary Russian calls and roughly1.40MB→832B
on Chinese calls. Chinese baseline samples have fractional/per-process variation;
do not claim an exact old fixed payload. Finite/live Russian3674416→968B,
lookahead~4.33MB→1352B and repeated history~4.33MB→1880B. Late/full-history/tiny/
anchored-miss/control allocation medians are unchanged. All complete results
agree. Control movement is retained, not used to normalize observations.

Strict full validation/facts remain at this checkpoint. Its remaining3.506/1.248ms
on Russian/Chinese is not a managed lower bound or isolated validation timing.
The source still requests whole-input UTF16 metadata there; investigate the
existing validity-only/ASCII-proof route separately before claiming that cost
can be removed. The current prefix-map comparison is closed; no tuning rerun.

Baseline is the accepted frozen Windows pair implementingd6e5974f (actual assembly
informational identity9b03f15d plus its measured finite-Match patch, retained as-is).
Prototype is based ond6e5974f plus this patch. Distinct Core/PDB pairs and343 compiler
documents each verify; common caller/runtime controls are identical. Stable.NET11,
normal8CPU startup before fixedCPU2, one producer/process, no VM producer build.
FullOpts outer entry1029→1097B, inner1449→1449B, map helper700→653B.

Windows/Linux.NET10/11 each pass789 focused Release cases. Five expanded mode/
alias checks per target additionally cover both policies with RTL/NonBacktracking
and retained Unicode map views, reusing the exact measured Core images. Existing
coverage includes strict malformed tails, every capture/history coordinate,
empty/missing results, offsets, refused work/working/result admission and original
cancellation/deadlines during projection. One general prefix fixture replaces
the ASCII-only fixture; shared oracle assertions avoid duplicate tests.

Modeled map/description work and working/result extents may shrink. Every needed
array remains admitted before allocation. The Core specification documents the
revised accounting scope; it does not promise a heap/RSS limit. No public API,
matching algorithm, captures or lifetime contract changes.

Raw: artifacts/experiments/detailed-prefix-projection-20261115/evidence.tar.gz,
SHA256 `07A2A80F2487748475D69C45CD93257BEF7CF780EE20AFFEFD9A92F088D5C4C8`.
Local-validation and expanded-test archives alongside it retain all sample/process
medians/ranges, source patch, before failures and final checks. Original worker/
collector handles are terminal0; raw verified offVM before keeper release. No
package/Full refresh; qualified0.5.0 and historical staged bytes remain immutable.
