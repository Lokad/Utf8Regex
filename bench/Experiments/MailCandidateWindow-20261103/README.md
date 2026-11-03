# Class-delimiter Count windows

Core's existing scalar-class/whitespace candidate window now also admits a class
immediately followed by an exact ASCII delimiter. It searches that actual byte,
rewinds complete scalars with the original predicate, and verifies every start
in the conservative window with the same VM. No retained hint, engine, mode or
unchecked access was added.

Capture-free nonempty Python programs reuse the shared Count loop and validate
without deriving unused UTF-16 coordinates. Captured and nullable programs retain
their cursor and hints. Full validation, original interruption, required screening
before scratch, major-buffer admission and funded refunds remain. Skipped starts
and discarded metadata lower route-dependent work/temporary requirements.

The current 15-instruction mail program has no Core rendering or mixed-UTF8 start
hint. Its 6,839,410-byte corpus returns 92 matches. The existing class-union census
is diagnostic evidence, not instrumented production cost attribution.

The initial broad nonempty delegation ran once for **57.500s** and produced a
stable captured-backreference regression of **27.57%**. It bypassed that cursor's
ASCII interval. This implementation was rejected. Before further timing, PLAN
explicitly allowed one scope correction: preserve captured execution and isolate
capture-free counting in one private lexical owner. This was a revised dispatch,
not a noise confirmation. Initial assets/results remain immutable.

The corrected comparison used the **same frozen 6f72dbf6 baseline, 17 workloads,
driver and settings**. Core DLL/PDB bytes are identical to the initial candidate.
One **57.481s** run, minimum **56.166ms**; normal startup then CPU2, 500ms warmup,
independent 60ms calibration and five alternating batches. Every result is
consumed after timing. .NET diagnostic lanes cover overlapping semantics;
incompatible Python Unicode/reference rows have explicit exclusions.

| Public operation | Before us | After us | Allocation B/op |
| --- | ---: | ---: | ---: |
| Full mail Count | 809445 | 3780 | 3568, unchanged |
| Finite/live Count | 929919 | 5562 | 3680, unchanged |
| Finite/deadline/live Count | 1121663 | 6218 | 3728, unchanged |
| Miss runs | 321.25 | 0.862 | 80 -> 0 |
| Long word miss | 897.07 | 0.813 | 80 -> 0 |
| False delimiters | 352.98 | 252.75 | 3568, unchanged |
| Captured backreference control | 46.49 | 44.27 | 2256, unchanged |
| Literal control, largest adverse | 0.243 | 0.261 | 0, unchanged |

**Promote:** target about 214x faster, all public adverse medians <=7.47%, no
allocation increases. Dense ASCII costs +3.48%; retain it and the unchanged
literal's +7.47% rather than claiming universal equivalence. [All corrected
observations](corrected-summary.json) and [initial rejection](initial-summary.json)
remain separate. No third run or further variant was attempted. This does not
replace the fixed-a40 cumulative release gate or CPython qualification.

Release checks on .NET10/11: full PythonRe4639/runtime plus final18 ownership/
publication cases, Core361 and PCRE2724 focused checks/runtime. Coverage includes
scalar floors/canaries/tails, captures/references, nullable progression, each work
cutoff with an enclosing loan, search/rewind/verification interruption, funded
success/refusal/cancellation refunds, and token cancellation on the last deadline
read. Runtime code was unchanged after the corrected measurement. Contracts and
unreleased notes describe reduced resource costs. No public API changed.

Immutable archives under `artifacts/experiments/mail-candidate-window-20261103/`:

- `evidence.tar.gz`: exact parent, initial/corrected assets and patches, complete
  measured source, corpus, driver, preflight and both raw comparisons. SHA256
  `3E9A8162FD0580531458158C58C6993DE2DE8547FD93932A96683D810ACC7B60`.
- `correctness-source.zip`: final source/tests/contracts and Windows check logs.
  SHA256 `16A85ECFAD581CBE97E5F848199CC17610F9C111F2FEAC6D879F5B468AA336A7`.

Existing dev.14 packages remain unchanged. Selective clean-source CPython evidence
and the next coherent cumulative/package/consumer delivery gates are separate.
