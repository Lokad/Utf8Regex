# Historical scalar.33 core capture assessment

2026-10-13 bounded producer assessment, prompted by the unchanged downstream
capture/timing handoff. Its timings and clock counts apply to the recorded scalar.33
bytes; they are not current scalar.116/117 costs. This measures Utf8Regex, not a
complete consumer invocation. The assessment is closed; its selection/API boundary
is separate from later Core/Python package qualification.

The input is `sku007`, with pattern `^([\p{L}]+)([0-9]+)$` (the consumer's alpha
class translation). Public `MatchDetailed(ReadOnlySpan<byte>, CancellationToken)`
uses the owned scalar slice VM. No BCL matcher or whole-subject decode is used.
Immutable core scalar.33-dev, source
`894fa641c291bbb36b395c714d4e252f62f118f5`, DLL SHA256
`CAF67325DF31E10BE3179FB8988A3489292B1F17645D26BE16AA0F3D82BCA600`.

| Group | Value | Byte start / length | Derived UTF-16 start / length |
| --- | --- | --- | --- |
| 0 | sku007 | 0 / 6 | 0 / 6 |
| 1 | sku | 0 / 3 | 0 / 3 |
| 2 | 007 | 3 / 3 | 3 / 3 |

Consuming group bytes for 4,096 records produces the exact checksum 40,960.
All policy lanes agree. Pattern compilation, decoding, record splitting, script
execution, capture-array publication and I/O are outside these measurements.

| Policy lane | Median ms / 4,096 calls | Sample range, ms | Allocated B / call |
| --- | ---: | ---: | ---: |
| Default: unlimited, infinite timeout | 8.238 | 8.165–8.634 | 6,416 |
| Unlimited, 5-second timeout | 10.096 | 9.773–10.390 | 6,576 |
| Finite limits, 5-second timeout | 10.286 | 10.014–10.496 | 6,576 |
| Finite limits, timeout and live token | 10.590 | 10.154–10.727 | 6,576 |

Finite per-call limits are 1,000,000 work steps, depth 8,192, working 64 MiB and
result 64 MiB. The quiet Linux VM runs .NET 10.0.12/Ubuntu 24.04.4 on an Intel Xeon
Platinum 8573C, pinned to logical processor 0. Release FullOpts measurements use
`DOTNET_TieredCompilation=0`, seven alternating whole-lane samples, eight batches
per sample and at least 100,000 calls/500 ms warmup per lane. The initial normal
tiering run transitioned late and was rejected as settled throughput; this is its
single bounded repeat. These are focused operating costs, not full-catalog Status,
normal-tiered performance qualification or a consumer speedup estimate.

Instruction deadline polling already has a 2,048-step cadence. Infinite deadlines
read no clock. Separate untimed diagnostic counts observe 34 clock reads for this
six-byte detailed call with timeout, 37 with finite limits; the corresponding
4,096-byte controls read 334/337. Admission, bounded buffer work and projection
also check the absolute deadline directly. Counts do not establish CPU attribution
or justify removing checks. No per-transition Stopwatch calls exist in the core.
All four tiny-budget denials, pre-cancellation, real checkpoint cancellation and
same-invocation absolute deadline controls pass. Production metering is unchanged.

Core selection remains ordered first success. `(a|aa)` on `aa` returns `a`; the
GNU awk 5.2.1 overlap oracle selects `aa`. Core has no POSIX leftmost-longest or
POSIX subcapture-selection API. Existing consumer routing keeps such patterns on
its own matcher, so this assessment does not qualify that route or replace it.
The unique anchored ASCII result above agrees; nested, empty and optional control
values also agree with the sampled oracle, without proving general selection parity.

The supplementary control `^(.)()([0-9]+)$` on `😀007` returns whole byte range
0/7, group 1 range 0/4, empty group 2 range 4/0 and group 3 range 4/3, with derived
UTF-16 ranges 0/5, 0/2, 2/0 and 2/3. These are complete scalar boundaries. The GNU
oracle reports a conflicting start for that empty group; its metadata is retained,
and broad metadata equivalence is not claimed. Producer scalar semantics stay intact.

Raw samples, exact identities, semantic controls, failed driver observations and
the rejected initial tiering run are in
`artifacts/awk-capture-cost-20261013/assessment.json` and its referenced files.
No consumer source, matcher, dependency pin or default was changed. Further core
capture allocation or timeout work needs a concrete bounded mechanism with retained
semantics; this result does not authorize a POSIX profile project or benchmark campaign.
