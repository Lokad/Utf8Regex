# Eager string-findall: remove a duplicate checkpoint

Accepted source change after857edb79: remove only the loop-tail invocation
checkpoint in `FindAllScalarStrings`. Every successful cursor advance leaves
`_finished` false, so the immediately following `MoveNext` checks the original
invocation before discovery. Staging is private, without callbacks. Keep inner
per-capture/decoding checks, EOF, major admission and final publication.
No new executor, mode, public API or accounting abstraction.

Intentional accounting revision: one fewer modeled invocation-work step per
materialized match on this scalar string route. Short named/tuple/empty fixtures
each produce three matches and require79->76,154->151,67->64 steps respectively;
controlled exact literal requires28->27. Limits still deny explicitly and never
publish partial output. Working/depth/result scopes and retained values stay.

## One bounded public comparison

Quiet VM20.13.128.207 CPU2, .NET10.0.12, FullOpts/ReadyToRun off. One before->after
pair16.967s;120samples,each>=71.935ms,five samples per cell,alternating lane order.
120ms warmup and80ms calibration target per lane. No confirmation needed.
Same frozen Core and driver; only Python DLL changed. All expected values/shapes,
counts and short-unit work cutoffs checked outside timing; timed results retained
as their public struct without boxing or extra checksum traversal.

| Public FindAllToStrings | Unlimited before -> after | Finite change | Finite + token change |
| --- | --- | --- | --- |
| Named capture,768 strings/5376B | 283.730 ->271.445us (-4.33%) | -5.32% | -6.95% |
| Two captures,384 tuples/2944B | 314.593 ->317.517us (+0.93%) | +0.32% | -0.43% |
| Supplementary/empty,257 strings/768B | 101.480 ->101.904us (+0.42%) | -1.42% | -1.99% |
| Exact literal,768 strings/4608B | 42.687 ->42.285us (-0.94%) | -2.26% | -3.47% |

Only the exact-literal **unlimited** lane is an unchanged control: its canonical
literal shaping bypasses the edited loop. Source tracing corrected the initial
predeclaration's overly broad control description: finite/live-token exact calls
enter scalar shaping and are affected workloads. The per-row minimum-work probe
always uses finite policy, including when printed beside an unlimited timing.

Sample spreads<=1.035x. Exact-control allocations remain6168B unlimited and47280B
controlled. Other lanes have small sample allocation variation (raw ranges retained); no allocation algorithm changed and observed before/after
medians differ by at most0.98B/operation. Do not claim byte-exact allocation parity.
Finite allowances:50Mwork,1024depth,64MiBworking,128MiBresult; experimental budgets,
not defaults. This is source evidence, separate from default-tier CPython
qualification and immutable115 actual-package cost. No general speedup claim.

## Correctness and evidence

Affected Python Release closure built once;690 focused tests pass, then668 scalar,
capture,cancellation,detailed-publication and cursor checks reuse matching binaries.
No failures/skips. Twelve new theory cases inject original cancellation or expiry
at every observed public checkpoint for named/tuple/empty matches, including the
last: no partial publication, original exception authority, retained values and
fresh calls survive. Existing tests locate complete finite-work cutoffs and cover
working/result denial, ownership, malformed tails and cursor lifecycle.
Core/PCRE production source is unchanged; no package or platform matrix per edit.

[Raw samples](../../../artifacts/findall-checkpoint-20261020/vm.jsonl),
[summary/identities](measurement.json),
[predeclaration](../../../artifacts/findall-checkpoint-20261020/PREDECLARED.md).
Frozen Core865CD637...,before Python2A163B36...,afterC3B30188...,driver7CAACADB....
Local artifact directory retains complete hashes,frozen binaries,payload,runner,
preflights and test logs. Original immutable packages/staged bytes remain intact.

Root disk full: RAM-only VM job, evidence saved locally. Initial heredoc preflight
failed before timing; setting TMPDIR before payload decoding fixed it. Both actual
driver cells and batch completed successfully; trailing PowerShell pipeline CRLF
made the outer shell return127 after the batch-complete marker. Preserve this
transport failure; all expected120 samples and completion identities are present.
No measurement repeated or result discarded because of that outer-shell error.
