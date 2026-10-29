# Reuse complete simple branches for captured ASCII Boolean searches

bb37b029 admits Boolean searches that previously entered scalar execution solely
because captured atoms had Unicode semantics. After full validation proves an
ASCII subject, the existing complete ordered branch plan decides existence.
Mixed subjects retain the scalar VM under the same invocation. Capture-sensitive
effects, atomic groups, boundaries, folding, RTL and NonBacktracking keep existing
routes; Match/Count/results are unchanged.

The existing repeated-digit whole-subject helper can prove a hit when its complete
maximum length is at most 32 bytes and the subject fits that envelope. Original
work/token/deadline checks surround that bounded call. Other searches reuse the
existing controlled branch finder. No grammar, matcher, API or mode was added.

[Initial route](initial-route.txt), [plan inspection](plan-inspection.txt),
[comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt)
and [project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2, exact 059c3127
baseline versus bb37b029. One **48.79s** public comparison, capped at 120s:
four equal-result lanes, one-second warmup, independent calibration and three
alternating samples. No confirmation or full package/catalog run was needed.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Credit-card IsMatch, ordinary | 1.547 | 0.083 | 3888 / 0 |
| Credit-card IsMatch, compiled | 1.485 | 0.083 | 3888 / 0 |
| Finite limits and live token | 1.670 | 0.130 | 4000 / 112 |
| Match after 8 KiB prefix | 1116.738 | 79.928 | 3888 / 0 |
| Dense partial misses | 1194.610 | 101.909 | 3888 / 0 |
| General captured branch plan | 1.422 | 0.127 | 3872 / 0 |
| Mixed-Unicode scalar control | 1.686 | 1.652 | 3888 / 3888 |
| Unchanged Count control | 1.310 | 1.371 | 1696 / 1696 |
| Unchanged Match control | 1.376 | 1.371 | 3888 / 3888 |
| Unchanged literal control | 0.232 | 0.208 | 0 / 0 |

Control variation is -10.1% to +4.7%, with unchanged allocations; no control
speedup or exact cost equivalence is claimed. Finite-policy costs remain distinct
from unlimited costs. a40bda03 remains the fixed cumulative release baseline.

The [selected six-lane refresh](refresh.txt) qualifies compiled credit-card
IsMatch at **1.52x** predecoded .NET 11, previously **26.35x** (Status=Gap);
ordinary is **0.51x** (Status=Win). Both allocate zero bytes. Every sample exceeds
20ms. [Parity verification](parity.txt), [Markdown generation](markdown.txt),
and [snapshot/managed-dependency checks](verification.txt) pass. Every other JSON
case/parity row was checked unchanged. Reporting-only 9d2891cd displays tiny
positive ratios with two significant digits; the unchanged balancing ordinary
row now reads 0.0017x rather than 0.00x. Ordering still uses unrounded values.

Focused Release checks pass on .NET 10 and 11: simple-pattern regressions,
construction, Unicode/NB/capture behavior, Core work policies and relevant
PCRE2/PythonRe ownership and finite-policy entry points. New tests compare public
operations to direct scalar execution across Unicode digits, anchors, branches,
excluded effects and modes. They cover padded spans, captures/retained results,
offsets, malformed tails, every short-kernel work cutoff, original interruption
after validation and final publication cancellation. An initial direct-VM witness
omitted the original contiguous-anchor origin; corrected tests retain that case.
A whitespace fixture outside this admission remains in scalar comparisons.

Immutable local archives under `artifacts/experiments/captured-simple-20261029/`:

- `parity-captured-simple-bb37b029.tar.gz`: exact DLL/PDB/driver/bundle, build,
  comparison and final snapshots/reports. SHA256
  `C39F4378C5B6EE53186BE27E7F265B84C49A1F9E12638600B9746326BE2E51F7`.
- `correctness-attempts.zip`: initial and corrected checks, final Core branch
  regressions and adapter checks. SHA256
  `D78F9140F1EB67D6BA2E9ED494D932926542F8FCFA720A7BF6253B8805CB9BD6`.
