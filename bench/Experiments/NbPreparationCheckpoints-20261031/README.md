# NonBacktracking preparation checkpoints

Accepted source `b1dd8414`; frozen baseline `51cb070b`. Bounded epsilon cells now
use existing TryChargeStep instead of TryChargeInputStep. Every logical work
charge and original-token check remains; deadline Step retains its2048-unit
cadence. An immediate deadline check precedes table publication. Both scalar
passes, preallocation admission, stored boundaries/rows and replay are unchanged.
No new executor, field, mode, work quantum or unchecked access. [SPEC](../../../docs/SPEC.md)
documents preparation's bounded deadline checks.

[Release checks](verification.txt) pass on net10/net11: Core718/runtime,
PythonRe589/runtime, PCRE280/runtime and snapshot3/runtime. New witnesses interrupt
an empty4097-state row within the existing cadence and prevent publication when
the final cell expires/cancels; failed preparation refunds the enclosing loan.
Existing public captures, aliases, Unicode, widths63/64/65, admission and every
work/storage cutoff remain covered.

The [production-state probe](state-probe.cs.txt) yields exactly identical complete
table hashes, work counters and storage before/after. [Before](state-before.jsonl)
and [after](state-after.jsonl): region15states/561steps; Unicode14/286;
wide79/8797; final-cell63/64; long-empty4097/4098. Every case returns the original
13B loan after release. This checks full states, beyond public match-count equality.

One [public A/B](comparison.json), **94.909s**, eleven cases/four lanes, minimum
sample **41.765ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Equal Count results,
whole-subject validation and publication included; setup/compilation excluded.
Warm1.5s/min64 calls, independent60ms calibration, five alternating samples;
retained batches consumed after timing. **No confirmation** needed: deadline gains
are clear and the unchanged control does not show an adverse regression.

| Public Count | Before us | After us | Change | Allocated B, unchanged |
| --- | ---: | ---: | ---: | ---: |
| Region ordinary | 8607.469 | 8271.463 | -3.90%, unsettled | 574560 |
| Region compiled | 8327.400 | 7947.588 | -4.56% | 574560 |
| Region live | 6534.294 | 5876.544 | -10.07% | 574680 |
| Region finite/live | 10637.775 | 9703.625 | -8.78%, unsettled | 574680 |
| Region deadline/live | 21847.725 | 12594.650 | -42.35% | 574728 |
| Unicode NB | 1503.406 | 1452.997 | -3.35% | 135384 |
| Unicode NB deadline/live | 3759.550 | 2398.925 | -36.19% | 135552 |
| Wide NB | 5847.513 | 5577.256 | -4.62%, unsettled | 99560 |
| Nullable NB | 545.328 | 513.259 | -5.88% | 26832 |
| Non-NB scalar control | 6.003 | 6.075 | +1.20% | 0 |
| Literal control | 0.552 | 0.497 | -10.10%, unsettled | 0 |

Deadline targets have <=1.01x spreads, compiled<=1.05x and live<=1.02x. Ordinary,
finite/wide and literal lanes are bimodal; preserve uncertainty and do not claim
their small median differences as stable effects. Full [summary](summary.json)
retains every sample/spread. No parity, universal speedup or blanket equivalence
claim. The large region-marker gap still needs a larger algorithmic improvement.

**Input correction:** raw Scope text incorrectly says46429B. Authoritative raw
rows and hashes show that all four A/B lanes use47858B, SHA
`32957950F3A2CEE80F9E71B98CBC8201A6E73194946B4016F778B8E5D6AA016C`:
the Windows CRLF catalog copy. The [input probe](input-probe.cs.txt) confirms
1429CRLFs; [normalizing](input-windows-corrected.json) produces46429B and SHA
`507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2`,
exactly the [canonical Linux input](input-linux-canonical.json). Raw evidence is
preserved unchanged. The metadata probe's initial launch lacked adjacent data;
the same DLL was corrected beside data, without repeating a performance run.

Only canonical LF region rows were refreshed from clean `b1dd8414`: ordinary/
compiled **7940.211/7862.083us**,557416B, **154.233/158.583x** predecoded.NET11,
both **Gap**. Those point estimates exceed the older145.85/144.33x snapshot;
these separate observations are not a before/after regression experiment. They
also use LF rather than the A/B's CRLF input. Do not transfer A/B percentages
between input identities or rerun to improve Status. [Selective log](selective-refresh.txt)
retains raw durations (minimum34.923ms); generated JSON/Markdown and both-runtime
snapshot checks pass. No full catalog or package was requalified.

[Driver](driver.cs.txt), preflights and [manifest](identities.json) bind timed
precommit DLL/PDBs to their patch/test hash; clean committed-source refresh has
separate [hashes](clean-linux-hashes.txt). Immutable raw/source/correctness:

- [Raw/source/binaries](../../../artifacts/experiments/nb-preparation-checkpoints-20261031/evidence.tar.gz):
  `C09748D3236C6429A9B448C0F05CD0E318563D94DD964B66769CA8DA9BA5926F`.
- [Correctness/source](../../../artifacts/experiments/nb-preparation-checkpoints-20261031/correctness-source.zip):
  `95D030067813258EED09C601A0D13EC8771F474A6AD4CD9234A41E0B62943FC9`.

The earlier row-cache experiment remains closed. Published/staged packages stay
immutable. Final-source cumulative costs, current full refresh and fresh common-
version package/consumer acceptance remain release work.
