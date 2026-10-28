# Reuse the admitted scalar URI scan for IsMatch

8c31ab39 extracts the existing Count scan's next-token operation and reuses it
inside the owned IsMatch invocation. 2881306e updates its benchmark diagnostic
consumers after the executor rename. No new grammar, prepared payload, engine or
execution mode was added. Count retains its non-overlap progression, complete
body consumption and original checkpoint charges.

Eligibility retains the existing complete URI proof: Unicode word scheme,
two required complete body scalars, no captures, ordinary forward case-sensitive
execution from zero. Nonzero starts, captures, RTL, NonBacktracking, IgnoreCase
and ECMAScript keep their previous scalar routes. Whole-subject validation runs
first; finite work, original token/deadline and publication remain authoritative.
Like native Count, Boolean existence needs no VM scratch/result allocation and
can succeed under a one-byte working/result cap. Numerical resource charges are
route/version dependent under the existing SPEC.md contract.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; immediately prior
97b3f20e versus 2881306e. Four independent public-operation lanes, 250 ms warmup,
three alternating approximately 50 ms samples. Equal-result preflight and timed
results pass. One valid comparison took **15.07 seconds** within its 120-second
cap; no confirmation. a40bda03 remains the fixed cumulative release baseline.

| Public operation | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| URI miss, compiled | 4.885 | 0.162 | 30.1x faster | 3600 / 0 |
| URI hit, compiled | 3.137 | 0.177 | 17.7x faster | 3600 / 0 |
| URI miss, finite limits and live token | 5.614 | 0.340 | 16.5x faster | 3712 / 112 |
| 4 KiB delimiter-free miss | 0.698 | 0.729 | +4.5% elapsed | 0 / 0 |
| URI after 4 KiB prefix, mixed-scalar hit | 81868.800 | 0.806 | Repeated scalar attempts avoided | 3600 / 0 |
| Full URI Count control, 6,839,410 bytes / 5301 results | 1953.623 | 1964.031 | +0.5% elapsed | 0 / 0 |
| Unchanged English literal IsMatch | 44.110 | 44.190 | +0.2% elapsed | 0 / 0 |

The first ordinary-miss row drifted during warmup: candidate samples
3.605/2.382/0.187 us, baseline 5.211/10.184/5.873 us. Retain these raw results;
they do not establish a stable ordinary-lane speedup. Acceptance rests on the
stable compiled/finite comparisons and controls, not selecting its final sample.
The large mixed-scalar improvement is specific to this repeated-start witness,
not a general URI throughput multiplier.

The independent five-sample generated README refresh reports miss
**72.81x -> 2.62x compiled**, **24.88x -> 0.81x ordinary**; hit
**29.56x -> 1.90x compiled**, **11.30x -> 0.71x ordinary**. These costs/verdicts
stay separate from the paired comparison. [Miss refresh](uri-refresh-miss.txt),
[hit refresh](uri-refresh-hit.txt), [verification](uri-verify.txt).

[Both-runtime Core checks](core.txt) cover complete scalar minima, BMP and
supplementary schemes/bodies, query/fragment tails, offset and padded subjects,
Count/Match/capture agreement, malformed tails, each small work cap, bounded
delimiter/scheme/body/failed-candidate interruption, original-token cancellation
from the final deadline poll, and unchanged Count resource checkpoints.
[PythonRe](python-net11.txt) and [PCRE2](pcre-net11.txt) affected public/resource/
cancellation checks pass. No public API or grammar contract change.

One preliminary build missed two renamed diagnostic references; a premature
driver run consequently loaded the old candidate bytes. That attempt is invalid
and excluded from performance evidence. Its raw output/build failure is retained,
not relabeled as a candidate comparison. The corrected driver checks distinct
hashes and the expected candidate revision before any timing. An incorrectly
quoted refresh command entered the benchmark selector without measuring cases;
the subsequent file-based command performed the refresh above.

Exact baseline/candidate DLLs and PDBs, drivers, measurements, generated snapshots
and invalid-attempt records are retained in the
[immutable archive](../../../artifacts/experiments/uri-existence-20261028/parity-uri-existence-2881306e.tar.gz),
SHA256 `9CADF25C86EEAEAD282601F6AA22DE7A21184CFBD3601FE5BEC357AA5E6B4981`.
No full catalog or package matrix was repeated.
