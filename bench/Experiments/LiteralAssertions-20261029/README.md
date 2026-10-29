# Bounded ASCII literal assertions

Accepted source **362ba340** runs one-to-32 unfolded ASCII literal instructions
followed by Accept directly in the existing assertion driver. It avoids child
cursor/continuation storage without a new prepared representation or engine.
Capture writes, folding, Unicode, longer bodies and other instructions retain
the VM. Original fixed-forward scalar rewind, required end, polarity, logical
depth admission and bounded work/token/deadline checks remain. SPEC.md clarifies
that assertion depth is admitted even when physical child storage is unnecessary.

The [initial attribution](initial-attribution.txt) is diagnostic, not a speedup
claim. [Driver](driver.cs.txt), [project](driver.csproj.txt),
[raw public comparison](comparison.json) and [console](comparison.txt) compare
exact 907f0c51 baseline and 362ba340 candidate on the quiet VM, .NET 11 RC3,
CPU2. One **68.42s** run under a 120s cap, fourteen public Search/Count workloads,
one-second warmup, independent calibration and three alternating samples;
minimum timed duration **29.97ms**. Preflight and timed checks consume equal
results/ranges; .NET diagnostic lanes use matching ASCII-literal semantics.
No confirmation or further timing campaign was needed.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| PythonRe Search miss, catalog | 0.819 | 0.352 | 1872 / 0 |
| Search hit | 0.774 | 0.355 | 1872 / 0 |
| Search miss, finite limits/live token | 1.089 | 0.440 | 3288 / 112 |
| Search hit, finite limits/live token | 1.035 | 0.468 | 3288 / 112 |
| Dense Count | 98.560 | 42.458 | 1872 / 0 |
| Search, Unicode context | 1.258 | 0.601 | 1872 / 0 |
| Negative lookbehind Search | 0.748 | 0.346 | 1872 / 0 |
| Long-body control | 1.135 | 1.154 | 1872 / 1872 |
| Captured-body control | 0.963 | 0.989 | 4064 / 4064 |
| Folded-body control | 0.779 | 0.798 | 1872 / 1872 |
| Literal control | 0.288 | 0.289 | 0 / 0 |
| Core Count | 517.440 | 172.050 | 1872 / 0 |
| Core Count, finite limits/live token | 792.340 | 182.143 | 2173144 / 112 |
| NonBacktracking control | 255.294 | 236.977 | 26336 / 26336 |

Positive control costs are <=2.8%; the -7.2% NonBacktracking point is not
attributed to this change. Search improves about 2.2-2.5x; Core Count about
3x/4.4x. These are public-operation costs, not private-kernel estimates.

The selected [raw refresh](raw-refresh.txt) and nine-sample
[paired CPython qualification](paired-refresh.txt) retain exact source/PDB/DLL
checks and clean relevant inputs, official CPython 3.13.2, alternating order and
the existing floor/spread/order gates. The paired run chose CPU5 (0x20), distinct
from the fixed CPU2 direct A/B. Qualified producer / predecoded CPython improves
**9.899x -> 1.508x [1.502,1.512]**, still **CPython faster**: managed **0.335us**,
CPython **0.222us**, zero managed allocation. The historical snapshot also spans
earlier shared-prefix work; do not attribute that entire ratio change to this
pilot's separately demonstrated 2.3x miss improvement.

The independent unpaired raw producer median is **2.228us**, versus 0.335us in
the paired protocol; both are retained without substituting observations or
mixing raw .NET costs with paired producer costs. No causal explanation or extra
rerun is claimed. Only lookbehind/search-miss changed; catalog/corpus/lifecycle/
scaling and all other case records remain intact.

[Validation](verification.txt) passes on .NET 10 and 11: complete PythonRe;
focused Core assertion/rewind/depth/resource/cancellation/inventory checks;
affected PCRE2 assertion/depth/resource routes; API/snapshot and generated-page
checks. New tests cover both directions, positive/negative forms, Unicode/full
context and offset spans, body bounds/exclusions, captures, every small work
cutoff/loan retention, original interruption and logical depth denial despite
avoided storage. Initial fixture mistakes (scalar-boundary argument and a group
property name) were corrected and archived.

The [global qualification verifier](global-verification.txt) correctly rejects
an untouched historical row: `alternation/search-miss: managed product changed`.
It requires the complete frozen current product and remains a release-boundary
gate. Its first invocation also lacked the explicit CPython path; that setup log
is archived. Do not requalify unchanged rows to make this global gate pass now.
No package/full catalog/platform matrix was repeated.

Immutable files under `artifacts/experiments/literal-assertions-20261029/`:

- `parity-literal-assertion-362ba340.tar.gz`: exact DLL/PDBs, host, source bundle,
  driver/binaries, build/preflight/comparison, raw/paired evidence and snapshots.
  SHA256 `1103FDC4F73A5265CFD74498D96FA3BF080BCE5DBC4326BEDE2AFDA3D3DE7776`.
- `correctness-and-verification.zip`: initial/final fixtures/checks, readonly
  attribution and verifier logs. SHA256
  `CE6EBFD0F0D9049925E9C37D86A4586B14C89556D231BC18E892C171A150750F`.
