# Current Python capture discovery assessment

One existing60s-capped diagnostic, reused dev60 graph (`848dbb29`):
`--measure-pythonre-findall-phases findall/many-capture-strings 200 5`.
Core/Python/benchmark DLL hashes match the accepted snapshot exactly. The reporter
selects CPU mask0x8 after startup and raises effective iterations to1000; five
samples complete. This differs from the CPU2 producer comparisons and establishes
no new parity ratio, speedup or regression. [Raw output](phases.txt).

The18,432-byte subject produces2,048 tuples/4,096 strings/14,336 UTF-8 output
bytes. Public matching/shaping measures803.957µs/242,008B; prepared projection
67.706µs/229,400B and final shape43.007µs/98,376B. BCL predecoded discovery is
333.168µs; benchmark-only separated-capture replay307.023µs. Checksums agree.
These cumulative controls are **not additive** and do not isolate VM instruction
costs. The replay uses BCL enumeration and case-specific delimiter extraction;
it supplies no owned capture, budget or lifetime handoff for production.

Next investigate a general owned byte-range capture/discovery proof, with actual
production engagement and independent scalar/nullable/repeated/rollback/LastIndex
and original-policy counterexamples before public qualification. Keep current
matching and necessary metering until such a proof exists. Do not ship this BCL
replay, add a case-specific executor, or reopen the closed search-hint caching,
ASCII decoder/bulk-runner or committed-close trials because their ratios remain.
No source change, new test framework, producer A/B or package/catalog refresh.

The first `pwsh -File` invocation passed the comma-separated array as one argument;
CLI parsing rejected it before measurement. A direct bounded invocation then
completed once. Raw output, rejected invocation and matching identities are at
`artifacts/experiments/python-capture-assessment-dev60-20261109/evidence.tar.gz`,
SHA-256 `864734C5E414EA61E2F517AFADB20FF42E6D04149EEA5D35127EA72F32A114BA`.
