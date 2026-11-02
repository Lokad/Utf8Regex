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
is at least 39.12ms. The sole selected caller confirmation follows below;
no product variants or third comparison.

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
Source `253d5983` is pushed; CI 41391386838 passes Windows/Linux. The sole selected
confirmation uses the exact official `MeasurePythonReQualificationBatch` caller
and its own timer, original full consumption checksum/digest and unchanged
benchmark bytes. Separate default-load-context hosts compare parent284 with
committed253: miss 1.617 -> 0.778us (-51.90%), hit 0.998 -> 0.779us (-21.95%),
literal control 2.382 -> 2.366us (-0.66%), unchanged allocations. Each host took
about 7.4s, warmed each case for at least 2s, independently calibrated and kept
five samples >=20ms. The trace's reported WarmupMilliseconds includes subsequent
calibration/samples; its fixed 2s guard and call count describe actual warmup.
The confirmation allowance is exhausted.

An initial selective launch mistakenly wrapped the host in external `taskset`.
Readonly probes prove that this changes .NET startup ProcessorCount from 8 to 1;
the repository's in-process placement keeps startup count 8 while pinning its
main thread. This is a different runtime environment from the full refresh.
Its ~3.9us and 15.013x/12.103x paired observations remain archived as single-CPU
evidence, not ordinary-environment replacements or an inferred producer regression.
AGENTS now records the distinction. No producer change was made to alter a result.

One corrected selective launch reused the exact clean253 binaries and the normal
placement workflow. Fresh official CPython paired estimates are miss **3.323
[2.967,3.459]**, hit **2.674 [2.371,2.728]**; both are **Inconclusive**, respectively
because of 8.25% lane-order variation and 1.138x managed IQR spread. No verdict
retry. The printed raw diagnostics are 0.774/0.743us, 3,552B. `--measure-pythonre-case`
does not persist raw rows: their historical source2c6 values/identities remain,
while these two authoritative paired qualifications carry source253. This is
intentional mixed-source evidence, not a claim of a fresh complete catalog.

Only these two qualification entries and generated Markdown changed. The other
88 cases, catalog/corpus metadata, lifecycle and scaling remain identical.
See `selected-summary.json` and `caller-summary.json`. All initial single-CPU,
normal-workflow, exact-caller, metadata-probe and snapshot-scope evidence is in
`selected-253d5983.tar.gz`, SHA256
`100EAE6E13AF404F89315F308AD6BA523C803AA73DB6ABE698526179C9D10714`.
Immutable dev.9/published packages are unchanged; batch the full cumulative and
delivery boundary after further meaningful progress.

Imported snapshot scope and generated Markdown verification pass. Matching-build
PythonRe snapshot guards pass on .NET10/11; no complete suite was repeated after
the data import. `post-import.zip` SHA256
`9DDAF0AFDAD9F5B33DB181CEA9D99D7413459320ED7EA4725D913AE54908CF41`
preserves these checks and the placement instruction.
