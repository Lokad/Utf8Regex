# Retire discarded substring-search census

Source `cafbc97b0bab2d8ca546ffc5593de9df7a1e4d26`, frozen Core `5d15e2a0`.
Remove191 net shipped lines and239 net benchmark lines: discarded candidate/
verification counters, duplicate metric methods, folded-tier overrides and their
compare-sweep replay. Production Count shares the existing folded IndexOf and
keeps its original exact-search dispatch. Preserve the vector/anchor/Horspool/
short-literal algorithms, checked access, Unicode/Kelvin/culture guards, public
captures/RTL/NonBacktracking and all invocation work/storage/interruption checks.
Diagnostic counters were separate from resource metering.

One internal boundary changes intentionally: prepared Count requires a nonempty
needle and fails fast for an empty one, instead of entering the old metrics loop
without progress. Public empty-regex matching keeps its scalar path. Construction
state, admission, covered capacities and result/cursor ownership stay unchanged.

Public-route Release preflight confirms the old counter-writing path. The vector
method shrinks1191→1065 bytes after removing discarded byref counters. New Count
is285 bytes; the old tier and metrics Count bodies were273 and337 bytes. These
codegen observations are not isolated-kernel speedups or additive execution costs.

ONE **63.233s** ordinary-process ABBA, eight rows/five150ms-target samples per
process,50ms calibration/20ms floor under150s. .NET11.0.0 default tiers/PGO; each
process starts with8CPUs, then pins itself to processor2 after startup. Distinct
frozen producer DLL/PDBs, complete compiler checksums, identical caller/.NET and
equal input/options/full Count outputs verify. Validate is timed unless explicitly
trusted; finite rows use the same limits and live original tokens.
[Observations](cost-summary.json), [acceptance](acceptance.json).

| Complete public Count | Before us | After us | Observed change |
|---|---:|---:|---:|
| Folded English corpus |149.315|144.205|-3.42%, separated|
| Trusted English |118.175|121.627|+2.92%, overlapping|
| English finite/live |1036.844|1014.058|-2.20%, separated|
| False-head folded text |17.231|16.852|-2.20%, overlapping|
| Dense folded matches |249.548|251.833|+0.92%, overlapping|
| Unicode/Kelvin |99.180|98.575|-0.61%, overlapping|
| Short folded |27.887|28.426|+1.93%, overlapping|
| Unchanged predecoded.NET |75.359|75.954|+0.79%, overlapping|

All allocation medians unchanged: ordinary0B, finite112B. Accept substantially less
machinery and separated strict/finite corpus improvements; retain trusted/dense/
short costs, control movement and process ranges. The short row's inherited
`unchanged-short-folded` ID is imprecise: its Count orchestration changes, so it is
an affected sensitivity case. .NET is the unchanged timing control. No normalized
or exact causal gain, confirmation, canonical parity/table refresh or package/
Full qualification. Existing staged/published assets remain immutable.

Focused Release checks pass Windows.NET10/11 and Linux.NET11 across all profiles:
Core13641/Python520/PCRE2874/POSIX160 per runtime. Three additional preparation/
byte-boundary/coordinate cases pass per runtime using matching binaries: two did
not match the main filter, one budget case repeats. Live borrowed-offset/tail,
punctuation alias, all-byte/length, nonoverlap, resource, original-token/deadline,
UTF8 policy and retained-result regressions remain. Tests now use production
Count/IndexOf; remove the packed-tier existence assertion and replay-only loops.
No new test or forwarding executor preserves deleted machinery.

Retire undocumented private commands `--inspect-ignore-case-literal-case` and
`--measure-ignore-case-compare-sweep-case`; no refresh/script/docs consumer uses
them. `--measure-literal-search-case` retains production prepared search and passes
a smoke run. Its one-iteration diagnostic timings are not qualification evidence.
Only affected Core/PCRE2/Core-tests/benchmark projects build; other hosts reuse
matching producer pairs. No new framework, switch or copied search implementation.

Baseline hash `90DED40BC1564BDE4FDE21741917CEB842DEFE326665FBAB6BFB2A2EA15FF141`.
A missing local import after baseline transfer was corrected without rebuilding
or rerunning preflight; the existing archive's remote/local hashes match.
Raw verified offVM before keeper release:
`artifacts/experiments/substring-census-retirement-20261115/evidence.tar.gz`, SHA256
`F7AE04ABD2A2588DB0792F118E445D3231863C056ED2CFE4C52B6478C8B0C977`.
Baseline and Windows evidence (including supplement logs) are alongside; hashes
are in acceptance.
All workers, collectors and keepers are terminal. This trial is closed.
