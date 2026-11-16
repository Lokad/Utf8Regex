# Exact UTF8 families reuse validity-only admission

Accepted: three predicates select existing well-formed family matching by
ExactUtf8Literals, replacing the incidental presence of a small ASCII primitive.
The facade already passed default metadata to every literal-family runtime;
its existing cursor derives coordinates from the consumed prefix. No new
matcher/projection, API, allocation, owner, mode or resource model. Strict tail
checking, priority/captures/aliases and controlled/trusted contracts remain.

Actual unchanged sherlock-zh and sherlock-ru fixtures select this execution kind
without the ASCII primitive. Independent BCL results verify complete byte/UTF16
values. Windows and SAME-byte Linux .NET10/11 pass Core1041/PythonRe892/PCRE2396/
POSIX50 focused Release cases per target; Windows also passes533 value/literal/
work/construction cases. Added Unicode overlap/branch-priority, astral, short/
larger ASCII families, policy authority, malformed-tail hit/miss and original-token
checks pass. Matching adapter hosts are reused; only Core/its test host build.
Scoped no-intrinsics public preflight passes.

One110.506s ordinary-process public ABBA,180s cap,0 confirmations:14 operations,
normal8CPU startup thenCPU2, five150ms-target samples/process/row, >=20ms floor.
Valid unchanged UTF8; strict whole-input checking is timed unless labelled trusted.
Complete byte/UTF16 output is consumed after timing. Finite control retains its
original token/deadline and work/depth/working/result limits.

| Public operation | Before µs | After µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| Chinese family Match, compiled | 1268.498 | 98.250 | -92.25% | separated |
| Chinese family Match, plain | 1271.512 | 98.490 | -92.25% | separated |
| Russian family Match, compiled | 3703.198 | 99.698 | -97.31% | separated |
| Unicode overlap | 447.528 | 50.249 | -88.77% | separated |
| Unicode branch priority | 444.522 | 50.130 | -88.72% | separated |
| Short ASCII family, Unicode subject | 572.109 | 65.588 | -88.54% | separated |
| Dense Unicode family, first Match | 1040.734 | 46.549 | -95.53% | separated |
| Missing Unicode family | 1661.431 | 545.495 | -67.17% | separated |
| Trusted Chinese Match control | 21.683 | 21.907 | +1.03% | overlapping |
| Finite Chinese Match control | 3184.271 | 3194.800 | +0.33% | overlapping |
| RTL Unicode control | 21504.497 | 21458.607 | -0.21% | overlapping |
| Existing small-family control | 66.860 | 65.089 | -2.65% | overlapping |
| Chinese Count control | 92.316 | 91.411 | -0.98% | overlapping |
| Unchanged .NET Match control | 0.094 | 0.091 | -3.52% | overlapping |

Keep the simpler eligibility and large affected-Match improvements. Every control
range overlaps; retain observed movement without normalization/causal attribution.
No confirmation or validator tuning. Allocations agree:0B ordinary,688B finite,
528B RTL and208B.NET. Count parity and canonical tables are unchanged; this does
not erase earlier ASCII/borrowed-English trade-offs in their separate receipts.
The finite five-alternative Match remains about3.2ms versus unlimited~99us and
trusted~22us. Source selects native finding through at most3 alternatives while
Count already admits8; investigate that live operating-cost gap after delivery,
with original interruption and meaningful admission, not a new unmetered bypass.

Baseline dc01a287's actual measured producer; after is that source plus the patch.
All343 compiler documents, DLL/PDB identity and checked managed-member inventory
verify; caller/.NET controls identical. No VM producer build. SHA256s:

- Before Core: `D47001FCA7514C59FDEB3F79A9996E77E1BDC36A81FCB40F1C2F69C510260670`.
- After Core: `05E8DED67DA800DEC88D2AD3D5BFB4F89D00AEE77D9E9E75D1FD67BC5732FB0C`.
- Raw evidence.tar.gz: `FF50100C365ABFDC58CC8A9E190AE51894C0E72B7955C33C145DA6AF08B58BEB`.
- Validation archive: `29E76ECD5443E570E2A87A6D394AA63B46EF68F14E619F9A7C342595A4FE0AB4`.

Raw: artifacts/experiments/utf8-family-preparation-20261116/. Original worker/
collector handles terminal0; raw verified offVM before keeper release. Owned VM
task retired after accepted hash/zero process/cwd/FD/map references. Qualified
dev.99 assets rehash unchanged;660 feed files remain. This experiment repeated no
package/Full qualification and performed no publication/downstream adoption.
