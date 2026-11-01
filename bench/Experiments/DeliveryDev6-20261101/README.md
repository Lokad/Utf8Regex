# Qualified 0.5.0-dev.6 delivery candidate

All three packages share0.5.0-dev.6 and exact Core dependency pins. Frozen source
`825ed1f7c4b4eb54f0e092da495396d93b435a48`, .NET10/.NET11RC3. Packages and symbols
are staged at `artifacts/nuget/`; the immutable manifest/provenance is under
`artifacts/package-candidates/0.5.0-dev.6/`. **Not published.** Historical bytes
are unchanged. This batch includes readonly instruction/request/token borrows,
checked ASCII-boundary reuse and existing allocation-test collection isolation.
Rejected compiler/prefilter/diagnostic/ASCII-decoder experiments remain absent.

| Gate | Result |
| --- | --- |
| Windows package qualification |252.119s; two graph builds/one pack per profile;107206detached passes|
| Linux package qualification |107206passes, consuming exact Windows package bytes; no repack|
| Original consumers |Awk19/Lython118checks per runtime/platform; loaded hashes match|
| Native/funded producer probes |Pass both runtimes/platforms; final root loans0|
| Source/restore identity |463original Windows compilation documents including generated inputs; six assets/24Linux feed/cache/lock/PDB/SourceLink audits|
| Full quiet-VM refresh |2643.729s (**44m03.73s**), Full/Complete/all83required steps passed/exit0|
| Fixed-original cumulative gate |26cross-profile +17PCRE2date workloads,108.826s+64.657s, minimum36.682/34.812ms; largest adverse median+4.52%, no confirmation|
| Final snapshot guards |Core3/Python1/PCRE1 pass per runtime, reusing matching binaries|

The full refresh includes its one Release build, both-runtime correctness and
disabled-dynamic checks, complete .NET11 catalogs/paired comparators, lifecycle,
scaling, resources and verification. It meets the60minute VM target. Package
qualification, consumers and cumulative comparison are separate scopes/clocks.

The [cumulative gate](cumulative-acceptance.json) compares with fixed `a40bda03`
using the same typed public drivers in isolated contexts, warm1.5s/min1000,
independent60ms calibration, five alternating samples and complete result-batch
consumption. PCRE2date hit/miss-21.84/-26.94%, denseCount-41.20%, capturedCount
-28.15%; all17PCRE medians improved. This covers representative operations,
not universal parity. Retain actual8-16B increases and noisy control/Unicode-heavy
spreads up to1.695x. New-funded APIs have no a40 counterpart and retain their
separate feature-cost evidence. No resampling or additional variants ran.

The [fresh ratios](ratio-summary.json) preserve uncertainty and each comparator's
scope; changes between catalog ratios include comparator movement, not causal
A/B gains. Core106Win/3TieCandidate/67Gap/2Unqualified; Python25ManagedFaster/
45CpythonFaster/17Inconclusive/3Unqualified; native PCRE54ManagedFaster/
6NativeFaster/2Equivalent/38Inconclusive/1Unqualified/25Excluded. More uncertain
rows do not establish that the previous gaps closed. Core's two Twain rows and
PCREfoo-dense enumeration remain unqualified; date-miss9.34x and assertion-capture
0.493x remain Inconclusive. No uncertain row was rerun to improve its status.

Largest qualified gaps: Core capitals9.27x/imports9.09x/keyword7.59x/Chinese7.19x;
Python atomic6.46x/literal5.65x/date4.69x/UnicodeFull4.54x; native PCREdate-hit
10.52x/subroutine4.93x. Keep unlike comparators distinct. Imports' catalog managed
time31.159->50.479us and keyword72.620->81.624us need attribution: Core engine and
benchmark code are unchanged since the prior full refresh, apart from a Core
integration comment and shared release metadata. The covered cumulative gate
does not prove that every other catalog operation has identical cost.

Known documentation erratum: all three packaged READMEs retain literal dev.1
dependency prose. Actual nuspecs correctly pin Core[0.5.0-dev.6]. Staged bytes
remain immutable; source prose is corrected after this freeze, and the next
meaningful package/release boundary must include it. No runtime contract changes.

Sealed source/binary/raw/consumer archives and their SHA256 identities are listed
in [verification](verification.json), under `artifacts/experiments/delivery-dev6-20261101/`.
Published0.4.0/dev.4 history remains unchanged. N4 gaps, optional PCRE2 scalar
projection and later POSIX remain explicit unfinished work; adoption/publication
are separate actions.
