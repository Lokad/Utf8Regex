# Chinese alternation route census

Read-only, untimed inspection of accepted wide Core `3a8cf7e2`, using the actual
813478-byte Chinese corpus and207 public Count matches. Both ordinary and compiled
modes execute **literal_family_packed_nibble_simd_count**, after strict validation.
The existing public Count diagnostic confirms this earlier facade shortcut.

The live SIMD prefilter uses offsets2/5/8. A scalar census of its exact stored
nibble tables produces **212 candidates for207 matches**. Its full scan covers
approximately25420 32-byte blocks before match-induced realignment. Candidate
selectivity is already good; a longer-prefix filter has no demonstrated benefit.
These are work counts, not timings or a speedup estimate.

**Correction to the initial inspection:** the prepared runtime also has an
ExactPacked search and a leading-segment hybrid. Its packed predicate yields40540
discriminator candidates,829 second-anchor and344 third-anchor survivors; six-byte
prefixes occur254 times. Public Count bypasses that prepared route. Those counts
do not diagnose its execution cost. The earlier suggestion to reorder generic
packed/hybrid dispatch was withdrawn before any source change or timing experiment.

Further investigation must target the actual SIMD scan or validation work, with a
distinct demonstrated cause and one bounded public comparison. No new executor,
preparation, mode or permanent instrumentation has been added. This also does not
reopen the closed fused-validation or checked-access campaigns.

[Frozen public route and counts](route.json) identify the exact DLL and corpus.
Immutable evidence under artifacts/experiments/chinese-alternate-route-20261104/:

- inspection.zip, superseded prepared-only inspection including frozen DLL/PDB:
  `0829B7F806EA21F40F0AB6BD378922D5002281A7A7A07431CFBB4937F9B442C6`.
- actual-public-route.zip, corrected facade diagnostic:
  `DAE2F13CFC2B2015F2B252177E6D3880ACF41F114F8E407535D33A4CDF63819B`.
- actual-mask-census.zip, current public diagnostic and live mask census:
  `F1A2927DCACECA417F4CFC7260F6D05429DE96269C8EBAC38FBC7F03309AC685`.

No performance experiment, dispatch change or catalog refresh.
