# Current performance and hosting candidate

Frozen engine `2ebc01a79afbad0a39fd246b083e030239125dd9`, common **0.5.0-dev.4**,
Core/PythonRe/PCRE2, net10 and net11 RC3. These are immutable staged packages,
not a published release. This boundary includes the accepted anchored-line
NonBacktracking Count specialization and PCRE2 leading-byte slice search.
Earlier staged and published bytes remain unchanged.

The [candidate](candidate.json) passed Windows qualification in **269.076s**:
two graph builds, one pack per profile, exact Core pins, source/API/PDB/feed/
cache/lock checks and 107178 passing detached checks. Packages and symbols are
in `artifacts/nuget/`; original qualification is under
`artifacts/package-candidates/0.5.0-dev.4/`.

[Linux package checks](linux-packages.json) passed the same 107178 checks against
the **exact Windows-produced bytes**, without repacking. The
[identity audit](linux-identity-audit.json) covers six assets/PDBs/SourceLink and
24 feed/cache/lock observations. All 463 original Windows PDB compilation
documents, including generated inputs, are preserved with matching hashes;
Linux's LF checkout is a distinct input.

[Windows](windows-consumers.json) and [Linux](linux-consumers.json) passed the
frozen original Awk19/Lython118 scenarios per runtime, including cancellation
workers. [Only three copied dependency pins changed](consumer-provenance.json)
from dev.3 to dev.4; external consumer checkouts were untouched. Native/funded
public API probes passed on both platforms/runtimes, loaded asset hashes agreed,
and final root loans were zero. This proves compatibility, not downstream adoption
or general POSIX support. Resource controls admit major capacities and overlap;
they are not total heap/RSS accounting.

The [complete VM refresh](full-refresh.json) passed **2605.774s (43m25.8s)**,
meeting the 60-minute target. All83steps passed: one Release build, both-runtime
correctness and disabled dynamic code, full Core/PythonRe/PCRE2 catalogs,
paired comparators, lifecycle/scaling, resource checks and verification. Package
and consumer checks are outside this clock. Source and binaries stayed frozen;
there were no overlapping timing jobs or repeats to improve classifications.

The [fixed original baseline comparison](cost-result.json) took **109.587s**:
26 public operations, unchanged typed lane IL, warm1.5s/min1000calls, independent
60ms calibration and five alternating samples. Minimum observed sample48.334ms.
[All medians, spreads and allocations](cost-summary.json) are retained. Largest
covered adverse median is **+4.85%** for finite captured FindAllToUtf8, followed
by Chinese Count +4.73% and identifier Count +4.23%, below the10% allowance.
There was no confirmation. Several operations add8-16bytes; native template
paths reduce allocations. New funded APIs have no a40 counterpart and retain
their separate feature-cost receipts. This is representative coverage, not a
claim of universal performance equivalence.

[Original/final ratios](ratio-summary.json) use the actual archived a40 snapshots,
not stale snapshots at that commit. Ratios include comparator movement; causal
speedups belong to the focused public A/B receipts.

| Compiled Core operation | Original ratio | Current ratio |
| --- | ---: | ---: |
| Region Count |172.18x|0.192x|
| Lexer identifier |6.32x|1.81x|
| Lexer string |6.55x|0.64x|
| Lexer operator run |6.86x|1.87x|

Core now has107Win/2TieCandidate/65Gap/4Unqualified. Region ordinary/compiled
Count costs6.721/6.788us and64B, both Win. Unqualified river-window/symbol-count
rows remain unqualified. Keyword family9.57x, capitals9.43x, compiled imports7.17x
and ordinary Chinese literal7.13x retain their cause-specific closed deferrals.

Python has27ManagedFaster/1Equivalent/46CpythonFaster/13Inconclusive/3Unqualified.
Greedy miss, lookahead hit and possessive miss remain unqualified because CPython
removable floors are15.40%,11.68%,16.19%. Its qualified queue starts atomic miss
6.56x, literal early5.63x, date miss4.68x and Unicode FullMatch4.68x.

Native PCRE2 has69ManagedFaster/17NativeFaster/15Inconclusive/25Excluded. The
assertion-capture point ratio is0.499x (interval[0.450,0.567]), but remains
Inconclusive under existing variation rules; it is not a qualified native win.
Date miss/match11.50x/11.41x are now its largest qualified gaps. Next inspect
reuse of the existing bounded literal-window proof across zero-width boundaries.
The rejected LeadingAsciiSet boundary pilot stays closed. Different profile
comparators do not form one interchangeable ranking.

Existing final snapshot guards passed on both runtimes: Core3, PythonRe1,
PCRE2 1. Source CI41301029454 passed Windows/Linux. Following data/receipt changes
reuse this evidence without rebuilding or qualifying unchanged packages.

Immutable raw archives under `artifacts/experiments/delivery-dev4-20261101/`:

| Archive | SHA256 |
| --- | --- |
| full-refresh-2ebc01a7.tar.gz |359528FDCB31332132FB1D0DECF02E179AB5E53224BC9407E7D26CD0F05E5624|
| linux-consumers.tar.gz |85E80223ABD978EB8C602505EA4965C2861672377FDC8B097C149EBD5FC2AA06|
| producer-source.tar.gz |FF42B8A1150A4299692D7C77EE3D68B03C09B6A82E4F4D51F104CDC3553A09B7|
| windows-consumers.zip |46333242B7CE0A3B72987E67D997D42EB90A7EC9466FB376B2FCB15C0353AF2E|
| windows-compilation-inputs.zip |7F52640428C1182EF5DDD451EA54D4952EE2BC5885E81469F8AD0A8A3308A057|

The [handoff reconciliation](handoff-reconciliation.json) found no pending TODO:
H1-H4 handoffs were already integrated and removed. Historical receipts/archives
remain evidence. The overall plan remains active: remaining N4 gaps, optional
PCRE2 scalar projection and POSIX are unfinished. Publication/adoption are separate.
