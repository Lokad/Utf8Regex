# Prove immutable ASCII intervals during lowering

Accepted with explicit uncertainty and cold-path costs. Common lowering proves a
positive, unfolded ASCII mask's exact interval once, storing packed endpoints in
the Set instruction's previously unused D operand. Zero means unavailable.
The shared candidate/repeat helpers read that fact instead of repeating mask
reads, population counts and endpoint extraction for every match. No instruction
or program size increase, allocation, per-cursor field, owner, mode or new runner.
The Set emitter is shared by all four profiles; other instruction kinds retain
their D meanings. A private unused argument is removed from the existing helper.

Holes, empty masks, complements, folding and Unicode predicates keep their actual
matching routes. Existing bounded scans, work/depth/storage/result admission,
strict/default and trusted input contracts, captures/history, RTL/NB, original
interruption and retained aliases are unchanged. This moves a bounded constant
proof into already metered construction; numeric execution charges are unchanged.
No public contract change or additional accounting mechanism is introduced.

Baseline b39f3cca. One **123.004s** ordinary-process ABBA per policy, 15 public
lanes, 600 samples with minimum **38.858ms**. Quiet .NET11 RC3 VM, normal 8-CPU
startup then CPU2, default tiering/PGO, 1GiB actual available RAM admission.
Explicit distinct Core DLL/PDBs; Python and caller pairs are identical. All result
values are checked after timing. Calibrate to 75ms with one calibration check.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| Many-capture strings | −9.45% | −4.42% |
| One-capture strings | −5.50% | −3.21% |
| Many-capture byte tuples | +0.24% | −3.19% |
| Captured Count | −2.18% | −2.05% |
| Many strings, finite/live | +0.91% | −4.14% |
| Many strings, deadline/live | +0.80% | −3.67% |
| Long captured detailed match | −12.93% | +22.45% |
| Short captured match | +0.01% | −9.50% |
| Unicode-prefix captured strings | −6.13% | −5.90% |
| Captured split | −7.10% | +6.52% |
| Construct captured expression | +4.34% | +3.79% |
| Construct eight classes | −0.81% | −1.79% |
| Unchanged folded-Unicode control | −1.78% | +1.39% |
| Unchanged Core literal control | −7.84% | +0.29% |
| Unchanged predecoded .NET control | +0.88% | −0.53% |

Only strict captured Count has disjoint full sample ranges. Public capture-string
process medians suggest a useful gain, but long/split/construction variability
could change promotion. The sole **51.777s**, six-row confirmation reuses the
exact producer pairs with one common caller selecting those rows before timing:
240 samples, minimum36.920ms. No producer/source/tuning change or further timing.
A script-anchor assertion failed before confirmation timing; it was corrected
after terminal failure and a local six-row smoke, without altering any producer.

| Confirmation, public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Many-capture strings |776.096 →705.323 (−9.12%)|752.197 →690.151 (−8.25%)|
| One-capture strings |466.074 →444.936 (−4.54%)|452.987 →434.087 (−4.17%)|
| Long captured detailed match |11.558 →11.641 (+0.72%)|14.138 →15.389 (+8.85%)|
| Captured split |445.395 →439.915 (−1.23%)|464.647 →391.796 (−15.68%)|
| Construct captured expression |130.688 →131.627 (+0.72%)|128.074 →137.584 (+7.43%)|
| Unchanged predecoded .NET control |0.059 →0.061 (+3.82%)|0.060 →0.060 (+0.46%)|

Both candidate process medians beat both baselines for one-/many-capture strings
under both policies in confirmation. Full sample ranges still overlap. Accept
the consistent useful capture-string movement and simpler runtime proof; retain
the adverse cold and long observations and uncertainty. Split has no stable gain
claim. No control normalization, cross-caller pooling, CPython/native parity
verdict, catalog Status or generated-table update follows this producer A/B.
Regular allocations are unchanged (242008B tuples, 94344B single strings,
272B Count/short match, 69648B captured construction); occasional small runtime
traffic in the long-match samples remains in the raw evidence.

Windows and Linux Release pass per .NET10/11: Core509, PythonRe1070, Pcre2259,
Posix24; disabled-intrinsic boundary guards12 per target on both systems. New public cases
cover NUL, endpoints63/64/127, scalar prefixes/tails, holes, complements, Kelvin
folding and useful rewinds. Existing capture/lastindex/alias, finite policies,
construction admission, original cancellation/deadlines and malformed-subject
checks remain included. The validated Linux Core DLL/PDB exactly match both
comparisons: E1E16546445513E6379099CF5C7B90D8DE0572B86471F81392338327A32A876B /
B14170B39F77B4387A3B2DCEE6B15C98E1EC7969A2B556D19EAD31FFD59F7E66.

Primary and confirmation archives are copied off VM and hash-verified under
`artifacts/experiments/prepared-ascii-interval-20261111/`:

- Primary:38DEDF6CA0860C6ED9A9CCF30A5C28E5A7960EAB175AB9F5F43E249C6A69562F.
- Confirmation:537BC123606A5D4FCE2DAD62D8DACB1EE7DCD1125861A1225B8367F0EB5D5203.
- Windows checks:F0D58DB189E7EB395594F1193B9A66584690CD4AE1159A27C886AFF7DD222C18.
- Linux checks:57A9153A479121D9FECC02E4EC53FA4F5FC4A3B837EA7C93598294EE86785FD5.

Primary declaration names two source logic files; the actual archived patch also
contains the private helper's unused-argument call-site cleanup in a third file.
Both declarations, every sample/process identity, caller/source patch and explicit
DLL/PDB pairs remain available. Published/staged bytes and accepted catalog
identities are untouched. No Full/package matrix is repeated for this step.

Source6f46f988 CI42963239174 is green on Windows and Linux. The subsequent
selective refresh is recorded in [CurrentGapRefresh](../CurrentGapRefresh-20261111/README.md).
