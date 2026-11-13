# PCRE2 window Count omits unused coordinate facts

Accepted producer `b033f98d`, compared with `544e0169` on .NET11 GA. The ordered
window analyzer fixes Core's AsciiOrderedLiteralWindow kind; its prepared Count
branch consumes only bytes. Delete the eager suffix description, retaining the
supplementary-scalar eligibility guard, native UTF checking, options/finite
fallbacks and the same counter. No API, algorithm, allocation or ownership change.

One bounded public Count comparison used default UTF checking on both sides,
identical case-sensitive/default-newline window expressions, and scalar counts.
Native PCRE.NET used its reusable match buffer; compilation/input preparation
were excluded. Nine samples perlane exceeded20ms; each process was capped at120s.
Caller and native control assemblies stayed unchanged; staged DLL/PDB/source
checksums matched the actual producer. Counts were consumed after timing.

| Public Count workload | Before | After | Allocations before/after |
|---|---:|---:|---:|
| Mixed-Unicode miss | 441.868 µs | 42.352 µs | 0 /0 B |
| Unicode hits | 2073.676 µs | 1988.364 µs | 0 /0 B |
| ASCII miss | 31.056 µs | 31.081 µs | 0 /0 B |

The miss's unchanged native control moved−0.8%; ASCII's moved+0.9%. Keep the
clear removed-work benefit without another timing run. Different allowed CPUs
and the hit control's1588.847→2632.017us movement prevent a precise causal hit
speedup or version-parity claim. No confirmation was needed for the decision.

Focused Release Count/input/limit tests pass both runtimes. Added public tests
cover BMP gaps25/26, Kelvin/Chinese/two-byte text, a supplementary prefix before a
borrowed nonzero start, and supplementary-input fallback. Existing option,
finite-limit and native malformed-suffix rules remain.

The disposable caller initially failed to compile because regex owners were
incorrectly treated as IDisposable; only its reusable native buffer is disposed.
The caller was fixed before any timing; there was no measurement restart.
Raw samples, source, identities and logs are in
`artifacts/experiments/pcre-window-preparation-20261113/`. Immutable dev80 is
unchanged; a new package/Full matrix waits for the coherent delivery batch.
