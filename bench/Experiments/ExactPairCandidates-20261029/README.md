# Bounded full-needle candidate searches for exact pairs

715488c2 changes the existing raw exact-pair candidate helper, not match
verification. [River](initial-route.txt) and [Holmes](holmes-route.txt) already
use scalar Count with valid distance-zero prefix guidance. The old helper
replayed every common first-byte hit. It now uses the existing earliest-search
algorithm's checked full-needle span primitive for pairs up to 1024 bytes each.
Lookahead stays inside a 2048-byte scan; full miss windows advance at least 1025
starts. Lookahead-only hits are clipped so a short late needle cannot hide an
earlier long needle crossing the boundary. Ordered verification still decides
same-start priority, and the original VM verifies every public match candidate.

Basic work charges remain between scans and before publication, including
unlimited scopes. Larger families, long needles, folded/reverse searches and
confirmation predicates retain their previous paths. No prepared payload,
matcher, unchecked access, resource switch or accounting framework was added.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet Ubuntu VM, Xeon 8573C, .NET 11 RC3, CPU2.
Exact aa021223 baseline versus 715488c2 candidate. One **50.00s** comparison under
a 120s cap: ten public Count workloads, four equal-result lanes, one-second
warmup, independent calibration and three alternating samples. No A/B rerun.

| Public Count | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| River, ordinary, two matches | 20306.100 | 6720.175 | 3552 / 3552 |
| River, compiled, two matches | 19950.833 | 6731.688 | 3552 / 3552 |
| Holmes, seven matches | 825.995 | 803.225 | 3552 / 3552 |
| 32 KiB miss, finite limits/live token | 64.075 | 12.274 | 3664 / 3664 |
| Finite limits/live token, hit | 940.591 | 3.792 | 3664 / 3664 |
| Dense mixed-scalar hits, 128 matches | 158.865 | 157.632 | 3552 / 3552 |
| Dense false first bytes | 1023.440 | 5.645 | 0 / 0 |
| Larger-family control | 74.780 | 79.071 | 3568 / 3568 |
| Finite long-pattern control | 1.697 | 1.695 | 3664 / 3664 |
| Single-literal control | 70.752 | 70.338 | 0 / 0 |

Controls vary -0.6% to +5.7%, with all allocations unchanged. Preserve the
larger-family increase. Holmes (-2.8%) and dense hits (-0.8%) do not demonstrate
a meaningful gain; those costs still need verification-focused work. No Holmes
snapshot was refreshed merely to change provenance. a40bda03 remains the fixed
cumulative release baseline.

The [first selected refresh](refresh-first.txt) was unqualified: one decode/
compiled sample took 19.7683ms, below the unchanged 20ms V3 floor. Both full
snapshots and raw logs remain archived. The single allowed duration confirmation
used requested minimum eight iterations; [all final samples](refresh.txt) exceed
51ms. It qualifies compiled **13.30x -> 4.43x**, still Status=Gap, and ordinary
**0.60x -> 0.20x**, Status=Win, both 3552 B/op. No further confirmation or full
package/catalog matrix was run. [Parity](parity.txt),
[generated Markdown](markdown.txt), [snapshot/API/dependency checks](verification.txt)
and [unselected-row comparison](snapshot-diff.txt) passed. Only river's
ordinary/compiled snapshot and parity rows changed.

Focused Release checks pass on both runtimes: 500 pair/native-family/scalar-
count/work-policy, 118 additional runtime/admission/publication/budget, 209
PythonRe, 554 PCRE2 count/resource/boundary and 16 snapshot/API/dependency checks.
Coverage includes offsets, tails, 1024/1025-byte needle limits, crossing windows,
the unequal-length lookahead ordering witness, same-start branch order, Unicode
scalar gap bounds, NonBacktracking/RTL, retained captures, every work cutoff with
a caller loan, denied VM workspace, malformed tails and original cancellation/
deadlines. Long-needle split interruption remains covered directly.

Immutable archives under `artifacts/experiments/exact-pair-candidates-20261029/`:

- `parity-ordered-window-715488c2.tar.gz`: exact baseline/candidate DLL/PDB/
  driver/bundle, build, probes, comparison, both qualification attempts and final
  snapshots/reports. SHA256
  `BDD27DE45D1495F038806C90D82BA96C8F023829D33EA2F2EA0128224001DE51`.
- `correctness.zip`: focused Core/adapter and snapshot checks. SHA256
  `799E842F9EE57B3CE61A9AAEC8222572BF04D5BF310F80BAFC9EB2A14A01AC5D`.
