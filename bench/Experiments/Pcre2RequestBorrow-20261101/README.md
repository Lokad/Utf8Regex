# Borrow the immutable PCRE2 request in token calls

The VM program exposes its existing stored compile request by internal readonly
reference, and the existing token helper accepts it by `in` reference. No added
storage, execution route, engine, selector, state or accounting rule. The field
keeps its original declaration position. Other caller shapes may retain their
existing temporaries; this is not an exhaustive signature conversion.

The [patch](runtime.patch) follows enginee6779865/data5fa1b905. Internal signatures
change, so friend tests/benchmark binaries must be rebuilt together. Public APIs,
strict UTF-8/scalars, captures, retained aliases, cursor/owner lifetimes, original
cancellation/deadlines and all resource admission/checkpoints remain unchanged.
Published/staged dev.4 packages are immutable and do not contain this source change.

The request is **72bytes**, measured using actual Unsafe.SizeOf before/after.
Actual Linux .NET11RC3 Tier1 [before](codegen-before.txt)/[after](codegen-after.txt)
shows nine qwords copied into token arguments before, versus the stored request
address after. Each lane checks2million public results. Code size17258→17488bytes;
copy removal does not imply smaller code or a public speedup without measurement.
Complete probes/listings and exact binaries are archived.

Full affected PCRE2 Release checks pass **6472 per runtime** on .NET10/11. The
prototype kept the original field position before freezing the timing binaries;
initial/final source and check evidence are retained. No implementation-mirroring
test or package/solution matrix was added.

The [single immediate-parent public A/B](comparison.json) completed **63.807s**,
17workloads, minimum36.232ms samples, equal results and unchanged allocations.
Same Core bytes, existing typed delegate driver, warm1.5s/min1000calls, independent
60ms calibration, five alternating samples and all result batches consumed after
timing. No confirmation or variants ran.

| Public operation | Change |
| --- | ---: |
| Subroutine hit/miss |−9.48% / −9.29%|
| Dense unanchored search |−9.68%|
| Captured Count |−10.22%|
| Complex-call control |−14.69%|
| Recursive control |−10.05%|
| UCP control |−16.47%|
| Finite/live miss |−3.45%|
| Finite/live/deadline hit |−3.80%|
| Existing bounded control |+7.38%|
| Literal control |+0.31%|

The bounded control had also cost+6.44% in the prior instruction borrow. To avoid
stacking individually accepted losses, the combined copy-removal batch was checked
against the real frozen a40bda03 original, using the same17public operations and
protocol. [Selected cumulative check](cumulative.json): **63.653s**, minimum36.758ms,
largest covered adverse median **+0.45%** (bounded control); subroutine hit−19.75%,
Count−7.40%, dense−7.17%, UCP−23.11%. Finite/deadline allocations add8bytes versus
that original, unchanged versus the immediate parent; retain these increases.
This is selected affected-family coverage, not universal equivalence or a full
26-lane/package delivery qualification. No cumulative noise confirmation ran.

The original RAM baseline copy was no longer available during preparation. Its
authoritative local archive was hash-checked and uploaded to this new task before
preflight/timing; no timed run was discarded for that preparation-only lookup.
[Original baseline scope](CUMULATIVE.md), [all parent pairs](summary.json) and
[all original pairs](cumulative-summary.json) remain visible. Do not multiply
separate point estimates into a claimed measured regression.

Immutable archives under `artifacts/experiments/pcre2-request-borrow-20261101/`:

- `evidence.tar.gz`: exact parent/original/candidate DLL/PDBs, patch/full source,
  harness, builds/preflight, both comparisons and complete codegen probes/listings.
  SHA256 `76D935A06CCEFA267DE1435E8B85C9BE6DC1422BDA36A71AED6D51FD14E2CF0E`.
- `correctness.zip`: matching Windows assets and both-runtime checks,
  `45E99D85A065C2B9A7020A759F0E9FF55DEB36EA3DB8A195BA9A5637BD06B567`.

Next selectively refresh affected existing native rows at committed source;
preserve other identities/statuses, scaling, uncertainty and closed deferrals.
Batch complete package/platform/cumulative matrices at a coherent delivery boundary.
