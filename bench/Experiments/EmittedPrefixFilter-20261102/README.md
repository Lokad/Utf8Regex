# Deferred emitted SIMD prefix filtering

**Reverted.** Reusing the existing checked packed-nibble prefix filter greatly
improves keyword-corpus discovery, but stable public regressions exceed the 10%
allowance. No producer, test, package or benchmark-snapshot change is promoted.

The prototype replaced repeated single-byte discovery for emitted upper-word
families with two through eight prefixes, all at least four bytes. The existing
filter retained its admitted final buffers; full emitted alternative verification
and word-interior skipping remained. Short/large families and unsupported hardware
retained the byte search. Finite policies, live tokens, deadlines and Unicode
subjects kept their existing controlled/scalar routes. No unchecked operation,
new engine, mode or framework was introduced. This changed the actual emitted
discovery path, unlike the earlier closed global short-triple selector trial.

One **79.084-second** .NET11 RC3 quiet-VM/CPU2 comparison used exact parent
`fe523d04` binaries against that parent plus the archived patch. Eighteen public
Count/IsMatch workloads used four equal-result lanes, including predecoded and
decode-then-.NET. Two public constructors used producer lanes only, with typed
delegates and preboxed policies. Compilation was excluded from matching timers;
constructor timers excluded first-use matching/JIT. Complete batches were checked
after timing. Counts warmed at least 500ms/64 calls; constructors at least
2s/2,048 calls. Independently calibrated batches and five alternating samples
per lane all passed the 20ms floor (minimum 29.07ms).

| Public operation | Before us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Keyword corpus Count, ordinary |68.08|18.86|-72.30%|
| Keyword corpus Count, compiled |68.02|18.95|-72.14%|
| Dense-hit Count |36.14|40.93|+13.24%|
| Dense-anchor miss Count |3.20|9.12|+184.61%|
| Sparse miss Count |3.38|9.13|+170.40%|
| Boundary-failure Count |36.32|35.14|-3.25%|
| Two-prefix corpus Count |50.97|18.13|-64.43%|
| Four-prefix dense Count |15.40|12.06|-21.69%|
| Late IsMatch |3.76|9.86|+162.51%|
| Miss IsMatch |3.54|9.73|+174.53%|
| Zero-offset short-prefix Count |38.85|41.80|+7.61%|
| Literal Count control |6.37|6.38|+0.04%|
| Keyword constructor |114.92|115.17|+0.21%|
| Literal constructor control |33.05|32.50|-1.68%|

Count/IsMatch allocations are unchanged; affected construction adds **408 B**.
Finite/live/deadline observations move by -0.39% to -1.87%, without a changed
execution route or causal speedup claim. Unicode moves -1.02%. The short-prefix
route keeps its algorithm despite the layout-related variation. All raw comparator
observations and spreads remain in the archive; no catalog Status is inferred.

Dense-hit before/after spreads are 1.006x/1.004x; Count-miss spreads are at most 1.020x.
The evidence supports rejection without a noise confirmation. **No confirmation,
variant, third run, package qualification or full refresh.** Sparse discovery
cost and dense-match overhead need a broader design before this can replace the
current path; do not retain this prototype or tune variants in the same campaign.

Focused Release construction, boundary/public result, work, admission, rollback,
cache/funding and structural-cursor checks passed on .NET10/11 (650 + 1,120 per
runtime). A hardware-disabled run also passed the focused boundary/admission
checks with SSSE3 disabled (20 per runtime). The added eight/nine-prefix and false
prefix cases are archived with the rejected test patch. Both source files were
restored byte-equivalently to the accepted parent. Parent CI 41388233027 is green.

Raw inputs, both DLL/PDB sets, driver/binaries, logs, predeclaration, all initial
observations and exact patch are preserved under
`artifacts/experiments/emitted-prefix-filter-20261102-deferred/`:

- `evidence.tar.gz`: SHA256 `CCD4A74309C109C094E4407E92F200440271CEE19FD56FE9B68592F4AC5292FA`.
- `correctness-source.zip`: SHA256 `67190B9A1773C4FDEC831BC846C797F1D508B612864BBE46E4CFE4E63BAAE87D`.
- Runtime patch: SHA256 `3D3B87FF867752B1CBB3E5E4727E01C5F9CE5C17A55B807E608D51E88B79B2DC`.

See `summary.json` for all public producer medians, allocations and uncertainty.
The committed orphan-table deletion remains. Immutable dev.9 packages and their
full 41m38.50s delivery refresh remain unchanged. Continue the actual public
parity queue; this rejected experiment does not close the keyword gap.
