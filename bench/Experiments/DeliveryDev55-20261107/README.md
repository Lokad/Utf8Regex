# Common dev.55 delivery checkpoint

Accept the immutable, unpublished Core/PythonRe/PCRE2 `0.5.0-dev.55` candidate
at `d6067925`, with .NET 10/11 assets and exact adapter-to-Core dependencies.
Packages and symbols are in `artifacts/nuget/`; qualification records are in
`artifacts/package-candidates/0.5.0-dev.55/`.

This combines [literal string-output reuse](../PythonLiteralTextOutput-20261107/README.md)
and [once-per-window class proof](../WindowProofReadmission-20261107/README.md).
Strict whole-input UTF-8/scalar semantics, captures/aliases, cancellation/deadlines
and useful resource admission remain. Numeric admission can fall when redundant
work disappears; the profile contracts document that change. Controlled string
replacement retains its existing scalar route. No mode, owner, engine, unchecked
access or shipped dependency was added.

## Packages and consumers

Windows qualification passed in 295.482 seconds. Windows and Linux each passed
eight detached fixtures/110,324 checks. Linux used the exact Windows package
bytes without producer rebuilding or repacking. Original Awk19/Lython118 checks
passed per target/platform, alongside public probes with final root loans zero.
Evidence includes 12 Windows/24 Linux restore audits, 464 compiler documents and
12 checked Linux TRX identities. All 294 older staged assets remain unchanged;
six new package/symbol files bring the total to 300.

Two harness mistakes are preserved: the initial Windows consumer invocation used
a relative feed path and stopped before tests; Linux staging initially lacked
its source-identity file and stopped before extraction/tests. Corrected invocations
resumed only the affected steps. No producer fix, repack or Full repeat was needed.

## Full refresh and public costs

ONE completed Full passed all 83 steps in **2,837.230 seconds (47m17s)**, including
the Release build, both-runtime source checks, disabled-dynamic-code hosts, all
catalogs, paired comparators, lifecycle/scaling, resource checks and verification.
Ten matching-binary snapshot checks passed afterwards. Seven snapshots were
imported once; README retains four generated blocks, six descending tables and
all 178 ratios. Actual RAM admission and the unchanged one-hour cap remained.

ONE exact-package 54-to-55 ABBA passed in **110.236 seconds**: 22 public workloads,
440 samples, minimum 27.4351 ms. Identical caller, separate ordinary processes,
one producer per process, eight startup CPUs then CPU2 affinity, 750 ms/minimum
32-call warmup, independent 75 ms calibration and five samples with a 20 ms floor.
All ten intended gains persist in both processes with disjoint sample ranges.
Two miss-string allocation medians fall; the remaining 20 are unchanged.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual literal replacement, string miss | 4.40 | 2.79 | -36.62% |
| Unicode string miss | 14.67 | 8.96 | -38.90% |
| ASCII string hits | 7.45 | 6.21 | -16.63% |
| Unicode string hits | 30.15 | 18.93 | -37.21% |
| One string replacement | 10.88 | 6.70 | -38.43% |
| Core gap-window Count | 41.86 | 35.70 | -14.71% |
| Same finite/live | 47.08 | 40.58 | -13.80% |
| Same deadline/live | 58.93 | 50.04 | -15.09% |
| PythonRe mail Count | 2055.25 | 1868.02 | -9.11% |
| Same finite/live | 2426.16 | 2239.96 | -7.67% |

Actual string-miss allocation falls from 20,016.22 to 13,336 B/op; Unicode miss
from 13,872 to 8,728. Finite mail still adds 371.95 us, versus 370.91 before
(19.91% versus 18.05%); finite gap adds 4.87 us versus 5.21 (13.65% versus 12.45%).
Controls remain useful and have measurable operating cost.

Accept the local costs: false-gap windows +8.90%/+27.68 us, dense mail
+4.22%/+6.08 us and Unicode mail +4.28%/+5.21 us have disjoint ranges. Unchanged
Core literal Count +4.97%/+15.2 ns also has disjoint ranges, although it bypasses
the proof; its cause is unestablished. PCRE2 control +1.27%, false delimiters
+3.70%, captured reference +1.57%, RTL +1.50%, dot +0.24% and excluded deadline
miss +1.23% overlap. [All rows](package-cost-summary.json) and
[acceptance](package-cost-acceptance.json) retain processes, ranges and allocations.
No confirmation, retuning, normalization, composed speedup or universal 10% gate.

## Remaining gaps and evidence

The current Full has Core119 wins/3 tie candidates/56 gaps/0 unqualified;
PythonRe44 managed faster/30 CPython faster/2 equivalent/9 inconclusive/5
unqualified due to harness floors; PCRE2 76 managed faster/13 native faster/1
equivalent/11 inconclusive/25 excluded. Comparator scopes remain distinct.

[Catalog movements](catalog-movements.json) retain AWS quick compiled
+34.48%/+1645.61 us and ordinary +18.43%/+875.11 us; bounded-context ordinary
+4.13%/+831.38 us; PythonRe many-capture UTF-8 FindAll +4.39%/+40.36 us.
These successive catalogs do not establish isolated causality. Further source
work needs a demonstrated defect or distinct live avoidable cost; closed campaigns
remain closed. Optional POSIX and broader PCRE2 completion stay separate.

All six [canonical raw archives](canonical-archives.json) are verified off VM.
Frozen Full52/54/55 graphs remain intact. The prior dev.54 Linux scratch retirement
was fully compared with its retained canonical archive before removing only its
two named VM targets. Protected and previously denied scopes remain untouched.
[Source CI](source-ci.json) passed on Windows/Linux. Snapshot CI is pending.
No NuGet publication.
