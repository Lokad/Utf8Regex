# Broad selected-range string sizing — rejected

Production restored. PythonRe GetText lazily describes an unknown subject once,
then reuses its ASCII/coordinate facts across batch output. The prototype instead
counted each returned range when no facts were already available. It added no
field, cache, output mode or public API. Strict validation, bounded counters,
decoding and resource/interruption authority were retained.

One 119.456-second .NET 11 quiet-VM ordinary-process ABBA covered 14 public lanes
under Validate and AssumeValid: 560 samples >=31.454 ms, 75 ms calibration target,
one calibration confirmation, 180-second cap. Explicit distinct PythonRe DLL/PDB
pairs; identical Core/Pcre2 and caller assets. Eight processors visible at startup,
then CPU2 affinity; one bounded worker with 1 GiB available-memory admission.
Results/checksums were consumed after timing. Allocation and state size stay
unchanged, including the funded-cursor control's 417-byte peak/refund to zero.

| Public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| early text, 1 MiB ASCII suffix | 19.331 ->19.298 (-0.17%) | 10.505 ->0.303 (-97.12%) |
| early text, 1 MiB BMP suffix | 810.469 ->800.550 (-1.22%) | 200.016 ->0.260 (-99.87%) |
| early text, 1 MiB astral suffix | 2464.494 ->2491.092 (+1.08%) | 1042.945 ->0.259 (-99.98%) |
| finite/live early text, ASCII | 26.076 ->25.643 (-1.66%) | 13.994 ->0.371 (-97.35%) |
| many-capture strings | 670.639 ->661.142 (-1.42%) | 654.529 ->720.054 (+10.01%) |
| one-capture strings | 383.391 ->382.318 (-0.28%) | 380.485 ->397.199 (+4.39%) |
| finite/live many strings | 919.953 ->893.051 (-2.92%) | 925.002 ->1019.314 (+10.20%) |
| unchanged UTF-8 output | 728.207 ->701.742 (-3.63%) | 704.492 ->670.651 (-4.80%) |
| captured split | 135.398 ->147.293 (+8.78%) | 147.753 ->153.040 (+3.58%) |

Only the four trusted early-text ranges are disjoint; the other ranges overlap.
Under a 128-work limit the trusted early result changes from refusal to success;
Validate still refuses. Those gains are real but belong to the rejected producer.

One 18.294-second trusted-only confirmation used four lanes, 80 samples
>=21.796 ms, the same producer graphs, a shared filtered caller and a 40-second cap.
Many-string process medians are 682.503/694.190 ->2738.854/2774.425 us (+300.47%).
Samples still overlap (candidate 838.292–2898.542 us, baseline 616.560–970.397 us),
so this is not a qualified stable fourfold regression claim. Nevertheless both
candidate processes show severe adverse timing, while UTF-8 output stays +0.80%.
One-string +1.88% and finite/live many-string +7.51% also overlap. All observations
remain in the summaries; no sample exclusion, third run or tuning variant.

The broad change removes useful batch fact reuse and cannot be promoted on this
evidence. A future bounded design should preserve that reuse while distinguishing
the actual metadata needs of one-shot string output. The early-result opportunity
remains demonstrated; warm capture parity remains unresolved. No numerical
resource-contract revision ships from this experiment.

Candidate Windows full PythonRe 5,091 checks per target pass. Restored Windows/
Linux 529 focused checks per .NET 10/11 pass. Retained tests cover both policies
for ASCII/Unicode strings, scalar-aligned block tails, exact result-byte admission,
owned output and cancellation/deadlines during Unicode sizing. The prototype-only
128-work success tests are removed with its production change. No package/catalog
refresh, new version or mutation of accepted dev.67 artifacts.

Evidence is copied off the VM and hash verified under
`artifacts/experiments/selected-range-text-20261111/`:

- `evidence.tar.gz`: SHA256 18A47D0C11F99116F764FB5396E6DFDAE9A3C7BF686CE2D8B8420AC0D366E5D9.
- `confirmation.tar.gz`: SHA256 1EBACE2CC3907DE8CDA16F0F6D857874F12771592DB9F673A32D4D4316CF1589.
- `restored-linux.tar.gz`: SHA256 18AA9BE56A76486ABD4F9097D75756683C44F71BF88A676727844A6811386E9A.

Caller/source patches, baseline 93088eda9e4969d22822e156b3c83369a2c04d2a,
producer identities, raw samples and phase-free public results are retained there.
The initial VM setup stopped on CRLF-only test-file differences before builds or
timing. Its normalized contents were verified against the committed Git object;
setup then resumed. There was one primary timing run and one confirmation.
