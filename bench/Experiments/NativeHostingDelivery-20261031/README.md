# Native hosting delivery checkpoint

Frozen source `0e24cece`, common **0.5.0-dev.1**, Core/PythonRe/PCRE2,
.NET 10 and .NET 11 RC3. These packages are staged, not published.

Windows package qualification passed in **315.557s**, using the existing qualifier:
two graph builds, one pack per profile, exact adapter/Core pins, package/source/
PDB/restore identities and **106370 detached test passes** across both assets.
See [candidate manifest](candidate.json). Immutable packages and symbols are in
`artifacts/nuget/`; the original qualification is under
`artifacts/package-candidates/0.5.0-dev.1/`.

The actual copied Awk adapter passes 19 checks per runtime. The actual Lython
runtime/provider fixtures pass 118 per runtime, including five cancellation
worker stages. Consumer checkouts were unchanged. The first Lython attempt used
its staged 0.11 host package, which lacks the current adapter ABI. That failed
build is preserved; the pass uses matching committed runtime/provider source
`3bee54c34d0da29c3b95b22368138847221ebdc7` instead. This is compatibility evidence,
not adoption of new funded APIs or general AWK/POSIX acceptance.

The separate [public package probe](public-probe.cs.txt) uses no source/friend
references. It exercises native patterns/templates/metadata, funded destinations,
retained aliases, root refusal, original-token cancellation and final refunds.
Both assets pass with zero final loans. Every loaded producer DLL in these
fixtures matches the frozen package asset hash. See [Windows consumer results](windows-consumers.json)
and [net10](public-probe-windows-net10.json)/[net11](public-probe-windows-net11.json)
probe output. No throughput or complete-heap-accounting claim follows from this.

Raw Windows source/fixture/log/package-manifest evidence:

- [Consumer archive](../../../artifacts/experiments/native-hosting-delivery-20261031/windows-consumers.zip),
  SHA256 `927D0DC430653814574382231D1670B9DBDB80B3956D2F1540751E9C2E30A36F`.
- [Lython TRX companion](../../../artifacts/experiments/native-hosting-delivery-20261031/windows-lython-results.zip),
  SHA256 `DE5C7E61079135EEE611DE9AB975482F7EDF52E77A6C0E4C2FF7B3AADECC6318`.

Linux consumes the exact Windows-produced packages without repacking: all
106370 detached checks pass on both runtime assets. The same copied Awk/Lython
scenarios and public probe pass there too. See [Linux package results](linux-packages.json),
[consumer results](linux-consumers.json) and [identity audit](linux-identity-audit.json).
The audit verifies six package/symbol/PDB/SourceLink assets and 24 feed/cache/lock
identities through the existing readers/rules. Compiled-source evidence remains
the original Windows qualification. A direct Linux byte comparison was rejected
because that checkout has LF rather than the CRLF compilation input; preserve
that diagnostic instead of rebuilding or replacing immutable packages.
For the first rejected document, `Utf8BoundedPreparationCache.cs`, Windows/PDB
SHA256 is `AF1A0B7FEF227EDFF508B6AD067BD5DD23227433F9CF4FFF0E6242A168293234`;
its LF-normalized SHA256 is `E2FEBD30AB3231630434DAFB4C8A825A684CE0C6C071903F02B8C554EA06647A`,
which matches the frozen Linux checkout. No source or package was changed.

[Linux raw fixtures/logs/TRX archive](../../../artifacts/experiments/native-hosting-delivery-20261031/linux-consumers.tar.gz),
SHA256 `934740966398D63FCEB2BCB83A10D73A048F60C329FE6C170882B3B0C8BCF969`.
Source CI `41215373848` is green on Windows/Linux/both runtimes, including disabled
dynamic code; source CI is distinct from exact-package acceptance.

The complete source VM refresh passed in **2734.524s (45m34.5s)**, from
2026-10-31 16:23:13Z to 17:08:47Z. It includes a fresh Release build,
both-runtime correctness/disabled dynamic code, all 89 Core cases/178 rows,
90 paired Python operations, four lifecycle/seven scaling families, 126 PCRE2
rows/16 scaling families, resource checks and final verification. All 65 PCRE2
paired commands pass. No rows were stopped or omitted. The 3600s target is met;
package/consumer checks and separate experiments remain outside that refresh.

See [full phase receipt](full-refresh.json) and the
[raw/source/binary archive](../../../artifacts/experiments/native-hosting-delivery-20261031/full-refresh-0e24cece.tar.gz),
SHA256 `F74F4C64A6CF832BA5230E09EAFDA8D2866A3BE0EDE2FA922A5F34C0D661CE0E`.
Generated snapshots/tables were imported using the existing reporter output;
focused snapshot tests pass on both targets. Preserve uncertainty: six Core
unqualified rows; 17 Python inconclusive, none unqualified; 38 native-PCRE2
inconclusive, one unqualified and 25 exclusions. A passed refresh does not make
every row a qualified comparison. The [fixed-baseline cost experiment](../CumulativeHosting-20261031/PREDECLARED.md)
is a separate bounded comparison, not a replacement for catalog evidence.

A later public Count tail regression was reproduced on these frozen bytes:
`夏洛克·福尔摩斯` followed by padding and `夏洛克` can throw after a preceding full
match. Source correction `13ff4c5f` bounds leading-anchor candidates to complete
needle starts, with focused source tests, a closed cost check and green CI
`41221555011`. Preserve dev.1 as qualified evidence; it is not the final release
candidate, and its bytes must not be replaced with the correction.
