# Reuse admitted dotted-decimal discovery for Boolean matching

d020df64 reuses the existing complete ASCII language proof and candidate verifier
inside owned Boolean discovery after full UTF-8 validation. Candidate windows
admit at most 2,034 starts plus 14 lookahead bytes. The first hit ends discovery;
publication checks the original work/token/deadline authority. No capture
workspace, parser, matcher or mode was added. Count's implementation is unchanged;
captures, nonzero offsets, RTL/NB and wider class aliases retain existing routes.

[Initial route](initial-route.txt), [comparison](comparison.json),
[console](comparison.txt), [driver](driver.cs.txt), [project](driver.csproj.txt).
Quiet VM, .NET 11 RC3, CPU2, exact bb37b029 baseline versus d020df64.
One **53.73s** comparison under a 120s cap: eleven public workloads, four
equal-result lanes, one-second warmup and three alternating samples. The existing
catalog supplies the subjects. Counts are consumed and checked as integers.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Catalog hit, ordinary IsMatch | 1.257 | 0.124 | 3568 / 0 |
| Catalog hit, compiled IsMatch | 1.219 | 0.125 | 3568 / 0 |
| Legacy "miss" case, compiled IsMatch | 1.233 | 0.125 | 3568 / 0 |
| Finite limits/live token, hit | 1.334 | 0.144 | 3680 / 112 |
| Finite limits/live token, legacy "miss" | 1.418 | 0.150 | 3680 / 112 |
| Unicode prefix with 4 KiB gap | 736.546 | 0.715 | 3568 / 0 |
| Genuine dense partial misses | 908.418 | 13.232 | 3568 / 0 |
| Unchanged Count control | 0.071 | 0.075 | 0 / 0 |
| Unchanged dense Count control | 3.339 | 3.340 | 0 / 0 |
| Unchanged finite Count control | 3.379 | 3.474 | 112 / 112 |
| Unchanged literal IsMatch control | 0.223 | 0.226 | 0 / 0 |

Control variation is +0.05% to +4.94%, with unchanged allocations; exact cost
equivalence is not claimed. No confirmation was needed. a40bda03 remains the
fixed cumulative release baseline.

**Label correction:** `common/ip-miss` uses `012.200.033.19A`. The unanchored
pattern accepts `012.200.033.19`, so every lane returns true. Its inherited ID
and subject remain intact; these are equal-result positive measurements, not
negative-path evidence. The genuine dense-miss row returns false. README.md now
states this limitation; no historical measurement is reinterpreted or renamed.

Selective refreshes of [hit](hit-refresh.txt) and [legacy miss](miss-refresh.txt)
qualify compiled ratios **23.48x -> 2.55x** and **23.56x -> 2.43x**, respectively;
both retain Status=Gap. Ordinary ratios are **0.69x/0.66x**, Status=Win. All
allocate zero bytes and every V3 sample exceeds 20ms. [Parity verification](parity.txt),
[Markdown generation](markdown.txt) and [snapshot/managed-dependency checks](verification.txt)
passed. Every unselected JSON case/parity row was checked unchanged.
[Public runtime/route/construction guards](runtime-guards.txt) also pass on both runtimes.

Focused Release checks pass on both runtimes across Core/PCRE2/PythonRe. New
coverage includes padded/scalar-prefix windows, crossing tokens, Unicode digit
aliases, independent scalar discovery, excluded modes/captures, retained results,
offsets, every work cutoff with a parent loan, full-validation work authority,
malformed tails and original cancellation/deadlines/final publication. No package
or complete catalog qualification was repeated.

Immutable local archives under `artifacts/experiments/ip-boolean-20261029/`:

- `parity-ip-boolean-d020df64.tar.gz`: exact DLL/PDB/driver/bundle, build,
  comparison and final snapshots/reports. SHA256
  `D7FCD01C885B559B77BA7D65CF2AC9FEDF66F4E972A3FADC55A0BDE36A84034F`.
- `correctness.zip`: focused Core and adapter checks. SHA256
  `9B01DEA78DBEA949141F616ED78F268DAB3473879545D3F4CF74DB6499BB8494`.
