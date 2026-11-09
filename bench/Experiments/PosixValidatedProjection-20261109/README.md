# Reuse whole-subject validation for POSIX coordinates

POSIX entry points already validate the complete subject, and the scalar
interpreter returns complete scalar ranges. Project those ranges through Core's
existing bounded UTF-16 counting helper instead of validating each prefix and
capture again. Translate its internal work breach to the public resource failure.
Whole-input validation, capture ranking, cancellation/deadlines, admission and
output clearing remain. Numerical work requirements decrease; no new execution
mode, coordinate map, result allocation or Core algorithm.

One quiet-VM .NET 11 RC3 separate-process ABBA completed in **52.145 seconds**
under a declared 180-second cap. Fixed before is the producer from `eea507db`;
after changes only `Utf8PosixRegex.ProjectRange`. The same worker and Core DLL
run throughout, with normal runtime startup before affinity, 500 ms row warmup,
independent 75 ms calibration and five samples per process (20 ms floor).
Complete returned coordinates/captures and loans are checked outside timing.

| Public operation | Before µs | After µs | Change |
|---|---:|---:|---:|
| SKU × 4096, finite/live | 7807.28 | 7082.78 | -9.3% |
| SKU × 4096, funded finite/live | 8413.38 | 7681.75 | -8.7% |
| SKU × 4096, finite/deadline/live | 9475.84 | 8213.35 | -13.3% |
| Empty captures, funded finite/live | 2.37 | 2.21 | -7.1% |
| Unicode captures after 64 KiB prefix | 7.27 | 5.75 | -20.8% |

These gains appear in both after processes with disjoint sample ranges and no
allocation or modeled working-peak increase. Small unlimited changes remain
uncertain: empty captures -1.0%, SKU -2.9%, nested -1.6%, nullable -2.2%.
Unchanged POSIX Count moved -4.1%; unchanged Core Count moved +0.6%. Keep these
observations rather than normalize them away or claim the small deltas as causal.
No confirmation was needed to retain the simpler path. This is a before/after
producer comparison, not a refreshed native parity ratio.

[summary.json](summary.json) retains process medians, sample ranges, allocations
and working peaks. Release POSIX checks pass on Windows/Linux, both target
frameworks, including new scalar/block-boundary coordinates and deterministic
work refusal during projection, plus original interruption, publication clearing
and aliases. No package qualification or broad benchmark refresh.

Raw worker, source patch, fixed binaries and samples:
`artifacts/experiments/posix-validated-projection-20261109/vm-evidence.tar.gz`,
SHA-256 `FCD6DC275F9B16C22580EE96A9DE8CD1CC7DE60AE99A78CAD202FF9C17D0A343`.
The later Linux check log is alongside that archive as `tests-linux.txt`.
