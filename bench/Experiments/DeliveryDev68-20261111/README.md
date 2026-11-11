# dev.68: scalar search and one-shot output checkpoint

Frozen producer `a7e985ed2c1bb4ac7d63a09c74e3ba0a74ccb9b9`; all four profiles at
0.5.0-dev.68, .NET10/11, exact adapter-to-Core pins. Immutable packages/symbols
are staged in artifacts/nuget; nothing is published and downstream checkouts
are unchanged. The copied Awk/Lython handoffs pass on Windows/Linux using the
same Windows-produced bytes, including public producer probes and zero final
root loans. Earlier canonical package bytes are preserved.

One complete quiet-VM refresh passed **46m45s** (2805.303s/3600s),85 existing steps:
one build, both-runtime correctness/disabled dynamic code, four catalogs,
lifecycle/scaling, paired references, resources and final verification. The
[full receipt](full-refresh.json) records completion, not qualification of every
individual timing or scaling fit. Generated tables/summaries pass nine existing
snapshot checks per target using matching source binaries. Source CI42943161706
is green on Windows/Linux. [Acceptance metadata](acceptance.json) records scope,
package/source identities and raw evidence hashes.

The accepted batch shares distinct-head search, correlates short literal heads
and anchors, and avoids unrelated one-shot text sizing while reusing known
character counts. See the focused [vector-search](../DistinctHeadVector-20261111/README.md)
and [one-shot sizing](../OneShotKnownCount-20261111/README.md) receipts. Scalar
semantics, input policy, original interruption, resource admission and retained
lifetimes remain. No new execution backend or public API is introduced.

Ordinary catalogs use **Validate**. Lower managed/predecoded-reference ratios
are better. These independent Full observations include comparator movement;
they are not causal A/B attribution or composed focused gains. The fixed dev.66
baseline remains summaries imported at7b0cc1c3, producerb478f853. The table also
compares the immediately preceding dev.67 checkpoint.

|Public operation|dev.67 → dev.68 ratio|Managed cost before → after µs|Current verdict|
|---|---:|---:|---|
|Core industry/leipzig-name-family-count|1.247 → 0.553|3807.157 → 1683.549|Win|
|Core industry/leipzig-river-window-count|1.385 → 1.408|2098.844 → 2235.526|Gap|
|Core industry/mariomka-email-count|2.518 → 2.586|1183.506 → 1188.948|Gap|
|Core literal/sherlock-zh|3.429 → 3.691|80.792 → 89.179|Gap|
|Core aws-keys/quick|1.602 → 1.814|5841.663 → 6614.750|Gap|
|PythonRe findall/many-capture-strings|2.328 → 2.348|801.318 → 821.174|CpythonFaster|
|PythonRe findall/one-capture-strings|2.394 → 2.347|499.312 → 502.633|CpythonFaster|
|PythonRe findall/many-capture-utf8|0.681 → 0.673|821.765 → 839.890|ManagedFaster|
|PythonRe findall/named-capture-strings|1.763 → 1.682|160.601 → 160.909|CpythonFaster|
|PythonRe split/captures|1.741 → 1.652|430.831 → 428.314|CpythonFaster|
|Posix capture/sku007|3.997 → 3.908|2719.133 → 2639.543|Inconclusive|

Core rows use compiled .NET. POSIX SKU is4096 captures/batch, about0.644µs/call;
its apparent median gap remains **Inconclusive**, not a qualified improvement.
Byte-native Python output includes CPython's requested UTF-8 encoding and is
separate from string-output/isolated-matching claims. Warm captured strings
remain a material gap despite the Name Count improvement. Lifecycle is mixed:
Unicode construction53.213→91.363µs since dev.67, with48329B allocation unchanged;
literal/captured/fallback constructor medians fell. Keep both directions and the
five tiny unqualified Python rows, exclusions and rejected scaling fits.

Raw output remains in artifacts/benchmark-refresh/current/. Full and Linux
archives are copied offVM/hash verified under
artifacts/experiments/delivery-dev68-20261111; hashes are in acceptance.json.
No immutable package was rebuilt/repacked for metadata or documentation changes.
Performance work continues: the next focused pilot investigates reuse of the
accepted prepared search in governed windows and Python candidate discovery.
It does not require another complete refresh or unchanged package qualification.

Metadata f394237d CI42949895612 is green on Windows/Linux. This receipt-only
closure follows the existing workflow's experiment-receipt exclusion; it does
not repeat unchanged package or source qualification.
