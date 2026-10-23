# CRLF progression, benchmark ownership and corpus maintenance

Source: `f1e2278d` (benchmark cleanup), `af96da74` (corpus reconciliation),
`6e0f3183` (CRLF semantics), `667d1725` (candidate-check call shape),
`5aa1647b` (leading filters across assertions/alternatives).
Development remains `0.4.0-rc.2-dev`. No product package was created or qualified.

## Changes and contracts

PCRE2 CRLF-aware unanchored wildcard search no longer discovers the LF inside a
CRLF after failing at CR. On CR LF x, `.` and `(.)` find only x; `.x` misses.
Explicit caller starts at LF remain valid, including anchored starts. DotAll,
explicit newline alternatives/escapes, complete-subject UTF-8 validation,
supplementary scalars, captures and the original invocation authority remain.
The existing curated wildcard partial probes already implemented this progression;
the new tests preserve their agreement with normal matching.

One compiled progression Boolean is derived from existing parsed atoms and ASTs.
The reference distinguishes negated singleton NOT atoms from general leading
class bitmap filtering: `[^\r]` skips LF, whereas `[^\x0C-\x0E]` can discover it
without attempting CR. An initial interpretation got these two cases wrong;
native comparison corrected both before the semantic commit. A final adjacent
check also corrected leading filters after an assertion and across alternatives;
it changed compilation facts, with no additional runtime mechanism. The final 22
expectations agree with private PCRE.NET 1.5.0 / native PCRE2 10.47, including
captured class variants. This is a narrow progression fact, not a new search
engine, general first-set optimizer or claim of complete native compatibility.
Native code remains confined to benchmark/probe tooling.
The added fact scans charge construction work with bounded interruption checks;
tight construction budgets can deny earlier. Work units remain route-dependent,
and no retained-object accounting mechanism was added.

`bench.ps1` deletes its unconditional global process discovery/termination scan.
It retains contained artifact cleanup and termination of the child tree owned by
the current launch when its wall-time cap expires. Mocked discovery showed three
incorrect selections before the change; production AST inspection now finds zero
global process commands and retains owned-child termination. Unrelated/manual
workers are left running; AGENTS documents closing a locking worker manually.

Four already-proved duplicate-name detailed corpus cases are now active, with
their obsolete backlog records removed and provenance-index statuses reconciled.
Only those statuses changed: IDs, inputs, expectations and source provenance are
preserved. Other backlog records are unchanged. The dated historical
inventory remains identified as historical in PORTING.md.

Deleted the duplicated family-count ledger, repeated fixed-count summaries and
mandatory inactive/out-of-scope floors. Tests still execute supported cases and
check family/operation presence, active-case coverage, exact inactive reasons,
backlog coherence and provenance. Completing support no longer requires retaining
an artificial backlog. No replacement inventory framework was introduced.

## Validation

- Release corpus/branch-reset focus: 1,279 pass.
- Release CRLF/character/partial/cancellation focus: 465 pass after the final
  leading-filter refinement. Public coverage includes IsMatch, Match, Count,
  enumeration, detailed captures, replacement and destination replacement, with
  unlimited and all four finite invocation controls.
- Full affected PCRE2 suite: 6,126 pass in 7 seconds on the final source.
  Windows/Linux CI both passed for exact source `5aa1647b`:
  [run 39964724013](https://github.com/Lokad/Utf8Regex/actions/runs/39964724013).
  CI builds once per platform, then tests all four source fixtures using matching
  binaries. This is source validation, not detached package/consumer qualification.
- A benchmark/library closure builds Release tooling at `667d1725`. Matching
  binaries verify generated README/PythonRe/PCRE2 pages without snapshot writes.
  BDN `--list flat` is a script smoke check, not performance evidence.
- All 18 protected snapshot/frozen-RC1/original-TODO files retain exact bytes.
  PLAN.md and PLAN.HISTORY.md remain ignored. DLL/PDB identities are recorded for
  before, initial-after and final-after binaries.

## Bounded public source-cost check: closed

The reused dual-load-context host compares frozen producer `3dea7dec` with initial
`6e0f3183`, then `667d1725`. Release .NET 10.0.13 on Windows, processor 20;
2.5-second warmup per version/lane, 40 ms calibration target. Nine alternating
pairs were followed by the sole 17-pair reversed-first check. The initial noisy
captured-search result prompted passing the compiled Boolean directly instead of
the complete request through the hot candidate check. The later `5aa1647b`
compiler-only refinement preserves the matching loops and the selected lanes'
progression facts. Its construction cost was not remeasured; the construction
row is evidence for `667d1725`. No further timing follows.
All subjects have identical expected results before/after; timings do not compare
the old incorrect CRLF result against its correction. No concurrent local builds
or tests ran during measurement.

| Public operation | Initial change | Final check | Allocated bytes before/after |
| --- | ---: | ---: | ---: |
| PCRE2 CRLF character IsMatch, 32 KiB miss, unlimited | -8.28% | +5.14% | 0 / 0 |
| PCRE2 CRLF captured IsMatch, 32 KiB miss, four finite caps | +19.35% | -1.78% | 112 / 112 |
| PCRE2 LF class Count, 8 KiB, unlimited | +7.88% | +8.28% | 0 / 0 |
| PCRE2 CRLF word Count, four finite caps | -7.19% | -0.60% | 758,176 / 758,176 |
| PCRE2 CRLF capture construction plus tiny IsMatch | +19.04% | -12.54% | 5,240 / 5,304 |
| Unchanged Core Count, 1 MiB miss | +3.80% | +4.25% | 0 / 0 |

The final affected execution medians fit the +10% allowance; matching allocations
are unchanged. Construction adds 64 bytes for retained compile-request facts;
its timing includes construction and is not a matching-throughput result. Wall
times were 37.36/41.96 seconds. Final samples reach 6.27x spread, the unchanged
control drifts +4.25%, and the shortest actual sample is 30.22 ms. These limits
prevent precise causal speedup claims or individual-row certification. In
particular, the changed call shape and noise cannot be separated by this check.

This closes the bounded investigation. It does not establish fresh package
usability, a release-wide gain, or cumulative <=10% acceptance against fixed
correctness-equivalent `844dcbad`. Those actual-package/host and isolated-consumer
checks remain the release boundary; no intermediate product packaging, snapshot
refresh, exhaustive matrix or reopened checked-access campaign was performed.
