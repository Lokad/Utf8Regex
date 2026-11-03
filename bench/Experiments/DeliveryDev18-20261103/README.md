# Dev.18 packages qualified; final refresh pending VM recovery

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

The fresh Full refresh uses that test-corrected source, existing 83-step inventory
and 3,600s hard cap. It started at **2026-11-03 16:50:19 UTC** and passed its
build in 25.18s. The last guest observation was the tests phase. **No completed
Full refresh, new benchmark-table import or current 60-minute acceptance is claimed.**
The original run remains open; new SSH logins time out. Azure reports the VM
running with an unresponsive guest agent and approximately 146–158MB available
memory. A single guarded recovery command, limited to the completed, archived
Linux fixture's RAM copy, returned `VMAgentStatusCommunicationError`; cleanup is
unconfirmed. No reboot, resize or benchmark restart occurred.

Subsequent workflow fix `f164532535de41a21708da8bdb7e259eabff810e` serializes
project/target test hosts through existing MSBuild settings, preserving suite
parallelism, coverage and build reuse. Focused both-target checks pass. It does
not modify the live frozen run or package bytes. Future preparation must consider
actual available RAM and release completed fixture scratch.
CI `41684015379` passes on Ubuntu and Windows for this scheduling change.

Immutable evidence under `artifacts/experiments/delivery-dev18-20261103/`:

- `windows-evidence.zip`: `48182CF7E341BA4BEB18505994F16F8BAC8C7E3CB2EA049FA2F65807A77FF32F`.
- `windows-restore-inputs.zip`: `E20547B8294947A9440E1B81AF98545B0AEB51DEBA08F26E917581F4A7D7C773`.
- `linux-evidence.tar.gz`: `74BC491F2039324DB62F0C3693C80B7AC953EE6795AB3EADF8363229D4B22132`.

The Windows transfer hash is `9D1B06D1D10713348246717527C711B958C757590B7E8D313A43BE6BD43D1D9B`;
original repinned consumer input `40F8DFCD550EBF260C93E99950F7929E0AD81C044321D304B504623FD2E47544`;
original compiler-input archive `648A87869A8868E23083CBB3A93356C0B86FFFE446DD9B08728F1F5E53D94264`.
