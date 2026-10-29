# Second mandatory ASCII scalar before Count verification

Accepted production source **907f0c51** extends the existing bounded leading-ASCII
proof to a second mandatory unfolded ASCII scalar. It reuses the same positive
contiguous-range/literal test, looking through at most 64 straight-line mandatory
instructions. Optional entries, forks, repeat-end/exit, negation, folding and
Unicode predicates remain opaque. Every accepted candidate still uses the scalar
VM. Rejecting the first ASCII scalar advances one byte and returns to the existing
bounded search with the original invocation. No engine, prepared state, buffer,
unchecked access or resource policy is added.

The [readonly attribution](attribution.txt) establishes that capitals uses scalar
execution on the mixed 7.38MB corpus; validation is about 0.26ms of 143ms Count.
The [prefix census](prefix-census.txt) is a necessary-condition count, not timing
or realized speedup. [First driver](driver.cs.txt), [project](driver.csproj.txt),
[raw A/B](comparison.json) and [console](comparison.txt) retain all nine rows.
One **30.46s** run compared exact 8a471caa and 907f0c51 binaries on the quiet VM,
.NET 11 RC3, CPU2: four public Count lanes, 500ms warmup, independent calibration,
three alternating samples, equal-result checks; minimum timed duration 45.38ms.
The capitals corpus returns eleven matches; allocations remain 11880 B/op.

The initial 12.6-13.5% capitals improvement coincided with 13-14% faster control
points. The single allowed [confirmation](confirmation.json),
[console](confirmation.txt), [driver](confirmation-driver.cs.txt) uses separate
assembly contexts for both producer binaries and reverses their warmup order.
Same binaries, public operations and timing settings; seven selected rows in
**25.12s**, all samples >=46.96ms. No third comparison or further tuning.

| Confirmed public Count | Before us | After us | Change | B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Capitals, ordinary | 147936.800 | 128631.100 | -13.0% | 11880 / 11880 |
| Capitals, compiled | 149504.200 | 128367.500 | -14.1% | 11880 / 11880 |
| 32 KiB, finite limits/live token | 641.068 | 554.168 | -13.6% | 3680 / 3680 |
| No first-range candidate, 4 KiB | 0.857 | 0.969 | +13.1% | 0 / 0 |
| Literal-prefix control | 12967.500 | 11135.060 | -14.1% | 3600 / 3600 |
| ASCII native control | 3.582 | 3.733 | +4.2% | 0 / 0 |
| Literal control | 77.392 | 77.477 | +0.1% | 0 / 0 |

**Intentional cost tradeoff:** proving the second range adds about **0.11us** to
the no-first-range miss. It is an affected path, with a measured 13.1% penalty;
this receipt does not claim all workloads improve or stay within 10%. The main
catalog and finite workloads improve, while storage admission and allocations
remain unchanged. The first A/B's all-uppercase false-pair miss improves 468.51
-> 48.39us and avoids 1696 B of VM scratch; dense Unicode hits are -2.3%.
Negative control variation is not credited as another improvement; the existing
literal-prefix row's published snapshot remains unchanged.

The [selected V3 refresh](selected-refresh.txt), five samples per lane, all
>=54.20ms, changes only capitals' two rows. Producer / predecoded .NET goes
**10.41x -> 9.64x compiled**, **3.07x -> 2.87x ordinary**, still Gap. This is
separate from the direct A/B; no cumulative or general parity claim is made.

[Both-runtime checks](verification.txt) cover scalar enumeration and nonzero
floors, capture/backreference alternatives, optional/nested/folded/Unicode proof
exclusions, window boundaries, final scalar/tails, denied real VM admission,
allocation-free false-pair rejection, work denial, every small work cutoff and
scratch-loan retention, original cancellation/deadlines during rejection, and
final count publication. Adjacent Core resource/inventory/API, PythonRe and PCRE2
Count guards pass. Publication adds the final cutoff/interruption fixture cases;
production code is unchanged from the timed 907f0c51 DLL. Reused driver path
arguments were corrected before timing; initial setup failures remain archived.
The existing parity verifier and snapshot checks pass. No package/full matrix.

Immutable files under `artifacts/experiments/capital-pair-candidates-20261029/`:

- `parity-capital-pair-907f0c51.tar.gz`: exact DLL/PDBs, host, source bundle,
  both drivers/binaries, build/preflight evidence, raw comparisons and snapshots.
  SHA256 `EE6A092FF2CB85BA581A4E7EE65219F11C6E0EE43EFB479DFAEA33614294B6D5`.
- `correctness-and-attribution.zip`: exact final fixture, test logs, diagnostic
  attribution and prefix census. SHA256
  `CE87CB9C170020D5DC11C43C2078EAF3ACEC3EFA9EE7C8076162DC41E11049C6`.
