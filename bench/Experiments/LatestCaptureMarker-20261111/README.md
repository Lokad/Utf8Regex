# Latest closing-marker coalescing — rejected

Production restored. The prototype overwrote obsolete closing markers only in
assertion-free latest-value roots without live frames, balancing or opening
journals. It kept the latest actual marker for Python LastIndex. No field,
option, layout, internal signature, algorithm or validation-policy change.
Physical table reservations remained held until workspace release.

One 134.844-second .NET 11 quiet-VM comparison covered 15 public lanes under both
input policies: ordinary-process ABBA, 600 samples >=50.721 ms, explicit distinct
Core DLL/PDB pairs, identical caller/PythonRe/Pcre2 assets. Eight processors were
visible at startup; affinity was then set to CPU2. Calibration targeted 75 ms,
with one confirmation and a 180-second outer cap. Checksums/results were checked
outside timing; bounded workers used 1 GiB memory admission.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| many-capture strings | +10.39% | +7.85% |
| many-capture UTF-8 | -6.49% | +9.10% |
| finite/live strings | -0.72% | +1.76% |
| detailed completion control | +25.21% | +9.77% |
| twelve captures | -17.16% | -15.92% |
| one-capture split control | -3.03% | +5.18% |

All ranges overlap. One bounded 39.034-second four-lane confirmation (160 samples,
same producers, shared filtered caller) also overlaps throughout: many strings
-2.07%/+12.56%, detailed +19.98%/+10.11%, twelve captures -4.05%/+0.72%, and
one-capture control -4.00%/-6.95%. Neither a speedup nor a causal regression is
established. No more timing or variant tuning follows this trial.

Twelve-group allocation falls 53,776 ->53,624 bytes; common two-group allocation
stays 242,008 bytes and state size stays 368 bytes. Funded-cursor control peak
417 bytes and final refund to zero are unchanged; this is not the private
FindAll path's modeled peak. An untimed probe of 64/256 sequential captures at
2,048/3,072/4,096/6,144 working-byte limits finds identical public admission before
and after under both policies. FindAll with 64 groups completes at 6,144; its
detailed projection refuses. All other probed combinations refuse. A follow-up
only labels these phases separately, correcting the original combined-try
diagnostic without changing limits or timing again.

The small allocation saving and unproven working-budget benefit do not justify
another hot-path branch/helper with uncertain throughput. Candidate Windows full
PythonRe 5,088, Pcre2 6,667 and focused Core 285 checks per target pass. Restored
Windows/Linux 518 focused checks per .NET 10/11 pass. Two independent public
capture rollback/frame-transition cases remain; production is unchanged.
No package/catalog refresh or immutable artifact mutation.

Raw callers, patches, identities, samples and results are copied off the VM:
`artifacts/experiments/latest-capture-marker-20261111/`.

- `evidence.tar.gz`: SHA256 A184FBB85F05B09E4186257AFB9549EE9A3A95F7A22C1C00EB06A7EA65E84F6A.
- `confirmation.tar.gz`: SHA256 85D2FCCA5EFB8A146C4AB7751F280E9807AB51700D9692D8F03A2229876C4D2D.
- `restored-linux.tar.gz`: SHA256 189C5C0B5DEE1449D88944840E492F1029A8D7F4487E9AE8A5AD71E2448E2C35.

`summary.json`, `confirmation-summary.json` and phase-labeled budget results retain
the full observations. This rejection closes this implementation only; warm
Python capture/findall gaps remain material.
