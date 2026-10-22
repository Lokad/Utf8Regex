# PCRE2 replacement aliasing and preparation reuse

2026-10-22. Bounded implementation of the twenty-third review. Development is
0.4.0-rc.2-dev; this is source evidence, not a new qualified NuGet candidate.
Source commits: 33f9e899 (overlap), 3e6bab79 (preparation reuse). Final producer:
3e6bab79871e31aef4ed5326cb082dad49ee5451. Before binaries were frozen from matching
07780283fba3d24ec4d7e791641dd76420c80457 before edits/builds; six DLL/PDB hashes
per version are recorded. No frozen package, snapshot or original handoff changed.

## Correct overlapping output

PCRE2 TryReplace previously returned Done with wrong bytes, including malformed
UTF-8, when an early output fragment overwrote input needed by a later fragment.
The reviewed 48 public witnesses had 40 wrong outputs, including 20 malformed
ones. The unchanged witness now has zero wrong/malformed outputs; all 48 Core
comparators and ten disjoint/template-alias/short-destination controls also pass.
The witness source and both raw results are included.

The existing destination worker now stages successful matched overlaps through
Utf8InvocationOutput, retaining admission through the bounded final copy. It
removes unconditional direct fragment emission on aliased input. Shared memmove,
scalar dispatch and disjoint output algorithms are unchanged. No-match copies
remain direct; short destinations return before staging/writing. No new owner,
engine, copy framework or compatibility mode was added.

Intentional resource contract: this temporary output consumes working and
cumulative result capacity under the same invocation. An overlapping operation
can therefore require more admission than the disjoint operation. A public test
proves disjoint success, overlapping HeapLimit before writes, and fresh success
under the same 4 KiB working policy. Package README/XML and release notes disclose
this. Original cancellation/deadline authority and partial-copy interruption
rules remain; borrowed input must otherwise remain unchanged during a call.

New public tests cover literal, class, repeat and captured routes; coincident/
forward/backward overlap; expansion/contraction; whole-match repetition; gaps;
supplementary tails; exact/zero/short capacity; overflow-length/replacement-only;
three-way input/template/destination alias; finite policy and live/no token.
Four regression rows failed before the fix. The focused post-fix tests pass.

## Reuse the complete preparation

A declined simple selector already compiled a complete Prepared outcome. Its
Simple-only getter discarded that result, so an oversized uncached template was
compiled again by the executor (and destination fallback could acquire it again).
The existing nullable preparation now travels through that one operation. The
Simple-only forwarding getter is deleted. Existing lazy no-match/error handling,
option handling, bounded cache and foreign-waiter recovery remain; no cache-size,
compiler, accounting model or persistent carrier was added.

Untimed existing construction diagnostics for `(a)`, subject `a`, and `$1` plus
1 MiB literal show public Replace work falling from 29,360,328 to 14,680,164;
the latter equals one private preparation. Modeled retained construction capacity
falls from 4,196,672 to 2,098,336 bytes and peak from 12,585,744 to 10,487,408.
The cache intentionally retains zero entries in both versions. These are modeled
construction counters/capacities, not timings or exact heap measurements.

Public bytes, text, UTF-8-template and destination tests succeed with a bound
below two compilation allowances, under unlimited and all four finite controls.
An insufficient construction allowance still denies and a fresh call recovers.
Option and existing lazy/error/isolation tests pass. Numeric work costs can fall
when duplicate work is removed; they are not a stable per-template quota.

## Bounded operating-cost evidence

One identical Release driver loads frozen before/after DLLs into separate assembly
contexts in one process, validates full outputs before timing, retains outputs,
and pins one logical processor. Runtime .NET 10.0.13, Windows, default tiering.
Nine alternating pairs followed by the sole reversed-first 17-pair confirmation;
40.47 s and 47.26 s respectively. Same six public lanes, 2.5 s warmup per version/
lane, shared unchanged Core Count control, raw allocation/sample records included.
Calibration starts at one operation to keep the large-template experiment bounded.

| Public operation | Initial change | Confirmation | Bytes/op, before -> after |
| --- | ---: | ---: | ---: |
| Disjoint simple destination | -2.45% | +9.02% | 32 -> 32 |
| Same, all four finite limits | +5.89% | +5.30% | 296 -> 296 |
| Warm captured bytes | -7.75% | -9.15% | 408 -> 408 |
| Warm captured UTF-8 destination | +10.59% | -11.39% | 440 -> 440 |
| Uncached 1 MiB template | -48.80% | -46.12% | 23,072,816 -> 12,585,128 |
| Unchanged Core Count control | -5.68% | -5.60% | 0 -> 0 |

Decision: retain the bounded correctness/reuse fixes. Confirmation medians fit
the +10% source allowance, but wide spreads (up to 4.35x), control drift and actual
samples below the 40 ms calibration target (minimum 16.63/29.60 ms) prevent precise
equivalence or release-wide/cumulative acceptance. The large-template work and
allocation reduction has independent evidence; do not generalize its gain to all
templates. The old overlapping operation is incorrect and is excluded as a timing
baseline. No third run, VM campaign or reopened checked-access investigation.

## Validation and remaining delivery work

Focused Release replacement/preparation/error/isolation checks pass; full PCRE2
passes (6,040) and Core public API/shared shipped-member inventory passes (13).
One final affected benchmark/library closure builds friend tooling. Existing
generated README parity/PythonRe/PCRE2 page checks and full producer/PE/PDB/source
preflight pass. See validation.json, compiled-documents.json and ci.json.

PLAN.md/history remain ignored; immutable-files.json verifies all 18 protected
inputs. Package/configuration/platform matrices were not repeated locally for
these small fixes. All eight Windows/Linux CI steps, including Test Release and
Complete job, report success; GitHub still reports run/job/check aggregation in
progress. ci.json preserves both observations without claiming aggregate green.
A fresh common
version package candidate, isolated consumers and actual-package finite/cumulative
cost qualification remain the release gates. Completed older campaigns stay closed.
