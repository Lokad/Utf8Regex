# Common dev.54 delivery checkpoint

Accept the immutable, unpublished Core/PythonRe/PCRE2 `0.5.0-dev.54` candidate
at `553acbdf`. Both .NET 10/11 assets and exact adapter-to-Core dependencies are
qualified. Packages and symbols are in `artifacts/nuget/`; qualification records
are in `artifacts/package-candidates/0.5.0-dev.54/`.

This combines the accepted [assertion segment](../AssertionSegment-20261107/README.md),
[mixed-width prefix](../CjkPrefixPair-20261107/README.md),
[unmetered PCRE2 Count](../PcreCountValidation-20261107/README.md) and
[controlled Count](../PcreMeteredCount-20261107/README.md) changes. Scalar semantics,
strict whole-input validation, captures/aliases, cancellation/deadlines and useful
resource admission remain. Tight construction caps can need additional literal
preparation work; controlled literal Count uses the existing 2,048-byte candidate
windows and separately charged long comparisons. No field, mode, owner, engine,
unchecked access or shipped dependency was introduced by these changes.

## Packages and consumers

Windows qualification passed in 368.191 seconds: eight detached fixtures/110,300
checks. Linux passed the same checks against the exact Windows package bytes,
without producer rebuilding or repacking. Both platforms also passed original
Awk19/Lython118 checks per target and public producer probes with final root loans
zero. There are 12 Windows and 24 Linux restore audits, 464 original compiler
documents and 12 checked Linux TRX identities. All 288 older staged assets are
unchanged; six new package/symbol files bring the total to 294.

The [dev.53 interruption](../DeliveryDev53-20261107/README.md) remains preserved.
Two existing allocation fixtures now warm complete operations for at least 256
batches/250 ms while retaining exact zero-allocation assertions. No production
algorithm changed in that correction. A transfer-preparation marker was initially
sequenced before its completed Windows consumer marker; it was corrected from
the existing successful consumer/audit records without repeating tests.

## Full refresh and public costs

ONE completed Full passed all 83 steps in **2,787.747 seconds (46m28s rounded)**,
under the unchanged one-hour cap. This includes the Release build, both-runtime
source checks (110,340), disabled-dynamic-code hosts, all catalogs, paired
comparators, lifecycle/scaling, resource checks and verification. The initial
memory preflight stopped before execution; the run began after actual available
RAM recovered above 8 GiB. Servers were retained. Ten matching-binary snapshot
checks passed afterwards. Seven generated files were imported once; README keeps
four generated blocks, six descending tables and all 178 ratios.

ONE exact-package 52-to-54 ABBA completed in **113.719 seconds**: 22 public rows,
440 samples, minimum 49.6214 ms. Identical caller, separate ordinary processes,
one producer per process, eight startup CPUs then CPU2 affinity, 750 ms/minimum
32-call warmup, independent 75 ms calibration and five samples with a 20 ms floor.
All 22 allocation medians are unchanged. All 14 intended gains persist in both
processes with disjoint sample ranges. No confirmation or producer tuning.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Core actual Chinese Count | 86.27 | 81.41 | -5.63% |
| Same finite/live | 131.57 | 126.90 | -3.55% |
| Same deadline/live | 141.37 | 133.84 | -5.33% |
| PythonRe lookbehind miss, finite/live | 0.58 | 0.52 | -10.99% |
| Core dense assertion Count | 172.37 | 137.28 | -20.36% |
| Same finite/live | 184.81 | 153.51 | -16.94% |
| PythonRe dense assertion Count | 199.35 | 147.18 | -26.17% |
| PCRE2 dense assertion Count | 171.45 | 135.39 | -21.03% |
| PCRE2 actual Chinese Count | 1307.68 | 81.33 | -93.78% |
| Same finite/live | 2759.18 | 126.17 | -95.43% |
| Same live token | 2743.16 | 125.40 | -95.43% |
| Same deadline | 2744.39 | 133.19 | -95.15% |

PCRE2 finite extra cost falls from 1451.50 to 44.84 us (111.00% to 55.13%). Useful
controls still have operating cost. Retain the overlapping detailed-capture
+9.51%/+85.99 ns estimate: before-process medians 0.8174/0.9917 us, after
0.9802/1.0117. Other adverse controls overlap: Core ASCII +1.10%, BCL +2.11%,
finite Unicode miss +0.53%, FullMatch +0.48%. Ordinary mail -0.09% overlaps;
finite mail -2.78% is disjoint but is an unintended observation without a proven
causal explanation. [Acceptance](package-cost-acceptance.json) and
[all rows](package-cost-summary.json) retain absolute costs, allocations, every
process median and range. No normalization, composed speedup or universal 10% gate.

## Scope and remaining gaps

The new Full has Core123 wins/1 tie candidate/54 gaps/0 unqualified; PythonRe45
managed faster/31 CPython faster/2 equivalent/7 inconclusive/5 unqualified due to
removable harness floors; PCRE2 74 managed faster/13 native faster/14 inconclusive/
25 excluded. Comparator scopes and uncertainty remain separate.

[Successive catalog movements](catalog-movements.json) retain adverse observations:
AWS compiled +11.38%/+1043.84 us; word-Holmes Count compiled +22.61%/+113.11 us and
ordinary +21.51%/+108.58 us; PythonRe single-capture FindAll +7.84%/+34.39 us.
These are not isolated causal comparisons. Accept the measured overall gains
without erasing these observations; require a distinct live cause before further
implementation. Closed AWS/checked-access experiments remain closed.

All six [canonical raw archives](canonical-archives.json) are hash-verified off VM.
Full52 and Full54 frozen graphs remain intact after Linux/costs; the graph archive
also preserves the ten snapshot checks and the previously verified dev.52 fixture
retirement. No new cleanup or NuGet publication. [Source CI](source-ci.json) passed
on Windows/Linux; the snapshot/receipt commit receives its own CI check.
