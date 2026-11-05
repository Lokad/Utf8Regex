# AWS quick catalog cost investigation

The dev33 Full measured about 9.7ms for AWS quick Count, versus 5.09ms in the
previous Full, with similar BCL costs and unchanged allocations. This warranted
checking the actual subject before attributing a producer regression.

One **71.488s** A0/B0/B1/A1 compares preserved Core `0dc05633` with frozen dev33
`6cf84468`, in separate ordinary Default-context processes. Six adapter/benchmark
DLL/PDB companions and the typed caller are identical; no producer rebuild or
source change. Runtime startup sees eight CPUs before CPU2 affinity. Producer
warmup is one second/minimum 16 calls; five alternating independently calibrated
samples follow. All 120 producer samples exceed 59.2643ms. Complete Count results
are consumed after timing. [Costs, identities and per-process samples](cost-summary.json).

The catalog supplies the exact pattern and 32514627-byte lossy-normalized CPython
subject. It contains eleven `ASIA` prefixes and no `AKIA`/`AROA`/`AIDA`; full Count
is zero. Dense mixed-scalar input has 1024 matches. Every result agrees with both
predecoded and decode-then .NET. This caller uses the public token overload with
a default token for unlimited calls; it is not the catalog's timing loop. Finite
calls use a live token, with the same useful resource limits on both producers.

| Public Count | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Unchanged literal control, first | 2.499 | 2.486 | -0.53% | 0 |
| Actual ordinary | 4958.70 | 5014.08 | +1.12% | 1936 |
| Actual compiled | 4940.90 | 4977.38 | +0.74% | 1936 |
| Actual finite/live | 5144.28 | 5205.06 | +1.18% | 2048 |
| Actual finite/deadline/live | 5685.82 | 5722.57 | +0.65% | 2096 |
| Dense mixed-scalar hits | 344.49 | 327.19 | -5.02% | 1936 |

Both current ordinary process medians remain near 5ms (4922.56/5046.19us), as do
both previous medians (4971.90/4921.45us). Dense current medians 326.20/327.68us
consistently improve on 346.93/344.37us. Retain the small actual/controlled costs;
do not normalize them by control movement or infer a new catalog Status.

The Full's near-doubling is **not reproduced here**. The original catalog data and
its uncertainty remain authoritative for that run. A timing-loop, warmup, earlier
workload or runtime-state explanation is not established. No engine fix is justified
by this evidence, and no confirmation, tuning, catalog rerun or package qualification
follows. Revisit only with a new demonstrated cause.

Canonical raw graphs, caller, declaration, source diff, preflights, measurements
and guards: artifacts/experiments/aws-quick-cost-20261105/evidence.tar.gz,
SHA256 `23317354E74E55C085CCD64C654EBF261FFE0E3269385E93297270DD02EA5BEC`,
verified off VM. All staged/published packages and Full observations are unchanged.
