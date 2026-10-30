# Native PythonRe templates

Fixed baseline `505bc140`, initial implementation `47d7273e`, final producer
`1a4b4ea5`. PythonRe now accepts strict UTF-8 templates in Replace, Subn and
funded SubnRetained. Native literals, cache keys and output stay bytes; selected
group-name/error lexemes may project text. Existing string conveniences remain.

The first quiet-VM comparison took **59.803s**, eleven cases/28 lanes, under a
120s cap. The sole focused confirmation took **60.672s**, six cases/17 lanes,
under a 90s cap. Release .NET 11 RC3, affinity mask 4; independent lane calibration,
alternating samples and full result-batch comparisons after timing. Regex/template/
subject setup is outside timing. Cold lanes cycle 32 equal-size templates through
the existing 16-entry cache; warm lanes reuse one. Native baseline interoperability
decodes the supplied template per public Subn call. Funded lanes include public
receipt access/disposal and verify stable retained funding plus complete refunds
after program disposal. Numeric finite limits and the live token remain enabled.

Initial warmup was 1.5s/at least 1,000 calls per lane, 80ms calibration, three
samples. Confirmation used 2s/at least 1,000 calls, 150ms calibration, five samples.
Minimum measured samples were **46.018ms / 124.289ms**. These comparisons are
closed; no further confirmations or package/catalog qualification were run.

## Results and adjustment

The initial dual-source parser increased cold text-template cost at ~2KiB UTF-8
by **16.0%**, 21.965 ->25.486us, with unchanged allocation. Final parsing searches
and copies bounded literal runs instead of dispatching through the source form
for each character. Runs retain work/admission/interruption checkpoints and join
split scalars/surrogate pairs in the same fragment before publication. Growth
uses the existing admitted buffers, reducing intermediate arrays.

Final confirmation medians, independently calculated from all five samples:

| Public operation | Baseline (us) | Initial (us) | Final (us) | Baseline -> final B/op |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core literal Count | 0.4140 | — | 0.3810 | 0 ->0 |
| Warm text-template Subn | 2.7398 | 2.4197 | 2.8591 | 6064 ->6064 |
| Cold text-template Subn, ~2KiB UTF-8 | 35.1388 | 35.7677 | 23.4818 | ~32056 ->29760 |
| Cold native template, ~2KiB | 40.9169 via strings | 38.6196 native | 17.7924 native | ~34136 ->31888 |
| Warm native template, finite/live token | 2.4733 via strings | 2.3804 native | 2.3775 native | 6232 ->6184 |
| Cold native template, funded retained output | 4.1449 via strings | 4.3927 native | 4.2863 native | 7784 ->7768 |

Warm text has large timing-level drift: baseline 2.009–3.278us, final
1.977–3.230us. The unchanged control also drifts; its initial-run medians were
0.38119/0.38123us. Neither a precise warm equivalence nor a matching speedup is
established. Large-template final samples consistently improve within the paired
confirmation, but do not compare absolute timings across the two runs as if
environmental variability disappeared. The initial cold funded native penalty
was +11.9%; final confirmation is +3.4%, with 16 fewer B/op.

First-run ordinary warm native templates remove 48 B/op of decoding allocation;
warm funded retained native calls remove 344 B/op, including preparation-owner
work bypassed by borrowed cache hits. Ordinary native cold-small and cold-large
initially add 72/80 B/op against string bridges. Final cold-small ordinary paths
were not separately remeasured. All initial costs remain in the raw receipt.
Minor fractional allocation readings in a few confirmation cold lanes are
preserved; the table rounds those readings rather than inventing exact integers.

## Contracts and validation

- One grammar; full strict UTF-8 template validation precedes grammar/matching,
  including no match and negative count. Original Python escape/backreference,
  unmatched/empty capture and empty-match progression rules remain. Native
  grammar errors retain UTF-16 Position from the failed prefix; text fragments
  still repair unpaired UTF-16 surrogates independently.
- Cold byte keys have an invocation working loan before allocation, held through
  existing cache admission. Parser capacity includes that overlap. Warm queries
  borrow spans. Text/native keys share one bounded cache with independent
  representations; earlier eviction is intentional. Failed templates are not
  retained. No second cache, fallback, lifetime owner or accounting framework.
- The existing cache Read path provides pending waits and funded active uses.
  Eviction/disposal cannot refund active users. The original token/deadline,
  foreign pending-failure isolation and numeric limits remain authoritative.
  Retained output uses the existing independent receipt; escaped bytes remain
  valid after receipt release under caller admission.
- Final full PythonRe Release **4,368 tests per runtime** pass on .NET 10/11.
  Affected Core cache/replacement/API checks: **53 per runtime**. Tests cover
  byte starts, scalar/surrogate block boundaries, malformed tails, grammar errors,
  work/byte denial, interruption/refunds, concurrent native reuse, detached
  pending users and retained output lifetime. API snapshots and specifications
  document the three byte-template methods. An initial new test expected the
  wrong exact exception type at a mid-scalar start; only its expectation changed
  to the existing ArgumentOutOfRangeException contract.
- Initial source CI `41098299856` has passed Linux, both runtimes and disabled
  dynamic code; Windows is still running at this receipt checkpoint. Final
  literal-run adjustment has both-runtime focused/full local coverage; latest
  source CI remains a separate pending check.

## Evidence and remaining work

[First raw comparison](comparison.json), [sole confirmation](confirmation.json),
[first driver](Compare.cs.txt), [confirmation driver](Confirm.cs.txt).
The confirmation's Scope sentence about "both old/new controls" is inherited
from the first driver: native confirmation rows actually compare the baseline
string bridge with initial/final native lanes. Their explicit lane arrays and
driver calls are authoritative; no final candidate string-bridge lane was run.

Immutable exact DLL/PDB, measured drivers, raw samples, preflight/build/runtime
records and binary hashes:
`artifacts/experiments/native-python-templates-20261030/utf8regex-python-templates-1a4b4ea5.tar.gz`.
SHA-256: `59A1BD452C4432545042F6061B9E42ADFE3DC6926C13C4B8F6FF6A364060A515`.
Companion `correctness-source-logs.zip` preserves source bundles and local
focused/full/intermediate failure records.
SHA-256: `BEBD724969A68040341FA133807DE38D4F5AEA5E4D24BFA9F2B16E17C3082D92`.

Core/PCRE2 direct template/cache preparation, remaining funded replacement flows,
final immutable packages/actual consumers, current-source full refresh and
cumulative release operating cost remain delivery work. No published/staged
package bytes or benchmark catalogs changed. This is selected public-operation
evidence, not a catalog qualification, cumulative baseline or package acceptance.
