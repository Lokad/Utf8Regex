# Per-attempt class-start check: rejected and restored

The trial used the actual mandatory leading scalar-class predicate to reject
impossible positions inside existing conservative delimiter windows before a full
VM attempt. Membership-positive starts retained complete verification. ASCII
range and Dot prechecks were excluded, and no helper, field, plan, backend or
mode was added. The hypothesis targeted repeated failed starts in the dense
class/whitespace/finite-gap Count control.

One quiet-VM .NET 11 RC3 comparison completed **20 public workloads in 59.831s**,
under 120s, minimum sample **55.544ms**. Parent `8c641719` versus its archived Core patch, with identical
Python DLL/PDB bytes. Normal startup precedes CPU2 affinity; existing 500ms warmup
(minimum 128 calls except the full corpus), independent 60ms calibration, five
alternating batches, complete Count results after timing and allocations remain.
Compatible .NET predecoded/decode-then diagnostics are separate; Python-only
membership rows retain exclusions. No original-corpus or cumulative gate replay.

| Public operation | Change |
| --- | ---: |
| Dense Core gap Count | -14.12% |
| Finite/deadline gap Count | -16.22% / -11.95% |
| Full mail Count | -12.46% |
| Finite mail Count | -7.81% |
| False gap windows | -18.01% |
| Class/whitespace Count | **+11.73%** |
| Dot-prefix control | **+12.26%** |

**Reject:** both adverse controls are stable above 10%, with unchanged allocations.
[All results](summary.json) retain every case and sample. There was no noise
confirmation or tuning variant. Dot was excluded from executing the predicate;
its regression is not attributed precisely to predicate work or code layout.
The target gains do not override the failures.

The entire 16-line production precheck and two prototype tests were restored.
No admission/semantic change survives. Prototype Release checks passed Core
458+79 and PythonRe 65+44 per runtime on .NET 10/11, including scalar floors,
captures/references, malformed tails, every work cutoff, original interruption
and funded loans. After restoration, Core109/PythonRe39 per runtime pass again;
the VM recovers byte-identical parent Core/Python DLLs and PDBs. No package,
catalog, complete platform matrix or full refresh was performed.

Immutable raw evidence under
`artifacts/experiments/class-start-filter-20261103-deferred/`:

- `evidence.tar.gz`, SHA256
  `64A0CA7B2095B12A842C654CB1E2A89932A24C8C60232D292FAF990BED6D5864`:
  frozen parent source, exact before/after assemblies, patch, driver/project,
  preflight, raw timings/allocations, scripts and exits.
- `correctness-source.zip`, SHA256
  `12BF11BC12A6CE7412EB6AA8461E4B18052DAF7DFD4ABF20B0B0819097016009`:
  complete prototype source/tests and all focused checks, sealed before restoration.
- `restoration.zip`, SHA256
  `36F0868CD37870BDF7259A583762B64D1DF729460BCA2ABC07F5F149DDDD88B1`:
  restored checks and exact recovered runtime hashes.

The per-attempt predicate in the hot counter stays closed. Further work needs
a distinct demonstrated cause and invariant; it must preserve the negative
controls and the fixed-original delivery requirements. The accepted Search
checkpoint cleanup remains, with its separate source CI green. No release
acceptance or downstream adoption is claimed.
