# Qualified 0.5.0-dev.18 delivery candidate

Common **0.5.0-dev.18** Core/PythonRe/PCRE2 packages and symbols are staged in
`artifacts/nuget/`. Both adapters pin Core exactly. All three target .NET 10/11.
Nothing was published to NuGet.

Package source is clean `3e7bd2db3bb42c35f5cb8eb41af71c46e7900a27`.
Every production project tree equals measured dev.17 source `347b7845`; the
shared build properties change only the common version. Package records/feed
under `artifacts/package-candidates/0.5.0-dev.18/` are immutable.

Windows qualification took **299.420s**, building the two adapter graphs and
packing each profile once. Eight detached project/target fixtures passed on
each platform (107,730 runtime checks per platform). Linux consumed the exact
Windows-produced packages, without repacking. Six target assets, DLL/PDB hashes,
SourceLink, feed/cache/lock provenance and all 464 original compiler documents,
including generated inputs, were checked.

The original copied Awk adapter and Lython runtime/provider passed on Windows
and Linux, each on both targets: Awk 19 and Lython 118 checks per target, plus
the public native/funded producer probes with final root loans zero. The original
1,565-file input audit changes only three exact dev.8 -> dev.18 pins. Rehosting
and compatibility qualification do not claim downstream adoption or POSIX support.

[Dev.17 operating-cost evidence](../DeliveryDev17-20261103/README.md) remains
unchanged, including its old uniform-threshold `Accepted=false`. The user
clarified that local regressions are acceptable for relatively major gains;
the checked-access 10% allowance is not a universal 0.5.0 delivery ceiling.
The three local regressions and the substantially larger mail/capital/predicate
gains are documented together. No 217-case replay was needed for version metadata.
The three small follow-up prototypes were all rejected and fully reverted.

Source CI `41674322883` passed Ubuntu but exposed a Windows replacement-preparation
coordination timeout. Test-only fix `c743bd243d1ea0e8489f249f1c3d8c320e4ac3bd`
removes a competing owner-pause timeout and attempts both bounded worker joins
before asserting. Its complete `src/` and `bench/` trees equal package source;
focused checks and CI `41677011460` pass both platforms. Packages were not rebuilt.

The first Full refresh used that test-corrected source, the existing 83-step
inventory and 3,600s hard cap. It started at **2026-11-03 16:50:19 UTC**, passed
its build in 25.18s, then exhausted the cap during tests (terminal exit **137**).
Its incomplete manifest retains the interrupted test step as Running; that is
not evidence of a live process after termination. No benchmark phase ran.
SSH and the guest agent became unavailable while available memory fell to about
146–158MB. One Azure recovery request returned `VMAgentStatusCommunicationError`.
SSH subsequently recovered and the original connection exported the failed run;
7.4GB RAM was available and the completed Linux scratch was absent. No reboot,
resize or process kill was performed. The failed run is preserved separately;
its clock will not be combined with a retry.

Subsequent workflow fix `f164532535de41a21708da8bdb7e259eabff810e` serializes
project/target test hosts through existing MSBuild settings, preserving suite
parallelism, coverage and build reuse. Focused both-target checks pass. It does
not modify the failed run or package bytes. The new full invocation used this
fix after actual-RAM and disk admission, preserving all catalogs and both targets.
The package/consumer and 217-case qualifications will not be repeated.
CI `41684015379` passes on Ubuntu and Windows for this scheduling change.

Preparation compared two completed dev.14 consumer scratch trees byte-for-byte
against their sealed, locally hash-verified `linux-consumers.tar.gz` archive
(`8DAA2A5823FA83FA7C6A4C49765C308A8FCBF59C76F5FD722EDBE3D3C7629BD0`).
Only those duplicate scratch trees were retired: 2,188,871,264 logical bytes.
Canonical packages, qualification records, sources and archives were retained.
Available RAM rose from 7.4GB to 9.6GB; repository headroom rose to 2.2GB.

The fresh [Full refresh](full-refresh.json) completed in **2652.0147515s
(44m12.015s)** from `60022add0dce365779ef2cbe03b079364a953de2`: Full/Complete,
all **83 exact expected steps passed**, terminal exit zero, below 3,600s.
It includes one Release build, both-runtime source correctness and disabled
dynamic code, complete .NET 11 catalogs, paired comparators, lifecycle/scaling,
resource costs and final verifiers. Its complete `src/` and benchmark-project
trees equal package source; test, orchestration and receipt changes differ.
The failed and completed clocks remain separate. Package/cumulative/consumer
qualification was not replayed.

All seven snapshot/Markdown files were imported byte-identically. Existing
Core/PythonRe/PCRE2 snapshot guards then passed on both targets using matching
VM binaries (3/1/1 checks per target, no build). The root README's introductory
date/time and receipt link were subsequently corrected; generated tables were
not edited by hand. Source CI `41689413903` is green on Windows and Linux.

[Current rankings](ratio-summary.json) retain uncertainty and exclusions:
Core has 114 wins, two tie candidates, 58 gaps and four unqualified rows;
PythonRe has 34 ManagedFaster, 40 CpythonFaster, 12 inconclusive and four
unqualified rows. Native PCRE2 has 77 ManagedFaster, one equivalent, 11
NativeFaster, 12 inconclusive and 25 excluded rows. No verdict was resampled.
The mail Count row is now 0.0131x CPython versus 3.5717x in the prior snapshot;
catalog movement includes comparator variation and is separate from the causal
fixed-original operating-cost evidence linked above.

The largest current Core gaps are keyword-family discovery **8.60x**, imports
**7.13x** and Chinese literal search **7.11x** against matching predecoded .NET.
PythonRe's largest are early literal search **5.39x**, IP hit **4.03x** and date
miss **3.96x** against CPython. Native PCRE2 date hit/miss remain **9.39x/9.07x**
on its separately scoped queue. These are distinct comparators, not one ranking.
Next Core work reassesses the archived keyword-filter trade-off on current
source; optional PCRE2 scalar projection and later POSIX remain unfinished.

Immutable evidence under `artifacts/experiments/delivery-dev18-20261103/`:

- `windows-evidence.zip`: `48182CF7E341BA4BEB18505994F16F8BAC8C7E3CB2EA049FA2F65807A77FF32F`.
- `windows-restore-inputs.zip`: `E20547B8294947A9440E1B81AF98545B0AEB51DEBA08F26E917581F4A7D7C773`.
- `linux-evidence.tar.gz`: `74BC491F2039324DB62F0C3693C80B7AC953EE6795AB3EADF8363229D4B22132`.
- `failed-full-evidence.tar.gz`: `16361DC04F7DA62F0F2B6C1EC24E5D584C5F4EC82E99FE7259259E023AD87B5D`.
- `full-serial-evidence.tar.gz`: `C81F83A2004F2E9B7D23842C6AC9F72A966CF75C0FCC6E2F96B689C0A5AB01CA`.
- `post-refresh-guards.zip`: `B413C02F6354DA2660C4F74941E78553E0E3D59D368A10E7C8C26B4AA17838B5`.

The Windows transfer hash is `9D1B06D1D10713348246717527C711B958C757590B7E8D313A43BE6BD43D1D9B`;
original repinned consumer input `40F8DFCD550EBF260C93E99950F7929E0AD81C044321D304B504623FD2E47544`;
original compiler-input archive `648A87869A8868E23083CBB3A93356C0B86FFFE446DD9B08728F1F5E53D94264`.
