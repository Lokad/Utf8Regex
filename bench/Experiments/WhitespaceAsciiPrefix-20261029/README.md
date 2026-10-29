# Mandatory ASCII prefixes after nullable whitespace

3258ff25 narrows the existing Count candidate window after a nullable whitespace
repeat. [Inspection](ruff-proof.txt) found 116678 '#' starts in real Ruff's
32.5 MB subject, but only 20 complete '# noqa' prefixes. The previous route
verified ordinary comments in the scalar VM. A bounded straight-line filter now
checks mandatory ASCII literals/positive sets before rewinding. Captures are
transparent; branches, wider classes and other effects stop the optional filter.
The VM still verifies every possible start in each surviving whitespace window:
references or assertions can make a later start win. No prefix accepts a match.

The existing 2048-byte checked candidate search now also serves finite/token
whitespace-only discovery. Scalar whitespace rewinds use the original invocation;
each filter inspects at most 64 instructions. Count's terminal charge and
publication checks remain. No prepared payload, matcher, framework, unchecked
access, public API or resource switch was added. RTL, folding, NonBacktracking,
other result operations and the existing class-prefix route retain their fences.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt), [inspection probe](probe.cs.txt), [probe project](probe.csproj.txt).
Quiet Ubuntu VM, Xeon 8573C, .NET 11 RC3, CPU2. Exact 7da785da baseline versus
3258ff25 candidate. One **50.31s** comparison under a 120s cap: nine public Count
workloads, four equal-result lanes, one-second warmup, independent calibration
and three alternating samples. No confirmation or full package/catalog matrix.

| Public Count | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Real Ruff, ordinary, 20 matches | 420217.700 | 4804.418 | 3600 / 3600 |
| Real Ruff, compiled, 20 matches | 414432.400 | 4817.164 | 3600 / 3600 |
| 32 KiB miss, finite limits/live token | 7482.886 | 4.564 | 3712 / 112 |
| Dense mixed-scalar hits, 1024 matches | 1003.948 | 788.844 | 3600 / 3600 |
| Dense false prefixes | 756.897 | 21.979 | 3600 / 0 |
| Capture-sensitive later start | 1.598 | 1.613 | 3872 / 3872 |
| Tweaked Ruff control | 12822.875 | 13883.725 | 3600 / 3600 |
| Class-prefix control | 3.533 | 3.435 | 3552 / 3552 |
| Literal control | 70.593 | 70.149 | 0 / 0 |

Controls vary -2.8% to +8.3%, with unchanged allocations. Retain the tweaked
control's higher cost; no exact equivalence claim or favorable rerun. The finite
timing is a miss, not evidence for finite-hit throughput; funded successful
matching is covered by correctness/resource tests. a40bda03 remains the fixed
cumulative release baseline.

The five-sample [selective refresh](refresh.txt) qualifies compiled **20.05x ->
0.21x** and ordinary **6.03x -> 0.06x** predecoded .NET 11, both Status=Win and
3600 B/op. Every V3 sample exceeds 20ms. [Parity](parity.txt),
[generated Markdown](markdown.txt), [snapshot/API/managed-dependency checks](verification.txt)
and [unselected-row comparison](snapshot-diff.txt) passed. Only real Ruff's
ordinary/compiled snapshot and parity rows changed.

Focused Release checks pass on both runtimes: 63 whitespace/class-prefix/
publication/count-budget checks, 364 additional Core candidate/work/budget checks,
209 PythonRe and 554 PCRE2 count/resource/boundary checks, plus 16 snapshot/API/
dependency checks. Coverage includes every scalar floor, padded spans, window
edges, Unicode whitespace/wider classes, branches, references and retained capture
context, every discovery work cutoff with a parent scratch loan, working denial,
malformed tails and original cancellation/deadlines after validation. One initial
fixture mistakenly applied Count's tight work budget to unchanged Match execution;
its capture assertion now uses a small subject. Both attempts remain archived.
The inspection probe's initial wrong working directory was corrected before the
comparison; no timed result was discarded.

Immutable archives under `artifacts/experiments/whitespace-ascii-prefix-20261029/`:

- `parity-ruff-3258ff25.tar.gz`: exact baseline/candidate DLL/PDB/driver/bundle,
  build, inspection, comparison, final snapshots/reports and next-route inspection.
  SHA256 `D474DEEC029A3F490B3CB17E3869AAC33D503A2A215166C774D6B9F3484A8BDA`.
- `correctness-attempts.zip`: initial/corrected Core, adapter and snapshot checks.
  SHA256 `A1DEBD667DAAB3FB5D9F3350E820F325ECF5885E450C7D73BB79D771B3CA1C82`.
