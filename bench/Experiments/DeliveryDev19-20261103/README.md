# Dev.19 delivery boundary

Common **0.5.0-dev.19** freezes the accepted keyword filter, bounded multiline
candidate search, PythonRe IPv4 reuse and positive-boundary candidate skip.
Core/PythonRe/PCRE2 retain exact adapter pins and .NET 10/11. Packages are not
published automatically. Dev.18 and earlier staged/published bytes stay immutable.

| Decision | Evidence and remaining scope |
| --- | --- |
| Keyword filter accepted | [One public comparison](../EmittedPrefixPolicyReview-20261103/README.md): corpus Count about3.7x faster, matching allocation unchanged; dense hits+14%, miss scans+6–7us. |
| Line-start search accepted | [One public comparison](../AnchoredLinePolicyReview-20261103/README.md): ordinary/compiled Count about2x faster, finite/deadline gains; false-prefix+5.8%, sparse+0.16us. |
| Python IPv4 accepted | [Public comparison](../PythonIpv4PolicyReview-20261103/README.md): repeated IsMatch about15x faster/0B; construction confirmation+10.61% and retained Core root. CPython catalog observation remains Unqualified due short paired samples. |
| Positive-boundary skip accepted | [Public comparison and one confirmation](../PythonBoundaryPolicyReview-20261103/README.md): date miss about20%, dense word interiors46% lower; Unicode+9.6% and boundary Count+7%. Identical Core control's23–25% difference remains an unresolved attribution limitation. |
| Chinese literal/alternation deferred | [Validation](../ChineseValidationCost-20261031/README.md) and [search](../ChineseAlternateCost-20261031/README.md) attribution: existing validity-only and checked sequence/packed search routes; earlier cheap substitutions do not help. A combined SIMD validation/search design needs separate work. Strict whole-input validation remains. |
| Python early literal deferred | Full65542B input must be validated even for an early match; CPython's input is predecoded. No permission to skip validation, and no demonstrated hard performance lower bound. |
| PCRE2 native date gaps and optional scalar projection | Separate profile queue; not a blocker for Core/PythonRe performance delivery. POSIX remains its own later optional package milestone. |

The user accepts local regressions for relatively major gains. The 10% allowance
belongs to the completed checked-access campaign. The
[old217-case fixed-original results](../DeliveryDev17-20261103/README.md) and their
`Accepted=false` remain unchanged and historical; they are not a current-source
cumulative claim or a universal release ceiling. Focused public comparisons above
and the new boundary's catalog/resource checks serve different purposes.

Clean package source is `87db1ced59fcfcfa7b3bb2eaadf782b23b4c6042`.
Source CI `41713070105` is green on Windows and Linux. Windows qualification
completed in **272.817s**: eight detached fixtures, **107,858** runtime checks,
six target assets and **464** original compiler documents, including generated
inputs. Core is packed once, each adapter once; exact pins and DLL/PDB/source/
feed/cache/lock identities pass. Packages/symbols are staged in `artifacts/nuget/`.

Original copied Windows consumers pass: Awk19 and Lython118 checks per target,
plus native/funded public API probes with final root loans zero. The first five
consumer entries passed; the last direct .NET11 probe selected the system dotnet
host, which lacked that runtime. Only this unfinished probe resumed through the
pinned local SDK; no package, completed test or consumer build was repeated.
The original1565-file prehosting audit changes only three exact version pins.

Linux qualification also passes: the exact Windows-produced packages, all eight
detached fixtures (**107,858** checks), all six original consumer entries and
six assets/24 restore records. No repacking or consumer-source edit. This is
compatibility qualification, not downstream adoption or POSIX acceptance.

The current-source Full refresh started at **2026-11-03 20:48:12 UTC**, with
the existing83-step inventory/3600s cap after actual-RAM admission. Its build
passed in25.886s; runtime tests are active. It is not yet a completed refresh
or current-source catalog qualification; final snapshot guards and data import
remain. The prior frozen44m12s Full evidence stays unchanged.

Completed Linux scratch was compared against its hash-verified off-VM archive
before retirement:2,171,654,319 logical fixture bytes and807,290,433 bytes of the
verified VM archive duplicate. Canonical off-VM evidence and staged packages
remain. No additional qualification framework, per-pilot matrix, unchanged217-
case rerun or automatic publication.

Immutable Windows evidence under `artifacts/experiments/delivery-dev19-20261103/`:

- `windows-evidence.zip`: `8923C4704D1E76C341EDB03422EDF88CFA2F6B523E3FC012CF7B37AB89FAD3CD`.
- `windows-restore-inputs.zip`: `9C9D7E7394FB6DEFFA8EF0C82F659152DFEDA0E8246CA2790931016E9EED6120`.
- `linux-evidence.tar.gz`: `7BAFC5893A7A72DF170B60D51904AB91B61BEFAF203564E386F5AF4D1BB2C453`.

Transfer input hash `8233DD60F9B146D5966D3453C1E7A8D9CE90CE75E2C05600E484BA1FAF25DC38`;
repinned original input `85605696C3466782DBE459D4CEADA2B029550D39A9A14FA8FA2E9215B11A3B00`;
compiler-input archive `A186E80C430BD2E13DD74E58C4D2954B65D3ACCCE8FE39CE303508B5760ECD6D`.
