# Single-byte ASCII string output — accepted with local costs

Source06ef69e4 → df831e51, .NET11GA on the dedicated VM. Only the existing
result ledger's unknown-length one-byte ASCII branch changes: char.ToString
replaces generic UTF8 decoding. Admission occurs before construction; the same
conservative extent, original interruption checkpoints, release and result
traffic remain. Known-length decoding and every other byte/length keep their
prior path. No cache, fields, execution mode or decoder framework is added.

ONE six-case public A/B, nine250ms target samples/case,1s warmup, then the SOLE
noise confirmation in reverse producer order with identical DLL/PDB/caller bytes.
One Core and the unchanged PythonRe producer per ordinary process. Loaded PDB
source/image checks pass; output arrays are fully checked outside timing in
bounded16-result batches. Count is an unchanged non-string control. Samples all
exceed20ms; process medians/ranges are observations, not confidence intervals.
PythonRe's input is caller-valid unchanged UTF8, equivalent to Python str.
This version comparison does not introduce a new CPython parity ratio.

| Complete public operation | Initial before → after µs | Initial change | Reverse confirmation | Bytes/op, unchanged |
|---|---:|---:|---:|---:|
| captured-split | 299.723 → 295.331 | -1.47% | -6.38% | 274,648 |
| ascii-single-findall | 101.541 → 117.852 | +16.06% | +8.40% | 32,816 |
| word-findall | 321.951 → 314.083 | -2.44% | -0.74% | 94,280 |
| unicode-findall | 606.066 → 505.947 | -16.52% | -14.63% | 137,808 |
| finite-captured-split | 396.539 → 398.873 | +0.59% | +0.54% | 278,848 |
| unchanged-count | 132.254 → 135.302 | +2.30% | +1.42% | 0 |

ACCEPT: Unicode string output improves14.63–16.52%; captured split1.47–6.38%.
INTENTIONALLY RETAIN the ASCII literal FindAll+8.40/+16.06% operation cost and
finite split+0.54/+0.59%. Word output improves0.74–2.44%; Count moves+1.42/+2.30%
and is not subtracted. Local costs for larger gains follow the authorized policy;
10% is not a universal delivery gate. This is not a claim that every case wins.
The exact-literal FindAll reuses a canonical string; its regression does not
prove repeated decoder calls became slower. Unicode includes one ASCII scalar
in four; we do not assign the entire observed gain to that branch.

Split is11776 bytes/2048 matches/4097 strings, single FindAll4096/4096,
word11776/2048 and Unicode5120/2048. The prior CPU receipt's prose10752-byte
description was a transcription error; its raw profile/acceptance already say
11776. Corrected without another capture or changing any measurement.

Focused Release checks on Windows AND Linux, EACH target net10/net11:
Core418 + PythonRe607 pass. Coverage includes all256 single-byte decoder
inputs (invalid bytes preserve internal decoder fallback, not a new malformed
subject promise), ASCII controls/NUL, Unicode/captured split, conservative
denial, original cancellation/deadlines, output and retained-result lifetimes.
Frozen Python producer hashes stay unchanged. No package/platform/Full matrix
or published/staged artifact was rebuilt. Qualified dev86 remains immutable.

[Acceptance](acceptance.json) records image identities, both observations,
checks and verified raw hashes. Evidence lives locally at
artifacts/experiments/python-ascii-output-20261114/. Both keepers are terminal0.
Campaign CLOSED: no more timing/tuning, kernel claims or decoder specializations
based on these same samples. Remaining capture/progression/admission work is
distributed; another map-removal or accounting-precision project is unsupported.
