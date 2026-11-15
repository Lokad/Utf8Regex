# Folded Count batching — rejected

One bounded public comparison tested retaining correlated vector candidate masks
across nonoverlapping folded Count matches. The selected anchors and complete
verification stayed unchanged. The prototype added 51 production lines and four
public boundary/dense assertions; all were removed after the decision.

It did not demonstrate a material gain on the primary English corpus. A synthetic
false-head row improved, but that alone does not justify another Count loop.
No confirmation, subsequent tuning campaign, package rebuild or full refresh.

| Public Count workload | Before µs | Prototype µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| folded-english-corpus | 126.914 | 131.132 | +3.32% | Overlap |
| trusted-folded-english | 104.853 | 104.007 | -0.81% | Overlap |
| finite-live-folded-english | 1038.188 | 1047.365 | +0.88% | Overlap |
| false-head-folded | 18.160 | 14.118 | -22.26% | Separated |
| dense-folded | 259.058 | 257.760 | -0.50% | Overlap |
| unicode-kelvin | 101.466 | 101.404 | -0.06% | Overlap |
| short-folded-contrast | 28.264 | 27.917 | -1.23% | Overlap |
| unchanged-dotnet-folded | 77.588 | 75.186 | -3.10% | Overlap |

All complete integer results agree and allocations are unchanged (112 bytes for
finite/live-token English, zero for other rows). Validate includes strict subject
checking; the trusted row explicitly uses AssumeValid. Finite/live calls retain
the original metered path. The .NET row uses the same predecoded string/options.
Overlapping ranges do not establish equivalence or a gain.

Exact Windows-built producer/PDB pairs and all 343 compiler documents per producer
were verified on the Linux quiet VM. The caller and Python control were identical.
Normal eight-processor startup preceded fixed processor-2 affinity. Stable .NET 11,
one producer per process, ABBA, five warm samples per row/process, 150-second cap;
actual comparison 62.868 seconds, zero confirmations, no VM producer build.

Release disassembly proves the new helper is reachable from this public workload
set, but does not prove per-row helper coverage. In particular, dense/English
observations must not be presented as isolated-helper gains or regressions.
The Count entry grew from 1,022 to 1,074 code bytes; the new helper had 866 bytes.
Omitted/inlined methods are not a complete code-size total.

Managed-member inventory permits only actual Unsafe.SizeOf queries: no unchecked
Unsafe/vector reads or P/Invoke. After restoring the exact source, 504 focused
Release checks pass on each of .NET 10/11. No API, semantic or resource contract
change remains. Qualified 0.5.0 packages and prior benchmark identities are intact.

Raw: artifacts/experiments/folded-count-stream-20261115/evidence.tar.gz
with SHA-256 `E8B48FBCBD5AAC3AA76D3703B7009DA5DD6E3E4EF77A62322EC0E227BB604AA2`. Local closure, rejected patch, all sample ranges/
process medians and restored test logs are in the sibling closure.tar.gz.
Original worker/collector completed with exit zero; evidence was verified off-VM
before release. This receipt records a closed experiment, not a new parity claim.
