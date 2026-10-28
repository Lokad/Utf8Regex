# Necessary quoted prefixes and whole-relation repetition

The admitted quoted-relation grammar already proves that every match contains
a quoted prefixed operand. Expose quote+prefix alternatives through the existing
prepared required-family searcher, and let owned scalar rejection consume exact
families as well as single literals. The immutable buffers are admitted before
allocation. Presence still runs the full VM. Strict UTF-8 validation, captures,
line context, scalar predicates and original interruption/resource authority
remain. No new matcher or execution mode was added.

New tests also exposed a pre-existing native defect: the planner discarded an
outer greedy `+`, so two adjacent complete relations were counted separately.
The value plan now retains that grammar fact; native discovery extends adjacent
complete relations under its original clock. Lazy outer repeats and minima above
one retain scalar selection. The old broad start-anchor feature includes internal
multiline `^`; the known unanchored quoted-relation proof can use its necessary
filter. Native ASCII discovery keeps its own quote search and skips the redundant
global filter. A fully validated Count call site previously ignored that skip
policy; its guard is now consistent with the other routes.

Same quiet VM, .NET 11 RC3, CPU2, frozen a40bda03 baseline. Four independent public
Count lanes, 250 ms warmup, alternating samples and approximately 50 ms independent
calibration (whole slow baseline calls retained). Equal-result preflight and timed
checksums pass. All allocations below are measured public-operation costs.

| Evidence | Candidate | Elapsed | Decision |
| --- | --- | ---: | --- |
| [Initial comparison](initial-comparison.json), [console](initial-comparison.txt), [driver](initial-driver.cs.txt) | a7369faf | 75.93 s, cap 120 s | Mixed corpus improves, but ASCII hit +52.5% and prefix-present miss 0.423 -> 11.175 us. Rejected as a complete candidate. |
| [Focused correction](intermediate-comparison.json), [console](intermediate-comparison.txt) | 699fb26d | 6.30 s, cap 60 s | Avoid needless repeat-extension attempts and skip redundant native filtering. The final Count call site still ignores that policy: hit +11.5%, present miss 11.110 us. Not accepted. |
| [Final policy repair](final-comparison.json), [console](final-comparison.txt), [driver](final-driver.cs.txt), [project](driver.csproj.txt) | a557c748 | 6.27 s, cap 60 s | Correct the missing guard, with a production-route regression test. ASCII hit +5.4%, present miss -8.3%, control flat. Accepted. |

The two short follow-ups fix demonstrated regressions; they are not noise retries.
The expensive full baseline was not repeated. No further tuning or benchmark
campaign was run. Both failed candidates and their exact binaries remain evidence.

| Public Count | Frozen baseline | Candidate | Allocation B/op before / after |
| --- | ---: | ---: | ---: |
| Full 32,514,627-byte mixed corpus, ordinary | 5041.316 ms | 146.752 ms | 6752 / 0 |
| Full corpus, compiled | 5053.215 ms | 146.333 ms | 6752 / 0 |
| 32 KiB slice, finite limits and live token | 8210.52 us | 230.646 us | 3744 / 112 |
| Dense mixed-scalar hits, 512 matches | 1499.06 us | 1477.88 us | 3632 / 3632 |
| ASCII separated hits, final repair | 72.095 us | 76.010 us | 0 / 0 |
| Prefix present, no match, final repair | 0.415 us | 0.380 us | 0 / 0 |
| Unchanged literal control, final repair | 70.399 us | 70.381 us | 0 / 0 |

The first four rows are measured at a7369faf; the corrections change only native
ASCII discovery/policy, leaving those mixed-scalar/controlled paths unchanged.
The final ASCII rows are measured at a557c748. Their identities are preserved,
not relabeled as one binary. Correctness-only adjacent-hit evidence in every JSON
records **old 2 / new 1 / expected 1**; unequal-result throughput is excluded.

The separate [five-sample README refresh](refresh.txt), [verification](verify.txt)
is measured at 699fb26d: full corpus **7.57x compiled** against predecoded .NET
(was 258.81x), **1.65x ordinary** (was 57.18x), both qualified Gap. Its 144.4/144.0 ms
medians remain distinct from the paired comparison. The later missing-guard fix
affects the native ASCII call site; the full mixed-input snapshot keeps its actual
699fb26d identity. No refresh solely to update provenance.

Focused Release checks pass on final [Core .NET 10](core-published-net10.txt) and
[Core .NET 11](core-published-net11.txt). They cover both operand orders, whole
repeats/ranges, captures, scalar-only Unicode tails, span offsets, malformed input,
construction and execution admission, original tokens/deadlines and retained
results. Affected [PythonRe](python-net11.txt) and [PCRE2](pcre-net11.txt) checks
also pass. Preserve the [initial regression failures](core-initial-net11.txt).

Immutable measured archives under artifacts/experiments/quoted-prefix-20261028/:

| Archive | SHA256 |
| --- | --- |
| [a7369faf](../../../artifacts/experiments/quoted-prefix-20261028/parity-quoted-initial-a7369faf.tar.gz) | `CCD103E2C664258CDA37A00EBBB27C7EAA2DBB6D3DFF1DDBC538253BBFBCEB3A` |
| [699fb26d, including generated snapshots](../../../artifacts/experiments/quoted-prefix-20261028/parity-quoted-699fb26d.tar.gz) | `BDE3A1A7EEA6C163B15EFA3CE0A7A8B2D24AB69ADA313C1399E2924202F3B977` |
| [a557c748](../../../artifacts/experiments/quoted-prefix-20261028/parity-quoted-published-a557c748.tar.gz) | `1D0F25E65A4168A555FE36E3285C1110CE080AC722CA85F638C0C225746BBC03` |

No complete catalog or package matrix was repeated. Remaining cost includes
the generic family search and normal VM work when a necessary prefix is present;
do not reopen this batch while larger qualified gaps remain.
