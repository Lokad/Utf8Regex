# PythonRe IPv4 boolean reuse: qualification deferred

**No runtime change was promoted.** Source, tests and tentative documentation were
reverted locally and on the quiet VM. Current benchmark snapshots and immutable
dev.12 packages are unchanged. This is incomplete measurement, not evidence of a
performance regression.

The candidate retained PythonRe's already-constructed optional Core program for
the existing complete IPv4-language proof and bounded boolean scanner. Strict
whole-subject validation, scalar offsets, original interruption and resource
authority remained. Retaining that Core root was an explicit resident-memory
tradeoff; construction already charged it conservatively.

Tests exposed a prerequisite defect: profile literals `25` and `://` were compared
only against decoded node text, declining native-byte nodes. A checked comparison
through the existing ASCII literal accessor corrected this without decoding. The
candidate added no engine, stored flag, parser, unchecked API or execution mode.
This prerequisite correction is also archived and reverted.

Quiet VM, .NET 11 RC3, normal eight-CPU startup then CPU2. Before DLL/PDBs were the
matching cb1d443e benchmark graph; candidate Core/PythonRe were built from data-only
parent 1de180e5 plus the archived runtime patch. Four equal-result public lanes:
before, after, predecoded Regex and decode-then-Regex. Five alternating samples per
lane; the existing scratch driver independently calibrated toward 60 ms.

The first five cases completed; their execution samples were never replayed.
See [partial comparison](comparison-partial.json). Medians in microseconds:

| Public IsMatch | Before | Candidate |
| --- | ---: | ---: |
| Actual 15-byte catalog hit | 1.200399 | 0.076249 |
| Finite limits/live token | 1.371951 | 0.123045 |
| Deadline/live token | 1.541637 | 0.198553 |
| Legacy partial positive | 1.242684 | 0.075986 |
| Genuine dense miss, 2 KiB | 383.032704 | 6.939403 |

The sixth case's 8,213-byte Unicode-prefix decode comparator sampled **11.368 ms**,
below the required 20 ms. The one bounded follow-up selected only interrupted or
unmeasured cases, with longer comparator warmup; it stopped at **18.506 ms** on the
same comparator. No third attempt or optimization variant followed. Remaining
controls and construction costs are unmeasured, so the complete <=10% parent gate
was not established. These numbers do not replace fixed-original acceptance or
qualify a new CPython ratio.

The copied probe capped batches using **total allocation traffic**, although
retained outputs were booleans. Transient decode strings hit that ceiling before
the requested sample duration. Future work must preflight actual retained-output
bounds using existing benchmark facilities; this pilot adds no framework.

The initial driver flushed JSON only at process exit. Completed medians and sample
durations survive in stderr; iteration/allocation arrays do not. The later untimed
512-call allocation audit includes its BCL output check and cannot supply isolated
allocation figures. Initial driver source and surviving follow-up driver binaries
are archived; the overwritten initial driver binary was not preserved. Compared
engine DLL/PDB bytes and their input hashes remain intact.

[Verification](verification.json): candidate focused Python124/Core83/PCRE2 55 per
runtime pass on .NET 10/11; additional26 Python tests cover funded cursor aliases
and retained snapshots after program closure. Complete affected PythonRe suite
4,649/runtime passes. After reversion, canonical builds and Python60/Core22 focused
checks pass on both runtimes. Initial classifier failures and corrected fixture
assumptions about legal two-digit partial matches remain in the evidence.

Immutable archives under `artifacts/experiments/`:

- `python-ip-boolean-20261102-deferred/evidence.tar.gz`: exact engine DLL/PDBs,
  patches, drivers, preflight, raw initial/follow-up logs and restored VM build.
  SHA256 `A216C19617E1DB56F685577866AC5134D275A87C9E6DEDAB220EF0E84E3ABEF8`.
- `python-ip-boolean-20261102-correctness.zip`: candidate source/test snapshots,
  proof, local build/test logs and preparation. SHA256
  `A79546D5DF067B495B66F50FA0E248BA47B2A3F73FD4B99EA468848FAF92267F`.
