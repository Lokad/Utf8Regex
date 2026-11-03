# Checked keyword filtering: gain with explicit local costs

**Accepted** under the user's clarified policy allowing local regressions for
relatively major gains. This is the exact runtime/test change from the
[earlier rejected experiment](../EmittedPrefixFilter-20261102/README.md), now
qualified on current source. Its historical rejection remains unchanged.
The checked-access campaign was not reopened.

The completed dev.18 Full refresh ranks keyword-family discovery as Core's
largest qualified gap, **8.595x** matching predecoded .NET. The existing checked
packed-nibble filter now discovers candidates for emitted upper-word families
with two through eight prefixes, each at least four bytes. Full alternative
verification and word-boundary skipping remain. Other families and unsupported
hardware keep their existing search. No new engine, switch or framework.

One **79.137s**, 20-case .NET 11 public comparison used frozen `60022add` DLL/PDBs
without rebuilding the baseline, against candidate `8c6e1b5c`. Candidate matcher
blob `5481d84904a51c826407c15944dea60d2d6b1a30` is identical to the archived
prototype. The driver changes only identity assertions and scope text: same
corpus, operations, options, four lanes (constructors two), warmup, independent
calibration, alternating five samples and post-timing checksums. Minimum batch
**28.679ms**; all outputs and identities pass. No confirmation or variant.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Mixed-text corpus Count, ordinary | 66.34 | 17.90 | -73.01% |
| Mixed-text corpus Count, compiled | 66.16 | 17.85 | -73.02% |
| Two-prefix corpus Count | 49.20 | 17.47 | -64.49% |
| Four-prefix dense Count | 15.20 | 11.94 | -21.43% |
| Dense-hit Count | 35.56 | 40.46 | +13.80% |
| Dense-anchor miss Count | 2.45 | 9.32 | +280.29% |
| Sparse miss Count | 2.68 | 9.25 | +244.79% |
| Late IsMatch | 3.18 | 9.34 | +193.29% |
| Miss IsMatch | 3.13 | 9.31 | +197.66% |
| Short-prefix Count | 38.75 | 41.73 | +7.69% |
| Literal Count control | 6.32 | 6.31 | -0.20% |
| Keyword construction | 113.93 | 113.86 | -0.06% |
| Literal construction control | 33.52 | 33.54 | +0.05% |

The target saves about 48us per corpus operation; miss scans cost 6–7us more,
dense hits about 4.9us more. This is a workload trade-off, not universal improvement.
Matching allocations are unchanged; affected construction adds **408 B**
(108,216 -> 108,624 B). Existing conservative construction/program admission
covers the additional wrapper/filter storage before allocation; exact finite
capacity/work requirements remain route/version dependent. No metering is removed.

Finite/live/deadline and Unicode cases retain their controlled/scalar routes;
their observed movement (-2.92% to +0.95%) does not establish a causal speedup.
The ordinary target's before spread is 1.106x; the compiled target's before/after
spreads are 1.004x/1.003x. All raw samples are retained; noise does not block the
large target gain or stable regressions. [All 20 results](summary.json) include
allocations and spreads without aggregating unlike operation ratios.

Focused Release checks pass **1,129 per target** on .NET 10/11, covering public
boundaries/results/cursors, construction, admission, caches and authority. Full
PythonRe passes **4,647 per target**; **31 per target** pass with SSSE3 disabled.
The restored tests cover eight/nine-prefix families and false prefixes. Strict
whole-subject UTF-8, scalar/NonBacktracking behavior, captures/aliases and original
cancellation/deadlines are preserved. No API/ownership contract changes.

Preparation first stopped before timing because logout cleanup had removed the
VM's RAM archive. The already sealed, hash-verified off-VM Full archive was copied
to persistent task scratch, then preparation resumed. No baseline rebuild,
timing retry, package replay or second Full refresh occurred.

Immutable evidence under `artifacts/experiments/emitted-prefix-policy-review-20261103/`:

- `evidence.tar.gz`: `35262529C4048AD655083CF2A0805241D3CC6171B6F46FC23F2F21A4E87126F5`.
- `correctness.zip`: `588BF4FEAF63DF1507F6C065DB0EF30DE8B2189E1526C62E3B974C3D59A86D66`.

The raw comparison is not a catalog Status or a package qualification. One
selective catalog refresh on documentation-equivalent source `6f108ad7` used
the existing six-lane runner and parity verifier. Only this case's ordinary and
compiled records changed: compiled ratio **8.595 -> 2.331x** predecoded .NET,
ordinary **0.0817x**, with zero matching allocation. All three generated files
were hash-verified and imported; other rows retain their previous evidence.
The root README clarifies the distinction between selective rows and a complete
run. Raw `selective.tar.gz` has SHA256
`1B5278AF91AFF0C545CBED151CE4B9675BCB7CEA119A0B18A728692744B6C955`.
Immutable dev.18 bytes and their complete 44m12s Full receipt remain unchanged;
package/platform qualification stays at the next coherent delivery boundary.
