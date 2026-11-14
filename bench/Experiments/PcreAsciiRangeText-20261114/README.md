# PCRE2 positive ASCII range Count over mixed text — accepted

Source d6a9ccc3 →30fce913, .NET11GA on the dedicated VM. The existing prepared
byte set is only the class's ASCII projection; it cannot alone exclude Unicode.
One positive, nonnegated single range ending below128 supplies a constant-time
complete proof: member bytes are scalar-aligned ASCII, and every byte of a
different scalar is excluded. Reuse the existing class-run counter. No field,
helper, unbounded term walk, cache, selector framework or executor is added.
Other classes keep their subject ASCII guard. Explicit ranges do not expand
under UCP; the existing caseless exclusion remains.

ONE public A/B: six focused Count cases, each immutable validation policy
compared with itself, plus a fixed-image Core control. Nine250ms target samples,
1s warmup; every sample exceeds20ms. No confirmation. One producer per ordinary
process, identical Core/caller DLL/PDBs, loaded source checks and equal complete
counts consumed after timing. Same valid unchanged UTF8 and matching options.
Native UTF checking/bypass is preserved; this is not a new native parity ratio.

| Complete public Count | Before → after µs | Observed change | Allocated bytes/op |
|---|---:|---:|---:|
| mixed-dense/Validate | 311.755 → 68.539 | -78.02% | 0 → 0 |
| mixed-dense/AssumeValid | 308.203 → 65.381 | -78.79% | 0 → 0 |
| ascii-dense/Validate | 66.825 → 65.469 | -2.03% | 0 → 0 |
| ascii-dense/AssumeValid | 66.413 → 65.467 | -1.43% | 0 → 0 |
| mixed-long-runs/Validate | 244.089 → 1.811 | -99.26% | 0 → 0 |
| mixed-long-runs/AssumeValid | 253.723 → 0.839 | -99.67% | 0 → 0 |
| unchanged-unicode-class/Validate | 245.906 → 251.460 | +2.26% | 0 → 0 |
| unchanged-unicode-class/AssumeValid | 243.172 → 247.865 | +1.93% | 0 → 0 |
| unchanged-bounded/Validate | 290.716 → 293.174 | +0.85% | 0 → 0 |
| unchanged-bounded/AssumeValid | 291.555 → 292.153 | +0.21% | 0 → 0 |
| finite-mixed/Validate | 2639.018 → 67.753 | -97.43% | 4,546,672 → 112 |
| finite-mixed/AssumeValid | 2660.047 → 66.622 | -97.50% | 4,546,672 → 112 |
| unchanged-core-count | 12.072 → 12.148 | +0.63% | 0 → 0 |

ACCEPT mixed-dense -78.02/-78.79%, mixed long runs -99.26/-99.67% and finite
mixed -97.43/-97.50% (Validate/AssumeValid). Finite allocation falls4546672→112B.
ASCII dense improves2.03/1.43%. INTENTIONALLY RETAIN Unicode-class control
+2.26/+1.93%, bounded control+0.85/+0.21% and fixed Core+0.63%. No subtraction,
composition, causal assignment of unchanged-path movement or broad parity claim.
Per-process medians/ranges are observations, not formal confidence intervals.

The public low-capacity preflight independently proves both policies change from
HeapLimit to correct Count3 at work100000/depth1/working1/result1 on mixed text.
This counter needs no VM scratch, result roots or depth; no external allowance
loan is attempted. Existing shared denials, original token/deadline, candidate
and4096-byte block charges remain. Limits still admit every real allocation
before acquisition. The contract explicitly records these lower requirements.

Windows AND Linux:288 focused PCRE2 Release checks EACH per net10/net11 pass.
Coverage includes4095/4096/4097-byte runs, borrowed offsets, Unicode scalar
boundaries/floors, empty input, UCP literal ranges, Unicode/negated/property and
Kelvin caseless exclusions, bounded/lazy/anchored/flagged paths, strict malformed
tail rejection, finite work/depth/working/result rules, shared denial, original
interruption/publication and cursor/result lifetimes. The old generic ASCII-
only guard remains necessary for classes not proved by this narrow shape.

[Acceptance](acceptance.json) preserves image/source identities, full costs,
low-capacity outcomes, checks and raw hashes. Evidence under
artifacts/experiments/pcre-ascii-range-text-20261114/ is verified offVM before
keeper39313 terminal0. No package/Full/catalog qualification or canonical-table
refresh occurred; immutable dev86 remains untouched. Campaign CLOSED: no tuning
or broadening into another class-proof/cache project from these samples.
