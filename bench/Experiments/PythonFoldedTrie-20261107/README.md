# Reuse the prepared trie for short PythonRe prefixes

The metered candidate helper now reuses its existing prepared prefix trie when
the whole remaining suffix is at most 256 bytes. It returns only a necessary
start; the scalar VM still chooses ordered matches and captures. The existing
small-family and larger-suffix algorithms remain. No state, searcher, allocation,
public API or execution mode is added.

[Actual-route attribution](actual-route.json) confirms that Unicode-folded
`sherlock` has 12 prepared prefixes: S/s/long-s crossed with H/h and E/e.
The trie shares that prefix work; the old metered loop revisited each needle
after first-byte hits. The broad S..s interval belongs to the separate ASCII-option
route and does not explain this improvement.

The whole suffix bounds both candidate starts and trie depth to 256 bytes;
child lookup is a checked binary search over at most 256 byte keys. Entry and
completion charge/poll the original invocation. A candidate escapes only after
completion admission succeeds. No cross-window lookahead, scratch growth or
new per-program size metadata is needed. Numerical work charges can decrease;
strict whole-subject validation, major admission and VM controls retain scope.

ONE [public comparison](cost-summary.json) completed in **107.918336235s**:
separate Default-process ABBA, 21 rows/420 samples, minimum 51.4034 ms. Source
`c6434ac5` plus the archived patch changes Core `B2D022DB` to `C107DE10`.
PythonRe, PCRE2 and caller bytes are fixed. Normal eight-CPU startup precedes
CPU2 affinity; default tiering/PGO, 750 ms/minimum 32-call warmup, independent
75 ms targets and five samples apply. Results are consumed after timing.
The outer cap remains 120 seconds. No confirmation or tuning followed.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Captured folded Search | 0.445 | 0.381 | -14.32% | 64 |
| Folded Search miss | 0.378 | 0.195 | -48.42% | 0 |
| Same finite/live | 0.543 | 0.323 | -40.45% | 112 |
| Same deadline/live | 1.040 | 0.434 | -58.30% | 160 |
| Dense false prefixes | 10.34 | 6.20 | -40.08% | 0 |
| 256-byte miss | 0.390 | 0.206 | -47.21% | 0 |
| 257-byte miss | 0.398 | 0.429 | +7.87% | 0 |
| Unicode folding partners | 0.405 | 0.333 | -17.86% | 0 |
| Folded Search hit | 0.307 | 0.281 | -8.57% | 0 |

**Promote with the documented local cost.** Captured Search, ordinary/finite/
deadline misses, dense false prefixes, the 256-byte miss and Unicode folding
partners improve in both processes with disjoint overall ranges. The 257-byte
miss is persistently/disjointly slower: **+7.87475%/+31.30432 ns**. Keep this
boundary cost rather than enlarging the bound or running another tuning campaign.
All 21 allocation medians are unchanged. Folded hit improves 8.57% with overlapping
ranges and remains uncertain.

The unchanged predecoded .NET control moves -0.05% with overlapping ranges.
Core literal +1.45%, PCRE literal -3.09%, anchored FullMatch +1.81% and ordinary
mail +0.55%/+11.68 us also overlap. Full Unicode hit finite improves 6.83% with
disjoint ranges while bypassing this helper: no causal credit. Other Unicode,
long-input and mail movements retain their process drift and uncertainty.
Do not normalize controls or infer fresh catalog/.NET/CPython parity from this A/B.
The finite miss's absolute extra cost falls 0.16486 to 0.12830 us while relative
overhead rises 43.58% to 65.75%.

[All process observations](process-observations.json), [declaration](declaration.json)
and [acceptance](acceptance.json) retain every row, identity and adverse cost.
Release checks pass on Windows/Linux, each .NET10/11: Core 210/PythonRe 437.
Each set includes 14 new cases for borrowed offsets/tails, 255/256/257 boundaries,
earliest starts, sticky work denial and parent loans, original interruption,
long-s/Kelvin partners, ordered captures, malformed tails and result admission.
This helper's sole production caller is PythonRe scalar discovery; PCRE2 is an
unchanged comparison control, with its broader checks reserved for CI.

Initial new assertions incorrectly borrowed .NET's long-s behavior and expected
VM storage for a straight-line value. They were corrected to explicit Python
outcomes and published-text admission before timing, without producer changes.
The failed/corrected test logs and [preparation notes](preparation-corrections.json)
remain in the raw archive. The VM lacked rg for an optional log summary; grep
replaced it before the measurement script started.

Immutable off-VM archive:
`artifacts/experiments/python-folded-trie-20261107/evidence.tar.gz`, 2423084 bytes,
SHA256 `04504B53D920D1A6FA7CCD3D0F056C3DAC6FB69959E50ABC01BFC6D7DDD75534`.
It preserves both graphs/PDBs, worker, patch/test sources, all samples and checks.
Frozen Full50 assets and all 270 staged package/symbol files remain unchanged.
No package/Full qualification or NuGet publication occurred.
