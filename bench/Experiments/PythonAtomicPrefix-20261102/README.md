# Seek necessary starts inside atomic bodies

PythonRe's existing bounded leading-ASCII proof stopped at an atomic opener.
The actual `(?>ab+)c` profile program starts with an atomic fork whose alternate
is `Fail`, followed by literal `a`. It can enter the mandatory body to prove that
necessary start, just as its non-atomic counterpart already does. The original
VM still verifies the complete body, atomic commits, alternatives and captures.
Nullable or unsupported bodies retain conservative fallback when the proof
cannot finish. Its 64-instruction/16-pending bounds and existing 2,048-byte seek
checkpoints remain. No new retained state, buffer, engine, mode or unchecked API.

This is a different live cause from the closed required-byte rarity experiment:
the original required-literal screen remains, including its absent-`a` rejection.
Full-subject strict validation, scalar floors, original tokens/deadlines, resource
admission, cursor aliases and retained captures remain. Skipping impossible VM
attempts reduces route-dependent work charges, as documented in SPEC-PYTHONRE.
It does not discard atomic behavior or capture state.

One quiet-VM .NET11 RC3/CPU2 public comparison took **98.562 seconds**, 17 workloads
and four equal-result lanes. Baseline is `284f0afa`; the candidate is the same
parent plus the archived profile-only patch. **Core DLL bytes are identical**.
Search/FullMatch retain complete UTF-16 start/end digests; Count retains complete
integer outputs, all checked after timing. Compilation is excluded. Diagnostic
predecoded/decode-then compiled Regex lanes cover overlapping semantics; official
CPython qualification is separate. Warmup is 1s/minimum 128 calls per lane,
independently calibrated 60ms batches and five alternating samples. Every sample
is at least 39.12ms. No confirmation, variants or third run.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Actual atomic miss Search |1.578|0.861|-45.48%|
| Actual atomic hit Search |0.962|0.793|-17.60%|
| Late-hit Search, 8 KiB prefix |620.204|1.177|-99.81%|
| Dense-miss Search |323.841|119.719|-63.03%|
| Finite/live miss Search |1.866|1.027|-44.94%|
| Finite/live hit Search |1.200|0.959|-20.10%|
| Deadline/live miss Search |2.162|1.284|-40.61%|
| Unicode captured Search |1.501|1.106|-26.33%|
| Possessive hit Search |1.077|0.827|-23.26%|
| Dense atomic hit Count |166.611|122.748|-26.33%|
| Dense atomic miss Count |325.756|119.119|-63.43%|
| Absent-a/present-c control |0.397|0.407|+2.50%|

**Promote.** Every allocation median is unchanged. Largest adverse median is
+2.50%, with control spreads below 1.009x. Actual miss spreads are 1.085x/1.092x;
they do not obscure the useful gain. Nullable/folded/literal/FullMatch control
movements are retained without causal gain claims. This parent comparison is not
the final fixed-original cumulative gate or a catalog verdict.

Complete PythonRe Release checks pass on .NET10/11 (4,545 per runtime), including
atomic choice ordering, mandatory/nullable/folded/nested bodies, capture values,
mixed scalars and byte floors, bounded proof depth, alias lifetimes, malformed
tails, result/working denial and original token/deadline checkpoints with a
retained parent loan. Focused proof/resource checks also passed. An initial new
test wrongly expected a folded literal to decline: Python had already expanded
it to a supported ASCII set. The corrected eligibility assertion and the initial
failure are preserved. No unchanged Core/PCRE2 or package matrix was repeated.

Raw source/IR probes, both producer DLL/PDB sets, driver/binaries, predeclaration,
all observations and logs are preserved under
`artifacts/experiments/python-atomic-prefix-20261102/`:

- `evidence.tar.gz`: SHA256 `D08C233D905D8D7DDEF734C67B4B59CCA5BF302E56E7AB491B030AD7958301EE`.
- `correctness-source.zip`: SHA256 `98D03C434DB7531F3D1798E5F7F4906EA6C73C18351E344ED49831E3178B8B08`.
- Runtime patch: SHA256 `0632982D823AA58211BE8A594D3BB8F05F455E7812A8B734D5F1F8B79795B0B8`.

See `summary.json` for every workload, exact identities, allocations and spreads.
Next: selectively refresh atomic miss/hit against official CPython on clean,
matching committed binaries. Preserve unrelated rows, uncertainty and immutable
dev.9/published packages; batch the full cumulative/delivery boundary later.
