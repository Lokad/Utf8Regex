# Bounded NonBacktracking preparation work

Accepted source `f42523ba`; frozen baseline `0f7ea84f`. Preparation now admits
one existing input-work unit before each group of at most 64 instruction cells
per scalar row. Scalar passes and atom predicates retain their own work charges.
Each group checks the original token and absolute deadline directly; publication
checks both again. Allocation admission, stored tables, depth and replay remain
unchanged. No unlimited exemption, additional mode, helper or unchecked access.

This deliberately changes preparation's numeric MaxWorkSteps costs. CPU growth
remains bounded by 64 simple cells per quantum plus independently metered atom
work. [SPEC](../../../docs/SPEC.md) and [migration guidance](../../../docs/MIGRATION.md)
document the revised units and finite-policy upgrade implications.

[Release checks](verification.txt) pass on net10/net11: Core 724/runtime,
PythonRe 589/runtime, PCRE2 80/runtime and snapshot 3/runtime. New witnesses cover
work refusal around 63/64/65/128/129/4097 states, cancellation during the final
group, deadline expiry before publication and synchronous loan refunds. Existing
public captures, aliases, scalar scaling and admission remain covered.

The [production-state probe](state-probe.cs.txt) confirms identical complete
viability hashes, storage and enclosing 13-byte loan returns in all five cases.
[Work totals](state-after.jsonl) intentionally differ from [before](state-before.jsonl):
region 561 to 155; Unicode 286 to 78; wide 8797 to 2868; final-cell 64 to 2;
long-empty-row 4098 to 66. Lower counters are not semantic or storage savings.

One [public Count comparison](comparison.json), **96.560 seconds**, eleven cases,
four equal-result lanes, quiet Ubuntu VM/.NET 11 RC3/CPU mask 4. Full strict UTF-8
validation and publication included; construction excluded. Warm 1.5s/minimum
64 calls, independent 60ms calibration, five alternating samples per lane;
minimum timed sample **56.700ms**. Batches checked after timing. **No confirmation**.

| Public Count | Before us | After us | Change | Allocated B, unchanged |
| --- | ---: | ---: | ---: | ---: |
| Region ordinary | 5015.181 | 4041.275 | -19.42%, unsettled | 557416 |
| Region compiled | 8144.313 | 6307.825 | -22.55% | 557416 |
| Region live | 8985.550 | 6652.200 | -25.97% | 557536 |
| Region finite/live | 9978.563 | 6907.750 | -30.77% | 557536 |
| Region deadline/live | 11927.625 | 9703.450 | -18.65% | 557584 |
| Unicode NB | 1448.309 | 1183.866 | -18.26%, unsettled | 135384 |
| Unicode NB deadline/live | 1638.067 | 1411.817 | -13.81% | 135552 |
| Wide NB | 3575.838 | 2755.591 | -22.94%, unsettled | 99560 |
| Nullable NB | 506.464 | 390.007 | -22.99% | 26832 |
| Non-NB scalar control | 5.990 | 5.901 | -1.48% | 0 |
| Literal control | 0.562 | 0.587 | +4.44%, unsettled | 0 |

Compiled/live/finite region, Unicode deadline and nullable spreads are <=1.01x.
Region deadline spreads are <=1.16x. Ordinary/Unicode/wide have bimodal samples;
their exact median gains remain uncertain. The literal control is <=1.13x spread;
no adverse control regression above 10% is demonstrated. Preserve all samples
and [paired differences](summary.json); no universal speedup or parity claim.

All four A/B lanes use the canonical LF region input: 46429 bytes, SHA-256
`507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2`,
ten matches. Only task-staged corpus bytes were normalized. Unlike the preceding
checkpoint experiment, this input exactly matches the Linux catalog corpus.

Only the region rows were refreshed from clean `f42523ba`: ordinary/compiled
**3988.654/4586.564us**, 557416B, **101.787/122.771x** predecoded .NET 11,
both Gap. These separate observations are not the A/B percentages above.
[Raw selective durations](selective-refresh.txt) meet the existing 20ms floor;
generated JSON/parity/Markdown verification and both-runtime snapshot checks pass.
The large remaining O(scalars * states) preparation gap remains deferred; do not
add a benchmark-specific replay engine. No package or full catalog was requalified.

CI `41245730730` failed on Windows in the existing funded-owner close-during-call
test while Linux passed. That test blocked a pool thread waiting for another
Task.Run worker. Scoped correction `a875bdb4` uses a dedicated blocking worker
and always resumes/joins it before disposing signals; ownership/refund assertions
and the timeout remain. Its 52 focused PCRE2 tests/runtime pass. Production
ownership behavior is unchanged; the original failed log is retained in the
correctness archive.

[Driver](driver.cs.txt), [preflights](preflight-linux.txt),
[identity manifest](identities.json) and [clean-source hashes](clean-linux-hashes.txt)
bind the precommit A/B to its patch and the separate committed-source refresh.
Immutable evidence:

- [Raw/source/binaries](../../../artifacts/experiments/nb-preparation-quanta-20261031/evidence.tar.gz):
  `3C19D8676A679AA44A5EF732A775CD25DE0152CB400B22045038B19B8184056C`.
- [Correctness/source](../../../artifacts/experiments/nb-preparation-quanta-20261031/correctness-source.zip):
  `D1E34284627CF6E1F7AC516BBAC01E8E2F4890F5944806D09A0132B8C12B2F4D`.

Published/staged packages remain immutable. Final-source cumulative costs, full
refresh and fresh common-version package/consumer acceptance remain release work.
