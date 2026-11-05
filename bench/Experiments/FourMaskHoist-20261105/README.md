# Reuse immutable four-position masks during candidate scans

**Accepted.** The existing packed-nibble scan admits its four offsets and eight
immutable mask vectors once before the AVX2 loop, matching its existing three-
offset structure. All input reads remain checked. The same256-bit SIMD,
correlation, full verification, pending candidates and SSE/scalar tails remain.
No new plan, retained state, owner, selector, engine, framework, mode or unchecked
operation. Allocation behavior and public resource/cancellation contracts stay
unchanged; finite/live/deadline calls retain their existing authority and routes.

The completed [dev29 Full](../DeliveryDev29-20261104/README.md) ranks compiled
keyword-family Count2.35x matching predecoded.NET11. Actual route inspection
identifies the emitted upper-word matcher and its existing four-position filter,
not the generic structural executor. The53352B corpus has SHA256
`E25F45289E44A6DE119149C10B258E8416A8E24E4FF01D94ED4753FA5A9A351B` and73matches.
An untimed text census finds91first-four prefixes and1431matched identifier bytes
(2.682% of input). Those are static text counts, not executed branch/work counters
or timing attribution. The prototype adds37lines to reuse the existing fixed-mask
loop structure; it does not add filtering or word-interior skipping again.

One **79.1768564s**,15-case public A/B compares the exact frozen43435906 Full
Core/PythonRe/PCRE2/benchmark graph against Core built on data-onlyb604c543 plus
overlay`BACEBB68`. All six companion DLL/PDB files are identical; only Core
changes. [All observations](cost-summary.json) preserve identities, allocations,
every row and sample spreads. Same-options predecoded/decode-then BCL lanes agree
on these overlapping semantics, as diagnostics rather than new catalog Status.

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual keyword corpus |18.56|10.66|-42.58%|
| Actual keyword corpus, compiled |18.47|10.66|-42.31%|
| Actual corpus, finite/live control |46.48|46.43|-0.12%|
| Actual corpus, deadline/live control |55.79|56.58|+1.42%|
| Dense keyword hits |152.58|141.28|-7.41%|
| Sparse keyword miss |19.55|9.91|-49.29%|
| Dense false prefixes |102.73|102.62|-0.11%|
| Word-interior prefixes |149.39|146.45|-1.97%|
| Long identifiers |47.75|47.57|-0.37%|
| Folded four-position literal family |236.27|237.09|+0.35%|
| Exact four-position literal family |110.70|108.87|-1.65%|
| Chinese three-position control |155.88|155.30|-0.37%|
| ASCII control |2.50|2.49|-0.37%|
| Python keyword Count control |58.75|58.52|-0.38%|
| PCRE2 control |152.31|153.31|+0.66%|

Allocations are unchanged:0B except finite/live3664B, deadline/live3712B and
Python control3552B. Retain the measured +0.79us deadline/control movement and
folded/PCRE costs without assigning a cause on unchanged routes. This reduces
public keyword corpus time by about7.8–7.9us and sparse misses by9.64us. It does
not establish a general profile speedup or revise the frozen catalog's parity
classification. The old accepted prefix filter's miss/dense costs remain in
their original receipts. These observations use the clarified local-trade-off
policy, not a universal10% gate.

Normal8CPU startup precedes CPU2 affinity. Producer warmup1.5s/min128 calls,
BCL250ms/min64, independent60ms calibration and five alternating samples.
All150 producer samples exceed54.7286ms. Typed Count results are consumed after
timing; no timed reflection calls. Spreads are below1.059 before/1.024 after;
actual corpus lanes are below1.013. No confirmation, tuned variant,
repeated campaign or completed checked-access work was reopened.

Windows/Linux Release.NET10/11 each pass focused Core910/Python176/PCRE165 checks.
The existing borrowed-offset/every-tail test now covers exact/folded four-position
families as well as three-position cases. Existing production emitted Count,
IsMatch, Match, cursor, word-boundary, malformed-suffix, original-token, finite/
funded/work/working/result controls remain covered. No forwarding test executor.
All15 public preflights pass before timing. The unchanged three-position and
SSE/scalar tails remain exercised by existing boundary cases.

Immutable artifacts/experiments/four-mask-hoist-20261105:

- evidence.tar.gz: `20CD478EFFBC595907B336C5E6FA953A61020DE0F5F9E9A6195C496AB636722C`.
- windows-correctness.zip: `74CAC1C783FF4B6D5BF0E50F5C276B480B100ED7F40493CFE4EF7D7DC3418EC6`.

Raw evidence includes frozen graphs, exact source overlay, declaration, driver,
public preflight, focused correctness, all samples and terminal0. Staged dev29
packages and its43m47.06s Full tables remain immutable and predate this change.
No new package/source/platform matrix or catalog refresh was repeated. Delivery
data CI41938502257 passed Windows/Linux; this source change follows normal CI.
