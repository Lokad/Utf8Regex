# Class/whitespace candidate windows with finite scalar gaps

16b85ecb extends the existing scalar Count candidate window to finite leading
class runs and a finite all-scalar gap before its exact ASCII literal. The
[initial probe](initial-route.txt) confirmed that mixed context input uses scalar
execution: the prepared 'Result' literal has Distance=0 but no authority to guide
match starts. Its native token-window plan cannot supply byte-gap matching for
Unicode scalar semantics.

The existing prepared search now locates the literal; the original invocation
meters backward scalar-gap, whitespace and class scans. A finite class maximum
bounds that rewind too. The window is conservative: every retained start is
verified by the unchanged VM, including capture-sensitive later starts. Greedy
selection, non-overlap progression, captures, offsets and publication remain VM
decisions. Unsupported shapes, RTL, folding and NonBacktracking keep their prior
routes. No public API, prepared payload, matcher, unchecked access or accounting
framework was added. Numerical work charges remain route/version dependent as
already documented in the Core specification.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt), [initial probe](probe.cs.txt), [probe project](probe.csproj.txt).
Quiet Ubuntu VM, Xeon 8573C, .NET 11 RC3, CPU2. Exact 3258ff25 baseline versus
16b85ecb candidate. One **57.54s** comparison under a 120s cap: nine public Count
workloads, four equal-result lanes, one-second warmup, independent calibration
and three alternating samples. No confirmation or complete package/catalog matrix.
The initial probe intentionally targets the baseline's private helper signature.

| Public Count | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Context, ordinary, 53 matches | 1073031.100 | 21441.967 | 10592 / 10592 |
| Context, compiled, 53 matches | 1062973.800 | 20611.467 | 10592 / 10592 |
| 32 KiB miss, finite limits/live token | 5524.111 | 359.838 | 8888 / 8888 |
| Finite limits/live token, hit | 2802.161 | 14.323 | 3696 / 3696 |
| Dense mixed-scalar hits, 128 matches | 4912.691 | 3196.275 | 5400 / 5400 |
| Dense false anchors | 701.231 | 738.247 | 1712 / 1712 |
| Real Ruff control | 4598.245 | 4571.545 | 3600 / 3600 |
| Class-prefix control | 3.441 | 3.380 | 3552 / 3552 |
| Literal control | 70.578 | 70.809 | 0 / 0 |

Unchanged controls vary -1.8% to +0.3%; dense false anchors are +5.3%, with all
allocations unchanged. Preserve that affected-path penalty. Dense hits improve
35% but remain far from parity (3.2ms versus 43us predecoded .NET in this A/B);
the pilot does not establish dense-match parity. No exact equivalence claim or
favorable rerun. a40bda03 remains the fixed cumulative release baseline.

The five-sample [selective refresh](refresh.txt) qualifies compiled **15.86x ->
0.31x** and ordinary **7.46x -> 0.15x** predecoded .NET 11, both Status=Win,
10592 B/op. Every V3 sample exceeds 20ms. [Parity](parity.txt),
[generated Markdown](markdown.txt), [snapshot/API/managed-dependency checks](verification.txt)
and [unselected-row comparison](snapshot-diff.txt) passed. Only context's
ordinary/compiled snapshot and parity rows changed.

Focused Release checks pass on both runtimes: 75 gap/class/whitespace/publication
checks, 364 additional Core candidate/work/budget checks, 209 PythonRe and 554
PCRE2 count/resource/boundary checks, plus 16 snapshot/API/dependency checks.
New coverage compares independent scalar execution at every scalar floor and
both 0/1/99/100/101-scalar gap boundaries, including emoji, Unicode class aliases,
overlapping literals, finite/unbounded/lazy controls, retained captures, parent
scratch loans at every work cutoff, long-class/working admission, malformed tails
and original cancellation/deadlines. One initial expected result was corrected:
neighboring whitespace repeats can absorb newline input beyond the dot-gap cap.
Both attempts and the final finite-class rewind checks remain archived.

Immutable archives under `artifacts/experiments/bounded-scalar-gap-20261029/`:

- `parity-context-16b85ecb.tar.gz`: exact baseline/candidate DLL/PDB/driver/bundle,
  build, inspection, comparison, final snapshots/reports and next-route inspection.
  SHA256 `A1527720C52AABF6068E1D8E12DC61E93573A46902A64613752B3028617F229F`.
- `correctness-attempts.zip`: initial/corrected/final Core, adapter and snapshot checks.
  SHA256 `D9D71F2A89E4034FC886FDB2A5772916D232085BB7D6F2E2DDC24B5B3476BFFA`.
