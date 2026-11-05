# Reuse the class loop for lazy repeats with a disjoint literal tail

**Accepted.** In `a+?b`, every shorter endpoint inside the a-run fails the required
b. Extend the existing pure-scalar repeat proof and RunClassRepeat loop to ordinary
RepeatLazy with an excluded, unfolded literal tail. Exact-count repeats also have
one endpoint. No new runner, storage, selector, mode or unchecked access.

Lazy Accept/boundary/anchor/fork and overlapping tails keep the normal VM; existing
RTL, folded-literal and compound-body exclusions remain. Capture-end markers still
execute normally. Full validation, scalar boundaries, capture histories/assertion
required ends, retained aliases and original interruption remain. Workspace and
remaining capture/counter storage stay admitted and released by their existing
owners. Intentional numeric revision: fewer instructions, frames and journal loans
on proved paths; the existing class loop retains bounded scalar checkpoints.
Declining proofs can add work. [Resource contract](../../../docs/SPEC.md).

One **103.015s** A0/B0/B1/A1 compares the accepted SmallVmChunks afterCore graph
with parent `8211fdcd` plus the archived patch. This reuses the current code baseline,
not the older dev33 Full Core. Six adapter/benchmark DLL/PDB companions and caller
are identical; only Core changes. One producer per ordinary Default process, normal
eight-CPU startup then CPU2. Producer warmup one second/minimum 16 calls, BCL 250ms/
minimum four, independent 60ms calibration/five alternating samples. All 180
producer samples exceed 57.8922ms. Complete typed range/value/string/count digests
are checked after timing. Ref-struct ranges are copied into the same small record
inside each timed wrapper. BCL diagnostics cover overlapping scalar semantics;
they do not establish a CPython/catalog verdict. [All observations](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Core literal control, first | 2.486 | 2.513 | +1.10% | 0 / 0 |
| Actual reluctant Search | 0.778 | 0.498 | -35.93% | 1056 / 64 |
| Same finite/live | 0.937 | 0.682 | -27.22% | 1168 / 176 |
| Same deadline/live | 1.219 | 0.919 | -24.57% | 1216 / 224 |
| Two-capture neighbor | 1.223 | 1.262 | +3.16% | 3248 / 3248 |
| Overlapping lazy neighbor | 0.494 | 0.509 | +3.15% | 1056 / 1056 |
| Unconstrained lazy-root neighbor | 0.472 | 0.485 | +2.91% | 1056 / 1056 |
| Greedy neighbor | 0.365 | 0.364 | -0.36% | 64 / 64 |
| Unicode Split neighbor | 117.424 | 116.321 | -0.94% | 19512 / 19512 |

Both current actual Search medians (0.506/0.491us) beat both previous medians
(0.782/0.774us); controlled gains are consistent too. The two-capture neighbor
`(a+?)(b)` declines this literal-tail proof and retains its allocation; its after
medians vary (1.273/1.227us versus 1.224/1.222us). Overlap/lazy-root costs are about
0.015us. Core's unchanged literal control moves +1.10%; no normalization or causal
gain credit is assigned. Unicode Split after processes straddle the previous ones;
its pooled -0.94% is not a stable gain. No confirmation or tuning follows.

Windows/Linux focused Release per .NET10/11: Core1658/Python1069/PCRE174 PASS.
New public coverage includes 17 Core shapes (three option modes each), capture
histories/nesting, assertions, RTL, overlap/empty/lazy roots, minima/maxima,
BMP/astral scalars; 13 Python cases cover byte/value aliases, malformed full tails,
work/working/result denial, original cancellation/deadline during repetition and
successful reuse after failure. Existing suites retain funded/cursor/profile
coverage. FullOpts retains the existing loop, helper3075->3146bytes; no isolated
speedup claim. All nine independent preflights agree, with actual allocation
1056->64B, finite1168->176B and deadline1216->224B.

Immutable artifacts/experiments/lazy-disjoint-repeat-20261105, verified off VM:

- evidence.tar.gz: `8900CE7D9A3B94BF0FA73BCE4A6729685FD0EDC02151A2759FD9384B8D9BCC7E`.
- windows-correctness.zip: `594444AA22C783B2D5A2D8608E601C3DF011A63BDD9BDA39DC45BF124823F2F0`.

Declaration `A231BDA26A2F81D58A545520F76FB351780486FAF6FFAD6532CDF6D09F78E2B3`
and three-file patch `34D1703F2E3CD4F807B2E5F0491F479F257E9B2F673C6AC9220ECDA80F3734D2`
are unchanged. Documentation/version freeze follows measurement; no runtime tuning.
All previous packages, symbols, Full and raw evidence remain immutable. Combine
this with smaller fixed chunks at the next coherent package/Full boundary.
