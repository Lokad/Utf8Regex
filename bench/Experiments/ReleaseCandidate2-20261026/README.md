# Common 0.4.0-rc.2 package candidate

**Correction 2026-10-27:** cumulative performance acceptance below is withdrawn
as **inconclusive**. Correctness/identity/consumer evidence remains valid.
[The dated erratum](ERRATA.md) supersedes the original cost sign-off and explains
frozen ZIP navigation. Original observations and raw receipt bytes are preserved.

## Original 2026-10-26 record


Qualified, immutable and unpublished. Producer
d1a14f407ae4e34230430260fc58111c960c1e63; exact common Core dependencies.
[Handoff](HANDOFF.md), [consume](CONSUMING.md), [package identities](candidate.json),
[provenance](package-provenance.json), [final evidence](final-integrity.json).

The local canonical feed is artifacts/package-candidates/0.4.0-rc.2/feed.
The release ZIP is artifacts/Utf8Regex.0.4.0-rc.2.zip. Binary packages and complete
TRX/build logs remain local; this tracked receipt preserves identities and results.
No historical package, snapshot or original consumer assertion was rewritten.

Complete detached Release Core (39,655), differential (1,504), PythonRe (3,937), and PCRE2 (6,189) suites
pass on Windows/.NET 10.0.14 and Linux/.NET 10.0.12, without Linux library builds.
Both platforms pass the original producer Awk/Lython/PCRE2 consumers and combined
graph, 121 CPython capture fixtures and 91 semantic digests. Exact-feed/cache/lock/
DLL, API/member inventory and PDB/SourceLink/source checks are retained.
[Exact freeze CI](https://github.com/Lokad/Utf8Regex/actions/runs/40364734526) is green.

[Operating costs](RESULT.md) use actual package DLLs versus fixed baseline 844dcbad,
one 109.138 s comparison and sole 23.568 s confirmation. Fifteen public rows initially
change -18.79% to +4.99%. Finite Python Count initial +10.38% confirms at +0.06%;
both raw observations and substantial between-run absolute variation remain
visible. The declared representative cumulative gate is accepted: no repeatable regression above 10%. This is not universal equivalence or a new historical Status.
Controls, finite-limit costs, allocations, full protocol and loaded identities
are included. No third run, source optimization, package repack or snapshot refresh.

One Linux staging correction reran full Core with matching binaries after 15
repository-location assertions failed outside the checkout. Original failed
TRX/logs remain under the local delivery directory. No test or product was changed.
The existing qualifier was used once: two adapter-root builds and one pack/symbol
pack per package. No new permanent qualification/benchmark framework was added.

All 18 protected original files match. Core/PythonRe/PCRE2 retain strict UTF-8/scalar
semantics, useful invocation/construction controls and documented lifetime/profile
boundaries. Defaults remain unlimited and modeled capacities do not contain an
entire application. Read packaged migration/host/profile docs before adoption.
NuGet publication, tags and external consumer pin changes are separate actions.