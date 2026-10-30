# Qualifying the symbol-count priority signal

No producer code change. Exact benchmark/Core producer is
**dab878a6d49247a127c8ac638c5b3476d3236773**, the already built word-count candidate.
The frozen row's apparent 933x compiled gap was Unqualified: its decode+compiled
lane included a 16.267ms sample, below the existing 20ms gate. It could not drive
the qualified ranking without diagnosis.

[Public corpus proof](corpus-proof.json) loads the real benchmark context and
calls ordinary/compiled producer and predecoded ordinary/compiled .NET operations.
All return **69** for `\p{Sm}` on RiverCorpus: **16013977 UTF-8 bytes**,
16013973 UTF-16 units, SHA256
`F2AA28234E7A8212C9E009FA9C67D1960D2D063D076765DE46B0FAED5FE44AD8`.
This establishes equal scalar-compatible results on this corpus, without claiming
general UTF-16/scalar equivalence. [Probe](corpus-probe.cs.txt) and
[project](corpus-probe.csproj.txt) are retained.

One [focused qualification](qualification-first.txt), using the existing
--refresh-readme-case command with one requested iteration/five samples, reproduced
the short decode+compiled lane. The **single allowed confirmation** used the same
command with requested floor **five**, preserving independent lane calibration,
five samples, source/runtime and all six lanes. Its minimum batch is **30.891ms**;
all duration/provenance gates pass. No sampler modification or further campaign.
Both runs had a 120s outer cap. The successful recorded phase sum is 87.58s
(warmup 3.79s, calibration 16.58s, sampling 56.21s, allocation 10.99s), excluding
parent setup/serialization. [Confirmation log](qualification-confirmation.txt).

The resulting qualified gap is **46.62x ordinary / 836.35x compiled**, about
1.074s producer versus 23.035ms/1.287ms predecoded .NET. The difference from the
historical hint is **not a producer speedup**. Clean source, matching existing
binary identities, .NET 11.0.0-rc.3.26522.148 and the quiet VM are unchanged.
Only this case's ordinary/compiled snapshot entries and generated tables/report
changed; every other measurement keeps its historical identity. It now precedes
the recorded region deferral and the active AWS 7.57x gap in the qualified queue.

Next investigate the existing single-category proof and checked MathSymbol kernel
preempted by scalar dispatch. Preserve strict UTF-8/scalars, supplementary category
membership, basic metering, original interruption, useful finite limits and actual
shared PCRE2 consumers. No implementation is qualified by this measurement alone.

Immutable archive under artifacts/experiments/parity-symbol-count-20261030/:
symbol-qualification-dab878a6.tar.gz, SHA256
`064DEB4C4250B887861CDE8DFD4446F7453B7C131D1DC7BDF0C04E6DCE77E90A`.
It contains the exact probe/binary, failed/successful raw runs and final snapshots.
The unchanged producer/benchmark DLL/PDB pairs and source bundle are preserved in
the [word-count receipt's archive](../WordRunCount-20261030/README.md). Published
packages and the final current-refresh/cumulative/package/consumer gates remain
unchanged.
