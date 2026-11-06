# Two-scalar Unicode class search

The qualified dev.39 PythonRe `split/unicode` row costs 116.18us, 2.63x CPython.
Its `[,，]` pattern previously had Core `FallbackRegex`/no search hint; the scalar
VM tested starts throughout the subject. An equivalent literal alternation
already used the complete native pair search.

The compiler now represents a positive, unfolded class containing exactly two
scalars, including a non-ASCII member, with existing UTF-8 literal-pair facts.
Distinct scalar encodings are prefix-free, so these alternatives describe the
same single-scalar language. Existing search, confirmation and capture paths
consume the facts; no new runtime, state, cache, mode or framework is added.
The pair and encoded arrays are admitted before allocation. Larger/negative/
predicate/subtracted/folded classes and existing ASCII lowering keep their paths.
Numerical work/scratch requirements can fall; limits and interruption remain.

One **28.846-second** separate Default-process ABBA compares frozen dev.39 Core
`4272946B...` against private changed Core `3946AF42...`, both with the identical
PythonRe `094B8B39...` DLL. Normal eight-CPU startup then CPU2 affinity, default
tiering/PGO, 100 samples (minimum 43.745ms), equal exact arrays and post-timing
digest consumption. Oracles use BCL literal character splitting only on the
overlapping BMP cases. This is a producer comparison, not a refreshed CPython
ratio or new catalog Status.

| Public SplitToStrings workload | Before | After | Change | Allocations, both |
| --- | ---: | ---: | ---: | ---: |
| Equivalent-alternation control | 55.229us | 55.830us | +1.09% | 19512B |
| Actual Unicode class | 97.982us | 55.723us | -43.13% | 19512B |
| Actual class, finite limits | 115.200us | 78.400us | -31.94% | 23712B |
| Dense second wide scalar | 856.523us | 988.188us | +15.37% | 323760B |
| Unicode false prefix | 184.143us | 33.722us | -81.69% | 6200B |

[Raw process summaries](cost-summary.json) support the three gains and the dense
cost in both after processes. The latter splits 8192 copies of `ê` with `[é-ê]`,
producing 8193 values; it costs an additional 131.66us. Accept this local cost
for the substantial public gains under the user's delivery trade-off policy.
The completed checked-access 10% allowance is not a universal release gate.
Finite policy adds 22.68us to the changed operation versus 17.22us before, even
though its total cost improves. Preserve that overhead and the control movement.
No normalization, tuning, confirmation or additional benchmark campaign.

Focused Release tests pass on Windows/Linux and both targets: Core native/class/
work/cutover, Python split/Unicode sizing/native/resource, PCRE scalar/class/
resource fixtures and the final pair fixture. Scalar ranges, supplementary
members, RTL/NonBacktracking, retained captures, malformed tails, finite admission
and original cancellation are covered. Windows net11 managed inventory also
passes. [Validation](validation-summary.json) retains counts and the harmless
post-test report failure: Linux lacked `rg` after successful test commands;
terminal zero and original logs were verified without replaying tests.

Immutable `artifacts/experiments/python-unicode-split-20261106/` evidence:

- `raw-evidence.tar.gz`: `15DB2037759ECB2D930B337CD7888044A1A5B88579AD8EFB48EA0CC0874E26D9` (1678394 bytes, verified off VM). Includes both graphs, source/patch, declarations, samples, environment and Linux checks.
- `windows-evidence.zip`: `BD7B20BEE6086B5C8AC56627619E26EE918CD1E5AA212ACF255C6499C14209F2`.

Source parent is `1289f4c9bb0d31e4b153747f6ca9b2be7f7ed102`; full hashes and the
exact patch are retained. No Full/package qualification or NuGet publication.
Existing dev.39 packages and catalog remain immutable and do not contain this edit.
