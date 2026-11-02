# Skip impossible word-interior declaration candidates

The existing emitted identifier-family kernel requires a leading word boundary
and a word-only prefix. After rejecting a candidate inside an ASCII word, it now
skips that word's remaining interior. It scans from the candidate origin: the
anchor may lie beyond punctuation. Subtraction-based bounds keep the next origin
inside the subject without overflow. The complete prefix, separator and identifier
verifiers remain; no pattern/corpus recognition, new state or execution mode.

The anchor helper now establishes the nonnegative origin and leading boundary,
removing the duplicated checks from emitted IL. Both it and the existing prefix
executor borrow the same fixed checked word SIMD state, moved to the existing byte
predicate utility. Larger anchor sets retain their original prepared search.
No buffer, per-operation allocation or public API was added. Strict full-subject
UTF-8 validation precedes this ASCII route. Existing live-token, deadline, finite
policy and NonBacktracking routes retain their original invocation authority;
captures, scalar coordinates, cursor aliases and retained lifetimes are unchanged.
There is no intentional resource or ownership contract revision.

The actual 53,352-byte code corpus has 5,878 raw byte anchors. An untimed census
preserves all 1,387 boundary-eligible anchors in order while visiting 4,038 anchors;
this is work attribution, not public throughput evidence. Current producer identity
and actual public dispatch were checked separately. Release FullOpts generated
Count is 762 bytes and calls the boundary helper; its warmed Tier1 body and the
original FullOpts listings are archived. Code size alone is not a speedup claim.

One quiet-VM .NET11 RC3/CPU2 public comparison took **66.979 seconds**, 18 workloads,
four equal-result lanes, warmed 500ms with independent calibration and five
alternating samples per lane. Minimum duration: 28.0719ms. Complete integer/boolean
batches are consumed after timing; compilation is excluded. Baseline is frozen
f48327e6, candidate receipt-only parentfeaf867d plus the archived patch. These are
parent comparisons, not the cumulative fixed-a40 gate or a catalog refresh.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Corpus Count, ordinary |72.95|65.85|-9.74%|
| Corpus Count, compiled |73.10|66.41|-9.15%|
| Dense-hit Count |37.03|35.70|-3.58%|
| Dense-word miss Count |391.80|3.04|-99.22%|
| Sparse-miss Count |3.64|3.28|-9.81%|
| Boundary-failure Count |32.18|34.92|+8.50%|
| Larger-anchor Count |14.03|14.94|+6.49%|
| Zero-offset anchor Count |57.09|37.64|-34.07%|
| Dense-miss IsMatch |398.94|3.59|-99.10%|
| Literal control Count |6.24|6.26|+0.28%|

**Promote.** Allocation medians are unchanged in every case. The largest adverse
median is +8.50%, largest paired sample +9.98%; no confirmation or variants.
The larger-anchor row exercises the changed path despite its inherited control
name. Finite/live/deadline observations use unchanged controlled routes, and their
small movements are not attributed to this optimization. Literal is an unchanged
control; the shared word-state control retains its algorithm and allocations.
The complete results, comparator medians and spreads are in summary.json/raw data.

Release checks pass on .NET10/11: full Core, focused PythonRe/PCRE2 Count/resource/
interruption controls, and candidate-origin, zero-origin, unaligned-span, boundary,
punctuation-within-offset, tail, strict malformed-input and original-token tests.
They execute the production emitted implementation directly and public Count,
IsMatch, Match and cursors. The fixed word-set tests cover all byte values.
No full solution/platform/package matrix was repeated for this change.

Raw binaries/PDBs, source patch, driver, input, all timings, scope, corpus census,
original/current route output and Release disassembly are preserved under
`artifacts/experiments/emitted-word-boundary-20261102/`:

- `evidence.tar.gz`: SHA256 `14D283108EB7F76C18D0F31997102CA53DC72DBDF71FB5A6A1AC8C8000F7CC18`.
- `correctness-source.zip`: SHA256 `C296861E3250C834A2C5CE1EFEB2DA1101B0E7219061ACE5E0596BEE548231AB`.
- Measured runtime patch: SHA256 `8A7058D4B95F8940980A44E5B4787FFA544B74D214BF045F7B1847AB3F31CA5D`.

Refresh only the affected keyword catalog case with the existing V3 command on
clean matching binaries. Preserve all other row identities and immutable dev.8/
published packages. Recheck the complete checked candidate against fixed a40 at
the next coherent delivery boundary; this pilot does not claim cumulative acceptance.
