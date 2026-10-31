# Core live-token Boolean matching and funded template destinations

Fixed baseline `cc5fb784`; producer `5770b86c` includes funded-destination source
`8713d84b`. The new TODO handoff is integrated into high-priority H4 in the ignored
root plan and removed. Its 0.4.0 observation is historical; no Awk code is changed.

## Delivered scope

Eligible controlled ASCII `IsMatch` calls reuse the existing complete simple-
branch finder with its original bounded checkpoints. They avoid scalar capture/
backtracking scratch. All existing grammar fences, strict full-subject validation,
original-token cancellation and deadlines remain. Ordinary/Unicode/RTL/NB routes
outside that proof keep their existing behavior. No new engine or control mode.

`Utf8RegexProgramOwner.TryReplace` accepts a strict native template and a caller
destination. The same shared working ledger admits cold key/parser capacity and
live output overlap; loans remain live through the call. The existing program
allowance prepays the conservative cache maximum. No refund/reacquire gap or new
retained result type. It reuses the controlled scalar replacement route, including
native literal guidance and RTL/NB semantics; ordinary unlimited grouped-worker
throughput is not promised. Output is caller-owned and survives program disposal.

## Current-source allocation qualification

The provider-only probe runs 96 scenarios per runtime: six ASCII/Unicode hit/miss
subjects, None/Compiled/NB/RTL, absent/live construction token, absent/live current
operation token. Every row warms 1,000 calls, then measures 10,000 synchronous
public `IsMatch` calls using the current-thread counter. Completed construction
tokens are canceled before matching; original-token pre-cancellation is checked.
Both .NET 10.0.12 and .NET 11 RC3 agree:

| Route | None operation token B/call | Live operation token before -> after B/call |
| --- | ---: | ---: |
| Anchored ASCII hit/miss, None/Compiled | 0–0.0128 | 1800 ->120 |
| Unicode scalar hit/miss | 0 | 120 ->120 |
| Unicode category hit/miss | 1680 | 1800 ->1800 |
| Anchored ASCII NB | 640 | 760 ->760 |
| Anchored ASCII RTL | 1680 | 1800 ->1800 |

The targeted live-token allocation drops **93.3%**. Its controlled invocation
owner remains; do not claim zero allocations or a general scalar workspace fix.
The other routes are explicit unchanged scope, not missing public qualification.

The initial post-edit probe used stale private file-reference copies and still
reported old costs. Preserve it labeled stale in the archive. Corrected runs
explicitly copied and hash-checked the loaded/source DLLs. Local precommit
allocation identities are `cc5fb784` plus the funded-source patch before, and
`8713d84b` plus the Boolean-source patch after. The VM comparison below instead
builds the exact clean frozen revisions and records DLL hashes.

## Bounded public costs

One quiet-VM run completed in **68.985s**, fourteen cases/32 lanes, under a 120s
cap. No confirmation was needed; this experiment is closed. Release .NET 11 RC3,
affinity mask 4, warm 1.5s/at least 1,000 calls per lane, independent 80ms
calibration, three alternating samples. Minimum sample **45.074ms**. Setup and
compilation are excluded; operations include validation, preparation, matching,
publication and synchronous refunds. Complete result batches are checked after
timing. Raw measurements remain authoritative.

| Public operation | Before (us) | After (us) | B/op before -> after |
| --- | ---: | ---: | ---: |
| Unchanged literal Count | 0.5413 | 0.5424 | 0 ->0 |
| ASCII hit, live | 0.9656 | 0.2460 | 1800 ->120 |
| ASCII miss, live | 0.5779 | 0.2183 | 1800 ->120 |
| ASCII hit, compiled/live | 0.9445 | 0.2534 | 1800 ->120 |
| ASCII hit, None-token control | 0.1318 | 0.1380 | 0 ->0 |
| ASCII hit, finite/live | 0.5986 | 0.1501 | 1800 ->120 |
| Unicode scalar live control | 0.2374 | 0.2240 | 120 ->120 |
| Unicode category hit live control | 0.5819 | 0.5919 | ~1800 ->~1800 |
| Unicode category miss live control | 1.0355 | 1.0515 | ~1800 ->~1800 |
| ASCII NB live control | 1.1565 | 1.1978 | 760 ->760 |

