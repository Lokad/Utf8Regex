# ASCII-run span substitution: rejected

Baseline `c28f5de4f200ff155b3c0c5a87d72a404ea57ca2`. One prototype replaced the
manual ASCII vector/word/tail scan with checked BCL range search. The common
validity drain reused it after proving an ASCII vector with no continuation
carry; idle accounting counted the complete vectors crossed. Whole-ASCII fast
return, initial selection, constrained proof, first errors, metadata and original
2KiB governed interruption extents remained. No new state, threshold or mode.

**Production fully restored.** Removing about 50 scan lines did not establish a
useful public benefit, while governed email calls had clear costs. Nine independent
long-island regression cases remain: all ASCII byte values, Unicode metadata,
borrowed offsets, first malformed bytes and covered caller reservations. They
also pass with the original scans; they do not require the rejected implementation.

ONE **110.714s** public comparison in separate ordinary-process ABBA order:
16 rows under Validate/AssumeValid, 640 samples, minimum 44.686ms and maximum
0.321MiB timed batch allocation under the 140s cap. .NET 11.0.0, normal 8-CPU
startup then CPU2, default tiering/PGO. Explicit distinct Core DLL/PDBs, common
caller/.NET identities, input hashes and complete results verify. English value
Match checks both byte and derived UTF16 coordinates after timing.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| Email Count | −1.05%, overlap | +1.35%, overlap |
| Email finite/live | **+3.17%, disjoint** | −0.02%, overlap |
| Email funded/deadline/live | **+4.09%, disjoint** | −0.13%, overlap |
| URI Count | +0.30%, overlap | +1.63%, overlap |
| Chinese literal Count | +0.99%, overlap | +0.21%, overlap |
| Chinese alternation Count | +1.93%, overlap | −0.44%, overlap |
| Chinese finite/live | +1.87%, overlap | +0.01%, overlap |
| Russian literal Count | −0.37%, overlap | −1.33%, overlap |
| Symbol Count | −0.22%, overlap | +0.20%, overlap |
| River Count | −1.90%, overlap | −0.68%, overlap |
| Constrained mixed literal | +0.95%, overlap | +0.86%, overlap |
| ASCII literal control | +0.40%, overlap | +0.21%, overlap |
| Short literal control | +5.73%, overlap | −0.41%, overlap |
| English folded-family Count | −0.30%, overlap | −1.30%, overlap |
| English folded value Match | +1.98%, overlap | −3.83%, overlap |
| Unchanged .NET predecoded email | +0.29%, overlap | +0.29%, overlap |

Strict river 2258.034→2215.153µs and symbol 1651.289→1647.668µs ranges overlap.
Finite email 1450.640→1496.597µs and funded/deadline 1545.739→1608.970µs costs
are disjoint. No material gain justifies keeping this version. No confirmation,
tuned variant, normalization, parity Status or inferred validation lower bound.
Ordinary allocations stay 0B, finite/live 112B and funded/deadline 160B; caller
peak stays its held 17B and final loans return to zero. This is not RSS evidence.

Before restoration, focused Release checks per .NET10/11 on Windows/Linux pass:
Core 1018, PythonRe 382, PCRE2 232, Posix 7; software/256 each add 123. Native
pattern/template checks add Core 96/PCRE2 69. Linux's 342-document Core source
inventories and PE/PDB identities verify, differing only in intended source and
generated revision. After restoring Core, only Core is rebuilt: Core 123 and
adapter input-policy checks PythonRe 10/PCRE2 8/Posix 4 pass per target/OS.
The retained test source was normalized for transfer; its C# behavior is unchanged.

Default/forced-256 engagement and Release listings remain in the raw archive;
they do not supply a public gain absent from the comparison. All 468 canonical
package/symbol bytes remain unchanged. No Full or package cycle for this pilot.

Verified off-VM evidence in artifacts/experiments/ascii-run-search-20261112/:

- Gate: 427AABC6C0236978A6CF7DA37302D331F7B0F31F65C9BD75CA8227C0BC2DCC15.
- Public: 65D07BD5A1648E941D9A6D4489A94F747C7ACE62C97934F8370617BFC72C8E65.
- Windows: D0A75554D3C924DC1EA9488A6EFAD3CDFFF7209EBFC79C5C12A84A2431C8A0CE.
- Linux restoration: 834D7F53043402A8887DD5152ADDDAC3F56CFE8F84BD053E721BD5D682640F30.

Raw producers, callers, patches, samples, allocation observations and checks stay
there. Keepers release after off-VM verification. This substitution is closed;
do not retry it unchanged or turn the result into a hardware-bandwidth claim.
