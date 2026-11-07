# Literal assertions within the scalar segment

Promote the existing bounded ASCII assertion evaluator into `RunSegment`, avoiding
VM suspension and re-entry for eligible bodies. The existing scalar rewind loop
is shared with complex assertions. No field, prepared representation, mode,
engine or public API is added. Capture rollback, polarity, required end, logical
depth, work charges, original cancellation/deadlines and parent loans remain.

The proof still accepts only 1–32 unfolded ASCII literal instructions followed
by Accept. Captures, folding, Unicode, longer bodies and other instructions retain
the continuation driver. Required-literal families retain their separate invariant:
members occur inside the returned match, so lookbehind context was not added to
that collector.

## Public costs

ONE separate-Default-process ABBA completed in **103.171 seconds** under a
120-second cap: 20 public workloads, 400 samples, minimum 56.9968 ms. The exact
qualified dev.52 Windows Core DLL is compared with the same source plus this
patch; PythonRe/PCRE2 DLLs remain fixed. The caller is identical in all processes.
Warmup is 750 ms/minimum 32 calls, independent 75 ms calibration, five samples
and a 20 ms floor. Startup sees eight CPUs; affinity then selects CPU2. Results
are checked after timing. All 20 allocation medians are unchanged.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| PythonRe dense lookbehind Count | 186.407 | 140.787 | -24.47% | 0 |
| Core dense lookbehind Count | 167.289 | 127.111 | -24.02% | 0 |
| Core same, finite/live | 179.864 | 144.665 | -19.57% | 112 |
| PCRE2 dense lookbehind Count | 168.218 | 127.595 | -24.15% | 0 |
| PCRE2 negative lookbehind Count | 171.186 | 130.614 | -23.70% | 0 |
| PythonRe miss Search, finite/live | 0.513 | 0.479 | -6.64% | 112 |
| PythonRe hit Search, finite/live | 0.546 | 0.502 | -7.92% | 112 |
| PythonRe miss Search, deadline/live | 0.723 | 0.690 | -4.62% | 160 |

All eight gains persist in both processes with disjoint sample ranges. Ordinary
short Search remains uncertain: miss after-process medians are 0.3001/0.3373 us
against 0.3296/0.3286 us before; hit 0.3089/0.3504 against 0.3542/0.3434 us. Ranges
overlap and not every process improves. Do not claim settled gains for those rows.

Retain overlapping control movement: PCRE ASCII +2.94%/+13.07 ns, Core ASCII
+0.08%/+0.253 ns, BCL -2.89%/-0.870 ns and mail Count -1.17%/-24.80 us. The 33-byte
fallback improves 4.73% with disjoint ranges; capture/fold/Unicode fallbacks overlap.
No normalization, unmeasured causal explanation, uniform speedup or new catalog
Status. No confirmation or tuning: the clear dense/finite gains decide this pilot.
[Summary](cost-summary.json) and [acceptance](acceptance.json) retain the scope.

## Validation and evidence

Focused Release checks pass on Windows/Linux and .NET 10/11: Core 130, PythonRe
78 and PCRE2 201 per target. They cover parent-alternative capture rollback,
positive/negative assertions, RTL, scalar context, offsets, 1/31/32/33-byte bodies,
fallbacks, work cutoffs, depth, original interruption and retained results. Initially
missed fixture-name prefixes were covered with exact SkipBuild filters, without
repeating cases or builds.

Untimed FullOpts confirms the evaluator call moved from RunFrom to RunSegment.
RunFrom is 5260→4988 bytes; RunSegment 10554→10827; evaluator 939→1012. These are
[code-generation facts](codegen-summary.json), not public-operation speedups.
The first untimed codegen invocation hit the caller's timing-override guard;
normal 20-row preflight had passed. The guard was corrected for preflight only
before timing. Initial caller/source/scope/input remain archived. No failed timed run.

All 282 staged assets and frozen Full52 graphs are unchanged. No package or Full
qualification was repeated. Candidate dev.52 and its source/data remain immutable;
this is subsequent source work, awaiting a later material delivery boundary.

Canonical raw evidence is immutable and verified off VM:

- `artifacts/experiments/assertion-segment-20261107/evidence.tar.gz`:
  `0FDB9871A166E8150C574DCC287A598EE2FD30CC628349484A3A6391118F4334`.
- `windows-evidence.zip` in that directory:
  `057A4DC0ABCC9693CCEB9B3517D709C85FD27D348F71C19A8D5C5062DB040923`.

Source parent `4972688b`; Core before `D9667053`, after `00D7A259`; fixed PythonRe
`47870A68`/PCRE2 `3236E597`; final caller `5E58093B`. Full hashes and the source
patch live in raw evidence and [scope](scope.json). No NuGet publication.
