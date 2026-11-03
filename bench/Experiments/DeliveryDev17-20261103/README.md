# Dev.17 fixed-original gate: three remaining cost failures

Frozen common `0.5.0-dev.17` source
`347b7845c5062deddb61d029290213248fdd85b0` includes the accepted
[value publication](../PythonValuePublication-20261103/README.md) and
[empty literal confirmation](../EmptyLiteralConfirmation-20261103/README.md)
changes. CI `41658342576` is green on Windows/Linux.

The [complete assessment](cumulative-acceptance.json) is **false**. No dev.17
package qualification, consumer run or Full refresh was launched. Immutable
dev.14 remains the canonical staged candidate; nothing was published.

All **217** named outputs and loaded identities preflighted: existing 172 cases,
mail 17, retained entry 26 and two explicit Python Match calls. The original
baseline remains `a40bda03`, archive SHA256 `333FF4E5...`. Valid completed parts
total **995.418s**, minimum sample **26.441ms**, five alternating samples per
producer, unchanged allocations. Diagnostic .NET comparators are separate lanes.

| Failing public operation | Original us | Dev.17 us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| Core compiled Count, ASCII repeated capitalized words | 13.989679 | 17.145111 | **+22.56%** | 0 |
| Core Count, bounded gap before `Result` | 68.288785 | 76.140103 | **+11.50%** | 3,568 |
| PythonRe Count, nullable `x*\|y` | 21.510101 | 23.946796 | **+11.33%** | 3,552 |

Every pair exceeds 10%: respectively 22.19–23.55%, 11.16–11.58%, and
10.94–11.68%. No further confirmation or unchanged-gate replay is justified.
The earlier Python Search failure passes on this source; that does not override
these failures or relabel earlier evidence. Next work traces the actual largest
live route before a bounded correction.

The first attempt stopped on an invalid **19.5016ms** union/miss candidate
sample, below the unchanged 20ms minimum. One permitted correction raised only
that diagnostic driver's independent calibration target from 60ms to 90ms
(decode comparator stays 100ms). Same workloads, outputs, options, producer
binaries, warmup, affinity and five-sample protocol; no library rebuild.
The completed first 43 cases were reused byte-for-byte. Only the failed union
part was repeated; all remaining parts ran once. Invalid partial union samples
were not pooled into the valid result. The resumed wall clock is 823s; the valid
part total above also includes the reused first two parts. Neither is a claim
of one uninterrupted end-to-end run. The existing assessor reproduced the
false 217-case result locally without timing again.

Immutable raw evidence in `artifacts/experiments/delivery-dev17-20261103/`:

- `initial-cumulative.tar.gz`, SHA256
  `E6B999E222D6562EBCF17C3B7132E49B6FEE0D8ABD6D23EBA9D8C4C5FA162A1F`.
- `resumed-cumulative.tar.gz`, SHA256
  `7CEE560FFD9C97C95115C6DA1068CBB6AEE337F44516834E458AD8E95B29B36A`.

These retain original/candidate DLLs/PDBs, drivers, hashes, source, preflight,
results/allocations, the invalid attempt and explicit calibration adjustment,
scripts/exits and assessment. The reused cost and PCRE2 result hashes remain
`27B59D07240C6258E2FE8121AE9F91141257FA4916F86DCF9F170906F4DD23ED`
and `77F098050160E7A729F94A838739BAA727E5DE3EE6034B9CC56FA0280BD089FB`.
Consumer preparation changed only three exact version pins across the original
1,565 files; prepared input is not qualification or downstream adoption.
