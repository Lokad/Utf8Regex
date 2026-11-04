# Dev.20 delivery boundary

Common **0.5.0-dev.20** freezes the accepted ASCII validation fast return,
bounded dot-tail search, small output copies, PCRE2 boundary-window proof and
literal-repeat proof reuse. All three packages target .NET 10/11, with exact
adapter-to-Core dependencies. They are staged, **not published**.

The user accepts local regressions for relatively major gains. The 10% allowance
belongs to the completed checked-access work; it is not a universal release
ceiling. Historical fixed-original results and rejected experiments are preserved.
The focused public comparisons document individual gains and costs; this frozen
boundary's catalogs measure the combined candidate against its comparators.

| Accepted change | Public evidence and explicit costs |
| --- | --- |
| ASCII validation fast return | [Comparison](../AsciiValidityFastReturn-20261103/README.md): actual ordinary/compiled matching about42% faster; finite/live about17%, deadline14%. Late Unicode +20.20% (~0.82us); allocations unchanged. |
| Bounded dot-tail prefix | [Comparison](../BoundedDotTailPrefix-20261103/README.md): Holmes about20%, River about38% faster; Unicode false-byte case about88% faster and less allocation. Single-byte tail +10.27% (~1.40us). |
| Small output copies | [Comparison](../SmallOutputCopy-20261103/README.md): representative replacement about2%, finite/live about3%, deadline/live about6% faster; allocations unchanged. Large-copy control drift remains unattributed. |
| PCRE2 boundary-window proof | [Comparison](../Pcre2WindowPolicyReview-20261103/README.md): date hit57% and miss54% faster. Finite/live +5.37–6.60%, deadline +3.62–8.48%, captured Count +11.11% (~7.16us), UCP +16.31% (~0.40us); UCP cause unresolved. |
| Literal-repeat proof reuse | [Comparison](../LiteralRepeatPolicyReview-20261104/README.md): nullable FindAll about20%, captured26%, finite Count20% faster; scratch and finite Count allocations lower. Body captures +12.42% (~9.69us), overlaps +23.52% (~9.31us), alternatives +10.94% (~21.07us), RTL +21.31% (~5.94us). |

Clean package/code source is `fe169a8fc3b0952ef14a96453366c9649b04c8e9`.
Source CI `41740520306` and runtime/data CI `41739249730` are green on Windows
and Linux. Windows qualification took **273.948s**, with two library-root builds
and one pack per profile. Eight detached fixtures pass **107,958** checks on each
platform. Six target assets and **464** original compiler documents, including
generated inputs, have matching source/PDB/DLL/feed/cache/lock evidence.

Linux consumes the exact Windows-produced package bytes; it does not repack.
The VM's Full refresh builds its own Release graph from the same frozen source;
those Linux-built DLL hashes differ from the Windows package assets. Measurement
records retain the loaded binary identities. Package-consumer qualification and
source-graph performance are distinct checks, not an exact-package timing claim.
Original copied Awk19/Lython118 checks per target pass on both platforms, as do
the native/funded public API probes with final root loans zero. The original
1,565-file prehosting audit changes only three exact version pins. These are
compatibility checks, not downstream adoption or POSIX acceptance.

The [Full refresh](full-refresh.json) passed in **2648.4756307s (44m08s)**:
all **83 exact expected steps**, Full/Complete/terminal0, below3600s. One Release
build, both-runtime correctness (107,998 source checks), disabled dynamic code,
all .NET11 catalogs, paired comparators, lifecycle/scaling, resource lanes and
verifiers are included. No restart or separate timing campaign. The previous
frozen dev.19 43m08s record stays unchanged.

All seven generated files were imported byte-identically. Only the root README
introductory date, duration and receipt link were then changed. Existing snapshot
guards passed3/1/1 checks per runtime with matching binaries and no rebuild;
the four generated blocks and six descending tables/178 ratios were checked.

[Current rankings](ratio-summary.json) retain uncertainty: Core116 wins/2 tie
candidates/58 gaps/2 unqualified; PythonRe36 ManagedFaster/1 equivalent/35
CpythonFaster/15 inconclusive/3 unqualified; native PCRE2 78 ManagedFaster/1
equivalent/12 NativeFaster/10 inconclusive/25 excluded. Chinese literal Count
remains Core's largest gap,7.13x/6.51x/5.61x. Its rejected fusion experiment stays
closed. Python early literal is5.47x with required full-subject validation;
date miss3.60x and Unicode split3.50x remain useful next targets. Nullable FindAll
is2.99x/16,528B in this Full, distinct from the selective2.57x observation and
the focused causal A/B. Native date hit/miss are4.42x/4.04x. No catalog movement
is presented as an isolated code-change effect.

Resource-lane interpretation: the reporter's `IsMatch` label uses Core.IsMatch,
PythonRe.Search(...).Success, and PCRE2.IsMatch. Python's lane therefore includes
value-result construction. Core/Python finite lanes set work/depth/working/result
caps; PCRE2's finite lane sets MatchLimit. Report overhead within each profile's
same public operation and policy; do not treat these as identical cross-profile
IsMatch costs or as measurement of every PCRE2 memory control.

For captured9B `(a+)(b+)`, unlimited -> finite/live-token medians are Core
**1.134 ->1.189us**, PythonRe **1.011 ->1.064us**, PCRE2 **1.181 ->1.174us**;
each lane returns the same Boolean result and allocates **5,744 ->5,856B**.
The small PCRE2 timing difference is not claimed as a speedup. These are scoped
operating-cost examples, not total-heap bounds or universal overhead. Raw lanes
retain all sizes/operations and profile-specific nullable-result differences.

Completed Linux fixtures were archived and compared before retirement. Actual
available RAM, rather than tmpfs capacity alone, admitted this Full run. Verified
new pilot/archive duplicates were retired; canonical local evidence, published/
staged bytes and protected historical cleanup targets remain intact. No servers
were shut down or unrelated processes killed.

Optional PCRE2 scalar projection and broader profile completion remain separate.
POSIX is post-performance-release. A possible tiny unfolded scalar-class reuse
for Unicode split has only a source trace, no prototype or performance claim.
No new qualification framework, unchanged217-case replay or automatic publication.

Immutable evidence under `artifacts/experiments/delivery-dev20-20261104/`:

- `windows-evidence.zip`: `2E7CD4DE3D8058685E0DCD39C7ABD8F8908D1F783DA8E97737681EBA94D14350`.
- `windows-restore-inputs.zip`: `C3FD5B47250CC338E3E811F2A1CC62CC2EA7BBD95BACC3E5DE026422749581D3`.
- `linux-evidence.tar.gz`: `51A532CB6365C3F32DE6E31DA0CC4646190274215F548C144ACA36378171BC57`.
- `full-evidence.tar.gz`: `B2A6418844B66B2D4E1F8AD5F236D9DF1FD22218979C4C81276A330D4A9F05CF`.
- `post-refresh-guards.zip`: `3A7EFD86CA342499D3364D0520460618F8EEBBFD18563EB5FA34BAF5978CB32C`.
