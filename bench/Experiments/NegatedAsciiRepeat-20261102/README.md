# Negated ASCII repeat lookup: rejected

The actual import Count program has 45 instructions, three groups and five
repeats. Its negative ASCII Set at instruction33 already uses the no-rewind proof
through two CaptureEnd instructions to the excluded quote. Count also already
elides unobserved capture buffers. Its remaining per-scalar loop decodes a Rune
and dispatches class membership, while positive unfolded Sets use a checked ASCII
mask lookup.

This pilot extended that existing lookup to negative unfolded Sets, applying the
same negation as the ordinary predicate. Non-ASCII/folded/other classes retained
their existing routes. Per-scalar work, bounds, interruption, terminal publication,
depth and major-capacity admission stayed unchanged. No retained state, array,
engine, mode, unchecked access or accounting contract was added. This was a Set-
only change, distinct from the previously rejected Hybrid/CategorySet mask pilot.

One predeclared quiet-VM .NET11 RC3/CPU2 public comparison took **112.498 seconds**.
Baseline was clean `51527db7`; the candidate was the same source with the archived
Core-only patch. Both sets retained identical benchmark/PythonRe/PCRE2 caller
bytes. Normal runtime startup reported eight processors before in-process affinity.
The real 46,429-byte import corpus returned19 in every lane. Complete Count/range
outputs were consumed after timing; setup and compilation were excluded. Four
lanes include before/after and diagnostic predecoded/decode-then compiled Regex
on overlapping semantics. Managed lanes warmed2s/minimum512calls, comparators
250ms/minimum128. Independent60ms calibration, GC-matched duration confirmation
and five alternating samples were used. Every sample is at least27.667ms.

**Reject and revert.** Actual import ordinary/compiled medians improve only1.82%/
1.36%, below the predeclared5% useful-primary threshold. The Core folded control
regresses **23.33%**, with before/after spreads1.013x/1.009x; this is a demonstrated
public-operation regression above the10% allowance. Its folded predicate retained
its original route; no compiler attribution is asserted. Long negative-set Core
Count/Match improve36%, PCRE2 Count35%, while positive-set controls also improve.
These do not qualify the mixed result. Allocation medians are unchanged. The
unchanged literal control is+0.43%. All18 rows, including unfavorable and small
movements, remain in `summary.json`. No confirmation, variants or third run.

Focused Release correctness passed per runtime on .NET10/11: Core416,
PythonRe291, PCRE2139. Tests cover every ASCII value, negative masks, scalar bounds,
nested captures, mixed BMP/astral storage, RTL/NonBacktracking, work cutoffs and
parent-loan cleanup, original cancellation/deadline and strict malformed input.
PCRE2 witnesses assert the real shared slice route. One new test initially used
value-match coordinate names on a detailed context; its compile failure is saved.
Preflight initially lacked the benchmark context's AppContext-relative corpus;
only that fixture was staged before timing, with the failure/hash records saved.
No product or timing protocol changed to repair setup.

All prototype production and test changes are reverted. Existing source515 runtime
behavior and immutable staged dev.9/published packages remain unchanged. Do not
use prototype test output binaries with SkipBuild: the next affected build must
replace them. No full suite, catalog or package qualification was repeated for
the rejected pilot. Any future work on this shared loop needs a distinct cause;
do not tune variants to chase these results. Move to the next coherent cumulative
and package boundary for the accepted atomic and profile FullMatch improvements.

Raw records under `artifacts/experiments/negated-ascii-repeat-20261102/`:

- `evidence.tar.gz`: SHA256 `BBC238BF957573A51E497BC3EED1C4B11C795D73EBB6004D3E1B16EE60DE079E`.
- `correctness-source.zip`: SHA256 `AB15626BC67DE57360DED4C7A6935B95A692111C1D339D478074C33027ACB53D`.
- `source-tests.patch`: SHA256 `B3E2E602858CFBA6CF55067449B4EDA2328C07A6BDD276A7EF25072A8E64A90D`.

The archive includes frozen DLL/PDB sets, driver/binaries, corpus bytes and hashes,
predeclaration, setup/build records and all timed samples. Existing import-plan
inspection is preserved separately as `21DB7C64` in
`artifacts/experiments/import-count-next-cause-20261102/preparation.tar.gz`.
