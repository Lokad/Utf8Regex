# Captured complete-repeat Count — accepted

Core, PythonRe and PCRE2 now reuse the existing constant-space scalar-run counter
when Count cannot observe a complete greedy repeat's plain capture markers.
A bounded walk of at most 64 instructions proves the original repeat/atom/end/
exit/accept skeleton, keeping its absolute targets and scalar predicates. No
program copy, executor, cache, public option or retained proof is added. PCRE2
reuses its admitted slice payload for Count alone under the existing compatibility
checks; other operations, native fast paths and nondefault-match continuation stay.

Actual captures and retained results keep their original execution/storage.
Nullable, lazy, trailing, capture-dependent, RTL and NonBacktracking programs
retain their paths. Both input policies, one strict validity pass, managed bounds,
original cancellation/deadlines and useful resource controls remain. Avoiding
unused VM state can reduce numerical work/depth/working requirements; the three
profile specifications record that route-dependent admission change.

One **135.744s** quiet .NET 11 RC3 ordinary-process ABBA covers both input policies,
16 public rows and 640 samples, minimum **70.453ms**. Each producer starts with
eight visible CPUs, then selects CPU2, with default tiering/PGO. All three explicit
DLL/PDB pairs differ; caller/control files and inputs are identical. Baseline is
`f62845b3`; candidate is that source plus the archived patch. English input is
151,521 bytes and every Count returns 1,833. Construction is outside timing;
complete public calls include selected subject validation and live policy costs.
Results are consumed after timing. No confirmation or further timing follows.

Outer is `([A-Za-z]{8,13})`; inner is `([A-Za-z]){8,13}`.

| Public Count | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Core outer |7494.345 →1108.299|7384.564 →1101.315|
| Core inner |7417.532 →1105.973|7370.873 →1102.112|
| Core finite/live |22232.800 →1300.818|20809.363 →1348.781|
| Core funded/live |22212.575 →1298.320|20819.113 →1356.547|
| PythonRe outer |8873.650 →1128.782|8934.856 →1104.562|
| PythonRe inner |20063.225 →1130.141|20143.900 →1110.150|
| PythonRe finite/live |21482.775 →1324.026|22035.537 →1324.016|
| PCRE2 outer |13567.408 →1137.874|13973.717 →1119.511|
| PCRE2 inner |24449.825 →1134.213|24571.062 →1121.180|
| PCRE2 funded/live |32452.267 →1319.092|31893.183 →1329.781|

All target ranges are disjoint: **85.0–95.9% less execution time**. Python outer
allocations fall 64→0B/call, inner 1480→0B, finite/live 1592→112B; PCRE2 funded/live
1592→112B. Other target allocations remain 0/112B. The measured shared-account peak
falls 1521→17B, including the caller's held 17B; final outstanding is zero.

Retain the unchanged native PCRE2 control's strict **+1.97% /79.092µs** movement,
whose observed ranges are disjoint. Trusted +0.44% overlaps. AWS +0.01/+1.94%,
actual Python capture output −1.87/−5.01% and predecoded .NET +0.49/+0.16% overlap;
their allocations stay unchanged. Strict category −2.94% and trusted uncaptured
Core −5.79% are disjoint process movements, not claimed structural improvements.
The predecessor's much larger category regression remains recorded in
[its receipt](../TerminalClassPrecedence-20261111/README.md); this comparison
does not erase it or establish general/native parity. Large target savings and
reduced unused storage justify the explicit small control cost.

Release checks pass on Windows/Linux for .NET 10/11: focused Core/PythonRe,
full PCRE2 (Linux excludes six SourceOnly repository guards), and 103 Linux
hardware-disabled Core checks per target. Tests cover the proof boundary, outer/
inner/nested groups, Unicode, offsets, excluded forms, actual retained captures,
strict malformed suffixes, single validation, original interruption and refunds.
An old depth expectation now distinguishes unobserved Count from observed
MatchDetailed/backreferences. Initial PCRE2 failures exposed and fixed Count-only
payload ownership and dispatch through Enumerate instead of dedicated Count.

Raw evidence is copied off VM and hash verified under
`artifacts/experiments/captured-class-count-20261111/`:

- `evidence.tar.gz`: B6E8A3382D673FD91F139735F673EC75014FA78E3E2A6FF222B20FDD1869104B.
- `windows-checks.tar.gz`: F56D4E6AE2E28649D7F3A599A818207558010CEB1B8F690D4EE43769277C7C6C.

Raw samples, per-process ranges/identities, patches, initial failures and final
checks remain available; summary and offline identity/result verification sit
alongside the archives. The collection shell reported an invalid trailing `exit`
argument after successful completion/archiving; the independent stage exit is zero
and no work was repeated. Existing catalog summaries and immutable dev.69 packages
are unchanged. Warm Python capture-output, strict Core and POSIX history gaps
remain active; this closes only the shared Count change.
