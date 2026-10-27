# One callback writer and typed PCRE2 probing roots

2026-10-22. Review checkout `f9f26694`, matching producer `5ee10614`.
Accepted source: `3b101d5e` (writer) and `66180b1c` (PCRE2 metadata).
Development remains `0.4.0-rc.2-dev`; no new package is qualified or published.
Four snapshots, nine frozen unpublished RC1 files and five original TODOs retain
their exact bytes. PLAN/history remain ignored.

## Delivered changes and contract

- Removed the callback writer's ArrayBufferWriter field, internal constructor,
  internal legacy reader and four fallback Append bodies. Core/PCRE2 callbacks
  use the existing owned scope and buffer. The test compares production output
  with explicit expected bytes rather than maintaining a second backend oracle.
  Actual Unsafe.SizeOf changes 16 -> 8 bytes; per-call callback allocations and
  conservative output-control charges remain unchanged in the selected lanes.
- **Intentional boundary change:** valid Append calls outside a replacement
  callback now throw InvalidOperationException, including empty byte/character
  fragments. AppendAsciiByte still rejects non-ASCII arguments first. Previous
  standalone accumulation had no public reader. Default/reset writers within
  callbacks join the original invocation; initialized aliases, reset semantics,
  discarded-buffer reclamation and nested restoration remain valid.
- Removed the unread PCRE2 syntax-kind interface/enum and stateless literal and
  character markers. Those compilers now return the real programs without
  tag-bearing outcome objects. Backtracking construction also passes the root
  directly, retaining its real outcome flags and branch-reset metadata.
- The compiled PCRE2 owner retains an optional typed ProbeRoot only for curated
  partial probing. Removed its backtracking wrapper. Actual partial-probe reads,
  operation selection, captures/names, match options, lowering, major-buffer
  admission and expansion/depth/interruption checks are preserved. Coarse
  construction charges are not refined to account for the removed helpers.
- Removed marker-existence assertions and the marker-rooted retention traversal.
  Existing semantic/budget tests remain; direct owner checks cover absent roots
  for literal/character/unrelated programs and a required root for partial probing.
- The host guide now presents public controls/factories before construction
  details. Removed retired-layout and wrapper chronology, preserving its original
  prose in ignored history and prior receipts. Actual scope exclusions, host
  envelope, lifetimes and useful admission guarantees remain. XML, SPEC and
  release notes document the writer boundary. No replacement framework or switch.

## One bounded comparison and its sole confirmation

Both versions use the identical Release driver bytes in separate load contexts
within one process, .NET 10.0.13/default tiers on Windows, pinned logical CPU 20.
Public callback outputs are checked byte-for-byte before timing; result lanes
retain the last output and traverse the same length/first-byte checksum. Finite
lanes enable all four controls together. No private-kernel costs are presented.

The initial 34.19-second pair (750 ms warmup per version/lane, nine pairs) is
inconclusive: several callback medians exceed 10%, with large outliers and
changing controls. The sole confirmation uses the same workloads/driver/product
bytes, 2.5-second warmup, reversed first/warm order and 17 alternating pairs.
It completes in 88.96 seconds. Raw samples remain in both JSON files.

Confirmation public costs, including validation and required output/projection:

| Operation | Before ns | After ns | Median change | Before/after B/op |
| --- | ---: | ---: | ---: | ---: |
| Core callback, mixed Append overloads | 2,300.75 | 2,391.99 | +3.97% | 11,504 / 11,504 |
| Core callback, four finite limits | 3,154.12 | 3,324.45 | +5.40% | 11,616 / 11,616 |
| Core callback, RTL | 3,769.34 | 3,600.81 | -4.47% | 13,464 / 13,464 |
| Core callback, resets/aliases | 2,445.52 | 2,600.73 | +6.35% | 11,784 / 11,784 |
| PCRE2 callback, mixed Append overloads | 3,872.40 | 4,063.99 | +4.95% | 19,104 / 19,104 |
| PCRE2 callback, four finite limits | 4,691.67 | 4,665.96 | -0.55% | 19,104 / 19,104 |
| PCRE2 callback, resets/aliases | 3,939.16 | 4,098.23 | +4.04% | 19,384 / 19,384 |
| PCRE2 partial probe, retained root | 140.98 | 143.14 | +1.54% | 0 / 0 |
| Unchanged Core Count miss, 128 KiB | 8,489.96 | 8,211.17 | -3.28% | 0 / 0 |
| Unchanged PCRE2 literal Match | 115.71 | 125.37 | +8.35% | 0 / 0 |

Selected affected execution medians fit the 10% allowance. Controls and spreads
remain noisy (up to 4.04x); the shortest observed confirmation sample is 16.06 ms,
below its calibrated 40 ms. These are bounded median observations, not precise
equivalence, causal gains or a cumulative/package-wide qualification. Accept
these small deletions and stop timing; no third comparison or VM campaign.

Public construction is separate:

| Constructor pattern | Before ns | After ns | Median change | Before/after B/op |
| --- | ---: | ---: | ---: | ---: |
| abc, direct literal | 534.10 | 506.40 | -5.19% | 1,728 / 1,696 |
| .{2}, repeat/backtracking compilation | 38,499.60 | 41,279.41 | +7.22% | 35,992 / 35,968 |
| (a)b+, captured backtracking compilation | 63,950.77 | 61,428.32 | -3.94% | 51,184 / 51,160 |

The raw driver mistakenly labels .{2} as `Construction/Pcre2-character`.
`route-evidence.json` corrects this: it selects Pcre2SingleTokenRepeatDirectProgram
through backtracking compilation. Direct `.` selects Pcre2CharacterDirectProgram;
no direct-character construction timing is claimed. The original label/results
remain intact; no extra timing was used to repair the coverage description.

## Correctness, provenance and remaining delivery gates

Focused Release: Core writer/evaluator 131, PCRE2 construction/evaluator 217,
partial-probe/branch-reset/retention 1,759. Full local Core 39,604 (102 s) and
PCRE2 6,007 (15 s) pass without failures/skips. Source
Windows/Linux CI (run 39788061003)
is green at `66180b1c68c4c27af0d1371e57b290d033936011` across all four fixture
projects, including unchanged PythonRe and Diff suites.

Affected projects build incrementally during edits; matching binaries are reused.
One final benchmark/library closure supplies existing producer/PE/PDB/compiled-
source and generated-page/parity checks, all passing. No package matrix, build-
server shutdown, new qualification framework or unchecked-access campaign.

The next release still needs fresh immutable common-version packages with exact
Core pins, final downstream/package acceptance and actual-package/host finite-
limit/cumulative costs. The fixed `844dcbad` cumulative baseline remains intact.
This focused pair does not replace that gate. Broader PCRE2 completion must not
block scoped Core/PythonRe delivery; rejected layout/Count trials stay closed.

`validation.json`, `ci.json`, `compiled-documents.json`, `immutable-files.json`,
before/after identities, route evidence, original driver/host source and raw cost
samples preserve the evidence. Published/staged immutable bytes are unchanged.
