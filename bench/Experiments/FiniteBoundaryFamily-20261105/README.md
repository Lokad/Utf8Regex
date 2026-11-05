# Reuse the family overlap proof for finite Count

**Accepted.** Finite-policy ASCII Count now relies on the structural family's
existing overlap proof instead of additionally rejecting every generic prefix
overlap. A positive leading word boundary resolves proper-start overlap inside
ASCII word prefixes. Same-start alternatives and unproved overlaps still require
scalar retry. The analyzer supplies the same prefix vector to both plans.

This removes two eligibility checks and reuses the existing metered search and
linear verifier. No new executor, owner, metadata, switch or framework. Full
strict UTF-8 validation, original cancellation/deadlines, bounded work and
admission remain. Avoided VM frames reduce route-dependent work/depth/working
requirements; fixed invocation metadata still allocates. Unicode, folded, RTL
and NonBacktracking routes, detailed results, cursors and captures are unchanged.

The untimed dev30 route inspection established that the generic proper-start
overlap guard alone denied the route on the actual declaration corpus. Its
53,352 bytes are all ASCII, SHA256
`E25F45289E44A6DE119149C10B258E8416A8E24E4FF01D94ED4753FA5A9A351B`,
with 73 matches. This is distinct from closed negated-repeat experiments.

One **78.821417-second**, 15-case public comparison uses the frozen
[dev30 Full](../DeliveryDev30-20261105/README.md) graph at fb309380 against Core
built on parent39d377e9 with source overlay
`814E7706B08A5773B5E6AF2FB7C6483497E03B29149A1C0B1D50F36C0A5524DA`.
All six companion DLL/PDBs are byte-identical; only Core changes. Binary identities,
all observations, allocation costs and spreads remain in
[cost-summary.json](cost-summary.json).

| Public Count | Before µs | After µs | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Actual corpus, finite |45.31|44.81|−1.10%|3664 / 112|
| Actual corpus, finite/live token |47.97|45.90|−4.31%|3664 / 112|
| Actual corpus, finite/compiled/live |47.80|45.52|−4.78%|3664 / 112|
| Dense finite hits |2041.01|2235.29|+9.52%|3664 / 112|
| Sparse finite miss |6.98|7.34|+5.12%|112 / 112|
| Dense finite false prefixes |7.54|7.92|+5.01%|112 / 112|
| Finite word-interior prefixes |759.06|547.78|−27.83%|176 / 112|
| Finite long identifiers |283.39|150.12|−47.03%|3664 / 112|

Promoted for 3,552 fewer bytes per actual-corpus call (96.94%) and the long-run
gains, with the dense-hit cost of about 194 µs and miss costs below 0.4 µs kept.
Actual-corpus time improvements are modest and overlap some sample movement;
the allocation reduction is the primary result. Unchanged controls moved:
unlimited −0.39%, deadline +0.88%, same-start −2.56%, Unicode +2.72%,
NonBacktracking +0.96%, Python +1.35%, PCRE2 +1.53%. Their allocations are
unchanged. Do not attribute those movements to faster or slower execution paths.
These are operating-cost observations, not new catalog parity classifications.

Normal eight-CPU startup precedes CPU2 affinity. Producers warm for at least
1.5 seconds/128 calls; diagnostic BCL lanes for 250 ms/64 calls. Each lane
calibrates independently to 60 ms, followed by five alternating samples. All
150 producer samples exceed 58.4662 ms. Typed results are consumed after timing;
same-result predecoded/decode-then BCL lanes cover overlapping semantics only.
No confirmation, tuned variant, full refresh or package matrix was repeated.

Windows/Linux Release .NET10/11 each pass focused Core974/Python176/PCRE165
checks. New borrowed-slice cases cover resolved internal overlaps and proper
suffix prefixes with one-byte working/result limits and depth one. Same-start,
punctuation, absent-boundary and Unicode retries remain correct. Work exhaustion
shares the validation allowance; discovery and verification retain original-token
cancellation and absolute deadlines. Existing folded/RTL/NonBacktracking,
public match/cursor, finite and caller-funded checks pass.

Immutable evidence under artifacts/experiments/finite-boundary-family-20261105:

- evidence.tar.gz: `E6C56D84B0D080D9992D7A66108A2C2B4D4AC38C1EEF0C4687E86AF1670F908D`.
- windows-evidence.zip: `90D2B6E0FB0B657428CA1682CEB5780E14BD773B51B0CFE4CA75723972B79400`.

Raw evidence includes frozen graphs, exact source, predeclaration, driver,
preflight, every sample, focused Linux logs and terminal0. The untimed route
archive is `8F4DEFB91E83FD398B9F903D7ADBB56F09153A6E69DB40B85B765696AF49A80F`.
Staged dev30 packages, symbols and 44m12.49s Full tables remain immutable and
predate this change. Documentation records the resource-cost revision; normal
source CI follows this scoped change.
