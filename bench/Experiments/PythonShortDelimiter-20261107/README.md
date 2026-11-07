# Reuse class/delimiter windows on short subjects

Python scalar search now admits the existing class/delimiter window from 16
subject bytes instead of 256. The original change deliberately left short
subjects on general discovery. Its correctness proof is independent of that
cost gate: at most 64 instructions establish the direct ASCII class/delimiter
shape, allowing capture markers. Existing metered search, scalar rewind and VM
verification retain scalar context, ordered captures and backreferences.
Match/FullMatch stay anchored. No state, helper, allocation or public API is added.

The actual detailed-search catalog case is `([a-z]+)-([0-9]+)` over the 22-byte
`prefix item-123 suffix`. [Untimed discovery](work-attribution-summary.json)
falls from 99 to 36 work units, with the same 7..15 match and 320 working bytes.
Known-start verification remains 29 units. This is work attribution, not public
throughput or an exact VM-attempt count. Raw probes' ExistingLengthGate field
describes the original 256-byte expression, as the summary clarifies.

ONE [public comparison](cost-summary.json) completed in **108.623755455s**:
separate Default-process ABBA, 21 rows/420 samples, minimum 51.4695 ms. Source
`6a6432a6` plus the archived patch changes PythonRe; Core, PCRE2 and caller bytes
are fixed. Normal eight-CPU startup precedes CPU2 affinity; default tiering/PGO,
750 ms/minimum 32-call warmup, independent 75 ms targets and five samples apply.
Full detailed results are retained and checked after timing. The outer cap stays
120 seconds. No confirmation or tuning followed.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual detailed Search | 1.367 | 0.776 | -43.22% | 496 |
| Same finite/live | 1.980 | 1.195 | -39.67% | 608 |
| Same deadline/live | 2.854 | 1.900 | -33.44% | 656 |
| Actual value Search | 1.121 | 0.551 | -50.80% | 272 |
| 16-byte detailed | 1.741 | 0.767 | -55.93% | 488 |
| 15-byte detailed | 1.634 | 1.780 | +8.92% | 488 |
| Dense false delimiters | 2.410 | 2.501 | +3.77% | 272 |
| Folded miss neighbor | 0.211 | 0.222 | +5.35% | 0 |
| Explicit whitespace neighbor | 4.798 | 4.963 | +3.42% | 1344 |

**Promote with the documented local costs.** Detailed/value Search, finite/live
and deadline detailed Search, and the 16-byte case improve in both processes
with disjoint overall ranges. Three adverse rows also persist/disjointly:
15-byte detailed +8.9184%/+145.7109 ns, dense false delimiters +3.7731%/+90.9245 ns,
and folded miss +5.3537%/+11.2966 ns. The 15-byte case still declines the window in
both versions; do not assign its real cost to an unmeasured cause. No additional
threshold, suffix-independence shortcut or retuned variant followed.
All 21 allocation medians are unchanged.

Core literal +0.87%, PCRE literal +2.94% and unchanged predecoded .NET -3.14% have
overlapping ranges. Anchored FullMatch +5.54%, explicit whitespace +3.42%, mail
+0.88% and the Unicode/long-subject neighbors also overlap. Preserve all
[process observations](process-observations.json) without normalization or causal
credit. The finite detailed extra cost falls 0.61320 to 0.41858 us while its
relative overhead rises 44.86% to 53.93%. This does not refresh qualified catalog
ratios/Status or establish .NET/CPython parity.

Focused Release Python checks pass **340 per .NET10/11 on Windows/Linux**,
including the expanded 54-case window fixture. It covers short and long
proved/unproved prefixes, scalar/unaligned/nonzero floors, 15/16/17/255/256/257
boundaries, later-start captures, anchored operations, explicit gaps/whitespace,
every work cutoff, caller loans, original cancellation/deadlines and retained
cursor aliases/snapshots. Existing admission and strict full-input validation
remain. Numerical work can fall; no memory scope or lifetime contract changes.

The static graph guard initially inherited the prior Core-change experiment's
unchanged-Python assertion. It was corrected to assert unchanged Core before
the measurement/preflight script started. Producers and settings did not change.
[Preparation notes](preparation-corrections.json), [declaration](comparison-declaration.json)
and [acceptance](acceptance.json) retain scope.

Immutable off-VM archive:
`artifacts/experiments/python-short-delimiter-20261107/evidence.tar.gz`, 2416368 bytes,
SHA256 `D5B149034C337434D4B9611E97293A9D639215BC5470ABB7E5A3A04348AA5D9E`.
Both graphs/PDBs, worker, source patch, probes, samples and checks remain preserved.
Frozen Full50 assets and all 270 staged package/symbol files are unchanged.

Together with [prefix-trie reuse](../PythonFoldedTrie-20261107/README.md), this is
the common **0.5.0-dev.51** delivery batch. The source A/B above used version50
graphs. Final version51 packages, platform/consumer acceptance, cumulative costs
and one complete refresh are separate pending delivery checks. No qualification
was repeated for this individual change and no publication is authorized.
