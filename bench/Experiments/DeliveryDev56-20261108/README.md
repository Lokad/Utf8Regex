# Four-profile dev.56 delivery

Freeze source `1c1cb97d`, common `0.5.0-dev.56`, with .NET 10/11 assets for Core,
PythonRe, PCRE2 and the new POSIX adapter. Adapters pin the exact Core version.
Packages and symbols are in `artifacts/nuget/`; the immutable Windows qualification
is in `artifacts/package-candidates/0.5.0-dev.56/`. Nothing is published.

## Complete refresh

ONE [Full](full-refresh.json) passed all **85 steps in 2,845.882 seconds
(47m26s)**, below the unchanged one-hour cap. It includes one Release build,
both-runtime source tests, both disabled-dynamic-code hosts, all four .NET 11
catalogs, paired comparators, lifecycle/scaling, resource costs and final evidence
verifiers. Source/PDB/loaded identities match the frozen revision. Five focused
snapshot checks pass afterward using the same .NET 11 test binaries.

Eight generated README/compact-summary files were imported once. Ordinary CI
regenerates/verifies presentation offline from `bench/Snapshots/`; expanded samples
remain ignored artifacts. No archive service, upload or durability gate is needed.
Local raw evidence is `artifacts/experiments/delivery-dev56-20261108/full-evidence.tar.gz`,
SHA-256 `C5A7C419BF3DE95AFD8AC8C032C7320FCF8429F1F1C13921C08E20AEE84F633A`.
The archive was copied off the volatile VM and hash-verified.

The source revision is green in Windows/Linux CI, run `42503386814`. That source
check and package/consumer acceptance remain distinct from performance evidence.

## Public costs and limits

The [leaf-history simplification](../PosixLeafTags-20261108/README.md) has one
separate-process causal comparison: eight changed public capture workloads improve
17–49%, including finite/funded/deadline lanes, with fewer allocations. Three
unchanged controls overlap. No confirmation or further tuning was needed.

The final catalog independently measures nullable POSIX captures at **1.59 µs**
and SKU captures at **5.32 ms per 4,096 calls**, versus native 0.14 µs and 0.61 ms.
Paired ratios are 10.98 and 8.74; these remain substantial gaps. SKU finite/live,
funded finite/live and finite/deadline/live cost 6.91, 7.48 and 8.75 ms. Captures
retain scalar byte/UTF-16 coordinates and competing public histories. The native
ordered NFA is comparable only on verified equal-result inputs: four ambiguity
cases stay excluded, with ordinary costs retained. No general POSIX/native parity
or worst-case linear-time claim.

Catalog statuses, with their separate comparators:

- Core: 122 wins, one tie candidate, 55 gaps, no unqualified rows.
- PythonRe: 45 managed faster, 32 CPython faster, three equivalent, seven
  inconclusive and three unqualified rows, retaining their reasons.
- PCRE2: 81 managed faster, ten native faster, ten inconclusive and 25 excluded.
- POSIX: ten native faster and four excluded.

Successive catalogs are not isolated causal comparisons. Core matching algorithms
are unchanged here: postcode matching moves 16.51 to 29.97 ns, compiled full AWS
keys 9.13 to 10.71 ms, and Sherlock `ing` Count about +13–14%. Keep these adverse
observations without assigning them to the POSIX change, normalizing by controls,
composing speedups or reopening closed campaigns.

Resource admission covers modeled major capacity and growth overlap, not all CLR
traffic or retained process memory. Values/caller-owned POSIX capture spans create
no producer result payload; a positive result-byte cap admits them. Strict UTF-8,
scalar semantics, synchronous original cancellation/deadlines and working loans
remain. Unsupported ERE syntax/locale behavior is documented in the profile spec.

## Packages and original consumers

Windows qualification passed in 287.149 seconds. Windows and Linux each accepted
ten detached fixtures against the exact same eight package/symbol files: all four
packages, both target frameworks, 110,506 checks per platform. Linux did not rebuild
or repack a producer. POSIX contributes 91 public/API/resource/selection checks per
target; source-only presentation checks stay in source CI.

Original frozen Awk19/Lython118 checks pass per target/platform, along with public
hosting probes whose final root working loan is zero. Only exact producer pins
changed in those source fixtures. Twelve Windows and 28 Linux restore audits,
477 original compiler inputs and 14 Linux TRX identities were checked. Core has
no package dependency; adapters pin `[0.5.0-dev.56]`. Embedded migration/release
guides describe POSIX, and packages contain no native assets. All 300 older staged
assets are unchanged; eight new package/symbol files bring the total to 308.

Linux first stopped while compiling the new POSIX detached fixture: its project
inherited implicit usings/nullability inside the checkout. Make both settings
explicit in that test project. A resume-helper XML-node selection error then
stopped preparation before tests; it was corrected. Eight already-passed fixtures
were reused, only the affected POSIX targets were resumed, and the previously
unrun original consumers/audits followed. Test code and producer bytes did not
change. No new candidate version, Full repeat or package matrix was needed.

Final local Linux evidence is
`artifacts/experiments/delivery-dev56-20261108/linux-final-evidence.tar.gz`, SHA-256
`E5E164CE035362B51DC0A63BC769CECD16EE19B27E8FD66F37FBC17E236630A1`.
The initial fixture/preparation failures remain separately under the same ordinary
artifact directory; this does not create an archive/upload requirement.

The benchmark/summary import and standalone-fixture fix are metadata/test changes
after the frozen production revision. They do not qualify new producer bytes or
change the immutable packages. Publication, downstream adoption, broader PCRE2
completion and further parity tuning remain separate work.

## Acceptance closure

Source `1c1cb97d`, fixture correction `f483930f` and final contract documentation
`a5ad387b` are green on Windows/Linux. The final run is `42509422680`, completed
2026-11-08 at 18:28:09 UTC. Production inputs remain identical to the frozen
package/benchmark revision; later changes affect summaries, documentation and
explicit test-project settings only. B1–B4, H6 and POSIX P1–P4 acceptance is complete.
This receipt-only closure needs no repeated package, benchmark or source matrix.
