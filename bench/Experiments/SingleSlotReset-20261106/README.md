# Single-element capture-slot resets: rejected

Parent `8988013261bc34853c40c195dc4bd7e493b75d5e`. The prototype replaced the
entry/failure reset of a one-element capture array with a checked element store.
Outer empty guards and larger Array.Fill operations stayed. No field, helper,
allocation, algorithm, SIMD or resource/interruption policy changed. The24-line
runner diff is **fully restored**; no permanent option or alternative remains.

Untimed Release FullOpts confirmed direct stores on length1 paths and retained
Fill elsewhere. TryMatchAt grew1468->1685bytes (+217B). The earlier empty-array
reset change remains; the rejected pilot concerns nonempty single-element arrays.

ONE **86.179528741s** Default-process ABBA on the quiet .NET11 RC3 VM: normal
eight-CPU startup thenCPU2,1s/min128warmup,75ms targets/five samples,14public rows/
280samples, minimum43.6637ms. Before reuses accepted Core03C32073; after673B3B7B,
with identical qualified46 adapters and caller. Full strings/bytes/detail data
are checked after timing. All allocations remain identical. No confirmation.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual one-capture FindAll strings |374.18|369.26|-1.31%|
| Same UTF-8 results |388.55|388.12|-0.11%|
| Same finite/live |561.98|573.08|+1.98%|
| Same deadline/live |768.46|753.92|-1.89%|
| Unicode-prefix neighbor |394.55|386.20|-2.12%|
| Multiple-group FindAll |633.07|658.22|+3.97%|
| Zero-capture Unicode Split |62.07|61.26|-1.29%|
| PCRE long backreference Count |21.33|22.48|+5.37%|
| Single-capture detailed results |852.25|751.37|-11.84%|

Primary strings before-process medians382.75/365.74us versus after368.08/371.39us
interleave. UTF-8 is flat and finite/live retains its local cost. Multiple-group
after658.82/652.86us is slower than both before637.46/630.27us. PCRE after21.48/
23.09us versus before20.85/21.61us retains uncertainty and the adverse pooled cost.
Detailed after840.37/667.63us versus before855.52/848.98us does not establish a
precise persistent11.84% improvement. These observations do not justify the extra
branches/code for ordinary captured FindAll. No tuned variant or follow-up timing.

[All observations](cost-summary.json) retain native+0.30%, named miss-1.57%, Core
history-1.04%, open-reference-2.31% and RTL-4.49%, ranges and process medians.
Two equal-result predecoded/decode-then-.NET diagnostics remain separate from
the producer comparison and do not create a new catalog Status or table refresh.

Focused Release tests pass on Windows/Linux and .NET10/11: Core706/Python1378/
PCRE789 per target, covering zero/one/multiple captures, failure/rollback/history,
aliases, scalar UTF-8, finite admission, original tokens and deadlines. No new
implementation-existence tests. An initial scratch caller name collision was
fixed before execution. The first untimed BEFORE preflight exposed a missing
byte[][] content check; that caller-only repair happened before ANY timing.
Its original declaration, caller and failure archive are retained. No production
adjustment or repeated benchmark followed the preflight failure.

Both hosts restore the exact parent source. Affected Core binaries were rebuilt,
mutable test graphs updated, and Core21/Python12 capture sanity checks per runtime
pass on both platforms. Full46's eight measured assets and all immutable packages
remain unchanged. The accepted completed-reference journal reduction remains.

Canonical off-VM `artifacts/experiments/single-slot-reset-20261106/`:

- evidence.tar.gz: `68120680478B612CAEB1697EE0A2F38F158165A1698483420FA2B5602A726EF3`.
- preflight-failure.tar.gz: `1F15947C89133D6BA0C9F3E79D2701996C83800CDC1096D971778199A0E942DE`.
- restoration.zip: `B76CE82B6E0372597A5F5D043E0800DF456E4D167A007F3D93BCC8355252E2A8`.

This candidate is closed without a distinct new cause. No package regeneration,
full refresh or repeat unchanged qualification is warranted.
