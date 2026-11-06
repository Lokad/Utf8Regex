# Search proved negated ASCII-pair runs with checked spans

**Accepted.** Parent `2209a7c628267666c10009c38dd4a722d7ba4a58`. One runtime file
adds 34 lines, reusing the existing pure-repeat fusion proof and checked BCL
`IndexOfAny`. No new program facts, fields, storage, mode, cache or engine.

Eligible runs are unbounded, unfolded negative ASCII Set atoms with exactly two
excluded values and minimum zero or one. Checked little-endian mask reads and
popcounts recognize the pair. Searches admit at most 2048 bytes per work/deadline
checkpoint, with a terminal checkpoint. The subject was already strictly validated;
ASCII stops cannot occur inside wider scalars. Intermediate windows may split a
scalar, but the selected stop or subject end cannot. Nonempty byte extent suffices
for these minima. Finite bounds, larger minima, folding and paths requiring rewind
retain their existing handling. Numerical work requirements intentionally fall;
scalar/capture semantics, resource admission and original interruption remain.
The three specs and release notes document this scope.

The frozen actual imports probe returns 19 matches/0B on the same 46429-byte Linux
corpus, SHA507F5A33. Its 43-instruction program has the negative pair at31,
unbounded repeat at30 and excluded quote after capture endings at36. Existing
fusion therefore reaches this loop. The probe is untimed structural/shared-verifier
evidence, not a production branch census or public speedup. This whole-span search
removes decoding, predicate dispatch and per-scalar work; the closed per-byte
negative-mask and ASCII Dot prototypes are not repeated or tuned.

ONE **85.17700797-second** comparison uses separate Default producer processes in
ABBA order, normal8CPU startup then CPU2 affinity, default tiering/PGO, one-second/
minimum128-call warmup, independent60ms calibration and five samples/row/process.
All **300 samples exceed58.4755ms**. Only Core DLL/PDB differs; six adapter/benchmark
and both worker files are unchanged. Typed results are consumed and checked after
timing. Available6474964KiB passed conservative2GiB focused admission immediately.
The total cap was120s, each producer35s. No confirmation, normalization or tuning.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Native literal control |12.46|12.44|-0.10%|0/0|
| Actual imports Count |11.24|10.46|-6.94%|0/0|
| Same, compiled |11.23|10.39|-7.45%|0/0|
| Same, finite/live |13.60|12.34|-9.25%|112/112|
| Same, deadline/live |14.90|14.18|-4.80%|160/160|
| Short dense pair Count |513.07|490.54|-4.39%|0/0|
| Long ASCII pair Count |1285.41|827.49|-35.62%|0/0|
| Captured pair/backreference Count |95.71|92.59|-3.26%|400/400|
| Unicode imports Count |76.72|75.85|-1.13%|0/0|
| Bounded-pair control |145.62|152.00|+4.38%|0/0|
| Folded-pair control |103.67|104.23|+0.54%|0/0|
| RTL imports control |9651.49|9749.14|+1.01%|58312/58312|
| NonBacktracking imports control |1647.89|1651.38|+0.21%|557872/557872|
| PythonRe pair IsMatch |781.28|781.66|+0.05%|448160/448160|
| PCRE2 pair Count |156.07|150.84|-3.35%|0/0|

[All observations](cost-summary.json) retain ranges, process medians, allocations
and identities. Both actual-imports after medians are below both before medians;
the bounded-repeat control's after medians are above both before medians. Accept
that local cost for the demonstrated target gains. Several small control/Unicode
estimates have overlapping ranges; PythonRe's measured operation does not improve.
Do not infer universal profile gains, additive speedups, new catalog Status or a
universal10% gate. Peak process RSS144160/149836KiB before and142876/148880KiB after
is lifetime evidence, not a per-call allocation or memory-reduction guarantee.

Release .NET10/11 checks pass: Windows29 new cases plus178 existing Core,
49PythonRe and23PCRE2 per target; Linux180Core,49PythonRe and23PCRE2 per target.
Coverage includes offsets/block tails, supplementary scalars, zero/one/larger
minima, low/high/cross-half masks, finite maxima, folded/overlapping/lazy/atomic
paths, captured results, RTL/NB, malformed suffixes after early stops, every work
cutoff with enclosing loans, original cancellation and deadlines. An initial CRLF
patch-format rejection occurred before source application; its log is retained.

Exact Core before:
`9D72967C932FAC2FAC56753248D951EE6A9A69DC8D3584FB3A405307415C1B82`;
candidate:
`B515BD2DF5E67A96D8FD78F110223F7507F93858CF22B8891142206DBA3B83F9`.
Runtime Git blob `e8e44b828564d07fb5a92847abb954b5aac89856` matches both machines.

Immutable `artifacts/experiments/negated-pair-span-20261106/`:

- `evidence.tar.gz`: `4E1B3F81D01FEF660C7EFD6F85C9B6AEAE714B43354D61DA06EFC3AE6CAB24A7`.
- `windows-evidence.zip`: `5A26EA2A2F7C74F6B7BFAC90F5FAF3E2ED64D7F25760500DAC6BF9166F880921`.

Raw retains exact graphs, source/test patches, workers, preflights, all samples,
process resources and terminal zero. All8dev40 Full DLL/PDBs, qualified packages
and published tables remain unchanged. Full/platform/package matrices are batched
at the next coherent delivery boundary; no intermediate package qualification.
