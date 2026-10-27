# Utf8Regex 0.4.0-rc.2 release handoff

**Correction 2026-10-27:** cumulative performance acceptance below is withdrawn
as **inconclusive**. Correctness/identity/consumer evidence remains valid.
[The dated erratum](ERRATA.md) supersedes the original cost sign-off and explains
frozen ZIP navigation. Original observations and raw receipt bytes are preserved.

## Original 2026-10-26 record


**Qualified common prerelease candidate; unpublished.**
Producer d1a14f407ae4e34230430260fc58111c960c1e63.
Core, PythonRe and PCRE2 share0.4.0-rc.2; both adapters pin exactly Core.
Frozen RC1 remains unchanged and superseded. No NuGet publication or tags.

Canonical feed: ../package-candidates/0.4.0-rc.2/feed.
See CONSUMING.md, the candidate.json/package-provenance.json beside that feed,
packaged-documentation.json and final-integrity.json. The release ZIP contains
the same six package/symbol files and reusable source-mapped NuGet.Config.

One producer closure (two adapter-root incremental builds), one pack/symbol pack
per package. Complete detached Release fixtures consume exact source-mapped feeds,
fresh per-fixture caches, locks and actual DLLs on Windows/.NET 10.0.14 and
Linux/.NET 10.0.12; libraries are not rebuilt on Linux.

| Suite | Windows passed | Linux passed |
| --- | ---: | ---: |
| Core |39655|39655|
| Differential |1504|1504|
| PythonRe |3937|3937|
| PCRE2 |6189|6189|

No final failed/skipped cases. API/member inventory checks inspect consumed
assemblies; portable PDB/SourceLink/source/configuration and symbol hashes are
verified by the producer qualifier. Twelve packaged documentation assets match
frozen source. Exact freeze CI 40364734526 is green on Windows/Linux, including
the existing disabled-dynamic-code checks. NativeAOT/trimming are not qualified.

Both platforms pass the unchanged original producer Awk/Lython assertions and
PCRE2 public consumer, including 676 recorded native-property observations, plus
a combined consumer resolving all three to one Core graph. Exact lock/feed/cache/
loaded DLL identities are retained. Each platform passes 121 CPython capture
fixtures and 91 semantic digests including supplementary coordinates. CPython is
3.13.2 on Windows and3.12.3 on Linux for this overlapping slice. These checks do
not change external consumer pins or prove consumer deployment.

The initial Linux test directory sat outside the source tree; fifteen source/API
checks failed to locate repository files. Corrected staging reused the test DLL
and reran the complete Core suite before the remaining profiles. Original failed
TRX/logs remain; no product, fixture, package or assertion was changed.

## Representative operating cost

One 109.138 s dedicated-VM comparison against fixed correctness-equivalent baseline 844dcbad,
nineteen public/control lanes, plus the sole 23.568 s reversed-first confirmation.
Fifteen public rows initially change -18.79% to +4.99%, with unchanged allocations.
Finite Python captured Count initially +10.38% confirms at +0.06%, with stable
controls; absolute times differ between runs. Both observations are preserved.
No repeatable regression above 10% remains under the declared bounded protocol.
This accepts the representative cumulative gate, not universal equivalence.
See RESULT.md, raw samples, identities and predeclaration.

Finite controls have real public costs: Core identifier Count is 0.825 us
unlimited, 9.092 us finite, 9.261 us with a live token and 18.320 us with deadline.
Dense PCRE2 replacement is 104.541 us unlimited/171.347 us finite. Policy-dependent
routes can also be faster. Do not interpret these ratios as pure accounting cost,
safe defaults or a blanket performance guarantee. No old Status/snapshot refresh.

## Contract and exclusions

Strict whole-subject UTF-8/scalars, primary byte coordinates, captures/history,
NB/RTL, borrowed/owned result lifetimes, original cancellation, absolute deadlines
and useful modeled work/depth/working/result controls remain. Actual Unsafe.SizeOf
queries remain allowed; no unchecked reads, BCL matcher/result or native PCRE2
implementation is shipped.

Defaults/static calls are unlimited. Major modeled capacities are admitted before
acquisition; total heap/RSS, callbacks, getters, separate cold preparation and
arbitrary host retention have separate scope. PCRE2 partial/grammar completion is
independent; documented byte-atom/non-slice exceptions and unsupported generic
partial decisions remain explicit. Read packaged release/migration/host/profile
documentation. No complete .NET/Python/PCRE2 equivalence is promised.

All 18 protected snapshots/frozen RC1/original TODO files match. Existing historical
package evidence remains immutable. Publication is a separate user action.