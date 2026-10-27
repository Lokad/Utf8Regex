# Bounded PythonRe result-getter comparison

Before timing: review baseline 8724b586, candidate 589bb885 (library fix 070a8013).
Matching clean-source Release Core/PythonRe DLLs/PDBs are frozen under the ignored
artifacts/fortieth-review-20261027 before/after directories. No package is created.
Source version remains 0.4.0-rc.3-dev.

Reuse the existing ThirtySixthReview isolated-load-context host and processor
selection without changing their protocol. Nine public lanes: short captured
SearchDetailedData hit/miss, Unicode optional-capture FullMatchDetailedData and
64-result captured FindIterDetailed, each unlimited and all-four-finite with a
live token, plus an unchanged Core compiled literal Count control. Full results,
coordinates, names and optional captures are checked before timing. Identical
defensive caller shaping normalizes original null misses and corrected empty
defaults to the same expected checksum. Timed results are retained; record
allocations. No constructor measurement or isolated-kernel claim.

Quiet Linux VM, default tiers, one selected logical processor. One bounded
alternating before/after comparison: 2.5-second warmup per lane/version, 40-ms
calibration target and nine sample pairs; existing 100-second internal gate and
120-second outer cap. If noise prevents a decision, permit one 17-pair reversed-
first confirmation over the same complete catalog/context, then stop. No filtered
confirmation, third run, tuning, snapshot refresh or package qualification.

Accept representative public costs within the 10% allowance with supporting
unchanged-control behavior and unchanged per-call allocations. Disclose any
inconclusive result. This does not reopen or discharge frozen RC2's separate
cumulative operating-cost gate, or revisit the rejected FullMatch design.
