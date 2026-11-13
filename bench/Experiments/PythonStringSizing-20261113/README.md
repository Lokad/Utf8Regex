# Python string sizing and anchored search

Accepted producer `c741ef7e`, compared with `67bd1fd8` on .NET 11 GA. PythonRe's
valid-text contract is identical on both sides. Captured-string shaping now
counts only the selected range unless existing coordinate facts suffice. LTR
backtracking search reuses Core's beginning-anchor finder, avoiding futile scalar
attempts after an initial `\A`/`^` anchor. There is no new mode, cached hint or
executor. RTL/NonBacktracking and candidate verification retain their paths.

One bounded comparison used the same caller DLL/PDB, explicitly staged producer
pairs and nine samples in separate ordinary processes. Each sample lasted at
least 20 ms. Retained batches were conservatively capped at 64 MiB and each
process at 120 seconds. Checksums/results were consumed after timing. No
confirmation, catalog expansion, package qualification or Full refresh was run.

| Public FindAllToStrings workload | Before | After | Allocation before/after |
| --- | ---: | ---: | ---: |
| Dense captured strings | 553.529 µs | 580.715 µs | 242008 / 242008 B |
| Anchored capture, 1 MiB Unicode subject | 242.198 µs | 0.499 µs | 136 / 136 B |

The dense case is the existing `findall/many-capture-strings` input. Its roughly
5% slowdown is accepted for removing work proportional to an unused suffix in
the anchored case. Different allowed CPUs prevent an exact causal percentage:
the unchanged predecoded .NET control moved +1.2% for sparse output and −0.7%
for dense output. It materialized equal capture-string results and serves only
as a drift control; this is not a Python/.NET parity claim.

Both-runtime Python suites pass, including large valid subjects under 128 work
steps and finite working/result limits, Unicode sizing, captures, empty-match
progression, aliases and original interruption. Pattern/template validation is
independent. Public XML comments no longer promise a subject-validation pass.

Raw samples, staged producer hashes, unchanged caller hashes and the disposable
caller source are in `artifacts/experiments/python-string-sizing-20261113/`.
The next delivery boundary refreshes the existing canonical catalogs and final
packages once; it does not extrapolate these focused observations to other rows.
