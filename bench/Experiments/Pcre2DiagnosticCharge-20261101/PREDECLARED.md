# Move diagnostic classification out of PCRE2 step enforcement

Baseline committed48df5d6c, engineecfd28a1. Move the unchanged instruction/token
diagnostic switch to the existing collector. Keep BacktrackingSteps++ and the
same TryChargeStep/ThrowIfSliceBreached at every VM instruction, including
unlimited execution. Preserve original cancellation/deadline authority, finite
work/depth/working/result caps and diagnostics/alias semantics. No attribute,
mode, framework, descriptor, candidate, selector, allocation or public contract
change. Core bytes are identical between the two compared lanes.

Existing production diagnostic/shared-budget/resource/interruption tests and full
affected PCRE2 Release .NET10/11 suite; do not add forwarding executors or mirror
the moved switch in tests. Inspect actual Release Tier1 code in both lanes.

One <=120s quiet-VM public A/B using the existing17case date driver: hit/miss,
finite/live/deadline hit/miss, no-digit miss, dense failed starts, dense Count,
Unicode, UCP, nonword/captured Count, Unicode-heavy, subroutine/bounded/literal
controls. Warm1.5s/min1000, independent60ms calibration, five alternating samples
each>=20ms; complete result batches consumed after timing, 16million-call/64MiB
batch cap. Preserve all raw durations/allocations/observations. At most one
justified selected noise confirmation; no variants. Accept only useful public
gain with <=10% adverse public cost; otherwise revert fully. Fixed-original
cumulative gate remains at a coherent delivery boundary. No package/full-catalog
requalification or modification of immutable published/staged artifacts.
