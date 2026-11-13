# Assembled Python string output

At `b7a7e631`, text substitution output assembled from valid scalar slices,
validated/repaired templates or encoded string callbacks reuses the existing
bounded result decoder without a second validity pass. Byte callbacks retain
completed-stream checking, including joined fragments. No new API, mode or owner.

Unknown output up to4096 bytes uses the existing conservative byte-derived
string charge before allocation; Unicode budgets can deny earlier. Larger output
keeps bounded sizing/decoding. Specification and4095/4096/4097-byte tests describe
this deliberate model.426 relevant Release checks pass pertarget; original
interruption, refunds, retained strings, callback repair and malformed byte-output
rejection remain covered. Core DLL/PDB controls are unchanged.

One public SubnToString A/B plus the sole permitted confirmation, separate ordinary
processes, unchanged caller within each pair, equal complete outputs and a public
Subn byte-output drift control. Caller-valid unchanged text, same options, one
replacement per call; four template cases and one finite/live-token string callback.
Full outputs are checked before timing; bounded batches consume result checksums
afterward. No CPython ratio or general capture-throughput gain is inferred.

| Public workload | Before us | After us | Unchanged byte-control movement |
|---|---:|---:|---:|
| unicode-4095 | 6.885 | 5.229 | -1.62% |
| ascii-4096 | 5.512 | 5.246 | -1.74% |
| unicode-large | 276.260 | 180.078 | -2.98% |
| ascii-large | 203.050 | 195.768 | -3.62% |
| unicode-callback-finite | 234.492 | 154.632 | -5.15% |

Unicode ranges are disjoint between versions; control medians move1.6–5.1%.
Differing placement and that drift prevent exact causal-percentage claims. Large
ASCII movement tracks its control, so no distinct gain is claimed there. Short
allocation counts are identical; large-output counter observations have small
fractional differences retained in the raw data, not claimed as allocation gains.

The first pair is UNQUALIFIED: its after process hit the20ms sample floor. Raw
data and the driver exception remain. The one confirmation uses1000ms warmup and
250ms target samples in both lanes, the same32MiB retained bound and120s/process
cap; all measured samples exceed216ms. Its before source body is proved equivalent
to frozen82 and rebuilt only for matching generated-source/PDB metadata.
A checkout assertion interrupted after setup before that process started; its
completed baseline was preserved, and only the missing after half was resumed.
No further attempts or package/catalog/platform qualification were run.

Raw confirmation SHA256 `83EED98576A9745CAA48AA067E52E9A40016A811881F26EE945629EBB5108F4D`; all evidence is verified offVM
before keeper release. First failure and setup repair remain under
`artifacts/experiments/python-substitution-text-20261113/`, alongside confirmation.
The immutable [dev82](../DeliveryDev82-20261113/README.md) assets remain unchanged.
Current profile tables retain their separately recorded producer identities.