The targeted ASCII hit is ~3.9x faster, miss ~2.6x, compiled hit ~3.7x and finite
hit ~4.0x. These are public-operation observations, not isolated kernel estimates
or a catalog Status change. Controls vary -5.6% to +4.7%; do not claim precise
equivalence or apply these gains to unsupported routes.

Funded destination is a new API: no funded baseline exists. The extra lane is
an explicit feature-cost observation against the existing unfunded live-token
destination calls, with equal results and lightweight atomic program/working
host counters. No claimed funded speedup/regression:

| Destination operation | Existing before / after (us) | New funded (us) | Existing / funded B/op |
| --- | ---: | ---: | ---: |
| Warm capture template | 4.3763 / 4.4303 | 4.7751 | 6656 /6896 |
| Cold 32 capture templates | 4.9488 / 4.9889 | 7.5584 | 9568 /9672 |
| Finite capture template | 2.6610 / 2.7457 | 3.0611 | 6656 /6896 |
| Native literal guidance | 1.7930 / 1.7552 | 1.9634 | 560 /800 |

The new funding cost is ~0.2–2.6us, largest on cold preparation. Do not hide it
behind allocation improvements or present it as a <=10% equivalence pass.
Program funding stays constant across calls, working loans return to zero after
each call, and program close returns its final loan. Cold keys use five positions
of `${1}` versus `$001` (group zero for the literal case): 32 distinct equal-size
keys with identical expansions. Therefore caller buffers have invariant output
and can be fully checked after timing without timed copies/snapshots. Before and
after use the same template source and direct native APIs, without host decoding.

## Correctness and remaining work

Full Core Release **40,892 passes/runtime** on .NET 10 and 11 after both changes.
Focused funded/preparation/resource checks: 478/runtime, related reused-binary
checks: 47/runtime; final Boolean/work/funded checks: 347/runtime. API snapshot
and docs are updated. Tests include cold/warm/no-match, strict malformed tails,
scalar/sparse/unmatched captures, RTL/NB, aliases/short destinations, actual
matching cancellation/deadline after full validation, original-token entry,
terminal shared-root refusal, all numeric controls, close during use and
synchronous refunds. A public numeric-overlap case denies cold preparation plus
output, then admits warm reuse under the same cap with the valid cached plan.

H4's targeted producer boundary is source-qualified. PCRE2 funded replacement,
final immutable packages/actual consumers, current full sub-hour refresh and
fixed-baseline cumulative costs remain. Do not repeat completed template/parser
campaigns or remove basic metering to recover overhead. The separate Core grouped
template warm-throughput debt in the previous receipt remains unchanged.

## Evidence

Adjacent [comparison](comparison.json), [driver](Compare.cs.txt), allocation
probe [source](AllocationProbe.cs.txt) and [project](AllocationProbe.csproj.txt),
and before/after allocation JSON retain the exact evidence. Immutable local
archives under `artifacts/experiments/core-hosting-20261031/`:

- `utf8regex-core-hosting-5770b86c.tar.gz`: before/after DLL/PDB/builds, VM driver,
  source bundle, preflight and raw comparison. SHA-256:
  `AAAB717F6A685964EBFCBD152BBE43A6C3F7DD05721380F4DD7E61655EB848E8`.
- `source-correctness-logs.zip`: both-target allocation source/binaries/results,
  labeled stale attempt, matching focused/full logs and final source bundle.
  SHA-256: `514D3BAEA2D45BE15996129328615417BCDD311C23E1FA706D6FE543EAC14F41`.

No published/staged package bytes were regenerated or modified.
