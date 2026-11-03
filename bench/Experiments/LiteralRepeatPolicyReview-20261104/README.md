# Reuse the existing repeat proof for literal atoms

Accepted source `9f2d0c46`. The same eight-line extension archived on November2
admits unfolded Literal atoms to the existing no-rewind scalar-repeat helper.
Exact scalar equality reuses its loop; required endpoints, greedy/capture trails,
folded/lazy/RTL exclusions, whole-subject validation and per-scalar interruption/
work remain. No runner, storage mechanism, unchecked API, mode or switch was added.
Unused repeat journal storage can disappear; needed state still requires admission.

The [old rejection](../LiteralAtomRepeat-20261102/README.md), incomplete PCRE2
validation and raw data remain unchanged. The current user policy accepts local
regressions for relatively major gains; this is current-source evidence, not a
relabeled historical pass. No completed checked-access campaign was reopened.

One **88.953s**, eighteen-case public A/B under120s, frozen `6b17c1e0` graph versus
only candidate Core rebuilt. PythonRe/PCRE2/benchmark DLL/PDBs remain identical.
Normal eight-CPU startup thenCPU2,1.5s/min128 managed and250ms/min64 comparator
warmup, independent60ms calibration, five alternating samples and complete result
digests after timing. Minimum22.107ms; no confirmation or alternate implementation.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
|empty-findall|139.424|111.906|-19.74%|20016/16528|
|finite-findall|163.964|146.365|-10.73%|24216/20728|
|deadline-findall|220.071|210.734|-4.24%|24264/20776|
|mixed-findall|115.943|91.659|-20.94%|14872/11384|
|captured-findall|76.283|56.277|-26.23%|16040/12552|
|bodycapture-control|78.029|87.720|12.42%|15016/15016|
|overlap-control|39.562|48.867|23.52%|8696/8696|
|alternative-control|192.592|213.661|10.94%|52808/52808|
|count-finite|125.900|101.066|-19.72%|3664/176|
|core-rtl-control|27.885|33.826|21.31%|3552/3552|
|core-chinese-control|134.769|135.920|0.85%|0/0|
|literal-control|8.741|8.830|1.02%|2112/2112|

[All observations](summary.json) retain offsets, lazy/folded/assertion and Core
class/NonBacktracking controls. Primary gains about20-26% and finite Count scratch
3664->176B justify the explicit local costs: capture-inside+12.42% (~9.69us),
overlapping tail+23.52% (~9.31us), alternative+10.94% (~21.07us), RTL+21.31%
(~5.94us). Those costs are stable observations; their runtime causes remain
unresolved. Do not invent JIT attribution. Chinese+0.85% and native literal+1.02%
are control observations, not improvements. Chinese-after/class-after spreads
reach1.10/1.09; this limits causal precision and does not obscure the main decision.

Release validation per .NET10/11: full PythonRe4708 and PCRE26498 pass. Initial
Core41289/41290 passed; the single obsolete cursor-depth witness was corrected,
then all200 affected checks pass without repeating the unchanged broad suite.
Initial PCRE2 depth checks346/353 similarly became353/353 before its full pass.
Captures inside repeats and overlapping tails now provide genuine frame pressure;
exact depth denials, retained ranges, sticky alias failures and original loan
checks remain. New public tests cover nullable/nonempty retries, Unicode scalars,
embeddedNUL, captures, folding/laziness, lookbehind, offsets, funded limits and
original interruption. This completes the old attempt's missing PCRE2 coverage.
SPEC documents lower working/depth requirements; public APIs remain unchanged.

Only `findall/empty-progression-strings` was refreshed and qualified with the
existing nine-pair official CPython3.13.2 protocol. Fresh ratio **3.77x ->2.569x**,
interval[2.559,2.573],112.352us/16528B, minimum49.963ms, least-contendedCPU0x20.
That independent catalog movement differs from the CPU2 causal effect above.
All other cases, lifecycle/scaling and historical identities remain unchanged;
generated Markdown and reused snapshot guard1/runtime pass.

The whole-snapshot current-product verifier was also called after this selective
refresh. It correctly rejects the first preserved older row, `alternation/search-miss`,
as a changed managed product. This is not a successful current-source full
qualification; the selected row's qualifier passed its source/PDB/binary checks.
No old evidence was relabeled or retimed to silence the global gate. Complete
current-source/package/platform qualification stays at a coherent boundary.
Dev.19 packages and frozen43m08s Full evidence remain immutable.

Immutable evidence under `artifacts/experiments/literal-repeat-policy-20261104/`:

- `evidence.tar.gz`: `7AE44847A4FA0FF22295A27BE99277FC5F8B29FBF403A45626DD2657A4E6E30F`.
- `correctness-source.zip`: `8CE68AD917FA19BD6A1782A8618034BEB0C9E116725207CA6986A2A367C8F8E7`.
- `selective.tar.gz`: `C00FE44C27E409FB057ABA620D323D078E1F67531DA921BBFC9E7520E42161B2`.