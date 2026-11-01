# Fuse exact-count scalar classes

Accepted source `27c0b2eb` reuses the existing class-repeat runner when a pure
single-class atom has equal minimum/maximum counts. That repeat has one endpoint,
independently of its tail or an assertion's required end. Captures and following
instructions still execute normally. Variable counts, captured bodies, lazy
repeats, RTL and NonBacktracking retain their existing paths/proofs. No new opcode,
state, API, engine, unchecked access or public contract.

The [actual Python FullMatch program](route-before.json) for
`[A-Z]{3}[0-9]{3}` has10instructions. Its first exact repeat previously used the
general loop because its tail is another repeat. The existing fused path now
avoids that repeated dispatch and bookkeeping. Per-scalar/classification work,
terminal interruption, original invocation/deadline and major-buffer admission
remain. Numeric work/storage charges already depend on route/version; skipped
bookkeeping is not charged. Less existing journal growth can also lower storage.

[One public comparison](comparison.json),91.262s,16cases/four lanes, minimum39.917ms.
Frozen `6592113a` Core versus that source plus the archived seven-line patch;
PythonRe bytes are identical. Local parent `496e15ca` adds receipts only. CPU2,
.NET11RC3, isolated contexts and typed public calls; actual-loop warmup1s/
min1000calls, independent60ms calibration, five alternating samples. Results are
consumed after timing. FullMatch and anchored .NET Match consume equal success/
UTF-16 range projections; Core lanes consume Counts. Comparators use overlapping
BMP class semantics; supplementary scalar behavior is tested separately.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual short FullMatch miss |0.598|0.538|−10.06%|
| Short FullMatch hit |0.614|0.530|−13.73%|
| Finite limits/live token |0.724|0.677|−6.54%|
| Deadline/live token |0.874|0.819|−6.33%|
| Mixed BMP scalars |0.726|0.675|−7.03%|
| Nonzero byte offset |0.711|0.634|−10.83%|
| Core folded-class Count |117.217|97.771|−16.59%|
| Core long exact-repeat Count |365.880|58.706|−83.95%|
| Core ordinary Count control |16.720|17.580|+5.14%|
| Unchanged literal Count |6.239|6.208|−0.50%|

[All medians, spreads, pairs and allocations](summary.json) are retained. Target,
finite/deadline, Unicode, offset, folded and long-repeat gains hold in every pair.
Largest adverse median5.14%; RTL has one adverse10.05% pair, and short-case spreads
reach1.129x. These are representative costs, not universal/noise-free equivalence.
Allocations are unchanged except long-exact4800→1680B. No confirmation or variants.
No CPython/catalog Status refresh or unchanged package qualification. Carry all16
cases into the next fixed-original coherent gate alongside the prior union14 and
existing43lanes; this parent comparison does not replace that gate.

Both runtimes pass7169Core VM/repeat/lifetime checks,356additional Core work/funded/
deadline checks and the full4513PythonRe checks. Focused coverage includes capture
rollback/history, folded and supplementary classes, exact/empty counts, overlapping
tails, variable/lazy/captured bodies, assertion-required endpoints, every work
cutoff, original-token cancellation/deadlines and caller loans. Existing contract
documentation already states route/version-dependent charges; no contract change.

Immutable artifacts under `artifacts/experiments/fixed-class-repeat-20261101/`:

- `evidence.tar.gz`, SHA256 `ED3B043CD24614453A8241F8748D5EEFBDEDA165B854D5DB15CE4089EBD3DA53`.
- `correctness.zip`, SHA256 `CD0B5785742C8D34644407DCAD4BBF4DAD7FBC169E3347EC3106178C22F35DDB`.

The raw archive includes binaries/PDBs, parent source, applied/source-test patches,
driver/identities and both preflights. The driver was revised before timing to
consume ranges instead of Success only; exactly one timing attempt ran.
[Verification scope](verification.json).
