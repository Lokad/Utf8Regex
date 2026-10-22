# PCRE2 branch-reset analysis and dead metadata cleanup

2026-10-22. Original checkout `f949629b`, matching Release producer `d011e9b6`.
Corrected source `844dcbad`, development `0.4.0-rc.2-dev`. No package created or
published. Frozen unpublished RC1 remains unchanged and unsuitable for release.

## Delivered

- Fixed public `Analyze().UsesBranchReset`: the old capture/name-length test
  always returned false. The parser now records the grammar fact and carries
  one boolean through the existing compilation outcome to the root program.
  Analysis is independent of capture count and the selected Match backend.
- Removed PCRE2 GroupNames generation, numeric-name conversion, root clone,
  dummy literal/character tables and lowerer/backtracking-program plumbing.
  Actual capture-slot counts and NameEntries remain. Two tests now assert
  public capture/name outcomes instead of preserving the dead tables.
- Removed only the unreferenced six-argument PythonReScalarMatchState constructor.
  Both production discovery owners retain the candidate-aware constructor.
- Clarified IsFullyNative: owned managed execution without matcher delegation;
  no public property rename/value change or machine-code/fast-path promise.

No matching algorithm, SIMD, candidate hint, construction buffer/map, resource
meter or ownership mechanism changed. Coarse construction admission is retained
unchanged despite fewer acquisitions; no finer accounting or revised budget
formula. Strict UTF-8/scalars, captures, named references, subroutines, result
metadata copies, original-token cancellation, deadlines and resource controls
retain their contracts. No total CLR/JIT/RSS bound is claimed.

## Focused validation

- New public regression coverage checks reset groups with different widths,
  names, no captures, empty bodies, nesting and assertions. Negative controls
  cover ordinary captures, escapes, quoted literals, classes and comments.
  Each case uses string and UTF-8 pattern construction.
- Focused Release PCRE2 tests: 81 passed. Full PCRE2: 5,987 passed, no skips,
  9 seconds; full PythonRe: 3,910 passed, no skips, 11 seconds. Builds targeted
  affected projects; the full PCRE2 run reused matching binaries.
- Existing producer/PE/PDB/compiled-source preflight accepts final source.
  Generated README parity, PythonRe and PCRE2 pages are current; no snapshots
  rewritten. Four snapshots and nine frozen RC1 files retain hashes and sizes.
- Source CI and final closeout are recorded in validation.json. No local full
  solution/package matrix was repeated; CI owns the Windows/Linux source boundary.

## One bounded cost comparison

Same driver bytes and frozen Core DLL, only PCRE2 changed. Linux/.NET 10.0.12,
CPU4, default tiering, 750 ms warmup per lane, bounded calibration, five samples
with alternating lane order, 60-second cap per process. All result checksums
agree; the old/new diagnostic flag differs intentionally. No confirmation run.

| Lane | Median change | Allocated bytes/op before -> after |
| --- | ---: | ---: |
| Literal construction | -7.4% | 1,976 -> 1,912 |
| Named reset construction | +1.6% | 92,640 -> 92,544 |
| Sixteen-capture construction | +1.9% | 209,968 -> 209,632 |
| Public Analyze, named reset | +8.5% | 328 -> 328 |
| Public Count, named reset | +5.9% | 0 -> 0 |
| Unchanged Core Count control | +3.2% | 0 -> 0 |
| Predecoded Regex Count control | +1.3% | 0 -> 0 |
| Decode-then-Regex Count control | -4.3% | 4,632 -> 4,632 |

Public medians are within the 10% allowance. Raw spreads are wide (including
unchanged controls), so these observations do not establish precise speedups
or a statistical upper bound. Construction/diagnostic costs are separate from
matching throughput. The VM root disk remains full and background services busy;
the experiment used a new RAM directory and changed no old files/services.

Driver/raw samples, hashes and summary are adjacent. This closes the scoped
comparison; it is not package/consumer/host operating-cost qualification.

## Remaining delivery boundary

Fresh immutable all-three packages with a common new identity/exact Core
dependencies, final package/API/provenance and isolated-consumer checks remain
separate work. Actual host/package costs including finite limits remain part of
that boundary. Sixteenth window/layout prototypes and prior PythonRe pruning
stay deferred; no completed checked-access campaign was reopened.
