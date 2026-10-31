# Reuse category admission for Unicode-letter Count

Accepted source `9a79aefc`; frozen baseline `4c1b3183`. Whole-expression letter
Count now uses the existing bounded category-counter envelope and existing owned
letter primitive. Both ordinary and caller-funded Count retain strict whole-input
validation, original-token cancellation, direct deadline checks before each
scalar-ended block of at most 4096 bytes, and final publication checks. No new
proof, matcher, field, policy, execution mode or unchecked access.

This avoids repeated VM execution for each letter. Storage stays constant and
native blocks retain meaningful work admission, even for unlimited calls.
[SPEC](../../../docs/SPEC.md) and [migration guidance](../../../docs/MIGRATION.md)
explicitly describe lower route-dependent work/storage costs and tighter storage
policies that can now admit Count. Detailed captures and NB/RTL routes remain.

[Release checks](verification.txt) pass on net10/net11: Core 478/runtime,
PythonRe 164/runtime, PCRE2 174/runtime and snapshot 3/runtime. New coverage includes
supplementary letters, captures outside Count, funded zero-storage admission,
offset canaries and two/four-byte scalars around 4096-byte tails. Work refusal
after validation, original cancellation and deadline expiry during native work
remain covered. Folded/compound/RTL/NB controls agree with scalar enumeration.

The initial fixture build incorrectly requested captures from value Match;
it was corrected to MatchDetailed/GetGroup before any measurement. The timed
patch includes the initial 474 tests; four later block-tail theories give the
final 478. The [final validation manifest](final-validation.json) confirms the
production DLL remained byte-identical. Failed and corrected logs are preserved.

One [public Count A/B](comparison.json), **77.071 seconds**, eight cases/four
equal-result lanes, quiet Ubuntu VM/.NET 11 RC3/CPU mask 4. Warm 1.5s/minimum
64 calls; independent 60ms calibration; five alternating samples per lane,
minimum **46.4ms**. Full validation/publication included; construction excluded;
result batches checked after timing. **No confirmation**.

| Public Count | Before us | After us | Change | Allocated B, unchanged |
| --- | ---: | ---: | ---: | ---: |
| Sherlock letters ordinary | 43816.800 | 2145.606 | -95.10%, unsettled | 0 |
| Sherlock letters compiled | 68403.500 | 2329.756 | -96.59% | 0 |
| Sherlock letters finite/live | 46674.600 | 1906.650 | -95.92%, unsettled | 120 |
| Sherlock letters deadline/live | 76989.600 | 2320.878 | -96.99% | 168 |
| Mixed letters | 1166.280 | 64.631 | -94.46%, unsettled | 0 |
| Shared category envelope control | 122.110 | 120.471 | -1.34%, unsettled | 0 |
| Unchanged scalar control | 4.878 | 4.898 | +0.41%, unsettled | 0 |
| Unchanged literal control | 0.436 | 0.423 | -2.91%, unsettled | 0 |

Compiled and deadline/live target spreads are <=1.01x. Ordinary/finite/mixed
samples are bimodal, so their exact median effects remain uncertain despite
large improvements. Control spreads are <=1.31x; no adverse regression is
demonstrated. [Summary](summary.json) retains all paired differences and spreads.
Finite policy: work 1 billion, depth 4096, working/result 16MiB; deadline 5 seconds.

Actual catalog subject: 594930 bytes, SHA-256
`9C87F6C7316C6690EC5F9697D6F3D2B0DD6011C7D51F4A4A72881D00BFBF03B1`,
447160 matches in all four lanes. The compressed source corpus requires no
line-ending normalization. Supplementary-letter correctness uses Rune-based
expectations in tests, because the UTF-16 comparator does not share that behavior.

Only the two Rust letter rows were refreshed from clean `9a79aefc`: ordinary/
compiled **2141.275/2146.289us**, **0B**, **0.108/0.111x** predecoded .NET 11,
both Win, from the previous qualified 2.663/5.268x rows. Comparator movement in
these separate snapshots is not a producer gain; the frozen A/B above establishes
the production improvement. [Selective log](selective-refresh.txt) and generated
parity/snapshot checks pass. No full catalog or package was requalified.

Adjacent test-only correction `f92441ab` fixes the second observed Windows
thread-pool starvation witness in PCRE2 cursor concurrency. It dedicates the
blocking worker and always resumes/joins before signal disposal, preserving
assertions and timeout. Baseline `4c1b3183` and candidate parent `f92441ab` differ
only by this test. Both previous failures and the source relationship remain
auditable; production ownership behavior did not change.

[Driver](driver.cs.txt), [preflights](preflight-linux.txt),
[identities](identities.json) and [clean-source hashes](clean-linux-hashes.txt)
bind the precommit comparison and separate source refresh. Immutable evidence:

- [Raw/source/binaries](../../../artifacts/experiments/letter-count-admission-20261031/evidence.tar.gz):
  `CC1B466B381096D41ACBBB5C28625B2B0425423DE6F4FDB1D192B2D956D61A5C`.
- [Correctness/source](../../../artifacts/experiments/letter-count-admission-20261031/correctness-source.zip):
  `32B83559635C8EB9EAA82A3D208A0314239643578274FF1BB4B82524BFDC868C`.

Published/staged packages remain immutable. The larger Chinese alternation gap
is explicitly deferred with its own attribution receipt. Continue the per-profile
ratio queue; final-source cumulative/full-refresh/package/consumer gates remain.
