# Russian literal Count: remaining work is validity and actual search

One **8.234s** read-only diagnosis on frozen dev96 `4d923414`, using the existing
count-attribution command: 1,000 calls/five samples, each lane warmed at least
250ms/64 calls, 120s outer cap, no confirmation. Normal runtime placement with
no outer taskset or new affinity policy. All five producer/benchmark DLL/PDB pairs
match the completed Full run before and after; tracked source stays clean. No
build, implementation change, package qualification or canonical table refresh.
[Observations](attribution.json).

| Phase / public Count | Median us | Sample range us |
|---|---:|---:|
| Validity only | 97.032 | 95.968–98.004 |
| Selected compiled search kernel | 70.974 | 69.649–71.108 |
| Validity then selected kernel | 167.341 | 165.061–169.727 |
| Public compiled Count | 165.733 | 164.660–166.510 |
| Predecoded compiled .NET Count | 86.894 | 85.581–93.394 |

Every search/public/reference result is 724 over 1,570,556 valid UTF-8 bytes.
The selected exact-literal route uses its existing correlated two-byte-prefix
search. Independent diagnostic replay reports 770 verifications/724 matches;
this is not a census of executed production branches. It does not establish
excessive false candidates as the cause of the remaining public ratio.

The public operation is close to the separately observed validation-plus-search
path. Required strict validity is substantial; this supplies no discarded
whole-input UTF-16/ASCII preparation to remove. Independently warmed phase sums
are attribution ceilings, not achievable gains, exact percentages or a matching
lower bound. Kernel-only time is not public AssumeValid throughput or a new
native parity result. The final canonical table remains authoritative.

Disposition: retain Core's default strict contract and explicit AssumeValid.
Do not reopen the rejected mixed/fused validation, decoder or checked-access
campaign. Further search work needs a distinct algorithm/candidate proof and a
bounded public sparse/dense/false-head comparison; this observation alone does
not justify another selector or execution mode. Continue to rank actual capture,
search and complete output costs without excluding required work.

Remote worker exited 0, raw is verified locally at
artifacts/experiments/russian-count-attribution-20261115/evidence.tar.gz,
SHA256 `C86E273628CBAA9B410AC904DEB0B88F5AABD5895A4BE5DCA89F8085C4347839`.
The local wrapper then failed only while printing Cyrillic to a cp1252 console,
after acceptance and archive creation. Read the archived UTF-8 result; no
measurement was repeated and no VM fixture or keeper remains.
