# Qualified 0.5.0-dev.8 delivery candidate

All three packages share0.5.0-dev.8 with exact Core pins, frozen producer
`27078deb7d80c96e7e9b020e037df304c3e931a4`, .NET10/11RC3. Packages and symbols
are staged at `artifacts/nuget/`; immutable records are under
`artifacts/package-candidates/0.5.0-dev.8/`. **Not published.** Older bytes remain
unchanged. Corrected dependency prose and the accepted Python search-plan and
candidate improvements, simpler invocation/anchored preparation and Core final-slot
template replacement are included.

| Gate | Result |
| --- | --- |
| Windows packages |246.552s; two graph builds/one pack per profile;107378detached passes|
| Linux packages |107378passes against exact Windows bytes; no repack|
| Original consumers |Awk19/Lython118 per runtime/platform; native/funded probes pass, exact loaded hashes, root loans0|
| Source/restore identity |463original Windows compilation documents, including generated; six assets/24Linux cache/feed/lock/PDB/SourceLink audits|
| Fixed-original public costs |79workloads,382.888s, minimum33.831ms; largest adverse median8.29%, within10%; no confirmation|
| Complete quiet-VM refresh |**2512.110s (41m52.11s)**; Full/Complete/all83required steps passed/exit0|
| Imported snapshot guards |Core3/Python1/PCRE1 pass per runtime on matching affected-project binaries|

The full run includes one Release build, both-runtime correctness and disabled
dynamic checks, complete.NET11 catalogs/paired comparisons, lifecycle/scaling,
finite resource costs and verification. It meets the60minute VM target. Package,
consumer and fixed-original operating-cost gates are separate scopes/clocks.
The first Linux launch stopped before any restore/test because an Accepted control
marker had CRLF; only that guard's newline handling was corrected. No package or
completed suite was repeated.

The [cumulative gate](cumulative-acceptance.json) retains the same frozen
`a40bda03` and79typed public workloads, finite/live/deadline costs, allocations
and unchanged controls. Captured FullMatch +8.29%, variable +7.23%, lazy +0.50%;
sample spreads/paired variation remain visible in the raw archive. Captured
FindAll ordinary -9.57%, finite -2.86%, deadline +2.04%, Unicode -7.76%, offset
-7.14%. This representative median acceptance is not a universal/noise-free
guarantee. The earlier2598 rejection/sole confirmation and parent trials remain
intact; new APIs without an old counterpart keep separate feature-cost evidence.

[Fresh ratios](ratio-summary.json) include comparator movement and uncertainty,
not causal A/B speedups. Core107Win/2TieCandidate/65Gap/4Unqualified;
Python29ManagedFaster/45CpythonFaster/13Inconclusive/3Unqualified;
nativePCRE73ManagedFaster/11NativeFaster/4Equivalent/13Inconclusive/25Excluded.
The four Leipzig Core rows and three Python rows remain unqualified; exclusions
retain reasons. No uncertain row was rerun to improve status.

Largest qualified Core gaps: capitals9.62x, keyword declarations9.48x,
Chinese7.20x, imports6.92x. Region Count remains Win at0.20x/0.19x.
Python atomic search6.45x/literal5.54x/UnicodeFullMatch4.63x; nativePCREdate
match9.42x/miss9.08x. Keep the different comparators distinct. Next Core
investigation is reuse of the existing repeated-segment plan on mixed UTF-8:
the real capitals corpus contains nine Unicode whitespace scalars, so an
ASCII-only projection would be incorrect. No implementation is promoted by that
untimed census. Optional PCRE2 scalar projection and isolated POSIX remain later
work, separate from usable Core/Python delivery and the remaining performance gaps.

[Verification and archive hashes](verification.json), [full receipt](full-refresh.json),
[Windows packages](windows-qualification.json), [Windows consumers](windows-consumers.json),
[Linux packages](linux-packages.json), [Linux consumers](linux-consumers.json),
[identity audit](linux-identity-audit.json). Raw measurements, source/DLL/PDBs,
drivers and consumers are sealed under `artifacts/experiments/delivery-dev8-20261102/`.
