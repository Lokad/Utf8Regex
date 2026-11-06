# Conditional two-byte lead mask

**Rejected and restored.** Classifying fewer vector masks did not improve the
public operations this experiment targeted. Keep the existing validator.

On source `1a483b3e`, the prototype computed the continuation and safe-three-byte
masks first. If those accounted for every high byte, the disjoint C2..DF mask
must be empty; otherwise it computed the original two-byte mask. Both checked
vector widths, scalar fallback, continuation carry, malformed offsets, mode
transitions, allocations and invocation authority were preserved. The runtime
patch was +10/-2 lines, with one mixed-width golden test after a dense Chinese
prefix. No new validator, state, selector, cache or execution mode was added.

Windows and Linux focused Release checks passed on both targets: Core444,
PythonRe202 and PCRE178 per target. Forced256 validation adds80 passing checks.
The new test covers 128 offsets, six scalar widths/ranges and malformed byte
replacements against the metadata scalar path. Existing tests cover all byte
tags, vector/slice boundaries, tails, original-token cancellation and finite
limits. Separate no-tiering Release listings show the conditional branch and
skipped vector classification; total code size remains1414bytes on this VM.
Codegen is diagnostic, not a throughput claim.

ONE **73.118351176s** separate-process Default-context ABBA comparison, normal
8CPU startup then CPU2, default tiering/PGO. Twelve public Count rows, five60ms
samples per row/process; all240 producer samples exceed **58.4838ms**. Each
worker warms1s/min128calls and calibrates independently. One unchanged typed
caller and six adapter/benchmark companion DLL/PDB files; only Core differs.
Result checksums are consumed after timing. No confirmation, tuned variant,
normalization, catalog refresh or package qualification follows.

| Public Count | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual Chinese |85.93|87.59|+1.92%|0/0|
| Actual Chinese, compiled |86.05|87.47|+1.66%|0/0|
| Actual Chinese family, compiled |105.33|107.17|+1.75%|0/0|
| Actual Chinese, finite/live |421.08|430.35|+2.20%|112/112|
| Actual Chinese, deadline/live |428.90|434.24|+1.24%|160/160|
| Actual English control |61.00|62.65|+2.69%|0/0|
| Actual email control |1233.55|1243.09|+0.77%|0/0|
| Folded Russian control |231.10|240.84|+4.22%|0/0|
| Mixed-width tail |97.01|96.75|−0.26%|0/0|
| Late constrained tail |10.54|10.57|+0.31%|0/0|
| Short mixed tail |0.1869|0.1910|+2.21%|0/0|
| ASCII native control |2.6419|2.6478|+0.22%|0/0|

Both after-process medians exceed both before-process medians for the primary
Chinese/family rows. The compiled Chinese and family sample ranges do not
overlap. Several smaller controls overlap; the Russian control also regresses.
There is no primary gain to justify added branching or the local costs. This is
not a rejection under a universal10% gate: local regressions remain acceptable
when substantial gains justify them. No uncertainty normalization or causal
credit is taken from other controls. [Every cost and spread](cost-summary.json).

One separate final BCL diagnostic process confirms equal counts against both
predecoded and strict decode-then Regex for Chinese and family. These are not
CPython measurements or catalog Status; finite limits have no BCL equivalent.
Producer peak RSS103804–103956KiB; no memory-reduction claim.

Before Core `A13D50C19F4FF9654E23EB4991CAAD94365AECB51371D12621CFB8DE5902D3B5`;
after `23DA6873CEB8C9C91CDCC726938900C96C0B23B8BBEF7C01A79DF66D6B20CE9B`.
The experiment preserves all eight frozen dev41 Full assets and staged packages.
A wrong scratch-driver email resolver and the scratch driver's codegen override
guard failed preflight; both were repaired before timing. Production was
unchanged by those repairs. Failures, initial/final caller, declarations, exact
graphs, codegen, every sample and process resource observations are retained.

Off-VM archive `artifacts/experiments/conditional-lead-mask-20261106/evidence.tar.gz`:
`DAD7B8645F39E4A0492381A36631BE61DFDF589EBCF9AF8B8F47375AC298FB81`.
Windows correctness archive `windows-evidence.zip`:
`CF504BA51C31AD978D7DD4B919E3006A1CBA18CB3F8FD7CCF3EC2302D0E2CE33`.

The known two-file patch was reversed after off-VM archive acceptance; the
candidate-only test is removed with it. Restored Core443 checks per target pass
on both hosts, VM source is clean and all eight Full graph identities remain
unchanged. Restoration archive `restoration.zip`:
`E250EE854D92C1BBE40BBC24553CE804A990EC3C964357831645D56B6F911BCE`.
No shipped contract changes.
