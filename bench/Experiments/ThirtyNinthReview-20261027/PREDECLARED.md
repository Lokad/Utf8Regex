# Bounded FullMatch ownership comparison

Before timing: original source 45af53e5, candidate ff6a5f6c (implementation
e5d58c1b). Frozen matching Release Core/PythonRe DLLs and PDBs live in the local
artifacts/thirty-ninth-review-20261027 before/after directories. No package is
created or changed. Source version remains 0.4.0-rc.3-dev.

Reuse the existing ThirtySixthReview isolated-load-context host and processor
selection, with a new small public lane catalog. Eleven lanes: unlimited and
all-four-finite-plus-live-token FullMatch alternation, Unicode lazy detailed
captures, long terminal word repetition and lazy text output; preserved native
literal FullMatch, Core compiled literal Count and default Core VM Match controls.
Expected ranges, full capture/text results and checksums are checked before
timing. Record allocations separately. Constructor/Compile observations use two
warm instances then eight retained instances, without timing dynamic construction.

Quiet Linux VM, default runtime tiering, one selected logical processor. One
bounded alternating before/after comparison: 2.5-second warmup per lane/version,
40-ms calibration target and nine sample pairs, with a 100-second internal gate
and 120-second outer cap. Record every raw sample and exact binary identity.
If noise prevents a decision, allow one 17-pair reversed-first confirmation over
the same complete catalog/context, then stop. No filtered-context confirmation,
third run, code tuning after timing, snapshot refresh or package qualification.

Promote when representative affected public costs fit the 10% allowance with
unchanged controls supporting interpretation; defer demonstrated regressions
above 10%, disclose inconclusive evidence without asserting equivalence.
This local change comparison does not discharge the separate frozen RC2
cumulative performance gate or restart its closed experiment.
