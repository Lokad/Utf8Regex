# Reusing the loaded literal successor — rejected

Restore production exactly to `2ead29e0`. The ten-line RunSegment addition carried
an ASCII literal run's already-loaded successor into the same instruction switch,
after its original step. No new opcode, field, helper, owner, mode, allocation or
accounting policy. Scalar boundaries, captures/assertions, rollback, RTL/NB,
validation policy, original token/deadline/hooks and every original charge remain.
This differs from the closed readonly-binding and capture-start/repeat trials.

The actual public Release gate confirms that the successor skips the generic
array fetch. It also exposes a trade-off: native code 10784→11287 bytes, stack
888→920 bytes, and entry clearing 816→864 bytes. Gate results are not a public gain.
Both Core PE/PDB identities and 342 compiled documents per producer verify,
including six generated observations and exactly one changed production file.
Windows/Linux focused Release passes per .NET10/.NET11: Core2043, Python1225,
PCRE2 180 and POSIX145. Existing literal tests cover every work cutoff, EOF,
effects, Unicode/folded successors, assertions, atomic scopes and interruption.

One **132.031-second** public ordinary-process ABBA comparison uses the existing
fourteen-row caller without rebuilding it. Both validation policies, 560 samples,
minimum60.482ms, maximum67.248MiB retained batch; normal eight-CPU startup then
CPU2, .NET11.0.0/default tiering/PGO, 75ms target/20ms floor. Complete values and
checksums are consumed after timing. Distinct staged/loaded Core pairs, common
Python/caller/.NET hashes and frozen source verify throughout.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| Many-capture strings |−0.65%|−0.03%|
| One-capture strings |−7.60%|+4.20%|
| Many-capture UTF8 values |+0.86%|+0.87%|
| Many strings, finite/live |+2.71%|+1.04%|
| Many strings, funded/deadline |+0.47%|+0.26%|
| Durable detailed cursor |+0.67%|+1.63%|
| Core captured Count |+0.54%|+0.82%|

All 28 observed ranges overlap. Many strings575.916→572.178µs and605.922→605.765µs
have no useful intended benefit; the one-capture directions disagree. No
confirmation or tuned variant: the additional branch, code and stack are not
justified. This is not a universal slowdown veto or proof that duplicate reads
are free. Preserve RTL+2.86/+1.07%, ordinal+2.29/−.96%, folded−.08/−.51%, Python
folded+.35/−1.51%, split−2.32/−6.84%, native control−2.68/+1.57% and .NET+.00/−.15%,
without normalization or causal credit. [All observations](cost-summary.json).

Every allocation is unchanged. Shared modeled peak357073B, held45425B, original
parent17B and final loans0 remain; modeled admission is not heap/RSS accounting.
No catalog/native ratio, Full refresh, package qualification or immutable-byte
change. All452 staged assets remain untouched.

Restoration is complete: both tracked trees have no implementation change, all
four affected test projects are rebuilt for both targets, and Windows/Linux
Core387/Python58 focused literal/resource/interruption checks pass per target.
The frozen experimental pairs remain unchanged after restoration. Source and
metadata revision differences are retained rather than demanding DLL identity
between builds with different informational versions.

Canonical hash-verified offVM artifacts/experiments/literal-successor-dispatch-20261112:

- Gate: `29FE82BFF42D75F4CD7064836A80508838D69AA3AB45965857EC05860146E198`.
- Public: `42642C9A41D7053B5CE9A0BD3EB8DD86876AE47623757E0C5909E2CBC706F57C`.
- Windows: `4E83E3BF1A371BC36B4CCA182CF359ACAC016A8AEA9A806F36D616384AE3B8C2`.
- Initial preflight: `DA189F91DE94C6C9FABF2076A4585D75B276665E39525B38F7D685954D8BC055`.
- Restored Linux: `0072C77054029E5F3B0C053DEEC711BAD9B8E2A6A5DD5EAD0F4D2D0672DE780F`.
- Restored Windows: `E35393F0014227A95DF943529E73987000330DFD21B0C987838FB63B50D1FD51`.

Initial public preflight checked relative manifest paths from the wrong directory
and failed before any public worker or timing. Correcting path prefixes alone
allowed the single intended comparison. Preserve that failed archive; source,
producer/caller bytes and protocol stayed frozen. All workers are terminal and
keepers released after canonical offVM verification. Close this dispatch design;
the broader warm-output, strict-validation and general POSIX-history gaps remain.
