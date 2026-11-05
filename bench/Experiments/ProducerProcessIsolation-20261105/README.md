# One producer per comparison process

**Measurement correction.** The disposable dual-AssemblyLoadContext A/B driver
produced a9.81% folded-Russian difference with byte-identical producer graphs.
It is unsuitable for attributing that difference to an implementation change.
Future source comparisons use separate ordinary processes, each loading exactly
one Core producer. Existing Full/catalog reporters already use one producer;
their published observations and frozen qualification remain unchanged.

One bounded identical-binary diagnostic took21.5661867s for four cases,40 producer
samples>=55.122ms. Both slots used Core
`B2C9D5A4CF8935E1AB68D26DCA4E8FDE07A8E49D7E2C4E28463279EE2F3D4B16`
and identical companion DLL/PDBs. [All observations](identical-binary-control.json):

| Same public operation / same DLL | First slot µs | Second slot µs | Movement |
| --- | ---: | ---: | ---: |
| ASCII |2.49|2.47|−0.91%|
| Email |1202.87|1189.61|−1.10%|
| Chinese |92.41|92.85|+0.47%|
| Folded Russian |232.24|255.03|+9.81%|

Two existing CLI runs in the default context observed241.858/253.926µs for folded
Russian. They are single-sample contextual diagnostics, not equivalent to the
qualified comparison or proof of a cause. No constant correction is subtracted
from old results; loading, code generation and process uncertainty are not
identified separately by this experiment.

One **94.173-second** corrected comparison then reused the exact frozen classifier
before433F388D and afterB2C9D5A4 binaries. This repairs a demonstrated measurement
defect; it introduces no source variant or optimization retuning. An identical
strongly typed caller runs sequential A0/B0/B1/A1 workers, each asserting one
loaded Core in the ordinary Default context. All six companions remain identical.
There is no permanent runner, new qualification framework or library switch.

[Corrected observations](corrected-comparison.json) pool ten normalized producer
samples per version/case and preserve each process's median and spread:

| Public Count | Before µs | After µs | Change | Bytes before/after |
| --- | ---: | ---: | ---: | ---: |
| ASCII control |2.48|2.46|−0.84%|0 / 0|
| Actual Chinese |95.39|91.17|−4.43%|0 / 0|
| Actual Chinese, compiled |96.05|91.46|−4.78%|0 / 0|
| Chinese alternation, compiled |152.52|149.88|−1.73%|0 / 0|
| Case-sensitive Russian, compiled |189.64|178.67|−5.78%|0 / 0|
| Folded Russian control |232.99|240.70|+3.31%|0 / 0|
| Chinese, finite/live |1171.98|1208.54|+3.12%|112 / 112|

The classifier's primary improvement remains. Retain the smaller observed folded
cost of7.72µs and finite cost of36.56µs without assigning a speculative cause.
The earlier dual-context11–15% folded figures remain historical observations;
they are superseded for producer-effect claims. Other historical deltas measured
by that scratch method also need appropriate uncertainty; this does not reopen
closed experiments or invalidate independent Full tables.

Each worker sees eight processors before CPU2 affinity. Producer warmup1.5s/min128,
BCL250ms/min64, independent60ms calibration, five alternating samples and typed
checksums after timing. All140 producer samples exceed55.7039ms. Each worker
also measures equal-result predecoded Regex and strict decode-then-Regex lanes.
Those are diagnostics, not a catalog refresh or new parity classification.
No further confirmation or tuning follows this corrected comparison.

Immutable artifacts/experiments archives:

- dual-alc-control-20261105/evidence.tar.gz:
  `47112579ADF2675E01099D6710F5327A9F256BD023F2D6AC0DFBCDCE66A71116`.
- default-context-comparison-20261105/evidence.tar.gz:
  `7928FA43D30BCDBAE1F2E371BC7C9C0CD55095BBB5872DE40EBC00459D57D5EC`.

They retain declarations, caller source/binaries, both frozen graphs, every sample,
preflights, load-context assertions, ordinary CLI logs and terminal0. Both scoped
experiments preserved production source, accepted benchmark graph, immutable
packages and existing tables. No source/platform/package matrix was repeated.
