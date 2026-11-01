# Borrow immutable PCRE2 VM instructions

The shared VM now reads each instruction by readonly managed reference from its
existing checked array element. This removes a value copy without changing any
execution route, algorithm, state, allocation, capture or metering rule. No new
helper, selector, engine, metadata or unchecked access. Public contracts and
numeric work/depth/working/result controls are unchanged.

The baseline is exact dev.4 engine2ebc01a7; parentce3a4e40 adds only data/receipts.
The [patch](runtime.patch) is one source line. This source change is **not** in
the immutable staged dev.4 packages; no package qualification or repacking ran.

Actual Linux .NET11RC3 [before](codegen-before.txt)/[after](codegen-after.txt)
Tier1 evidence confirms the change. The instruction is40bytes, queried with
Unsafe.SizeOf rather than estimated. Before, all fields were eagerly loaded and
spilled before ChargeBacktracking. After, only kind/token-kind are loaded there;
the selected case reads its remaining fields. Code size17526→17329bytes. Each
diagnostic lane checked2million public results. These are codegen observations,
not public speedup estimates. Full assembly listings/source/binaries are archived.

The [single public comparison](comparison.json) completed **63.837s**,17workloads,
minimum34.193ms samples, equal results and unchanged allocations everywhere.
Same Core bytes; existing typed delegate driver with isolated contexts, warm1.5s/
min1000calls, independent60ms calibration and five alternating samples. Result
batches are fully consumed after timing. No confirmation or variants ran.

| Public operation | Change |
| --- | ---: |
| Dense unanchored search |−7.77%|
| Captured Count |−5.46%|
| Finite/live hit |−4.25%|
| Finite/live/deadline hit |−5.56%|
| Named-call hit |−5.95%|
| Recursive control |−3.12%|
| Short no-match |+3.29%|
| Existing bounded control |+6.44%|
| UCP control |+0.86%|
| Literal control |+0.61%|

[All medians, paired observations and allocations](summary.json) are retained.
The largest observed adverse median6.44% is within10%. This is representative
public coverage, not universal parity or a replacement for the fixed-original
cumulative gate at a coherent delivery boundary.

Full affected PCRE2 Release checks passed **6472 per runtime** on .NET10/11,
including scalar/validation, captures, retained aliases, original interruption
authority and finite-resource behavior. No API or public accounting contract
changed, and no new implementation-mirroring test was added.

Immutable archives under
`artifacts/experiments/pcre2-vm-instruction-borrow-20261101/`:

- `evidence.tar.gz`: exact before/after DLL/PDBs, patch/full source, public driver,
  preflight/build logs, measurement and both complete codegen probes/listings.
  SHA256 `841C3FC9BE57B3838AE3DAA7656D4478C895EE195589468BBB341BB5E6EC7A76`.
- `correctness.zip`: matching Windows assets and both-runtime checks, SHA256
  `C94DA27EF5BD21CC9837644DD720662AB526FC827204575F093D3AEBEEEE6EF0`.

The rejected single-token subroutine expansion remains reverted and separately
documented. Sourcee6779865 CI41311508437 passed Windows/Linux.

Six existing native rows were selectively refreshed at that clean committed
source with the existing raw and nine-pair protocol. All remain qualified
NativeFaster: date miss/match11.44x/11.32x, subroutine4.35x, conditional negative
4.40x, conditional ACCEPT-negative4.59x and conditional positive3.89x. Keep the
adverse ACCEPT-negative ratio movement from4.35x; unlike the controlled A/B,
these ratios include comparator movement and are not causal speedup estimates.
Other120rows/identities/statuses and scaling evidence are unchanged. Minimum
paired sample32.286ms; existing qualification/Markdown verification and one
snapshot check per runtime passed. No resampling for Status or package refresh.

[Selective summary](selective-summary.json); exact source/binaries/generated/raw
evidence is sealed as `selective-evidence.tar.gz` in the same artifact directory,
SHA256 `C7A643CC2D17A73B202977E245FC6E5A64A602AC3DE03B31715CD23D06AD4744`.
