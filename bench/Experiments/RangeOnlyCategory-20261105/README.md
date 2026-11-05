# Range-only category lookup: reverted

**Closed without promotion.** MatchesTable skipped Rune.GetUnicodeCategory when
the existing table.Mask was zero. Explicit ranges have no category term to test.
Predicate early return, category folding, range/equivalence/negation, scalar
semantics, allocations, numeric admission/work and original interruption stayed.
No helper, table, matcher, selector, mode or unchecked API was added.

[Untimed live tables](live-tables.json) prove actual3200B UnicodeSplit `[,，]`
uses CategorySet+Accept, Mask0/range[65292,65293], no predicate/class program and
384separators. Captured FindAll uses the same table. [Release FullOpts](codegen-summary.json)
shows the zero-mask branch directly reaches ranges; true category calls remain.
Method298->304bytes. Neither inspection establishes a public gain.

Windows/Linux each.NET10/11 pass Core696+132NonBacktracking/subtraction,
Python1097 and PCRE156 focused checks. Existing cases cover positive/negative
ranges, categories and unions, folding/cultures, BMP/astral, captures, RTL/NB,
malformed subjects, aliases, finite work/working/result and original interruption.
No new implementation-existence test. All nine independent public digests agree.

ONE **105.635s** separate-Default-process ABBA; matching0dc graph on receipt-only
parent79d9256c, only Core changes. Six companions and typed caller are identical;
unchanged Core Count FIRST. Normal8CPU startup thenCPU2; producer1s/min16,
BCL250ms/min4 warmup, independent60ms calibration, five alternating samples.
All180producer samples exceed42.6646ms; complete typed digests follow timing.
[All observations, graphs, allocations and process spreads](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core Count |2.482|2.485|+0.13%|0/0|
| Actual Unicode Split |125.570|124.428|−0.91%|19512/19512|
| Same finite/live |157.873|155.583|−1.45%|23712/23712|
| Same deadline/live |210.273|213.123|+1.36%|23760/23760|
| Captured Unicode FindAll |222.547|226.896|+1.95%|25248/25248|
| Long astral Split |1738.503|1708.106|−1.75%|49272/49272|
| Folded Unicode ranges |283.947|281.101|−1.00%|59496/59496|
| ASCII Split neighbor |70.429|71.097|+0.95%|43088/43088|
| Core category-mask neighbor |278.969|285.391|+2.30%|64/64|

Actual after medians124.382/124.684us consistently beat before125.969/125.121,
but save only about1.1us. Long astral saves30.4us; the true category neighbor's
after285.874/284.813 consistently costs6.4us over279.194/278.744. Captured FindAll
has uneven after219.091/234.955 movement; retain its pooled cost without calling
it a stable causal regression. Controls are not normalization factors.
These modest target gains and unchanged allocations do not justify the branch
and category-path cost. No confirmation, tuning, selected refresh, CPython Status,
package or Full repeat; rejection uses no universal10% threshold. The previous
failed-start-width and small-class native-rendering experiments remain closed.

Immutable artifacts/experiments/range-only-category-20261105:

- evidence.tar.gz: `5AA02B02DE3B2BAF1AF1187F271BA143389286134517835C784667E56AC2F1C9`.
- windows-correctness.zip: `ABA5E88173070253E7A4CF2044BF674B03180F53B021AFF43E96BBB2EEA68DEC`.

Declaration8ED48EC5/patch3B0618BC, live probe, source, before/after graphs,
codegen, fixed worker, preflights, all samples and terminal0 remain. Production
is restored; matching Windows/Linux each.NET10/11 Core828checks pass. The eight
measured root DLL/PDBs are unchanged. Restoration archives:

- restoration.tar.gz: `BD319CDC68AA41C6B62445E245DDE08EA2FDDFC08B22DF820448D09FF273FE41`.
- windows-restored.zip: `D468225AACBD7D18AA5E98F754CF2D196F9018EB30AA514B3F10AB155A5E28F8`.

Published/staged packages/symbols, catalogs and43m07.82s Full remain unchanged.
