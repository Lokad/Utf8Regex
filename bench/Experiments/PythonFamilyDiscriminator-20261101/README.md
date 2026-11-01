# Select the possible literal with the prepared byte map

Large exact Python hint families can reuse their existing unique-column map.
Construction already proves every branch extends beyond that column and has a
different byte there. A bounded first-byte scan selects at most one needle;
the existing metered comparison still verifies **the whole needle**. An absent
entry or mismatch rejects that start. The profile VM then verifies the original
ordered expression/captures. This preserves false-prefix rejection, unlike the
closed prefix-only pilot.

No preparation, allocation, retained fields, thresholds or modes were added.
The sole Python consumer needs a start, so the concrete internal helper now
returns that index or−1; it no longer returns unused length/ordinal metadata.
Raw pair/small-family and native complete-result contracts remain unchanged.
Avoided comparisons reduce work charges under the existing version-dependent
policy; bounded search, full comparisons and publication/interruption still meter.

One102.673-second public comparison,16unchanged union-pilot workloads, on the
dedicated .NET11RC3 VM/CPU2. Frozen `2b95833f` versus its archived patch, both BCL
equal-result projections, plus a fifth **fixed `a40bda03` producer** lane on the
original six FindAll workloads in the same batch. Typed calls, warm1s/min1000,
independent60ms calibration, five alternating samples≥38.439ms, full results
consumed after timing. No confirmation or variants.

| Public FindAllToStrings | Parent us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Dense captured values |732.308|600.214|−18.04%|
| Finite limits/live token |914.924|739.280|−19.20%|
| Deadline/live token |1261.837|950.791|−24.65%|
| Dense false first bytes |878.880|80.180|−90.88%|
| False common prefixes |54.681|6.082|−88.88%|
| All-false common prefixes |105.673|10.327|−90.23%|
| Cyrillic prefix |221.450|184.488|−16.69%|

[All parent costs/allocations/spreads/pairs](summary.json). Every allocation is
unchanged, including64B for the false-prefix miss. Maximum adverse control
median1.86%, pair2.57%, candidate spread1.023x.

The [direct fixed-original measurements](original-summary.json) still **fail**:
dense ordinary+27.42%, finite+16.37%, deadline+17.97%, Unicode+24.43%, offset+26.48%.
Spreads are about1–2%; no ambiguous threshold decision needs confirmation.
Accepted parent improvement is not release acceptance. Other failed rows and
the coherent79-workload gate remain open. No package/catalog/Status refresh.

Focused Core571 and full PythonRe4530 Release checks pass per runtime on.NET10/11.
Coverage includes missing/colliding maps, different widths, full prefix/tail
verification, borrowed slice/window/end bounds, Unicode, long comparisons,
sticky work cutoffs, caller loans, original token/deadline and retained aliases.
Four initial long-fixture failures hit the existing67M construction-overlap
backstop because the test added a synthetic Core suffix predicate; long-tail
fixtures now use the real necessary-prefix shape. Suffix-exclusion tests remain;
no production backstop changed. Initial logs are retained.

Immutable artifacts under `artifacts/experiments/python-family-discriminator-20261101/`:

- `evidence.tar.gz`: SHA256 `55A57400B8B46D500AF6220620FED33543A68CC8DF8A06B0CB66E7B46959F1DD`.
- `correctness.zip`: SHA256 `F4C33C8446E324A5CFE78572AE85BFB60706BB6C0F702BC1366A0413958F1F40`.

They retain all three producers' DLLs/PDBs, identities, source/applied patch,
driver, preflight, predeclaration, raw samples and source/test logs. Existing
immutable dev.7 packages and snapshots are unchanged.
