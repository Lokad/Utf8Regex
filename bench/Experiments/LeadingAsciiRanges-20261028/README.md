# Proved initial ASCII ranges in scalar Count

75ef5759 extends the existing first-ASCII-scalar Count filter to positive,
unfolded contiguous ASCII masks behind mandatory greedy repeat entries. Optional
entries, forks/assertions, Unicode classes, negation and folding decline. Proof
inspection is bounded; it creates no prepared object or execution mode.

The existing literal search stays IndexOf for singleton ranges. Other ranges
use checked IndexOfAnyInRange, with the same 2,048-byte controlled windows and
original invocation. ASCII candidates cannot split a UTF-8 scalar. The scalar
VM still verifies every candidate, including the entire bounded repeat, Unicode
whitespace, captures and backreferences. Full validation, finite work/depth/
working/result admission, original token/deadline and terminal publication stay.

The actual capitals pattern is `(?:[A-Z][a-z]+\s*){10,100}`. Its existing ASCII
structural plan cannot serve a mixed subject's Unicode whitespace without proof;
this change does not force that route. Misses can now finish without VM scratch,
but actual attempts still require admission. Numerical work costs remain
route/version dependent under SPEC.md. Match and retained lifetimes are unchanged.

[Raw comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; immediately prior
4cee890a versus 75ef5759. Four public Count lanes, 500 ms warmup, three alternating
approximately 50 ms samples, equal-result preflight/timed checks. One comparison
took **34.91 seconds**, within its 120-second cap; no confirmation.
a40bda03 remains the fixed cumulative release baseline.

| Public Count | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Capitals full corpus, ordinary | 751802.700 | 143593.300 | 5.24x faster | 11880 / 11880 |
| Compiled | 751151.500 | 143737.300 | 5.23x faster | 11880 / 11880 |
| 32 KiB, finite limits and live token | 3378.747 | 617.962 | 5.47x faster | 3680 / 3680 |
| Dense mixed-scalar hits | 124.308 | 100.613 | -19.1% elapsed | 18208 / 18208 |
| 4 KiB mixed-scalar range miss | 329.597 | 0.836 | 394x faster | 1696 / 0 |
| Existing literal-prefix Count control | 12999.200 | 13044.975 | +0.4% elapsed | 3600 / 3600 |
| Unchanged ASCII native route | 4.332 | 3.692 | -14.8% elapsed, unattributed | 0 / 0 |
| Unchanged English literal Count | 70.484 | 70.586 | +0.1% elapsed | 0 / 0 |

The full corpus has 7,384,531 bytes and 11 results. The independent
[five-sample README refresh](leading-range-refresh.txt) reports **55.95x ->
10.41x compiled**, **16.26x -> 3.07x ordinary**, still Gap. The unchanged native
route's faster point estimate is not attributed to this scalar-only filter.
[Snapshot verification](leading-range-verify.txt) passes.

[Both-runtime Core checks](core.txt) cover nested/optional repeats, ASCII unions,
negated and Unicode classes, supplementary scalars, captures/backreferences,
nonzero floors, window tails and Unicode separators. Every small work cutoff
retains the original scratch loan; actual VM storage denial and malformed tails
remain authoritative. Bounded search and final publication observe original
cancellation/deadlines. [PythonRe](python-net11.txt), complete [PCRE2 .NET 10](pcre-net10-before.txt)
and affected [PCRE2 .NET 11](pcre-net11.txt) checks pass.

Windows CI 40705370593 at preceding 1a2915f0 repeated the unrelated PCRE2 warm
allocation assertion ([6368 B failure](ci-40705370593-failure.txt)). The old fixture
passes locally, including a [single-CPU full profile](pcre-net10-onecpu-before.txt);
the allocation source remains unconfirmed. 562aa7c9 warms the exact measured
call site for 250 ms instead of a few hundred fast calls at different sites.
The measured batch still requires exactly zero allocation and correct public
results. No pool/production matching code changed for this test adjustment.
The [single-CPU complete .NET 10 profile after adjustment](pcre-net10-onecpu-after.txt)
and .NET 11 checks pass; new CI must still validate it. No retry of the failed
job was used as a substitute for this fixture change.

Exact baseline/candidate DLLs/PDBs, driver, raw results and generated snapshots
are retained in the
[immutable archive](../../../artifacts/experiments/leading-ascii-ranges-20261028/parity-leading-range-75ef5759.tar.gz),
SHA256 `DA32C519C3FCB455EA9774C1DA6632957B919CBA0E204A6FBF5F0E8E61341981`.
No complete catalog or package matrix was repeated.
