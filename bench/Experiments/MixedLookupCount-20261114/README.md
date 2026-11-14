# Mixed-width lookup/count pilot — deferred

Prototype `bad2a143` counted prepared17–32-byte mixed-width UTF8 literals while
validating checked AVX512 windows. Reuse the existing anchor, prove/reuse the
ASCII prefix, confirm complete literals and preserve nonoverlap. Only the512-bit
validity predicate and prefix decoder were shared. No buffers, mode switches,
retained descriptors, unchecked access, native matcher or resource-policy changes.
Controlled/trusted/RTL and unsupported-hardware count routes were excluded.

Windows and Linux393 focused Release checks pertarget pass. Public correctness
covers borrowed offsets, vector starts/tails, dense counts, whole-input errors
before/after matches, default/trusted policies, finite limits and original
token/deadline behavior. Release FullOpts has three byte shuffles in each observed
512-bit method, no shared-predicate call and retained range-failure paths. Codegen
and isolated logic do not establish public performance equivalence.

ONE public VM A/B plus the sole unchanged-binary/caller confirmation. Core BF
dev85 baseline reused at checkout5b8505e8; changing DLL/PDB/source identities match.
Ordinary separate processes, identical input/options/full Count outputs,9 samples
perlane/250ms target/>=20ms actual. Strict whole-subject checking is timed;
AssumeValid Count is the matching-route control. Finite/live uses the same
work10M/depth4096/working16MiB/result16MiB and original token on both producers.

| Public strict Count | First before → after us | Sole confirmation before → after us | Confirmation trusted control movement |
|---|---:|---:|---:|
| chinese | 68.219 → 61.738 | 70.665 → 63.090 | +56.20% |
| dense-mixed | 58.683 → 16.486 | 59.563 → 17.065 | +0.06% |
| false-columns | 26.712 → 24.207 | 27.336 → 24.893 | +72.25% |
| ascii-miss | 29.417 → 14.516 | 30.304 → 15.111 | +43.07% |
| unaffected-validity | 11.065 → 13.389 | 11.422 → 13.773 | +54.15% |
| chinese-finite-live | 93.695 → 254.077 | 97.599 → 131.546 | +49.53% |

Defer this isolated patch. Chinese corpus improves9.50%/10.72%, dense mixed
71.91%/71.35% and all-ASCII miss50.65%/50.14%; measured allocations are identical.
RETAIN finite/live+171.17% then+34.78%, unaffected-validity+21.01%/+20.59%, and
large unchanged trusted movement (confirmation+43..+72%, dense control+0.06%).
No normalization, exact causal explanation or new .NET parity ratio follows.
The dense benefit does not justify the broad borrowed/control and finite costs
for this delivery priority. This is an engineering disposition, not a universal
10% veto or proof that every combined SIMD algorithm must fail.

Purge the entire counter, exposed anchor getter, predicate/prefix refactor,
facade selector and existence-preserving pilot tests. Production src/tests are
byte-equivalent to accepted5b8505e8. No dormant engine or alternate switch stays.
No further timing, tuning, source/package matrix or Full run for this prototype.
Dev85 and all540 staged package/symbol files preserve their hashes.

Raw verified offVM before keepers56214/29513 closed, both terminal0. First SHA256
`6BBC1F8E5B5B08643BD0C3B465B69B07A96E4EEBB0BDDB2EB08310E761F9DE7A`; confirmation `C96498345F7E0C5202FBE54ABDD6211A1D2D5E9B9BBC8881DC333183FFB0E042`.
Raw: `artifacts/experiments/core-mixed-lookup-count-20261114/`.
[Acceptance](acceptance.json) preserves samples, placements and uncertainty.
