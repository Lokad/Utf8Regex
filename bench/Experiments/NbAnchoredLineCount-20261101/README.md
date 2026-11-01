# Count a proved complete anchored-line root without a viability table

The existing owned Count runner recognizes a complete NonBacktracking graph of
at most64instructions: beginning anchor, optional Unicode-whitespace star,
nonempty unfolded ASCII literal with a non-white first byte, greedy non-newline
dot star, end anchor and transparent capture markers outside loop bodies.
Unsupported grammars retain the table runner. Match, captures and retained cursors
are unchanged. No new metadata, arrays, engine, execution mode or framework.

Candidates use checked span search. The original full subject supplies anchors;
whitespace rewinds respect scalar origins. Any valid start gives the same greedy
end, which is sufficient for Count. Failed end anchors retain overlapping starts
inside newline-containing prefixes; prefix width bounds shared tail rescans.
Each2048-byte forward scan,64-scalar rewind and at-most64-instruction candidate
verification admits work and directly checks the original token/deadline, even
for unlimited calls. Final publication checks again. Zero local working admission
still observes enclosing loans and first failure. Strict full-subject validation
remains before execution. [SPEC](../../../docs/SPEC.md) and
[migration guidance](../../../docs/MIGRATION.md) record lower route-dependent work,
working and depth requirements when no table or VM frames are needed.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json) took89.630s
on the quiet Ubuntu VM, .NET11RC3, CPUmask4. Fourteen cases/four equal-result lanes,
1s/min64 warmup, independent60ms calibration,5alternating samples (minimum58.068ms).
Full validation is included; completed batches are consumed after timing. Frozen
8cb45ef0 binaries before, parentf7222d08 plus archived runtime patch after.

| Public operation | Before us | After us | Speedup | Allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| region-ordinary | 1196.203 | 6.749 | 177.24x | 557416 -> 64 |
| region-compiled | 1271.566 | 6.780 | 187.56x | 557416 -> 64 |
| region-live | 1285.961 | 7.900 | 162.78x | 557536 -> 184 |
| region-finite-live | 1334.094 | 8.449 | 157.90x | 557536 -> 184 |
| region-deadline-live | 2223.097 | 16.088 | 138.19x | 557584 -> 232 |
| literal-head-no-white | 326.073 | 24.185 | 13.48x | 133296 -> 64 |
| unicode-whitespace | 276.261 | 31.804 | 8.69x | 93360 -> 64 |
| dense-false-prefix | 196.540 | 89.172 | 2.20x | 98416 -> 0 |
| newline-prefix | 79.963 | 7.807 | 10.24x | 23152 -> 0 |
| absolute-end | 198.316 | 21.623 | 9.17x | 49384 -> 0 |
| folded-control | 166.180 | 141.241 | 1.18x | 36976 -> 36976 |
| wide-control | 2314.950 | 2355.753 | 0.98x | 99560 -> 99560 |
| non-nb-scalar-control | 4.686 | 4.709 | 0.99x | 0 -> 0 |
| literal-control | 0.398 | 0.397 | 1.00x | 0 -> 0 |

**Promote.** Retain wide control+1.76% and non-NB+0.51%. Folded control remains on
the table route; its observed-15.01% is retained without attributing it to the
new specialization. [Pairs/spreads](summary.json) include every observation.
No confirmation or variant campaign. These are incremental public costs, not a
cumulative parity or final package qualification claim.

[Release checks](verification.json) pass on net10/net11:750 focused Core,
729 overlapping resource/budget/allowance/lifetime/cancellation,28 differential,
46 PythonRe and45 PCRE2 controls. Do not sum overlapping selections. The final
38-check new fixture adds pathological overlapping-newline-prefix hit/miss work
scaling to the earlier37checks. Public Count agrees with owned scalar cursors and
BCL overlapping semantics at every scalar origin in the edge-case corpus.
Positive and declining grammars, captures, LF/CRLF/EOF/Unicode whitespace/NUL,
failed end anchors, all finite cutoffs and actual seek/rewind/tail/last-publication
cancellation/deadline witnesses preserve enclosing13-byte loans. Initial witness
failures were test-offset handling of NUL and an impossible over-cap loan setup;
corrected witnesses pass. The newline-prefix end-failure algorithm correction
preceded timing and is covered directly.

[371 existing table-state probes](state-verification.json) remain byte-for-byte
identical, including every work count, viability bit, capture/offset, replay,
working peak and loan refund. Only admitted complete-root Count bypasses that
existing preparation; no other operation or dormant engine was replaced.

- [Frozen binaries, harnesses, patch and raw measurements](../../../artifacts/experiments/nb-anchored-line-count-20261101/evidence.tar.gz):
  `267802936D85E8ECD3E2F0F66ED2C22478FD06C43C0981DB7DB6B5944B33AFD6`.
- [Exact prototype source and focused correctness logs](../../../artifacts/experiments/nb-anchored-line-count-20261101/correctness-source.zip):
  `E27998F1A7F5AABD9A5917D9D6B129A3ABBD1C22C1D19B6E4FEDBF1CCB08AB5A`.
- [Final fixture/source and pathological-prefix checks](../../../artifacts/experiments/nb-anchored-line-count-20261101/final-fixture-source.zip):
  `A1A7BC53E8D29FAC075B07619D948305B284727FEE0131C057918A5B70BC763D`.

Previous checkpointf7222d08 CI41298221173 passed Windows/Linux, including the
controlled-clock recovery fix. Current-source full package/platform/actual
consumer/cumulative acceptance belongs to the next common immutable dev.4
boundary; published/staged historical bytes remain unchanged.
Only the region rows were refreshed from clean committed source `697015a6`:
ordinary6.946us/0.1882x and compiled6.942us/0.1891x predecoded.NET11, both Win,
64bytes (previous33.21x/33.42x and557416bytes). Separate point estimates do not
replace the controlled A/B. All30 raw samples exceeded48.911ms. Everyothercase,
identity/status, lifecycle/scaling and worst-first ordering remains unchanged;
only these two rows move to Win. Existing parity verifier and3snapshot guards per
runtime pass. No full package/platform/catalog repetition for this experiment.
[Selective raw/generated/binary evidence](../../../artifacts/experiments/nb-anchored-line-count-20261101/selective-evidence.tar.gz):
`337C608D2DAFB65BC14FA3F577540B4524B2AA0CC5FE0562E80F4FCD89D57575`.
Committed Core DLL SHA256:
`69497A5C97E7DF01882E69E54107233026A9496BA878181961AB8582653800F8`.