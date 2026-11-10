# dev.67: combined performance delivery checkpoint

Frozen producer `d8eb14a52b2c044f4345af03469356992be58861`; all four managed
profiles at0.5.0-dev.67, .NET10/11, exact adapter-to-Core pins. Packages/symbols are
in artifacts/nuget; this candidate is staged, not published. Earlier package bytes
remain unchanged. The batch simplifies subject preparation, Python ASCII/nullable
construction, range repeats, multi-leaf workspace reuse and checked byte/pair search.
No public matching, input-validation, cancellation or resource contract changes.

One complete quiet-VM refresh passed **47m05s** (2824.793s/3600s),85 existing steps:
one build, both-runtime correctness/disabled-dynamic-code, four catalogs,
lifecycle/scaling, paired references, resources and final verification. See the
[full receipt](full-refresh.json). Source CI42908811679 is green Windows/Linux.
Windows and Linux consume the same immutable Windows-built packages:10 detached
fixtures/platform; original copied Awk/Lython handoffs and public probes pass,
including zero final root loans. These are compatibility checks, not adoption.
Generated summaries/READMEs pass the nine existing snapshot checks per target.

Ordinary catalogs use **Validate**; smaller managed/predecoded-reference ratios
are better. Fixed baseline: actual dev.66 Full summaries imported at7b0cc1c3,
producerb478f853. Do not use the older summaries present at its freeze commit.
These separate full runs retain comparator movement and are not causal A/B effects.

| Public operation | dev.66 ratio -> dev.67 ratio | Managed cost before -> after |
|---|---:|---:|
| Core compiled river Count |2.126 ->1.385|3263.030 ->2098.844us|
| Core compiled name-family Count |1.327 ->1.247|4048.463 ->3807.157us|
| POSIX SKU,4096 captures |4.884 ->3.997|3251.532 ->2719.133us/batch|
| Python many-capture strings |2.225 ->2.328|778.585 ->801.318us|
| Python one-capture strings |2.218 ->2.394|474.299 ->499.312us|
| Python captured split |1.725 ->1.741|440.259 ->430.831us|
| Python many-capture UTF8 |.691 ->.681|863.612 ->821.765us|

The Core and SKU improvements coexist with substantial remaining gaps. Python warm
strings did not improve broadly. Lifecycle is mixed too: captured/fallback
construction allocations71633->67601B and83577->79761B, but fallback construction
275.872->310.887us. Focused nullable-construction wins do not establish a universal
construction gain. Five tiny Python rows remain unqualified due to harness floors;
PCRE2/POSIX exclusions keep their semantic reasons. Preserve the four/five-byte
search's documented dense/neighbor costs rather than hiding them in an average.
Strict Chinese/email validity work and POSIX history growth remain explicit future
work. AssumeValid/funded/deadline evidence stays separately labeled in focused
receipts; no composed speedup, weakened semantics or universal parity claim.

[Acceptance metadata](acceptance.json) records identities, scope and selected costs.
Local raw outputs remain in artifacts/benchmark-refresh/current/. Temporary-VM
evidence was copied offVM/hash verified under artifacts/experiments/delivery-dev67-20261110:
full archive C14CDC11A381293C3617BEB39D969769CE1C64415E84C2C46A8A8649239230BB;
Linux archive54CDF3663515CDC5FD5DCDE6906131003DB356C6A7CF939D828D36B6E9B23C8E.
No new qualification/archive framework or unchanged package rerun is required.
Final documentation CI42918197398 is green on Windows/Linux; this receipt-only
closure does not require repeating unchanged qualification. Performance work continues.
