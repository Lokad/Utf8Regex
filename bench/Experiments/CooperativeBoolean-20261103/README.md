# Cooperative Boolean handoff: current source qualified

The handoff's 1,792B/live-token observation describes published
`0.4.0+3d120bdcbbd49e25e9a3c6af7d408e34d239adca`. It was integrated as
high-priority H5 in the ignored root PLAN.md and the root handoff was removed.
Its original SHA256 is
`57BACF025CDFF7D924467FA65B981A8537E3CC8A76A0EE520268E5A9BE713B18`.
That evidence was not presented as a measurement of the development tree.

Current production already uses the existing controlled ASCII Boolean verifier
for `^[A-Z]{3}[0-9]{4}$`. No execution, ownership or resource mechanism was changed.
The exact positive/negative consumer predicate now has regression coverage with
live tokens, finite major-buffer limits, final newline semantics, supplementary
input, malformed tails and original-token entry cancellation. Existing focused
checks cover substantial-work interruption, deadlines, funding/refusal/refunds and
working/work limits: **381 passed per runtime**, Release .NET 10 and 11.

One current-source audit ran sequentially on the quiet Linux x64 VM:
**33.450s .NET 11 RC3**, then **33.364s .NET 10.0.12**, under one 120s cap.
Normal startup sees eight processors before in-process CPU2 affinity. Each of
11 routes/runtime gets a one-second warmup and two-second steady window. Programs
compile once; immutable hit/miss subjects alternate and complete public results
contribute to a checksum checked after timing. The live token comes from one CTS
outside the loop. Construction, collected heaps, calls and raw allocation totals
are retained separately. No parent A/B, throughput distribution or universal
allocation guarantee is claimed by this single-window audit.

The exact source is receipt head `57862b0ea3b7858048c1837f7b02a4ee3ec751fb`;
its complete `src` tree equals the dev.15 freeze. Both loaded Core hashes and
informational versions are in the [raw .NET 11](audit-net11.0.json) and
[.NET 10](audit-net10.0.json) records.

| Public predicate/route | .NET 11 ns/call | .NET 10 ns/call | B/call, both runtimes |
| --- | ---: | ---: | ---: |
| Fixed predicate, None | 183.89 | 106.07 | 0 |
| Fixed predicate, live token | 195.46 | 228.58 | 112 |
| Fixed predicate, finite limits/live | 199.98 | 231.02 | 112 |
| Fixed predicate, deadline/live | 401.57 | 421.81 | 160 |
| Fixed predicate, working allowance/live | 280.25 | 310.49 | 176 |
| Unicode letter control, live | 409.67 | 430.44 | 176 |
| Variable word/digit control, live | 585.17 | 610.77 | 2728 |

The fixed live predicate delivers about **5.12 million calls/s** on .NET 11 and
**4.37 million/s** on .NET 10 in this driver. Its remaining 112B invocation cost
means 7MiB per 65,536 calls, versus the handoff's historical 112MiB. This arithmetic
compares allocation observations across versions; it is not a causal same-driver
historical throughput comparison. The .NET 10 None/live timing difference remains
explicit; it was not resampled or relabeled as noise. The variable control still
allocates 2616/2728B for None/live; the result does not generalize to all patterns.

Caller-funded matching peaks at **96B modeled temporary capacity**, with its
unrelated 13B parent loan preserved at completion (109B peak including parent).
Collected process heap observations are diagnostics, not exact compiled-program
retention or a whole-process memory bound. Fixed metadata, program ownership and
borrowed inputs remain separate from modeled temporary capacity. No token was
substituted and no real acquisition or refund was skipped.

The first audit setup used a field lookup for an internal property and failed
before the first warmup or timed sample. The failed setup is preserved; the
corrected driver preflighted all routes on both runtimes before its sole audit.
No production change or performance variant resulted from that correction.

Immutable raw evidence under `artifacts/experiments/cooperative-boolean-20261103/`:

- `current-audit.tar.gz`, SHA256
  `17957A8CCFF1502B572BE1FAE9E7B2735DF41BFF0F1F5F52BCB1DDC22209ADE3`:
  complete frozen source, exact Core DLL/PDBs, driver/project, toolchain, handoff,
  preflight, all allocations/timings/collected heaps, scripts and exits.
- `failed-setup.tar.gz`, SHA256
  `A74AA6CECA6EA45CD813C2B8C161DA770D78E6A0DC0DC2538404A3FD49D86630`.
- `correctness-source.zip`, SHA256
  `C5FD6311220ADAEECBE51E7F036D152A8E289DAD219A3B4EE4653AA2F6E0171C`:
  final regression tests, contract clarification, focused checks and setup logs.

H5's current-source behavior is qualified with the stated residual costs.
[Dev.15's three stable fixed-original delivery failures](../DeliveryDev15-20261103/README.md)
remain unresolved. No catalog, package, full-refresh replay, publication or
downstream adoption was performed. The next delivery gate keeps every existing
189 case and the actual affected cooperative predicate; earlier immutable
packages and receipts remain untouched.
