# Shared byte-only native discovery — accepted

Baseline2a4f6755. Native one-shot discovery validates the entire subject first,
then returns byte endpoints. Text sizes only its selected range; value output
counts prefix/range coordinates after a hit. Remove51 net production lines:
the full-metadata Core FindLiteralForResult bridge, duplicate Search-value path
and discarded description plumbing. Preserve direct anchored literal comparison,
prefix-digit/dotAll proofs and empty literals. No new mode, state, cache or API.
Historical attribution fixtures remain bound to their frozen producer libraries.

The bounded comparison uses coherent distinct Core/Python DLL/PDB pairs and an
identical caller/input graph, one ordinary .NET11 RC3 producer per process,
8CPU startup thenCPU2, normal tiering/PGO. Actual operation warmup>=2s/1024calls;
75ms calibration, results consumed after timing,1GiB actual RAM admission and
256MiB large-string output cap per sample. **384 qualified samples, minimum37.312ms**.

Two interrupted attempts remain explicit. The first baseline stopped because a
cold .NET comparator allocation wrongly capped integer iterations. Repair only
that caller cap; preserve producer pairs. The corrected run completed strict
ABBA and trusted A slots, then an unchanged Core-control sample fell below20ms.
The **sole unchanged-caller B-slot confirmation completed in27.434s**. Successful
qualified workers total about107.220s; that excludes failed attempts, builds and
tests. Both failures/complete observations are archived. No further timing/tuning.

| Public operation | Validate before → after (µs) | AssumeValid before → after (µs) |
|---|---:|---:|
| Search text, early1MiB ASCII |19.901 →11.189|11.091 →0.108|
| Search text, early1MiB BMP |915.592 →55.531|212.460 →0.116|
| Search text, early1MiB astral |2592.217 →1106.734|1090.644 →0.113|
| Match text, early1MiB BMP |913.988 →55.588|211.976 →0.130|
| FullMatch text miss,1MiB astral |2586.945 →1107.810|1088.128 →0.040|
| Match value, early1MiB BMP |872.334 →55.318|211.956 →0.055|
| FullMatch value,16KiB ASCII |0.380 →0.412|0.197 →0.287|
| FullMatch value,16KiB BMP |15.146 →3.835|3.193 →3.034|
| FullMatch text,16KiB BMP |24.407 →11.428|10.426 →10.506|

Listed early-output gains and full-value changes have disjoint observed ranges.
Trusted full-text BMP+0.77% overlaps. Accept the full-ASCII value cost+8.48% strict
(32ns)/+45.55% trusted(90ns) for removed machinery and millisecond-scale gains.
Do not add an ASCII mode/flag to conceal it. Full-text ASCII+4.72/−35.17%, tiny
text+3.82/−16.76% and finite/live full-literal−28.35/+4.97% overlap; no gain claim.
All allocations stay unchanged: early strings40B, value/miss0B, full strings
32808/16424B and finite/live144B. Original working/result admission remains.

Previously deferred Search-value ranges overlap (−3.98/+14.49%). Captured-output
controls+4.70/−36.93% overlap and retain large process modes; unchanged Core
+0.55/−0.45% and.NET−1.15/−1.49% overlap. No normalization or native-parity claim.
The summary keeps every process median/range, including adverse/uncertain rows.

Windows/Linux .NET10/11 pass full Python**5106 checks/target**, focused Core222 and
native Python309 per target. Existing all-six-API tests cover byte/UTF16 endpoints,
strict malformed prefix/tail, nullable/empty, exact/full/anchored/dotAll, original
token/deadlines, every finite work cutoff, exact string admission, retained values
and parent refunds. Three bridge-preservation tests now exercise production
adapter projection directly. Final Linux Core/Python DLL/PDBs match measured pairs.

Hash-verified offVM evidence, `artifacts/experiments/native-result-discovery-20261111/`:

- Initial interrupted caller: `433BA7566044CCEECF836AB5A4FA5C2F658345D1CA227B203B3E26EFA8BBDF19`.
- Corrected primary/complete slots: `77F8144AE511D378A6F51A80DA47FDBD4013CE4FA40F0FA33FB2D2E99CD66FBC`.
- Sole confirmation: `7358263524CDE2C03F23F758D7B4A7B7E307DA89C4988F01CDB7C0C36AE9B1C8`.
- Full Linux checks: `BD62C0D4B960332DD557C7BDF39B3728DCBCF36F69977B9CF35B1ECF0875629B`.
- Final patch/Windows/repair script/identities: `B39DC3376ED54CBC5A44C673F1B0656DD66AC66FB9CCA4F302FCA205E6FC22FC`.

Specs record consumer-owned projection and variable numeric work requirements.
Immutable packages and generated catalog histories stay unchanged; no Full/package
matrix here. Warm captured output and mandatory strict-validity costs remain.

Sourceef0b60a4 CI42983204389 is green on Windows and Linux.
