# Short result decoder: rejected and closed

Source1cd0f219; immutable Core/Python116 is the before input. A nine-line prototype
used checked Encoding.UTF8.GetString for a nonempty <=4096-byte validated result,
retaining existing exact admission and two checkpoints. Empty/larger decoding
kept the existing callback/block loop. Only that Core method changed; Python and
the local public driver were identical on both phases. No public API, matching,
policy, ownership, Unsafe.SizeOf or checked-access campaign change.

One quiet-VM FullOpts/R2R-off public before->after pair, .NET10.0.12/CPU2:
35.913s,180 retained samples, every sample>=80.151ms. Each lane warms150ms,
calibrates to80ms and has five alternating lane-order samples. Result strings/tuples/
counts are preflighted outside timing; timed result values stay retained.
Unlimited, all-four finite controls and finite/live-token lanes execute.

| Public operation | Unlimited | Finite | Finite + token |
| --- | --- | --- | --- |
| Named capture findall strings |+9.11%|+8.22%|+7.56%|
| Capture tuples |+6.94%|+4.82%|+4.56%|
| Unicode capture strings |+2.59%|+2.61%|+4.05%|
| No-match replacement4096B |+1.83%|+3.36%|+2.38%|
| Replacement4097B, retained codec |+1.59%|+1.13%|+1.52%|
| Unchanged captured Count |−3.54%|−6.32%|−4.47%|

The4097B lane retains the original codec but pays the new length predicate;
despite its raw driver name ending in control, it is an affected neighboring
workload. Count is the unchanged public control and does not call this helper.

Allocations match per lane. Named/tuple/Unicode sample spreads are modest, while
some replacement/control samples vary up to1.232x. Count's between-phase shift
limits causal precision; do not normalize it away or claim exact kernel costs.
Affected result-heavy operations all slow down, so no useful gain supports the
additional branch. Reject without a confirmation or tuning campaign. These costs
are public operations, not isolated decoding timings. The <=10% unchecked-access
allowance is not a reason to retain a slower optimization that removes no unchecked
access. No broad profile speedup or current-package performance claim.

PCRE2 also calls this shared decoder. Its caller trace was recorded before any
promotion; it did not receive public timing qualification because the prototype
was already rejected. Core/Python measurements do not imply PCRE2 equivalence.
No profile, package or immutable artifact adopts the prototype.

Prototype focused Release: Core323,Python576 pass (no failures/skips); eighteen
public before/after preflight lanes also agree. Restore the production source
exactly and rebuild matching affected binaries with focused result-string checks: six Core admission and22 Python public
ownership cases pass. Production source is identical to the committed baseline.
No new permanent test/benchmark framework, switch, mode or qualification matrix.
Existing major admission, bounded interruption and actual SizeOf queries stay.

Raw samples, payload, prototype binaries, source identities, test/preflight logs
and summary: artifacts/short-result-decode-20261020. Core before3EC6DE92,
after660778B2; frozen Python6232CFFC; driver166D74B0. This is a closed source
experiment, not a mixed-package delivery. Immutable116 and prior bytes are intact.
