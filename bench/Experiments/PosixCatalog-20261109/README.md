# POSIX-specific catalog

Measured source `eea507db`, .NET 11 RC3 on the quiet VM. One selected catalog
run completed in **41 seconds** under a declared 180-second cap: 12 cases,
25 timed lanes, nine samples per lane. No Core/PythonRe/PCRE2 refresh or package
qualification. Shipped execution was unchanged.

Removed generic boolean hit/miss, dense Count and scalar-empty cursor rows.
The current catalog measures longest selection, capture participation,
nested/nullable histories and longest iteration. Unicode/nonzero floors share
a selection case. SKU is a separate historical consumer control, with per-call
presentation. Only three representative cases time every policy lane. All cases
still preflight finite/funded/deadline results and refunds; repeated records and
all four nested captures are observed outside timing.

The `(a|aa)*` capture series costs 2.14/14.88/117.43 µs for 4/8/12 input bytes;
funded working peaks are 960/1952/1952 modeled bytes. This demonstrates substantial
ambiguity growth, not native parity or an optimization speedup. Native captures
differ. Ordinary qualified gaps remain largest for empty captures (9.72×),
nullable intervals (5.86×) and the separate SKU control (7.96×).

Initial post-run verification rejected JSON indentation inside the summary's
result-contract string. Rebuilt only the presentation from the unchanged raw
snapshot using the exact measured reporter; its full verifier then passed.
Raw SHA-256 stayed `EDC138CCA18B0EE957E421A172AB8C891B7ACAB9D61881710221684E4E9CC777`.
The follow-up writer canonicalizes that embedded JSON; an isolated round-trip
fixture passes with the corrected writer. Its rebound identities are correctness
test setup, never timing evidence. No timing was repeated.

Release semantic/resource checks and offline snapshot checks pass on both targets.
Raw observations, original snapshot, logs, postprocessor and measured binaries:
`artifacts/experiments/posix-catalog-20261109/vm-evidence.tar.gz`, SHA-256
`E450290B761BC654E362C85B066ED7A78837523B205E236E2984593AC520651C`.
Previous accepted packages and Full-refresh evidence remain unchanged.
