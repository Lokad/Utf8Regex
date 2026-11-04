# Defer native Search coordinates until a hit

**Accepted**, source `8af18a5e`. Exact native Python value Search now validates the
whole UTF-8 subject without deriving discarded UTF-16 metadata, borrows the existing
complete literal finder, and projects prefix/hit coordinates only after success.
This resolves the source question in the [scalar.119 diagnosis](../PythonReLiteralFour119-20261020/README.md).
Existing eligibility, owned invocation and final publication checkpoint remain.
No new runner, retained state, API, policy switch or unchecked access.

Strict full-input validity, scalar ranges, byte and interoperability coordinates,
captures, retained values, original cancellation/deadlines and useful resource
admission remain. Existing native Match/FullMatch and controlled public routes
retain their algorithms. Version-dependent work reflects the actual validation
and projection performed; the borrowed-invocation work-cutoff/refund tests pass.

One **88.8121471s**, 17-case public comparison used the frozen dev.23 Full graph
`baae83e6`. Only Python DLL/PDB changed; Core, PCRE2 and benchmark companions stayed
byte-identical. Preflight confirms ten changed native routes and seven controls.
All 170 owned samples qualify, minimum **48.9287ms**. Normal eight-CPU startup then
CPU2, existing 1.5s/minimum128 managed and250ms/minimum64 diagnostic warmup,
independent60ms calibration and five alternating samples. All owned byte and
UTF-16 range fields are consumed after timing, with unchanged median allocations.

| Public operation | Before us | After us | Change | Allocated B |
| --- | ---: | ---: | ---: | ---: |
| core-control-first | 0.331 | 0.333 | +0.73% | 0 |
| actual-unicode-miss | 0.841 | 0.510 | -39.31% | 0 |
| actual-unicode-hit | 0.127 | 0.153 | +20.70% | 0 |
| unicode-early | 20.614 | 9.982 | -51.58% | 0 |
| unicode-late-eof | 40.982 | 31.216 | -23.83% | 0 |
| unicode-middle | 61.524 | 40.989 | -33.38% | 0 |
| actual-ascii-early | 1.222 | 0.708 | -42.10% | 0 |
| ascii-late-eof | 2.352 | 2.957 | +25.73% | 0 |
| ascii-miss | 2.316 | 1.683 | -27.33% | 0 |
| tiny-ascii-hit | 0.086 | 0.104 | +20.62% | 0 |
| supplementary-prefix | 0.149 | 0.204 | +36.17% | 0 |
| finite-live-control | 1.081 | 1.157 | +7.04% | 112 |
| deadline-live-control | 1.189 | 1.266 | +6.49% | 160 |
| offset-control | 0.258 | 0.288 | +11.62% | 0 |
| match-control | 20.261 | 23.023 | +13.63% | 0 |
| fullmatch-control | 0.065 | 0.069 | +5.79% | 0 |
| capture-control | 0.588 | 0.614 | +4.34% | 2192 |

The local costs are real observations, including ASCII late +25.73% (~0.61us),
tiny Unicode/ASCII hits +20.70/+20.62%, supplementary +36.17%, Match control +13.63%
(~2.76us), offset +11.62% and finite/deadline +7.04/+6.49%. No speculative JIT cause
is assigned. Core's unchanged first control is +0.73%. The user's local-cost/major-
gain policy supports promotion alongside the 24–52% gains on larger Unicode
searches, 39% on the actual Unicode miss and 42% on actual ASCII early Search.
No universal10% gate, noise confirmation or tuning variant. [All observations](cost-summary.json)
retain spreads and diagnostic qualification flags. Fixed-original/rejected evidence
and earlier immutable package/Full acceptance are unchanged.

The compatible compiled .NET predecoded/decode-then lanes are diagnostics. They
consume UTF-16 ranges without adding UTF-8 projection; their different result shape
is explicit and does not establish a catalog Status or the isolated owned speedup.
The existing CPython protocol supplies catalog ratios separately.

Release checks pass **91 focused /4736 full PythonRe per .NET10/11**, using one
affected-project build and matching binaries. Existing one-shot coverage adds
supplementary-prefix EOF/interior ranges and malformed Unicode hit/miss tails.
Original-invocation interruption after validation, every work cutoff, unpublished
failure, enclosing loans, resource limits, aliases and API/dependency inventory
remain covered. No unaffected profile or package matrix was repeated.

Only five directly affected CPython rows were refreshed/paired, reusing the exact
measured Python binary and existing matching companion/PDB source inputs, with
**no benchmark graph build**. [Selected observations](selected-ratios.json): ASCII
early5.44 ->2.91x, Unicode miss2.87 ->1.72x, both still CpythonFaster. ASCII late
0.10 ->0.12x and Unicode hit0.50 ->0.57x remain ManagedFaster while retaining their
costs; ASCII miss0.10 ->0.07x remains ManagedFaster. The other85 rows and all
catalog/corpus/lifecycle/scaling fields are unchanged. Two generated files were
hash-imported and existing snapshot guards pass once per target without rebuilding.

The incremental VM source bundle was406061B, avoiding another full-history transfer.
Exact archived Full snapshots were hash-checked before restoring only their seven
known paths for source alignment. Canonical packages/evidence and protected cleanup
targets remain untouched. No publication or another Full refresh.

Immutable evidence under artifacts/experiments/python-search-metadata-20261104/:

- evidence.tar.gz: `98E3F9E34CA4A06B7108154531828168C3590F58BA43F14AAC76BA674B135DD5`.
- correctness-source.zip: `4F5A440D4AF03F13A5A9BA67A50EAF838B31137F07441FFFE401E590122D1348`.
- selective-evidence.tar.gz: `53AF9791D4B75AE9F037896562D70BC3880A88717B43738994BD4F466B05B07A`.
- import-guards.zip: `46A354568DCC41F1E97A72E95BD5C38E0B4A4BD3589D825E10269F2A081585A2`.
