# One-token conditional proof

Accepted against frozen dev59 PCRE2/Core producer `d6120197`. Reuse the existing
finite ASCII Boolean compiler for a positive/negative lookahead containing one
eligible ASCII token, optionally wrapped in plain captures. Both branches must
consume an ASCII byte at the condition position. Intersect their first classes
with the guard or its ASCII complement; discard impossible alternatives.

This adds a cold proof to the existing compiler, without a new executor, retained
guard state or mode. Existing64-alternative/128-token caps, construction work/depth
and major allocation admission remain. Empty branches, multi-token/Unicode guards,
lookbehind, control verbs and capture-dependent syntax decline. Controlled calls,
match flags and capture/result APIs retain their owned VM. Strict whole-input
validation precedes every execution route.

One **123.403-second** ordinary-process ABBA on the quiet .NET11 RC3 VM, within
the declared180s cap. Core/worker DLLs are identical between graphs; only PCRE2
changes. The existing measurement loop uses independent75ms calibration after
500ms warmup, five samples/process/lane and full values/zero loans checked outside
timing. All620 samples exceed66ms. No confirmation or source tuning.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Negative conditional hit, unlimited | 0.376 | 0.010 | −97.3% |
| Negative conditional miss, unlimited | 0.371 | 0.013 | −96.5% |
| Positive conditional hit, unlimited | 0.387 | 0.010 | −97.4% |
| Guard-class intersection, unlimited | 0.379 | 0.008 | −98.0% |
| Dense candidate search | 434.92 | 7.65 | −98.2% |
| Negative hit, finite/live | 0.506 | 0.488 | −3.5% |
| Negative hit, funded/live | 0.508 | 0.527 | +3.6% |
| Negative hit, deadline/live | 0.597 | 0.598 | +0.1% |
| Excluded ACCEPT guard, unlimited | 0.374 | 0.432 | +15.7% |
| Excluded empty branch, unlimited | 0.350 | 0.403 | +15.3% |
| Excluded Unicode guard, unlimited | 0.404 | 0.467 | +15.8% |
| Captured Count control | 11.93 | 12.88 | +8.0% |

Allocations and modeled working peaks are unchanged in all31 lanes. Target and
dense improvements occur in both independent processes. The excluded unlimited
regressions and captured Count increase also occur in both processes and remain
explicit costs of this candidate. Their finite controls improve11–12%, funded
controls6–7%, although their VM instructions are unchanged; do not attribute that
movement solely to the cold proof. Existing bounded/literal controls move+4.6/−6.5%,
unchanged Core Count~0%. No normalization, composed gains or refreshed native
ratio is claimed. [Summary](summary.json) retains individual medians and ranges.

Focused Windows tests pass53 per target. Matching binaries then pass the whole
PCRE2 suite, **6,596 per target on Windows/Linux**, .NET10/11. New independent
expectations cover branch/class intersections and nested predicates; public checks
retain scalar floors, captured assertion values, retained aliases, malformed
tails, original-token cancellation and finite work refusal. Existing deadline,
working/result/depth/shared admission, conditional control and recursion checks
pass. The initial VM preflight included a generated README in its clean-source
guard; it stopped before mutation/build/timing. Narrowing that check to code and
project files permits the intended experiment; the adjustment is preserved.

Raw declaration, driver, source patch, matching graphs, Linux checks and all
observations are copied off the VM at
`artifacts/experiments/pcre-conditional-proof-20261109/evidence.tar.gz`, SHA-256
`6C83B8924D2918E6328749B0EAC5C32B315541140AC20F30F6907B2B31283376`.
Windows logs are alongside it. Immutable dev59 packages and generated tables are
unchanged. No package/catalog qualification was run. Source CI remains pending
during the GitHub Actions outage; push/check it after service recovery.
