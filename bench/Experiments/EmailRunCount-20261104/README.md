# Bounded ASCII runs in Unicode token Count

The existing complete Unicode word-token proof and counter remain. Checked
PreparedByteSetState searches consume ASCII head/body runs in <=2048-byte blocks;
Unicode word membership retains the original Rune fall-through. An interior-dot
search proves both nonempty neighbours: an ASCII dot cannot occur inside a valid
UTF-8 scalar, so excluding the first/last byte is sufficient. This removes the
per-character secondary-position state. No new recognizer, engine, owner, retained
program storage or unchecked API; two fixed prepared value sets reuse existing SIMD.

**Intentional accounting change:** ASCII scans charge one existing native work
unit per bounded block rather than per helper character. Unicode fall-through,
finite admission and original synchronous token/deadline checks remain; terminal
control checks are explicit. Working/result payload remains zero. The existing
64-work-unit diagnostic hook cadence is unchanged; head/body interruption fixtures
now use200000-byte runs so the coarser units reach that same hook.

Public route proof on .NET10/11 observes this counter on the actual6839410B corpus,
6194653UTF16 chars/482694nonASCII/92matches. One5.6216177s attribution diagnostic
recorded public1238.80us, isolated validation876.67us and validated counter281.58us.
Those phase costs are not additive public savings or causal optimization evidence.

One **75.5878344s**,14-case public A/B uses accepted `045b1810` Core, with six frozen
Full `e731a452` companion DLL/PDB files identical. Only Core changes. Normal8CPU
startup thenCPU2, managed1.5s/min128 and diagnostic250ms/min64 warmup, independent60ms
calibration, five alternating samples. **140 owned samples, minimum47.6327ms**;
typed results consumed after timing, no bridge boxing/materialization. BCL same-options
Count lanes are equal-result diagnostics on these BMP subjects; astral boundaries
have independent tests. Every raw duration, graph/source overlay and allocation is
preserved in the archive and [summary](cost-summary.json).

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual ordinary |1235.135|1233.710|-0.12%|
| Actual compiled |1241.700|1232.845|-0.71%|
| Actual live |2847.055|2791.059|-1.97%|
| Actual finite/live |2851.719|2815.495|-1.27%|
| Actual deadline/live |2956.329|2902.486|-1.82%|
| Tiny ASCII |0.0998|0.1020|+2.20%,1.05xafter spread|
| Tiny Unicode |0.1231|0.1242|+0.91%|
| Dense ASCII |121.821|43.691|-64.13%|
| Dense Unicode |86.731|53.799|-37.97%|
| Long ASCII heads |104.470|4.910|-95.30%|
| Long body, no dot |27.743|1.092|-96.06%|

Allocations are unchanged:0B except live/finite112B and deadline160B. Python email
+0.84%(3568B), native FIRST-0.01%, PCRE control-1.54%. No causal credit on unchanged
controls. **Promoted** for substantial dense/long public gains with the retained tiny
costs. **This does not close the published corpus email gap.** No confirmation,
tuned variant, full catalog/package replay or cumulative/parity claim.

Focused Release checks pass on Windows/Linux, each.NET10/11: Core424/Python176/
PCRE165 per target. Coverage includes BMP/astral/combining word membership,2KiB
edges, partial slices, dot neighbours, failed/cascaded delimiters, finite admission,
malformed full tails and original token/deadline interruption in search/head/body.
The prior window admission correction045b1810 also passes CI41871647682 on both OSs.
Published/staged dev.26 bytes and Full/table observations remain unchanged.

Immutable artifacts/experiments/email-run-count-20261104/:

- evidence.tar.gz: `FD8CE8C30822249EC0DF74D1144A52FF6A7F9B7160C354BD236474696D40B575`.
- windows-correctness.zip: `71F14755437CA79AFF4C6CE13BE1B641C6DBB3ABE2F8487BC7DE835E932A93B9`.

Final package/platform/consumer and cumulative operating-cost evidence stays at
the next coherent delivery boundary, without repeating unchanged dev.26 qualification.