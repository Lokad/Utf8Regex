# Reuse the word-interior proof for boundary-only Python discovery

**Accepted**, source `b5899253`. Complete capture-free Boundary/Accept programs
have no consuming start hint, but a positive boundary still cannot match between
two ASCII word scalars. Reuse the existing failed-outcome path after the VM's
first attempt, with the actual profile word predicate. Other grammar and uncertain
neighbors retain VM verification. No new runner, state, API or execution mode.

Strict whole-subject UTF-8, scalar progression and EOF, captures, retained aliases,
original cancellation/deadlines and useful limits/refunds remain. The first attempt
still admits whatever VM storage is required; pure-boundary matching needs no
major scratch buffer. Eliminated instructions can reduce version-dependent work
charges. The specification records that behavior.

One **84.8261493s**, 16-case public comparison used the frozen dev.21 Full graph
`9d464227` as baseline. Only candidate PythonRe was rebuilt; Core DLL/PDB bytes
are identical. Normal eight-CPU runtime startup, then CPU2; managed1.5s/min128
warmup, BCL250ms/min64, independent60ms calibration and five alternating samples.
Complete output batches are retained and consumed after timing. Every one of the
320 samples qualified, minimum40.46ms. Every median allocation value is unchanged.
Unchanged Core ordinary/NonBacktracking controls ran FIRST and moved-0.41/-0.75%.
No noise confirmation or implementation variant followed timing.

| Public operation | Before us | After us | Change | Allocated B, both |
| --- | ---: | ---: | ---: | ---: |
| ASCII boundary Split, actual2176B catalog subject |195.51|121.31|-37.95%|40016|
| Finite limits/live token |239.70|169.06|-29.47%|44216|
| Deadline/live token |320.25|233.20|-27.18%|40176|
| Mixed-Unicode subject, ASCII boundary Split |115.64|105.68|-8.61%|15448|
| Sparse65536B word, boundary Split |4204.20|803.37|-80.89%|131144|
| ASCII boundary Count, actual17408B catalog subject |1238.09|660.51|-46.65%|0|
| Unicode boundary on ASCII subject |208.31|138.79|-33.37%|40016|
| All-space no-boundary miss |139.34|147.07|+5.55%|4408|
| Empty input |0.419|0.447|+6.73%|32|

The miss costs about7.73us more; empty input about0.028us. Literal, captured and
lookahead controls move+3.13/+1.85/+1.69%, negative-boundary Split+0.61%.
[All observations](cost-summary.json) preserve medians, spreads, raw-duration
qualification and identities. Small control costs have no established attribution.
The user accepts local costs for major representative gains; there is no universal
10% release ceiling. Diagnostic .NET lanes use explicit ASCII boundary formulas
where needed; they do not establish a catalog parity Status.

Focused **64** and full **4724** PythonRe Release checks pass per .NET10/11, with
matching dependency hashes before binary reuse. Witnesses cover every work cutoff
with a parent loan, actual first-attempt working requirements, interruption during
word interiors and at EOF, scoped/negative/Unicode boundaries, malformed tails,
offsets/maxSplit, captures, aliases and program closure. One new test initially
assumed nonexistent major storage for boundary-only IsMatch; its expectation was
corrected without changing the runtime prototype. The initial host output was
returned by the tool; a stream-redirection mistake prevented its raw file. The
archive labels the retained failure excerpt explicitly, alongside complete
corrected/full logs. No production failure or timing rerun is hidden.

Only two rows received existing clean-source raw refresh/nine-pair CPython
qualification. Their loaded source/PDB/DLL identities are retained. Both generated
files were hash-imported; matching-binary snapshot guards passed1/runtime without
rebuilding. Other88 rows and catalog/corpus/lifecycle/scaling context are unchanged.

| Case | Previous Full ratio | Fresh ratio [95% interval] | Status |
| --- | ---: | ---: | --- |
| split/zero-width |3.540|1.821 [1.808,1.830]|CPython faster|
| zero-width/count |1.364|0.587 [0.584,0.590]|Managed faster|

[Selected evidence](selected-ratios.json): managed medians122.667us/40016B and
651.870us/0B, minimum managed samples49.99/49.36ms, CPU masks0x10/0x20.
These catalog observations differ from the CPU2 A/B and are not its causal
percentage. The existing dev.21 packages,43m21.5s Full refresh and historical
receipts remain immutable. This change follows that frozen delivery and waits
for the next coherent package boundary. No package/Full matrix was repeated, and
the stopped Unicode-class pilot was not reopened.

Immutable evidence under artifacts/experiments/python-boundary-interior-20261104/:

- evidence.tar.gz: `4AE142822205F0055B4E47F078B40A09925A1A0AB4BD95340FB8BD50B3C46807`.
- correctness-source.zip: `EE6201720BE73BECA58C5AA8DEC6D18A9ACB2FBD75AAB96EADE25EF44C6B84F6`.
- selective-evidence.tar.gz: `84DBC04DBA249B439F2D60464353FA1CCF4AE6B7F6CF0DB30C525734A2B27189`.
- import-guards.zip: 8DD6A785C3D6F93857B662332BF60EF092D56610A0333363A2C40EB80EDF3CF3.