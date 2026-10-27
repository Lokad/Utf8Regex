# Historical PCRE2 performance ledger

The former August ledger describes the **2026-08-17** snapshot
`A565BD95D4B152CF1CFB07F02C88576DAAD557EE2DC33E0EAEAE98669AE2BA25`,
source `9f3e6bcfb29d`. Its operation/scaling results and follow-up suggestions
are historical; that hash is not the current PCRE2.Benchmarks.json hash.

The full original ledger, including raw identities, comparisons, limitations and
later follow-ups, is preserved in
[repository history](https://github.com/Lokad/Utf8Regex/blob/d0fd579bc4eb3005a017ea8682b6a1e96f30a08a/bench/Lokad.Utf8Regex.Benchmarks/Pcre2PerfLedger.md)
and the local gitignored PLAN.HISTORY.md. No original receipt is edited or rerun.

Use the [snapshot JSON](../../PCRE2.Benchmarks.json) and its
[generated page](../../src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md) for recorded row
provenance and Status. They identify their own dates and exclusions; historical
rows do not qualify today's engine. Do not maintain another manually copied
qualification summary or priority queue in this file.

The frozen common **0.4.0-rc.1** candidate is unpublished and has known PCRE2
defects: **do not publish those PCRE2 bytes**. Its
[tracked receipt](../Experiments/ReleaseCandidate-20261020/README.md),
[identities](../Experiments/ReleaseCandidate-20261020/release.json) and
[qualification scope](../../src/Lokad.Utf8Regex.Pcre2/QUALIFICATION.md) retain
their original scope. Detailed local-only handoffs are absent from a source
checkout. For current source/candidate status, use the
[qualification page](../../src/Lokad.Utf8Regex.Pcre2/QUALIFICATION.md) and
[current operating-cost evidence](../../CURRENT-PERFORMANCE.md).
This historical ledger establishes no current throughput or benchmark Status.

The legacy `--emit-pcre2-perf-ledger` and `--emit-pcre2-managed-perf-ledger`
producers are retired. Use the snapshot/generated page and retained per-case
inspection or measurement commands; historical tables are preserved above.
