# Rewind direct negated-pair Count windows with checked spans

**Accepted.** Parent `bbcc3fdbffb90a24fd929e016798ddd9e15e2b34`; qualified dev41
Core is the fixed baseline. Two runtime files change +54/-28 lines. The existing
ASCII-mask-pair recognition serves forward fusion and backward window discovery.
No new program facts, state, buffers, cache, owner, execution mode or framework.

Only a direct literal after an unfolded negative ASCII Set pair, minimum one and
unbounded maximum, admits the new rewind. The last excluded ASCII byte plus one,
or the original scalar floor, bounds possible starts. Checked LastIndexOfAny
blocks cover at most2048 bytes, with pre-scan and terminal work/deadline checks.
Whole-input validity makes ASCII stops scalar boundaries, even when intermediate
blocks split a wider scalar. Literal discovery still rejects absent tails; the
VM still verifies every candidate and preserves captures/greediness/ordering.
Other classes, bounds, whitespace/gaps and reverse/NB routes retain their paths.
Work requirements intentionally fall; other admission and result lifetimes remain.

The Windows work probe changes direct Count10028->38 steps and its rewind10001->11,
while a single attempt remains15. Both finish with no working reservation. Public
Count now succeeds under10000 work/depth1/working1/result1 on the20001-byte Unicode
subject. These are untimed tracked-work/functional facts, not public timing or an
unmetered branch census. The probe retains its exact Windows identities separately.

ONE **99.702403874-second** comparison uses four separate Default producer processes
in ABBA order followed by independent ordinary BCL diagnostics. Only Core DLL/PDB
differs; six adapter/benchmark and both worker files are identical. Normal8CPU
startup thenCPU2 affinity, default tiering/PGO, one-second/minimum128 warmup,
independent60ms calibration and five samples/row/process. All **340 producer samples
are at least57.2557ms**; typed results are checked after timing. Total cap120s,
per producer35s, diagnostics15s. Focused2GiB admission passed immediately at
7928476KiB available. No confirmation, normalization or tuned variant.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Native literal control |12.51|12.47|-0.33%|0/0|
| Actual imports control |10.59|10.47|-1.19%|0/0|
| PythonRe Unicode pair Count |94.93|2.99|-96.85%|0/0|
| Core Unicode pair Count |94.82|2.89|-96.95%|0/0|
| PythonRe Count, finite/live |117.70|7.89|-93.30%|112/112|
| Core Count, deadline/live |197.52|8.35|-95.77%|160/160|
| PythonRe short dense Count |491.79|465.22|-5.40%|0/0|
| Positive-class Count control |155.20|182.43|+17.54%|0/0|
| Bounded-pair Count control |135.27|152.03|+12.39%|0/0|
| Folded-pair Count control |107.63|109.78|+2.00%|0/0|
| Wide-exclusion Count control |231.70|242.70|+4.75%|1056/1056|
| Overlapping-tail Count |177.16|171.49|-3.20%|1056/1056|
| PythonRe missing-tail control |1.76|1.72|-2.49%|0/0|
| Excluded-prefix window Count |2.40|1.18|-50.94%|0/0|
| PythonRe IsMatch control |12.48|11.62|-6.87%|0/0|
| RTL Count control |4.04|3.93|-2.58%|3288/3288|
| NonBacktracking Count control |2.81|2.81|+0.08%|888/888|

[All observations](cost-summary.json) retain process medians, ranges, identities
and allocations. Both positive/bounded/wide after process medians exceed both
before medians; accept those local costs for the large demonstrated Count gains.
Do not dismiss them as noise or apply a universal10% gate. Several small controls
overlap; imports movement is not a causal speedup. All allocations are unchanged.
Controlled Count retains112B/160B of overhead and measured elapsed work. Producer
peak RSS148236–149056KiB is retained, without a process-memory reduction claim.

Separate same-result ordinary BCL diagnostics report predecoded/decode-then Count
86.12/96.96us and IsMatch85.75/97.21us, allocation0/20024B. They do not emulate
owned limits and are not compiled-.NET, CPython or qualified catalog evidence.

Focused Release checks pass Windows/Linux .NET10/11: Core245, PythonRe181 and
PCRE2 23 per target, including25 new Core cases plus prior span/folding/resource
coverage. Scalar floors/tails, excluded whitespace, overlap, finite/positive/wide/
folded cases, every work cutoff/enclosing loan13 and original token/deadline during
backward blocks remain covered. A driver variable-shadow build failure and an
incorrect wide-class control oracle were corrected before timing; their original
sources/logs remain. Wide records now have an excluded separator; the unbounded
class otherwise consumes multiple intended records. Production code was unchanged.

Core before `3C89A97FD97CE2EF7E95A784FE9431135A287114DB15F4A780DFE1E037FE6469`;
after `A13D50C19F4FF9654E23EB4991CAAD94365AECB51371D12621CFB8DE5902D3B5`.
Runtime blobs CountCandidates `82d2e4887e69204ab09d73c67abbb65ac97f061f` and
TerminalClassRepeat `6384129ea324a0ad26a8a93d12f83ef33115f4d5` match both hosts.

Immutable `artifacts/experiments/negated-pair-rewind-20261106/`:

- `evidence.tar.gz`: `EA5E59514706C88F0696D41C2FACD4CCB70B6AFC64F343DAD51A9900FBDB4D01`.
- `windows-evidence.zip`: `3428B6EFADCF043968E20742308F1BBE789402C2569DC494ADE670C949AB1A0F`.

Raw retains exact graphs, source/test patches, probes, preflights, every sample,
process receipts and terminal zero. All eight qualified dev41 Full assets, staged
packages and published tables remain unchanged. No package/Full/catalog replay;
this accepted source change is later than the qualified dev41 package candidate.
