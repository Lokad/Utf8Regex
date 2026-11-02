# Dev.11 cost gate rejected before packaging

Producer `69d664a7f13ea9200282eda329f0a94afeadc986` freezes common
`0.5.0-dev.11`, .NET 10/11 and exact adapter/Core pins. It contains the accepted
endpoint-coordinate reuse and canonical Unicode contract tests. No rejected
annotation, Set, hint-table or shared-runner prototype is present. Source CI
41419854216 passes Windows and Linux.

**Not accepted. No dev.11 packages were created, staged or published.** The complete
fixed-original `a40bda03` gate ran once after the meaningful endpoint correction:
147 named public entries, all nine sections, **692.432 seconds**, five alternating
samples per lane, minimum **29.520ms**. The dev.10 protocols, case order, output
digests, allocation/finite/live/deadline controls and effective 100ms decoded-BCL
union correction were preserved. Only paths and candidate identities changed.

The three formerly failing Unicode FullMatch controls now fit the allowance:
captured +0.77%, lazy -1.96%, branched +2.88% versus the fixed original. Six other
observations exceed 10%:

| Public control | Original us | Candidate us | Change | Original / candidate spread |
| --- | ---: | ---: | ---: | ---: |
| Lazy Count |0.571|0.629|+10.23%|1.090 / 1.031|
| Nullable atomic Count |0.951|1.048|+10.14%|1.051 / 1.048|
| Folded FullMatch |77.73|89.31|+14.89%|1.01 / 1.01|
| Literal FullMatch |0.08|0.10|+29.94%|1.01 / 1.01|
| Suffixed FullMatch |33.09|38.87|+17.46%|1.02 / 1.02|
| Unchanged native Search |0.0783|0.0892|+13.90%|1.00 / 1.01|

The last four are stable failures; **no noise confirmation was spent** on the two
borderline observations while stable failures remained. No successful sections,
variants or full gates were replayed. Other section maxima are cost +4.17%, PCRE2
+1.68%, union +6.52%, FindAll -2.52%, capital +0.33%, word +0.74%. Full per-case
medians, spreads, allocations and paired movements remain in the raw data and
`cumulative-acceptance.json`.

Windows package qualification, Linux package/consumer checks and the full VM
refresh were deliberately not started. Original copied consumer input SHA256
`CBD35E993774B928D7D8C054F53A3BEABDFBE8C8024C138CF68253D5BA3C41AB`
was verified; the prepared copy changes only producer pins in three projects.
Its archive SHA256 is `94C2D0734AE4B7AEC8B5DE58F40155894317D090379AE85AC835543F754B8749`.
Preparation is not consumer acceptance. Existing dev.10/dev.9 and published
packages remain immutable.

Raw frozen source, original/current DLL/PDBs, exact reused drivers/binaries,
corpora, all samples/logs and preflights:
`artifacts/experiments/delivery-dev11-20261102/cumulative-69d664a7-notaccepted.tar.gz`,
SHA256 `808D5BB94D7FA75B276DEE014400C026FAFBABD3B78E132CCF4374F0E6991414`.
Local predeclaration, assessment and prepared consumer evidence: `local-proof.zip`,
SHA256 `ADB97781BF1496EB38E3B6D6905B29E020998A3F8CC8DD0361CC824D7ACF3DBE`.

Next diagnosis follows the remaining live entry/construction paths. Actual IR
also rules out one tempting attribution: folded FullMatch starts with Set
instructions and already declines the literal-repeat helper. Suffixed FullMatch
does enter that helper and fails its complete-root proof; this distinction must
remain visible. Do not retune the rejected scalar-method outlining experiment
or infer release acceptance from a smaller parent comparison.
