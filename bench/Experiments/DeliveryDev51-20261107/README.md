# Common dev.51 delivery checkpoint

Common **0.5.0-dev.51** freezes the folded-prefix trie and short class/delimiter
changes at `6032b9286c6bdfe8dcf97a5d7e26ccb2f0ae1743`. All three packages support
.NET 10/11 with exact Core pins. They are qualified and unpublished; older staged
and published bytes remain unchanged.

The changes reuse existing bounded search proofs without new fields, allocation,
execution modes or engines. Strict whole-input UTF-8 validation, scalar matching,
captures, retained aliases and synchronous resource/interruption controls remain.
[Trie evidence](../PythonFoldedTrie-20261107/README.md) and
[short-window evidence](../PythonShortDelimiter-20261107/README.md) retain their
focused correctness checks and adverse observations.

## Final package costs

ONE exact-package dev.50-to-dev.51 Default-process ABBA completed in **113.343s**:
22 public workloads, 440 samples, minimum 54.0161 ms, unchanged allocation medians
throughout. The fixed caller loads one producer per ordinary process. Existing
750 ms/minimum 32-call warmup, independent 75 ms calibration, five samples and
120-second outer/35-second producer caps remain. Results are consumed after timing.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Folded Search miss | 0.386 | 0.209 | -45.85% | 0 |
| Same finite/live | 0.559 | 0.321 | -42.52% | 112 |
| Same deadline/live | 1.047 | 0.428 | -59.12% | 160 |
| Dense folded-prefix miss | 9.335 | 5.849 | -37.34% | 0 |
| Detailed Search | 1.543 | 0.959 | -37.84% | 496 |
| Same finite/live | 2.205 | 1.306 | -40.76% | 608 |
| Same deadline/live | 3.039 | 1.988 | -34.59% | 656 |
| Value Search | 1.275 | 0.605 | -52.54% | 272 |
| 16-byte detailed Search | 1.978 | 0.950 | -51.95% | 488 |

These gains persist in both processes with disjoint sample ranges. Exact effect
sizes vary: detailed after-process medians are 1.047/0.855 us; the 16-byte case is
0.852/1.051 us. No confirmation or tuning followed.

Retain adverse observations with overlapping ranges: mail +1.28%/+27.62 us,
finite mail +1.52%/+49.43 us, PCRE literal +3.18%/+9.73 ns, full Unicode finite
hit +0.29%/+4.90 ns and explicit whitespace +0.96%/+46.49 ns. Core literal -2.60%
and anchored FullMatch -1.99% have disjoint ranges but bypass the edited algorithms;
give them no causal credit. BCL control -0.24% overlaps. No normalization or
universal speedup claim. Earlier source boundary costs remain separate evidence.

Finite folded extra cost falls 0.1731 to 0.1123 us while relative overhead rises
44.86% to 53.77%. Detailed finite extra falls 0.6613 to 0.3467 us, or 42.85% to
36.13%. [All samples](package-cost-observations.json),
[summary](package-cost-summary.json) and [acceptance](package-cost-acceptance.json)
preserve the complete scope.

Before any timing, the actual Unicode miss was added to the unexecuted 21-row
plan because fresh raw and paired catalog medians differed. Original input/source/
declaration are preserved. Its package A/B 0.3046 to 0.2929 us overlaps and does not
reproduce a version regression. Keep raw catalog 0.4543 us, paired 0.3461 us and
the raw 9.03x predecoded-.NET ratio distinct; do not compose a replacement ratio.

## Platform and refresh acceptance

ONE Full passed all 83 steps in **2783.8168845 seconds (46m23s)** with the original
8 GiB RAM/1 GiB repository/8 GiB tmpfs/3600-second guards. It covers one Release
build, both-runtime correctness, disabled dynamic code, complete .NET 11 catalogs,
paired comparators, lifecycle/scaling, resources and final verifiers. Seven files
were hash-imported once; README retains four generated blocks, six descending
tables and 178 ratios. Matching binaries pass six hosts/10 snapshot checks.

Core records 121 wins/1 tie candidate/56 gaps and no unqualified rows. PythonRe
records 45 managed faster/31 CPython faster/2 equivalent/9 inconclusive/3 floor
exclusions. PCRE2 records 72 managed faster/12 native faster/2 equivalent/
15 inconclusive/25 excluded. Retain uncertainty and exclusion reasons; successive
catalog movement does not establish causality. [Refresh](full-refresh.json),
[ratios](ratio-summary.json) and [movements](catalog-movements.json) retain details.

Windows qualification took 279.4017964 seconds: two builds and one pack per profile.
Windows and exact-Windows-byte Linux consumption each pass eight detached fixtures/
110,194 checks, original Awk 19/Lython 118 per target and public probes with final
root loans zero. All 464 compiler documents, 12 Windows/24 Linux restore audits
and 12 Linux TRX identities are retained. No producer rebuild/repack on Linux.
Compatibility remains distinct from downstream adoption and POSIX acceptance.

All 270 older staged files remain unchanged; six new assets bring
`artifacts/nuget/` to 276. Both frozen Full50/Full51 eight-asset graphs remain
unchanged after Linux and costs. [Windows](windows-acceptance.json),
[Linux](linux-archive-acceptance.json), [inputs](boundary-inputs.json) and
[preservation](frozen-graphs.json) record the boundary. Source CI is green on
Windows/Linux. [Snapshot CI](snapshot-ci.json) also passed on both platforms for
`27a5922b5c032cae1b9e143020f92dd1de763bde`.

Canonical raw evidence under `artifacts/experiments/delivery-dev51-20261107/` is
verified off VM and immutable. Hashes live in the archive acceptance JSON files
and [Windows archive record](windows-archive-hashes.json). No NuGet publication.
The [CJK trace](finite-cjk-source-trace.json) rules out two already-implemented
optimizations. The [coordinate-count follow-up](coordinate-count-followup.json)
is unmeasured and outside this candidate.
