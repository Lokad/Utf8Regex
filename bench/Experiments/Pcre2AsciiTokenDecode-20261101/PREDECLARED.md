# Checked ASCII branch in existing PCRE2 token decode

Baseline committed40c0659d, enginede2f5e67. Read-only literal/date-hot probes each
consumed2million correct public results; date-hot Tier1 still calls Rune.Decode
out of line in literal/class arms. This proves those calls exist, not their
share of public execution time. The standalone helper can inline the BCL path.

Add one checked ASCII byte branch in existing CharacterRunner.TryDecode after its
offset guard; construct one valid Rune/width1. Non-ASCII still runs the identical
Rune.DecodeFromUtf8/status comparison, including invalid/interior-byte failures.
Keep all strict full-input validation, scalar/capture/retained/cursor semantics,
original interruption authority and numeric resource charges/checkpoints. No
new decoder framework, mode, engine, selector, allocation or unchecked access.
Core DLL/PDB bytes match between lanes; retain earlier accepted improvements.

Public subroutine-forced literal/class/dot checks cover128ASCII, Unicode-width/
surrogate-gap boundaries, nonzero origin/EOF/retained captures with unlimited and
finite policy. Existing byte-escape/strict-input/resource/cancellation/shared-
budget checks plus full affected PCRE2 Release .NET10/11. Actual date-hot Release
codegen both lanes. No forwarding executor or whole solution/package matrix.

One <=120s quiet-VM public A/B with existing17date/finite/live/deadline/dense/
Unicode/UCP/captured/nonword/unchanged-control workloads. Warm1.5s/min1000,
independent60ms calibration, five alternating samples each>=20ms, consume complete
result batches,16million-call/64MiB cap. Preserve all raw durations/allocations/
adverse observations. At most one justified selected noise confirmation; no
variants. Accept only useful public gain within10% adverse public allowance;
otherwise fully revert prototype/tests. Fixed-original cumulative gate remains
at the next coherent delivery boundary. Published/staged artifacts immutable;
no package/full-catalog repetition for this small pilot.
