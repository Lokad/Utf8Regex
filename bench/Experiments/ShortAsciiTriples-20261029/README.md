# Deferred short ASCII triple search

**Reverted; no production or snapshot change is promoted.** Prototype 71026a08
let short ASCII triples reuse the existing packed discriminator/anchor search.
It retained the existing 24-byte Earliest threshold, larger ASCII-family routes,
ordering/eligibility checks and conservative table admission. No search state,
grammar, unchecked access or ownership mechanism was added.

[Initial attribution](initial-attribution.txt) put prepared discovery at about
106us of 109us public keyword Count, but these cold diagnostic samples are not
qualified throughput. The production family is record/struct/class, three
distinct initial bytes / seventeen literal bytes.

[Public A/B](comparison.json), [console](comparison.txt), [driver](driver.cs.txt)
and [project](driver.csproj.txt) compare exact 362ba340 and 71026a08 binaries on
the quiet VM, .NET 11 RC3, CPU2. One **36.59s** run under a 120s cap, thirteen
public workloads: 500ms warmup, independently calibrated lanes and three
alternating samples; minimum timed sample **27.55ms**. Equal-result preflight
and timing checks pass. Matching uses both predecoded and decode-then-.NET lanes;
the construction row compares only producer constructors through identical
off-clock-generated factory delegates. No confirmation or second campaign.

| Public operation | Before us | Candidate us | Before / candidate B/op |
| --- | ---: | ---: | ---: |
| Keyword Count, ordinary | 75.001 | 75.110 | 0 / 0 |
| Keyword Count, compiled | 74.473 | 74.677 | 0 / 0 |
| 32 KiB, finite limits/live token | 229.682 | 220.224 | 3664 / 3664 |
| Dense ASCII Count | 17.733 | 17.734 | 0 / 0 |
| Dense Unicode Count | 253.213 | 243.020 | 3552 / 3552 |
| Dense false initial-byte candidates | 19.637 | 19.607 | 0 / 0 |
| Sparse miss, 64 KiB | 7.301 | 7.340 | 0 / 0 |
| Overlapping literal family Count | 10.533 | 10.519 | 0 / 0 |
| Long-triple control | 1.472 | 1.475 | 0 / 0 |
| Four-literal control | 3.470 | 3.462 | 0 / 0 |
| Folded control | 92.167 | 89.345 | 3552 / 3552 |
| Literal control | 73.386 | 73.404 | 0 / 0 |
| Repeated constructor | 189.000 | 119.625 | 113776 / 117648 |

The targeted ordinary/compiled public Count costs differ by only +0.1%/+0.3%.
Dense ASCII and false-prefix workloads are effectively unchanged. Finite and
Unicode points are -4.1%/-4.0%, while an unchanged folded control is -3.1%; do
not promote these as isolated matching gains. Construction is faster (-36.7%)
but acquires another **3872 B/instance**. That does not justify more metadata for
this matching-parity objective without a concrete construction-heavy consumer.

[Both-runtime checks](verification.txt) passed focused Core exact-family,
wrapper/map admission, public ordering/reverse/overlap/offset/UTF-8, work/depth/
working/cancellation guards, PythonRe finite literal/cursor work policies and
PCRE2 direct/Count routes. Existing route assertions were updated during the
prototype, retaining Trie coverage through an unchanged four-literal family;
their initial expectation failures remain archived. These checks do not prove
the desired performance gain.

**be92d14a** reverts the selector, its dedicated tests and changelog claim.
`src/`, `tests/` and CHANGELOG.md match accepted a7cdf47b exactly, which passed
CI 40906235630 on Windows/Linux, both runtimes and disabled dynamic code. The
qualified **9.67x** keyword gap stays visible. Broader candidate-search work is
deferred for the current bounded pass; do not reopen this packed-selector trial.
No README row, package or full qualification matrix was refreshed.

Immutable files under `artifacts/experiments/short-ascii-triples-20261029-deferred/`:

- `parity-short-triples-71026a08-deferred.tar.gz`: exact baseline/candidate
  DLL/PDBs, host, source bundle, driver/binaries and build/preflight/raw evidence.
  SHA256 `434498F1F430F32DE80456A5636D7278D1EC8F24725AD8A77E8CC66EC0EA9D11`.
- `correctness-and-revert.zip`: initial/final checks, prototype fixture,
  diagnostic attribution and empty accepted-source diff after revert. SHA256
  `198822D60CDC58C9C9A6BD0B28FB3203D3B32A4B3F64B8DEEE8B527791C71882`.
