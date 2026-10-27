# Result absence, map-free misses and benchmark exit reporting

Development remains `0.4.0-rc.2-dev`. Source commits: default views `a4bde3fd`,
no-hit projection/Count forwarding `60f2f818`, benchmark outcomes/docs `56042eb1`.
No product package or published benchmark snapshot was created or refreshed.
The ignored PLAN/history preserve the active/deferred boundary; `review.md`
records the original assessment. Protected historical bytes remain unchanged.

## Delivered contracts and deletions

Default Core match/capture byte accessors now handle absence before consulting a
coordinate map. They expose zero ranges and empty values, with the existing
alignment/Try semantics. Actual misses still retain unsuccessful group zero;
default match views have no groups. Successful empty/supplementary captures,
unknown groups and retained aliases keep their behavior. GroupCount XML and
MIGRATION clarify the distinction. The internal boundary mapper and result
layouts are unchanged. Default split behavior was outside this change.

The unsuccessful-context factory no longer accepts a map. All callers
drop that argument, including six native no-hit branches that formerly forced
the lazy full-subject projection. The existing captured-family helper takes a
scoped reference to its existing validated input and requests the map after a
successful candidate. It retains discovery, branch IDs and successful shaping.
UTF-16-offset APIs still build maps needed to validate/resolve their input offset.

Complete-subject strict UTF-8/scalars, captures/catalogs, RTL/NonBacktracking,
byte/derived coordinates, cursor/result lifetimes, original-token synchronous
cancellation, absolute deadlines and work/depth/working/result/state controls
remain. No engine, policy, projection framework or ownership system was added.

Deleted two identical Count forwarding methods and combined the ExactLiteral/
LiteralFamily switch arms with the same default-validation argument. Real
fused/native/scalar Count logic remains. Their earlier characterization as
specialized logic was mistaken; this deletion has no separate speedup claim.

The benchmark entry point now checks BDN summaries, validation errors, report
success and actual result statistics. Its own parser distinguishes informational
empty returns from parse failures; the second parse is restricted to that empty
path and is silent. Removed the manual BDN option whitelist. Custom dispatch,
streaming, child exit propagation and wall-time caps remain. Errors return2 for
parsing or1 for failed/empty execution; help/list/info/version and real successful
measurements return0. AGENTS documents this workflow contract.

The implementation uses existing public APIs from pinned BDN0.15.8:
[switcher](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Running/BenchmarkSwitcher.cs),
[parser](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/ConsoleArguments/ConfigParser.cs),
[summary](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Reports/Summary.cs) and
[report](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Reports/BenchmarkReport.cs).
No additional command or qualification framework was introduced.

## Focused validation and actual tooling limitation

- Default/context/projection/API focus:24 pass. Miss/capture/resource/RTL/NB/count
  focus:568 pass. Full affected Release Core:39,643 pass in57 seconds.
- Final source Core context/miss/API/inventory focus:29 pass. PythonRe resource/
  cancellation/native/cursor/API focus:88 pass; PCRE2 resource/cancellation/
  capture/API focus:49 pass. Projects build incrementally; matching binaries reused.
- Final source `56042eb1` passes both Windows/Linux source CI in
  run40096157949.
  CI builds once per platform and tests all four source fixtures. No matrix added.
- Matching Release benchmark/library closure and all three generated-page
  verifiers pass without writes. The before/after public witness removes all
  eight default-context NullReferenceExceptions. Default split's existing explicit
  InvalidOperationException remains. Actual misses and supplementary controls pass.
- CLI invalid job/unknown custom command:2; empty execution filter:1;
  help/info/list/version:0. A successful one-benchmark in-process Dry smoke exits0
  and executes one benchmark. These are smoke checks, not throughput evidence.
- A real out-of-process generation failure exits1. An attempted successful
  out-of-process smoke also exposed this pre-existing failure: BDN recursively
  searches the ancestor solution and discovers archived copies of the benchmark
  project under artifacts. The requested one-second build timeout was never
  reached; generation failed first. The record labels the observed cause.
  No archive was removed. Changing cwd alone cannot override its ancestor-solution
  search. Repair/isolation for ordinary out-of-process BDN remains deferred;
  custom measurement commands and the in-process smoke work. Failure now reaches
  the shell instead of appearing as a successful run. No automatic fallback mode
  or toolchain wrapper was added.

The generation-failure and in-process-smoke transcripts are saved as adjacent
cli-generation-failure.txt and cli-inprocess-success-smoke.txt (display whitespace
normalized; original logs remain in ignored artifacts).

CLI checks used matching precommit C# binaries. Final source builds and CI are
recorded separately; these observations do not claim package provenance.
All18 protected files (four snapshots, nine frozen RC1 files, five TODOs) retain
their exact SHA256 identities. PLAN.md and PLAN.HISTORY.md stay ignored.

## One bounded public before/after comparison

Core before: source `28b31f70`; after:`56042eb1`. Both SDK10.0.401, Windows,
.NET10.0.13. DLL/PDB SHA256 identities are recorded. The driver invokes public
operations in separate assembly load contexts on one logical processor; it
verifies results before and during measurements. Constructors are outside timing.

Each version/lane warms for2.5 seconds and calibrates toward40 ms; nine sample
pairs alternate order. Total wall-time36.83 seconds. Raw samples retain noise;
the table reports medians. No confirmation or second timing campaign was needed.

| Public workload | Before ns | After ns | Change | Before B/op | After B/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native detailed literal Unicode miss,128KiB | 365,794 | 113,388 | -69.00% | 270,400 | 0 |
| Native detailed captured-family Unicode miss,128KiB | 402,358 | 160,670 | -60.07% | 270,400 | 0 |
| Detailed short ASCII literal hit | 355.06 | 271.38 | -23.57% | 0 | 0 |
| Detailed captured-family Unicode hit | 506,074 | 517,678 | +2.29% | 270,488 | 270,488 |
| Compiled literal Count control | 1,378.14 | 1,435.46 | +4.16% | 0 | 0 |
| Finite-work detailed Unicode miss control | 4,349,590 | 4,326,940 | -0.52% | 112 | 112 |

Accept within the10% affected public-cost allowance. The large miss improvement
is accompanied by removal of the full-map allocation; the unchanged/control
rows expose ordinary measurement variation. No separate causal speedup is claimed
for the short hit or forwarding deletion. The allocation witness also verifies
1,024-scalar Unicode misses drop4,288B to0B while ASCII and finite-work controls
remain0B/112B. This is source-built Windows evidence, not Linux timing, actual-
package/host costs or cumulative acceptance against fixed `844dcbad`.

## Deferred work

Fresh common-version/exact-pin packages, selected platform/API/package/provenance
and isolated Awk/Lython/PCRE2 consumers remain at the release boundary. Actual-
package/host finite costs and cumulative <=10% public execution against the fixed,
correctness-equivalent baseline remain separate gates. Frozen RC1 must not publish.
The quiet VM still needs disk space before new builds/benchmarks. Ordinary BDN
archive-discovery repair is a separate known tooling issue. Closed checked-access,
accounting/layout/retention investigations remain closed.
