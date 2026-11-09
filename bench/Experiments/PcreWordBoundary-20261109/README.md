# PCRE2 required word boundaries

Fixed baseline `7df3b5af`, .NET 11 RC3 on the quiet VM. Date match/miss use owned
PCRE2 backtracking, not Core translation. Extend existing auto-possessification
to a required positive word boundary after a positive-minimum word-only token
repeat. Every shorter endpoint lies between word scalars and cannot satisfy `\b`.
Zero minima, negated/mixed/caseless/unknown classes, `\B`, nullable suffixes and
differing alternative starts remain conservative. Reuse the existing opcode;
no executor, API or additional state mechanism.

Explicit finite-policy/timeout compilation retains its old lowering and numerical
accounting. An uncontrolled compiled program still observes original runtime
tokens, deadlines and working admission; its optimized scans remain metered.
Unused rewind buffers disappear rather than escape admission. Scalar validation,
captures and global progression are unchanged. The resource contract notes this.

One **68.064-second** ordinary-process ABBA under 180 seconds: identical Core and
worker, independent calibration, five samples/process/row after warmup; all result
and zero-loan checks outside timing. No confirmation or catalog/package work.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Date IsMatch hit, unlimited | 0.658 | 0.355 | −46.0% |
| Date IsMatch miss, unlimited | 2.582 | 1.728 | −33.1% |
| Date IsMatch hit, funded/live | 2.574 | 1.825 | −29.1% |
| Date IsMatch miss, funded/live | 7.48 | 6.66 | −10.9% |
| Word IsMatch hit, unlimited | 0.62 | 0.33 | −46.6% |
| Date Count | 1.446 | 1.129 | −22.0% |
| Word Count | 1.411 | 1.116 | −20.9% |

Funded date/word-hit allocation falls 1,592→112 B, modeled host working peak
1,504→0 B, with zero outstanding loans. Remaining fixed owner allocation is not
covered workspace. Unlimited matching stays at zero allocation. Date construction
allocation falls 71,848→70,680 B; timing −0.6% is uncertain. Explicit finite plans
retain their lowering/allocations: their observed −17.3% to +1.3% timing movement
is not attributed to this proof. Unchanged negative-conditional/Core Count controls
move −0.5%/−0.4%. Full process/sample ranges are in [summary](summary.json).
The large unlimited/funded gains are accepted; no new native ratio is inferred.

Focused then full Release PCRE2 checks pass on Windows/Linux and .NET 10/11.
New cases pin engagement, finite-plan exclusion, the zero-minimum counterexample,
mixed/negated and nonword boundaries, alternative suffixes and explicit Unicode
modes. Existing original interruption, controls, captures, global and resource
checks pass. Windows broad checks reuse the focused run's matching binaries.

The initial archive list incorrectly named an unused POSIX catalog. Rebuilt only
the archive from unchanged observations/binaries; timing was not repeated. The
original runner and correction note are retained with the raw evidence at
`artifacts/experiments/pcre-word-boundary-20261109/evidence-complete.tar.gz`, SHA-256
`00CB248F25E675B46D41A3DCE1D0545FDC4497BFBBAADA6C4ED9203E50B96167`.
It is copied off the VM and hash-verified. Historical package bytes/snapshots stay.
