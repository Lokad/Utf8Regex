# Bounded LF scan for a committed Dot repeat

Accepted against `c6de14e1`. Imports Count reaches the owned scalar runner. Its
existing repeat proof commits `.*$` without keeping useless rewind alternatives,
but finding LF still decoded every scalar. Share the existing checked ASCII-stop
scan with Dot excluding LF, for unbounded maxima and minimum0/1. A validated
UTF-8 scalar cannot contain LF internally, and nonempty bytes prove minimum1.
The existing caller still decides when an endpoint can be committed; captures,
anchors and publication remain in the same VM. No new engine, retained field,
buffer or execution mode. The withdrawn positive-class bulk loop stays closed.

Keep2048-byte work/interruption blocks and terminal checks. Strict whole-subject
validation, original cancellation/deadline, depth/working/result admission,
refunds and retained capture/cursor values remain. Numerical work can decrease
because the scan does less work; exact old-path cutoffs are not promised. Finite
maxima, minima above1, Singleline, RTL and tails requiring rewind retain their
routes. [Resource contract](../../../docs/SPEC.md#cancellation-deadlines-and-resource-limits).

One **80.384s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
20 public lanes,400 samples, minimum65.430ms. Explicit distinct Core DLL/PDBs,
same PythonRe/PCRE2 adapters and caller, one Core per process,8 startup processors
and affinity set afterward. Preflights verify complete results/zero loans, the
actual imports program's Dot0/min0/unbounded shape and changed runner IL.
No confirmation, package/catalog/Full repeat or new native ratio. Dev63 remains
immutable. The imports corpus uses the existing loader verbatim and its46429B
Linux catalog input, SHA-256
`507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2`.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Imports Count |10.394|9.263|-10.9%|
| Imports, finite |11.841|10.402|-12.2%|
| Imports, funded |11.913|10.457|-12.2%|
| Imports, deadline |14.475|12.851|-11.2%|
| Long-line Count |266.977|3.925|-98.5%|
| Long-line Count, finite |321.305|2.836|-99.1%|
| Long-line detailed captures |486.629|192.266|-60.5%|
| Dense short-line Count |133.447|123.519|-7.4%|
| Python Dot strings |286.203|20.406|-92.9%|
| PCRE2 Dot Count |265.216|3.545|-98.7%|
| Unchanged Python one-capture strings |396.242|400.972|+1.2%|
| Unchanged Python many-capture strings |749.648|748.184|-0.2%|
| Unchanged Core literal Count |0.884|0.870|-1.6%|

Imports process medians10.352/10.447→9.245/9.295µs and every controlled imports
lane have disjoint ranges. Accept these gains and the long-line benefit, retaining
Python's+1.2% movement without normalization or attribution. Count allocations and
funded peaks are unchanged. Detailed/string allocation averages show small lower
movement; this patch adds/removes no buffers and claims no precise allocation
reduction. [Summary](summary.json) retains every lane/process/range. The diagnostic
replay helper costs from the earlier investigation are not the public route.

Focused Core 4,679 and complete PythonRe 5,012/PCRE2 6,647 Release checks pass per
target/platform, .NET10/11 Windows/Linux. Three new cases cover2047/2048/2049-byte
offsets, split supplementary scalars, NUL/CRLF, retained captures and token calls.
Existing tests cover lazy/bounded/overlapping/Singleline/RTL/assertion and empty
semantics, every work cutoff, original-token/deadline interruption and borrowed
loan refunds. The interruption fixture is larger so it reaches the diagnostic
quantum even with bounded scans; it still interrupts inside actual execution.

Local driver preparation initially failed on a missing staged adapter, returning
a ref-struct context through a generic harness, a field/property reflection lookup
and a Linux-only corpus-size check applied to Windows CRLF data. Corrected before
any timing; failed preparation logs are retained. The Linux subject/driver and both
preflights passed before the sole comparison. No production edit followed timing.

Before Core `904D4B377EEA0AD3E50FFE49DDDD79DBB049CC50C7A3E66906EA867E5A1F2043`;
after `A244CC778683660166D54D44B84EF1EA490EA4ED4CE7A6352E55D7BDA7742DB0`.
Raw protocol, patch, graphs, preflights, Linux checks and samples verified offVM:
`artifacts/experiments/dot-lf-scan-20261110/evidence.tar.gz`, SHA-256
`A4B52CCA2F536BDBDBC7ACA58A79545E0CDA824C554CDBB199973CCC116F400F`.
Windows checks/preparation logs are alongside it. Source CI pending.
