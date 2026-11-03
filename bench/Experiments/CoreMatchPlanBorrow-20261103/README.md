# Borrow the immutable plan for Core value Match

The finite Core value Match path copied the immutable 2,720-byte SearchPlan before
passing it to the existing by-reference literal finder. Its owner already exposes
a stable readonly reference. One local now borrows that reference for the same
synchronous discovery/projection calls. No route, algorithm, allocation, work
charge, admission, cancellation, deadline or result-lifetime contract changes.

FullOpts code generation confirms removal of the actual 0xAA0-byte bulk copy.
The value helper's frame falls **3,272 -> 552 bytes**, code **3,594 -> 3,465 bytes**.
These are diagnostic facts; public measurements below govern acceptance.

One quiet-VM .NET 11 RC3 comparison completed **24 public cases in 110.782s**,
under 120s; minimum sample **27.056ms**. Frozen original `a40bda03`, parent
`fdbb9783` and its archived Core patch use identical Python DLL/PDB bytes across
the parent/candidate comparison. The previous twenty Search/value/count cases
are unchanged; four Core Match controls add finite Unicode, family, folding and
default policy. Normal eight-CPU startup precedes CPU2 affinity, 500ms/minimum128
warmup, independent 60ms calibration and five alternating >=20ms batches.
Complete range/count digests are consumed after timing, with actual allocations.
Equal-result predecoded/decode-then compiled .NET diagnostics remain separate.

| Public operation | Parent us | Candidate us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| Finite/live Core Match hit | 0.28660 | 0.21087 | -26.42% | 112 |
| Finite/live Core Match miss | 0.22071 | 0.14979 | -32.13% | 112 |
| Finite/live Unicode Match | 0.32309 | 0.25881 | -19.90% | 112 |
| Finite/live literal-family Match | 0.30577 | 0.24495 | -19.89% | 112 |
| Finite/live folded Match | 0.33224 | 0.29666 | -10.71% | 112 |
| Default Core Match control | 0.16808 | 0.14025 | -16.55% | 0 |

**Accept:** all allocations remain unchanged; maximum adverse parent cost is
**+4.59%**, finite Python Search miss. No confirmation or variant. These are
complete public costs, not kernel estimates. [All results](summary.json) preserve
every case, allocation and paired sample, including original-baseline ratios.
Smaller frame alone is not used to attribute changes on other routes.

Focused existing Release checks pass **Core952/PythonRe171 per runtime** on
.NET 10/11: native literal values/validation, work/working cutoffs, original
interruption, captures, cursors and funded ownership/refunds. No mirrored new
test, API, framework or unchecked access was introduced.

Immutable raw evidence under `artifacts/experiments/core-match-plan-borrow-20261103/`:

- `evidence.tar.gz`, SHA256
  `FED48205B6175CDD52CD9211223DE66375025CC6C8ABCD43FAE6C4882873509F`:
  frozen parent source, exact before/after assemblies, original asset hashes and
  immutable-archive reference, patch, driver/project, preflight, code generation,
  complete timings/allocations, scripts and exits. Original-byte symlinks are
  retained as references; original archive `333FF4E5...` remains authoritative.
- `correctness-source.zip`, SHA256
  `DB2BCC8B6F6FA6279FB50A642C80A90D2D74D48D13978F6563657BF74AD38F9D`:
  exact changed source and focused checks, sealed before committing.

No package, generated benchmark table, complete platform matrix or Full refresh
was repeated. This parent/source acceptance does not erase dev.15's stable
fixed-original delivery failures. Retain all 189 cases, H5's actual consumer,
Unicode Search, finite Core Match and these four added controls at the next
coherent common-version cost/package/consumer/Full delivery gate.
