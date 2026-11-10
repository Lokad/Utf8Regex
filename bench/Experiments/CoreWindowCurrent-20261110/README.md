# Current river-window CPU attribution after shared-load improvements

One fresh capture on clean `fbce9976`, reusing the matching Core/benchmark graph
built on `fadb8a7c` (library/benchmark C# unchanged between those commits).
Public compiled Count, AssumeValid,16,013,977 unchanged UTF8 bytes. Preflight
checks2 results and3445 candidates/verifier invocations on native_ordered_literal_window.
The existing corpus hash is
`F2AA28234E7A8212C9E009FA9C67D1960D2D063D076765DE46B0FAED5FE44AD8`.

One1.5s warmup followed by9408 public calls/15054.603ms, checking every result
after32-call batches. Existing997Hz cpu-clock:u/perf JIT-map capture,30s outer
cap;8 CPUs visible before CPU2 affinity, scoped PerfMapEnabled only. No runtime/
kernel changes, throughput comparison, optimization, catalog or package work.

16952 self samples/16.936s:16265 JITmapped,687 native/unresolved,0 non-stub map
ambiguities. Includes startup, warmup, corpus work, runtime and output checks;
inlined work belongs to its containing method, not an additive phase estimate.

| Method/group | Self samples |
|---|---:|
| Utf8SearchExecutor.TryFindLiteralFamilyCore |68.81%|
| BCL SpanHelpers.IndexOf |7.26%|
| Utf8Regex.CountCore |5.20%|
| PreparedSearcher.TryFindMatch |5.09%|
| AsciiOrderedLiteralWindowExecutor.TryMatchAt |4.78%|
| PreparedSubstringSearch.LastIndexOfExactAnchored |1.09%|
| BCL NonPackedIndexOfValueType |1.04%|

This replaces the historical pre-shared-load profile as evidence of current live
costs. The dominant work is now in the bounded family search including its
inlined short-pair kernel. Do not treat its containing-method share as pure
metering, SIMD, descriptor-copy or boundary-check overhead without inspecting
source/code generation. Verification/output is a smaller share; no new shortcut
or public speedup is established by attribution alone.

Next bounded investigation: inspect the current family-search block structure
and default Release inlined short-pair code generation for removable repeated
work. Retain basic metering, synchronous original-token cancellation/deadlines,
strict/AssumeValid contracts and bounded finite-state growth. Previously closed
mask-storage/descriptor/readonly-binding/loaded-argument trials stay closed;
source evidence must distinguish a new candidate before another timed comparison.
No new framework or open-ended benchmark campaign.

Exact Core DLL SHA256
`B09AC1068B585196FD9FC7B1C43852BA6E68E918A0C1F9D9652E89F034252AA2`;
PDB `F024917CA29F268884AB339776118950EA5ED188E53749692DE8EE9AF775A013`.
Raw graph/PDB, worker, protocol, perf.data, JIT map/dump and every sample:
artifacts/experiments/core-window-current-20261110/evidence.tar.gz, SHA256
`048A325606157987E9D8A74042AE6DCF428794B51C30FA19171FD9B5E2AC350A`.
Derived cpu-summary.json is beside the archive. Evidence is copied offVM/hash
verified; all workers closed. No timing repair, retiming or full qualification.
