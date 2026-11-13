# Python scalar queries avoid unrelated subject preparation

Producer `d261d444` retains the existing compact scalar-prefix index, but only
acquires it for mixed-text coordinates beyond the first 256-byte block. Queries
within that block use its existing bounded scalar-start counter directly.
The ASCII predicate no longer computes unused UTF-16 totals; proved ASCII facts
reuse the existing description slot, and an existing mixed-text index is reused.
No new field, owner, execution mode or public API was introduced.

This intentionally reduces working admission for first-block queries. Distant
mixed-text queries retain index admission, bounded interruption and synchronous
refunds. Detailed UTF-16 consumers still request their description/map. Captures,
byte offsets, repeated queries, aliases and retained results keep their meanings.

All 5,095 PythonRe tests pass per runtime in Release. The existing scalar-boundary
tests cover offsets around 256/512 bytes and scalars crossing blocks. Public
admission tests cover short/distant ASCII and mixed captures, repeated queries,
index denial, cancellation/deadlines, reentrancy, closure and retained results.
Index-specific interruption fixtures now place the capture beyond the first block.

The one bounded public cursor-create/advance/two-query comparison is **unqualified**.
An initial attempt and one confirmation stopped at the 20 ms sample floor; the
confirmation's unchanged predecoded .NET control recorded 19.7935 ms. The failed
after process did not publish a complete sample set. Do not infer throughput or
parity from the completed baseline alone. No additional timing run was attempted.
The change is retained for its demonstrated removal of unnecessary index admission
and subject preparation; its execution-time effect remains unqualified.

Both attempts used separately staged producer DLL/PDB pairs, source checksums,
an unchanged caller/control within each comparison, valid unchanged UTF-8,
finite limits and an original live cancellation token. Outputs were scalar ranges
on both sides; the .NET lane was a drift control, not a Python compatibility oracle.
Raw results, failures, producer identities, caller and correctness logs are in
`artifacts/experiments/scalar-query-preparation-20261113/`. Immutable dev80 remains
unchanged. Batch normal profile/package qualification at the next delivery boundary.
