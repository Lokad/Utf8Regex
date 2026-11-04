# Dev.24 delivery boundary

Common **0.5.0-dev.24**, frozen source `00a9756189c2e97b1da890df7e6d8a794b56bd6d`,
combines accepted Python Search projection, wide UTF-8 validity and SIMD mask
metadata hoisting. All three packages target .NET10/11 with exact Core pins.
Staged in artifacts/nuget, **not published**; older bytes remain immutable.
Freeze CI41815048304 is green on Windows/Linux.

[Python Search](../PythonSearchMetadata-20261104/README.md) improves larger Unicode
hits24–52%, actual Unicode miss39% and ASCII early42%; tiny/late hits, supplementary
projection and some controls cost more. [Wide validation](../CommonUtf8Wide-20261104/README.md)
improves Chinese Count29% and alternation18–19%; folded Russian costs13%.
[Mask metadata hoisting](../ChineseAlternateLoop-20261104/README.md) improves actual
Chinese alternation23% and miss26%; folded Russian costs13.44% and dense false
prefixes5.10%. These are separate matched public comparisons, not cumulative gains.
Allocations stay unchanged; local costs are accepted for substantial gains.
The rejected16->32-byte finite cutoff is fully reverted; its261x adverse result remains.

Windows qualification **318.0594985s**; Windows/Linux each pass eight detached
fixtures/**108178 checks** and six assets. Original copied Awk19/Lython118 per
target and public native/funded producer probes pass with final root loans0.
The1565-file input audit changes only three exact pins. Linux consumes exact
Windows bytes without repacking;464 original compiler documents, six target assets
and24 Linux restore records are retained. Compatibility is distinct from adoption
and POSIX acceptance. Package, platform and performance checks remain separate.

The sole [Full refresh](full-refresh.json) passes in **2631.785993s =43m51.79s**,
including one build, both-runtime correctness (**108218 source checks**), disabled
dynamic code, all .NET11 catalogs, paired comparisons, lifecycle/scaling, resources
and verifiers. Full/Complete/terminal0, exactly83 expected steps in order, all Passed,
below3600s. No restart or extra qualification matrix.

Seven generated files were hash-imported after source/inventory/ordering checks;
only the root introduction's duration and receipt link were then changed. Four
generated blocks, six descending tables and178 displayed ratios verify. Snapshot
guards pass six hosts/ten checks using matching binaries, without rebuilding.

[Current rankings](ratio-summary.json): Core113 wins/3 ties/56 gaps/6 unqualified;
Python39 ManagedFaster/37 CpythonFaster/1 equivalent/11 inconclusive/2 unqualified;
PCRE2 76 ManagedFaster/1 equivalent/13 NativeFaster/11 inconclusive/25 excluded.
Independent catalog movement is not an isolated causal A/B. Two former Python
harness-floor rows now qualify as CpythonFaster only because that conclusion
survives the existing conservative floor bound; their historical observations
remain. No favorable rerun or changed qualification rule.

Core's largest qualified ratios are Chinese literal4.97x, compiled Chinese
alternation4.25x and compiled Chinese literal3.93x; imports3.39x, compiled window
3.19x and email2.72x follow. Python Unicode Split3.61x is the already-closed pilot;
limited Split3.01x, early literal2.90x, date miss2.79x and one-capture FindAll2.58x
remain gaps. PCRE2 date hit/subroutine/date miss4.39/4.36/4.16x stay separate from
broader profile completion. Closed pilots and post-performance-release POSIX remain.

Resource costs compare each profile's same operation and policy. Reported IsMatch
is Core.IsMatch, Python.Search(...).Success(value), PCRE2.IsMatch. Core/Python finite
lanes cap work/depth/working/result; PCRE2's lane sets MatchLimit. For the captured
nine-byte case, unlimited ->finite/live medians are Core1.078 ->1.174us,
Python0.955 ->1.094us and PCRE2 1.155 ->1.226us; allocations5744 ->5856B.
These do not imply identical Boolean costs, all PCRE2 controls, universal overhead
or total-heap limits. Raw operation samples and allocations remain authoritative.

RAM admission waited **460s** for natural release, then admitted9078160KiB under
the existing8GiB guard. Full plus recorded readiness is **51m31.79s**; the actual
refresh retains its3600s cap including build/tests. No server shutdown or weaker
guard. Fresh2172548939B Linux fixtures and807525213B archive duplicate were retired
after off-VM SHA verification and tar comparison. SSH remained open through final
archive/guard acceptance. No protected cleanup targets or historical bytes changed.

Immutable evidence under artifacts/experiments/delivery-dev24-20261104/:

- windows-evidence.zip: `A018119165CADE3EEC932A11050DBA155F97E91B44D009CDAF5ACDC1A3C5FA8C`.
- windows-restore-inputs.zip: `92EA2C9B5ABE09704BB308ACFD11E96046311EBD0F5D1AFC79C7ECBEB03FFB9D`.
- original compiler input zip: `A2C6A3601AD0C38C0F41C9E657291AA9976CD0A8E292AFC48D612A7B7332D881`.
- linux-evidence.tar.gz: `0840D9341089073E63B73B2C4A10D7B1F5B5C2223619A388968B975D1483F183`.
- full-evidence.tar.gz: `96E9E3A7A6D0873484291FF829F50648FEC174A155D56261FE8434E24115EDFE`.
- post-refresh-guards.zip: `081E4F432512E22D90820A6303C36907B34D47C4ECFB57218629DB3E13328289`.
