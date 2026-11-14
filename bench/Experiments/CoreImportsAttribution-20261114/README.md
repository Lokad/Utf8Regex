# Imports Count attribution — no scanner change

One existing `--measure-case-deep lokad/imports/module-imports 20000`, **5.775s**,
current Core `f9fc88ad`, .NET11GA. Existing current-source caller reused; all producer
DLL/PDB hashes unchanged. No new implementation, public comparison or package/
catalog refresh. [Acceptance](acceptance.json).

The46429-byte Linux corpus is unchanged, SHA256
`507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2`.
Public Count returns19 at9.360us; CompiledCount9.830us also returns19. Validity-only
cost1.405us and descriptive validation0.718us are independently timed controls,
not additive phases or a subtraction from public cost. The public path uses the
owned scalar runner and already omits unrequested capture/UTF16 output. Historical
Dot/LF and negated-pair improvements remain; do not repeat those campaigns.

The fallback replay helpers are not production throughput: candidate replay
returns20 at11.654us; verified replay returns19 at32.424us. No new verifier or
scanner is justified by those slower diagnostic paths. These are single-sample
triage observations, not a changed native/.NET parity verdict.

Untimed inspection identifies the existing checked2048-byte newline/first-ASCII
pair search. Reconstructing the EXACT loader corpus and verifying its hash finds
19 newline+i candidates and19 newline+import candidates, zero false first-byte
candidates. A longer literal prefix would therefore remove none on the actual
representative corpus. Do not add a new prepared prefix/selector to chase this gap.

A separate read-only grouped-caret concern was disproved: the existing ASCII
eligibility guard routes non-ASCII subjects to the owned strict validator. Public
hit/miss probes for ordinary/Compiled grouped prefixes all reject malformed tails;
no duplicate validation pass or source fix was added.

Matching/verification remains the material cost. Without a demonstrated simple
removable cause, close this investigation and inspect profile-specific nullable
capture state next. Canonical dev92 keeps9.120us/2.099x compiled.NET. Raw verified
offVM before keeper release:
`artifacts/experiments/core-imports-attribution-20261114/evidence.tar.gz`, SHA256
`1D8D979A0D9DCEFA1FAED7E196FCF2181B12C54AEDD3E0A2975673163CE0A659`.
All workers/collectors/keepers terminal; no build/preflight failure or timing repeat.
