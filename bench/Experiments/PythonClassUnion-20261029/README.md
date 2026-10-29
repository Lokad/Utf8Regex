# Python Unicode character-class union lowering

**Accepted, source 5486db2e.** Compatible positive operands with exact ASCII masks
and pinned scalar predicates lower to the existing CategorySet instruction.
Mail's program drops from 24 to 15 instructions, removing per-scalar Fork/Jump
work and enabling existing single-class repeat handling. General class programs,
complemented operands and unresolved folding retain their original lowering.
Whole-class negation applies to the complete union. No new matcher, hint authority,
cache, public API, unchecked access or resource/ownership contract.

[Corpus census](census.txt) and [original IR](initial-instructions.txt) established
the live cause before coding: 6,839,410 UTF-8 bytes, 482,693 non-ASCII scalars,
834,237 leading-class runs, 2,856 @ bytes and 92 matches. Theoretical repeat probes
are a diagnostic estimate, not a measured phase attribution. [Candidate IR](candidate-instructions.txt)
retains scalar dispatch; an existing ASCII-only range hint becomes eligible from
the simpler node shape. No hint is admitted for mixed input.

[Initial A/B](comparison.json), [console](comparison.txt), [confirmation](confirmation.json),
[console](confirmation.txt), [driver](driver.cs.txt), [project](driver.csproj.txt):
exact ea44ad1e and 5486db2e DLLs in symmetric load contexts, quiet VM CPU2,
.NET 11 RC3, independent calibration, three alternating samples. Initial run
**63.89s**, minimum timed batch **24.38ms**, 500ms warmup. One **39.54s**
confirmation with 1s warmup covers the target and unchanged controls because
the first complement control varied +10.9%. No further comparison.

| Public Count | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Actual mail corpus | 2527608.9 | 862406.0 | 7200 / 3568 |
| Same corpus, finite limits/live token | 2740288.8 | 1002960.8 | 7312 / 3680 |
| Dense ASCII hits | 324.65 | 156.38 | 3568 / 3568 |
| Dense Unicode hits | 202.47 | 136.26 | 3568 / 3568 |
| Finite Unicode hits/live token | 238.54 | 144.62 | 3680 / 3680 |
| Genuine miss runs | 1795.17 | 360.59 | 3568 / 1696 |
| Long word miss | 11226.56 | 896.71 | 39472 / 1696 |

Confirmation retains mail **-66.1%**, finite Unicode **-38.3%**, unchanged
allocations. Preserve control costs: complement **+9.6%**, general class program
**+9.5%**, Core boundary **+7.0%**, literal **+8.6%**. Their original values were
+10.9%, +9.7%, +5.2%, +0.8%. No blanket equivalence or universal <=10% claim.
Folded-ASCII literals already represented by an unfolded mask can also combine;
the folding control improves while Python predicates keep their lexical meaning.

[Official CPython expectations](cpython-expectations.txt), [script](expected.py.txt)
and [public preflight](preflight.txt) verify counts before timing, including pinned
membership and declines. .NET predecoded/decode lanes cover overlapping controls;
Unicode/dialect-incompatible rows are explicitly excluded from those diagnostics.
The existing CPython worker remains the authoritative profile comparator.

[Raw refresh](raw-refresh.txt) and [nine-pair qualification](paired-refresh.txt):
qualified CPython ratio **7.936x -> 2.451x [2.447,2.471]**, still CPython faster.
Managed median **866056.1us**, CPython **352960.8us**, allocation **3568 B**, CPU7.
Warmup/order/spread/harness and exact-source/binary gates pass. Raw managed
**871391.6us** is separate. Historical comparison spans earlier shared changes;
the direct A/B isolates this lowering. [Scope](snapshot-scope.txt) proves only
corpus/mail-email-count changed; other rows/lifecycle/scaling remain untouched.
[Generated page](markdown.txt) verifies. No global stale-evidence rerun.

[Release checks](verification.txt) preserve pinned Unicode15.1, folded/complement/
class-program declines, whole negation, scalar offsets, captures/aliases, empty
progression, malformed tails and useful finite/interruption admission on both
runtimes. Full PythonRe and focused Core/PCRE2 guards pass. No package matrix.

Immutable archives under artifacts/experiments/python-class-union-20261029/:

- parity-python-class-union-5486db2e.tar.gz: exact producer/baseline DLL/PDBs,
  benchmark host, source bundle, probes/driver, raw measurements and snapshots.
  SHA256 `B916F7AC6A8FF88273A5C24565158904F6B7E73ECF0C007D23E7D8D76626E082`.
- correctness-logs.zip: SHA256
  `8D80BF79E1F2D01C81F1CC4E8293A64FB9DEF61837274D107D21DF74014D5AF6`.

Initial remote archive creation hit disk capacity; it is an incomplete attempt,
not a qualified artifact. The successful archive streamed directly to the local
workspace and passed a complete tar inventory read. No measurements were rerun.
a40bda03 remains the cumulative release baseline.
