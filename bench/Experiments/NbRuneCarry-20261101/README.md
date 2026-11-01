# Reuse the scalar decoded for the preceding row

Reverse NonBacktracking viability preparation decoded each scalar twice: once
as current, once as preceding. Carry the already decoded preceding Rune into
the next row. The end row has no current scalar; the first row has no preceding
one. One decode per scalar remains. The DP algorithm, instructions, packed rows,
ordered capture replay, work counters, allocation/admission and interruption
checks are unchanged. No API or accounting contract change.

One [predeclared](PREDECLARED.md) [public Count comparison](comparison.json),
**84.570 seconds**, quiet Ubuntu VM/.NET 11 RC3, CPU mask4. Clean `1a18b14e`
before; one archived patch after. Thirteen cases, four equal-result lanes,
warm1s/min64 calls, independent60ms calibration, five alternating samples,
minimum **60.955ms**. Complete result batches checked after timing. No confirmation.

| Public Count | Before us | After us | Change | Unchanged allocated B |
| --- | ---: | ---: | ---: | ---: |
| Region ordinary | 4203.713 | 4001.344 | -4.81% | 557416 |
| Region compiled | 4258.538 | 4100.088 | -3.72% | 557416 |
| Region live | 4659.350 | 4465.694 | -4.16% | 557536 |
| Region finite/live | 4912.756 | 4817.681 | -1.94% | 557536 |
| Region deadline/live | 7419.788 | 7424.700 | +0.07% | 557584 |
| Unicode word boundaries | 780.113 | 743.601 | -4.68% | 135384 |
| Unicode deadline/live | 1510.380 | 1500.672 | -0.64% | 135552 |
| Wide program | 2765.147 | 2687.159 | -2.82% | 99560 |
| Nullable program | 250.905 | 242.211 | -3.47% | 26832 |
| Dense ASCII | 607.530 | 576.581 | -5.09% | 98512 |
| Dense supplementary | 416.318 | 383.309 | -7.93% | 49360 |
| Unchanged non-NB scalar control | 4.818 | 4.709 | -2.28% | 0 |
| Unchanged literal control | 0.397 | 0.398 | +0.38% | 0 |

[All samples and paired differences](summary.json) retain control movement and
variation. Affected spreads <=1.04x; controls <=1.10x. No regression above10%
is demonstrated; gains are modest and do not close the large region parity gap.
Predecoded/decode-then-.NET are distinct equal-result lanes, not additive phases.

[Focused Release checks](verification.json) pass on net10/net11. Six added
subject theories use the ordinary owned scalar engine to check both compiled
settings, adjacent Unicode/NUL/combining/supplementary scalars, empty/end rows,
ranges, last captures and replacement. Existing admission, cutoffs, interruption,
packed-word widths and aliases remain covered. Complete production viability
hashes, numeric steps, held bytes and final13B enclosing loans are identical
in all nine [before](state-before.jsonl)/[after](state-after.jsonl) probe cases.

Frozen evidence includes the exact source patch, harness, binaries, preflight,
raw samples, state probe, builds and host identities:

- [Raw archive](../../../artifacts/experiments/nb-rune-carry-20261101/evidence.tar.gz):
  `9D0B6FE8242F579912351EAC0A3D508D2C4C3A88DA28D0D1EDBD52E6944127EA`.
- [Correctness/source archive](../../../artifacts/experiments/nb-rune-carry-20261101/correctness-source.zip):
  `456AE181987C2E5CA6F6DEDBFE1EDAB73028713F24E939553B12F84848F51E04`.

No complete catalog or package qualification was repeated. The separate8d
full refresh/common dev.2 package/consumer evidence remains that source's
immutable milestone. A general reachable-state design is unmeasured and deferred;
this change adds no additional pass, storage, runtime mode or fallback.
