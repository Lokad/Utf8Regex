# Bounded ASCII subroutine proof

Baseline `85d311a9`, .NET 11 RC3 quiet VM. Reassess the
[Nov1 proof](../Pcre2AsciiSubroutine-20261101/README.md) against current source and
the approved major-gain/local-regression policy. The old universal 10% rejection
and dual-load-context timings do not decide this pass.

Reuse the existing Boolean-only finite-ASCII compiler for a lowered callee
containing exactly CaptureStart(slot), one eligible ASCII token, CaptureEnd(same
slot), Return. No recursive expansion, lookup cache, new executor or state mode.
Numeric references share the existing analysis resolver; names/targets use current
program metadata. Existing 64-alternative/128-token caps and major construction
admission remain. Complex/recursive/Unicode/capture-dependent languages decline.
Controlled IsMatch, match flags and capture/result operations retain the owned VM.

One **123.030-second** ordinary-process ABBA under a declared 180-second cap:
same Core/worker, changed adapter, independent 75 ms calibration, five samples
per process of at least 20 ms after 500 ms warmup. The driver admits up to 16M
calls for very fast operations; it allocates no result batch. Full results and
zero loans are checked outside timing. No confirmation or tuning campaign.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Call hit, unlimited | 0.47 | 0.014 | −96.9% |
| Call miss, unlimited | 0.42 | 0.011 | −97.4% |
| Class call, unlimited | 1.28 | 0.015 | −98.8% |
| Dense call search | 506.08 | 6.30 | −98.8% |
| Call hit, finite/live | 1.46 | 0.91 | −37.4% |
| Call hit, deadline/live | 1.60 | 2.23 | +39.3% |
| Complex call, finite/live | 1.69 | 2.25 | +33.1% |
| Recursive call, funded/live | 2.09 | 2.76 | +32.0% |
| UCP call, finite/live | 1.46 | 2.04 | +40.0% |

**Accepted with explicit adverse controls.** Named/relative unlimited calls also
improve 96.9%. The large target/dense gains and adverse cases occur in both
processes. Allocations and modeled working peaks are unchanged across all 31
lanes. Controlled fallback instructions remain unchanged: observed finite gains
are not attributed solely to the callee proof. Deadline miss +46.2%, complex
unlimited +24.2%, recursive finite +30.0%, UCP unlimited +25.6% are retained.
Captured Count +4.6%, existing bounded −11.1%, literal +6.3%, unchanged Core Count
+0.9%. Do not normalize by controls, compose gains or infer refreshed native ratios.
[Summary](summary.json) retains every process median, sample range and allocation.

Focused Windows checks pass 198/target, then the whole affected profile passes
**6,574/target** on Windows/Linux, .NET 10/11. Matching Windows binaries are reused
for the broad suite. New independent public checks cover absolute/named/relative
and class calls, valid scalar floors, caller capture retention, malformed tails,
original cancellation/work refusal and conservative exclusions. Existing deadline,
depth/heap/result/shared admission and recursion coverage passes. An initial
resolver class-name build error was corrected before timing; its log is retained.

Raw protocol/driver, patch, matching producers, Linux checks and observations are
copied off the VM at
`artifacts/experiments/pcre-leaf-proof-20261109/evidence.tar.gz`, SHA-256
`EAE60CE7B187C57E848D6DE01B3A11E4769D688631AF117C3572F320140097BE`.
The reused runner's first comment names the earlier empty-workspace template;
its actual source/commands, protocol and 31 measured lanes belong to this trial.
Existing packages/snapshots stay immutable. Common dev59 will qualify the
accumulated source and current end-to-end operating cost once at delivery.
