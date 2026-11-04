# Rejected validity-only ASCII-prefix reuse

**Rejected and reverted.** Baseline `68349f0a`; shipped validation, selector and
accounting are unchanged. Extra prefix/error-offset and bounded interruption tests
remain. Packages and published snapshots are unchanged.

The prototype replaced the complete ASCII Boolean proof with a checked span search
for the first non-ASCII byte, then validated only the suffix. It kept the selector's
original input sample and restored absolute errors. It also removed the caller's
duplicate ASCII proof. Metadata validation, 2 KiB work/interruption blocks, original
cancellation/deadlines and admission were unchanged. No new state, mode or unchecked
API. This was work elimination, distinct from the closed selector/access campaigns.

One **79.9464047s**, 15-case public A/B, only Core changed; six frozen `e731a452`
companion DLL/PDB files remained identical. Normal eight-CPU startup then CPU2,
managed 1.5s/min128 and diagnostic 250ms/min64 warmup, independent 60ms calibration,
five alternating samples. **150 owned samples, minimum 51.5573ms**. Typed results
were consumed after timing. BCL lanes compare equal-result public operations on
these subjects; IsMatch results overlap for the astral case despite matching-unit
differences. [All observations and identities](cost-summary.json) retain allocation
counts and spreads; exact driver/source/DLL/PDBs and durations are archived.

| Public operation | Before us | Prototype us | Change |
| --- | ---: | ---: | ---: |
| Actual email Count |1231.96|1229.86|-0.17%|
| Actual email finite/live Count |2844.87|2897.21|+1.84%|
| Actual Chinese literal Count |94.92|96.31|+1.47%|
| Tiny ASCII IsMatch |0.11|0.11|-0.20%|
| Large ASCII IsMatch |1.50|1.87|+24.69%|
| Late Unicode IsMatch |3.86|2.59|-33.00%|
| Late Unicode finite/live IsMatch |2.41|2.71|+12.54%|
| Late Unicode deadline/live IsMatch |2.92|3.45|+18.35%|
| Two-byte IsMatch |2.44|2.48|+1.37%|
| Three-byte IsMatch |5.07|5.20|+2.59%|
| Constrained three-byte IsMatch |11.75|11.37|-3.31%|
| Four-byte IsMatch |33.71|33.46|-0.73%|

Allocations are unchanged: zero except finite/live112B, deadline160B and Python
email3568B. Unchanged native Count -0.24%, PCRE Count -2.00%; Python email +0.89%.
No causal credit on unchanged controls. Large ASCII and controlled late-Unicode
lane spreads are <=1.03, so no confirmation or tuned variant is justified. The
late-Unicode gain does not justify the common ASCII/controlled costs without a
gain on the actual target corpora. This decision does not impose a universal
10% gate: documented local regressions remain acceptable for substantial gains.

Focused Release checks pass on Windows/Linux, each .NET10/11: Core584, Python176,
PCRE165 per target. Coverage includes all ASCII bytes, sliced offsets/tails,
malformed absolute offsets, scalar widths, finite admission and original token/
deadline interruption. The retained tests also pass with production reverted.
No full catalog, package matrix or snapshot refresh was run for this pilot.

Immutable `artifacts/experiments/validity-prefix-reuse-20261104/`:

- `evidence.tar.gz`: `1BFC3EE563B7F66FAFACE84D29D2016545F09D21ECB8A75A8E026F1AA005455D`.
- `windows-correctness.zip`: `F6855EA389ADF5773BB671A15200ABAA153E9CBDE941E06FC2D67C296AE0B324`.
- `post-revert-checks.zip`: `F33958308F6F0F126DCDE29FED351A03D2155A369FC051A97C6D0A997D08EDA0`.

Do not repeat or retune this pilot. Keep the existing ASCII fast return and
complete-subject validation; resume the remaining performance/delivery queue.
