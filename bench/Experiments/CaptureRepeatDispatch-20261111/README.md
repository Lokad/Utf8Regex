# Direct capture-start/repeat dispatch — rejected

Production is restored exactly to54566419. The12-line prototype charged a
following repeat's original step after a plain capture-start write/journal,
then jumped to the same RunSegment repeat arm. It skipped one outer-loop/switch
dispatch without new metadata, opcode, field, executor, storage or public mode.
Opening-journal/unobserved starts retained their routes. All numeric charges,
effects, rollback, input policy and original interruption stayed unchanged.
The intended full capture operations did not gain enough to retain the change.

One **138.503s** quiet .NET11 RC3 ordinary-process ABBA per input policy,15 public
rows/600samples, minimum35.913ms. Explicit distinct Core DLL/PDBs; identical
caller/Python and inputs. Normal8CPU startup thenCPU2, default tiering/PGO,
1GiB actual available RAM admission. Full capture startup warmup lasts at least
2seconds/1024calls before per-row calibration to75ms; full outputs are checked
after timing. This common warmup avoids relying only on first-row500ms warmup;
it does not establish that every observed mode is eliminated. No confirmation,
tuned variant, kernel-to-public claim or comparator-ratio update follows.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| Many-capture strings |−1.74%|+2.38%|
| One-capture strings |+0.04%|+6.43%|
| Many-capture bytes |+1.51%|+1.99%|
| Captured Count |−1.25%|+2.34%|
| Many strings, finite/live |+0.01%|+6.75%|
| Many strings, deadline/live |−0.66%|+6.07%|
| Long captured detailed match |−2.45%|+29.76%|
| Captured split |+5.76%|+12.90%|

Only trusted Count has disjoint sample ranges, adversely. Strict many-string
638.421→627.330us and trusted632.920→647.992us overlap; constructor/short/Unicode
and unchanged controls also overlap. Regular allocations stay unchanged, with
occasional small long-row runtime traffic retained. Literal control−3.55/+1.63%
and predecoded.NET+2.33/+1.60% are observations, not normalizers. All15 rows,
per-process medians, ranges and source identities remain in raw evidence.

Candidate Windows Release passes per.NET10/11: Core679/Python1070/Pcre259.
Restored Windows/Linux pass Core679/Python1070 per target, covering captures,
required assertion ends, backrefs, rollback, RTL/NB, finite policies, original
token/deadline and aliases. No candidate Linux matrix or new mirrored tests.
Restored Linux Core DLL/PDB exactly match the measured baseline:
29035D21899C9F39CFC8EB772A6F01F8257BEEEE82949C809645EDA9A0C7BF6F /
2F28A5CBACFAFB121D119C8608FFA747C8357478E8A6AD6B895B351BDA546528.

Hash-verified offVM evidence under
`artifacts/experiments/capture-repeat-dispatch-20261111/`:

- Primary:542DEA5F760FD17E35210928285871DE006E1871C0A5C373E34C1B301F9E7F01.
- Restored Linux:ED0B5DCA296413FFF792F79DC1A19ECF0ED2C5FB803C74F06EC3616B1C439B89.
- Windows checks:3371A6BFF1E368C23D1849D3642348CBFD9202B43297A9A45F184E41CC7CE437.

All workers are terminal; the single production file is restored, with no dormant
branch/label. Existing published/staged packages and current generated tables
remain unchanged. Close this dispatch variant and continue larger supported work.
