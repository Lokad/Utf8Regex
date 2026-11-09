# Restricted greedy-selection investigation

Read-only local probe of exact qualified Windows dev61 Core/POSIX binaries,
producer `9bbf54de`. One30s-capped deterministic sample completes in1.860s:
6,000 flat greedy patterns ×121 ASCII subjects,726,000 complete result/capture
comparisons with ordered BCL matching. Four capture-wrapper shapes, one to three
single-scalar atoms, and optional/star/plus/bounded repeats are sampled. No
counterexample is found. This is **not a proof** or a performance comparison.

The excluded compound optional `a?(ab*)?` on `abbb` correctly differs: owned
longest length4, ordered length1. General first-acceptance selection remains
unsound. No production source, test framework, package or benchmark is changed;
the probe runs locally, without competing with the VM refresh.

Next investigate a simpler homogeneous-literal family: one arbitrary scalar,
greedy bounded/unbounded pieces and semantic tags outside repeated bodies.
Prove longest endpoint and hierarchical priority before any acceptance shortcut.
Different atoms, repeated compound/tag bodies, alternation and assertions retain
general selection until independently proved. No new executor, mode or TDFA.

Source, protocol, matching package identities and results are at
`artifacts/experiments/posix-flat-greedy-proof-20261109/evidence.tar.gz`, SHA-256
`196AFA78B6384403C6A54390AB34811B32481485945EA5B8ED93E8AD18719267`.
