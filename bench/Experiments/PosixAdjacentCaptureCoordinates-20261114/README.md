# POSIX adjacent capture coordinates

Accepted source `ec31fe66`, against dev91 POSIX `cd8eb5c7`, with identical Core
`1f2b34c1` in both processes. Within each public result, reuse the preceding
completed capture's byte/UTF-16 end when the next range advances beyond it.
Whole-end reuse remains; nested/reordered ranges use the whole-match start.
Two call-local integers, no subject index, owner, retained state, allocation or
public API. Selection/history algorithms and all admission/interruption controls
remain. [Acceptance and all observations](acceptance.json).

One **74.916s** ten-row ordinary-process ABBA, five150ms-target samples per
process/row, all>=20ms,150s measurement cap, **zero confirmations**. Distinct POSIX
DLL/PDB pairs and every Core/POSIX compiler-source checksum verified. Identical
Core/caller/.NET images; Count has the same path in differing POSIX DLLs.
Caller-valid unchanged UTF-8, same options and complete public Match/capture
byte/UTF-16 materialization into reusable caller-owned destinations. Check all
outputs and funded refunds after timing. No kernel-only or native parity claim.

| Complete public operation | Before µs | After µs | Change | Allocated B/op |
|---|---:|---:|---:|---:|
| Short SKU | 0.204 | 0.204 | +0.10% | 0→0 |
| Eight adjacent ASCII fields and final run | 46.641 | 45.263 | -2.96% | 0→0 |
| Funded/deadline adjacent ASCII | 65.250 | 63.722 | -2.34% | 160→160 |
| Four adjacent Unicode captures | 20.209 | 13.834 | -31.54% | 0→0 |
| Four gapped Unicode captures | 20.395 | 13.998 | -31.37% | 0→0 |
| Nested/reordered ranges | 23.034 | 22.847 | -0.81% | 0→0 |
| Short empty overlapping captures | 0.495 | 0.505 | +2.02% | 0→0 |
| POSIX Count path control | 0.185 | 0.186 | +0.31% | 0→0 |
| Identical Core Count | 12.023 | 11.968 | -0.45% | 0→0 |
| Identical .NET Count | 22.150 | 22.130 | -0.09% | 0→0 |

ASCII, adjacent Unicode and gapped Unicode have separated observed improvement
ranges. All other ranges overlap, including funded/deadline and the adverse
**+2.02%** short-empty median. No formal confidence interval or normalization.
Every measured allocation is unchanged. Avoided coordinate scans can lower
numerical work; buffer/result capacity and its admission are unchanged.

Release POSIX398 checks pass on Windows.NET10/11 and Linux.NET11. New focused
coverage includes adjacent/gapped Unicode ranges, backwards origins from nesting,
empty/absent slots, partial/excess destinations and retained values across EOF;
existing selection/history/cursor/resource/original-interruption coverage remains.
Focused project builds were used; the VM reused Core and built only POSIX/tests.
No solution/package build or full benchmark matrix.

Two harness preflights stopped before timing: depth4096 refusal, then a30s cap.
Uncaptured tails add fictive tags inside repeats and exclude the existing flat-run
proof. The final fixture captures its final run and explicitly asserts eligibility;
no production route or limit changed to make it pass. Both failure archives and
terminal1 statuses remain. The completed task reused the exact frozen producer
pairs/compiler documents, rebuilding only the private caller. No timing repeated.

Raw archived/copied/hashed offVM before releasing the SSH keeper:
`artifacts/experiments/posix-adjacent-projection-bounded-20261114/evidence.tar.gz`,
SHA256 `393EB38CA441A1273B16489DB62DE5673CA9B2322603FA15F4CC7E8778CCB599`. Local tests/setup evidence archived alongside it.
Workers/collectors/keepers terminated; all588 staged package/symbol bytes remain
unchanged. dev91 remains the usable staged dependency. No package/Full/canonical
refresh, new native comparison or modification of published/staged artifacts.
PCRE.NET remains an ordered NFA comparator on verified overlap, not a POSIX oracle.
