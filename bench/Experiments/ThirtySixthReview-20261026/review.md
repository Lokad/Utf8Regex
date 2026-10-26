# Thirty-sixth review — 2026-10-26

Checkout `9459ebc1e64686768f10f711e22bcb5bef2d57fb`. Shipped Release products
remain source `7650e7a5`, development `0.4.0-rc.2-dev`; the completed tooling
milestone has source `2c745703`. Source, PLAN/history and protected artifacts
are unchanged. No new incorrect matching result is demonstrated in this pass.

The useful follow-up is concentrated in PCRE2 replacement preparation: redundant
cache variants, a surprisingly expensive allocation path and a forwarding owner.
This does not justify reopening general cache/accounting or checked-access work.

## 1. Execution flags fragment identical replacement preparation (P3)

`Utf8Pcre2Regex.cs:1776` keys the replacement cache by template text and the
supplied substitution flags. Call sites strip different subsets of flags. The
prepared parser itself observes only Extended. UnsetEmpty and UnknownUnset
instead govern resolution while executing the already-prepared instructions.

Public witness: compile `(a)`, then replace subject `a` using `$1` under None,
UnsetEmpty, UnknownUnset and their combination. Every operation returns `a`.
The public calls leave 1, 2, 3 and 4 cache entries respectively. All four prepared
programs have the same structural SHA256, but four distinct instruction arrays.
Reflection is used after each public operation to inspect the preparation; the
recorded cache count is identical before and after reflection. No extra private
call is responsible for the duplication. The cache has only 16 entry slots.

This is not a semantic error and cache hits/particular budgets are not guaranteed
API behavior. It is avoidable repeated preparation/retention from an identity
that includes execution state. No retained-graph collector or precision change
is needed to establish it.

Recommendation: investigate one explicit preparation identity, with option
handling in one place, and share genuinely identical prepared variants. Preserve
execution-time unset/unknown behavior, no-match laziness, cancellation/deadlines,
retryable preparation and major construction/working/result admission. Do not
blindly mask every execution flag: these flags also influence current simple-plan
eligibility, as item 2 shows. Avoid adding a second cache or representation mode
merely to make the identity look precise.

Focused future acceptance: existing replacement/finite/cancellation tests plus
this public reuse witness, including Extended and error-producing references.
Use one bounded public before/after cost comparison for any changed live route;
package qualification stays at the delivery boundary.

## 2. The specialized simple replacement path needs evidence, not its name (P2 cost)

`Pcre2SimpleReplacementParser.cs:23` declines every non-None options value. For
the same public identity replacement, this sends otherwise harmless unset flags
to the prepared streaming path. The None route uses the simple literal driver,
which first collects match ranges in a ledger before emitting output.

One warmed public allocation observation, literal pattern `a`, 1,024-byte subject
of `a`, replacement `$0` (all output bytes independently checked equal input):

| Invocation policy | None, simple plan | UnsetEmpty, prepared plan |
| --- | ---: | ---: |
| Unlimited | 13,384 B | 2,264 B |
| All four finite controls | 17,584 B | 2,264 B |

UnknownUnset and the combined unset flags also select prepared plans and allocate
2,264 B. These are eight observations after five warm calls per lane, using the
same current library bytes. Preparation/compilation is outside the measured
public call. This is allocation evidence, not timing, a before/after product
comparison or a universal ranking. The streaming path may cost more CPU; that
has not been measured here. Do not remove a measured native fast path or promote
all variants to the simple representation on this evidence alone.

Recommendation: one short public comparison of the existing two strategies on
representative small/dense replacement workloads, preserving output semantics,
strict validation and all interruption/admission controls. If the range ledger
does not earn its complexity on its actual consumers, consolidate the routes;
otherwise retain it with a concrete justification and avoid increasing allocation
through an overbroad cache-key change. No new mode or benchmark campaign.

## 3. Pcre2ReplacementComponent is an allocated forwarding wrapper (P3)

`Utf8Pcre2Regex.cs:1803` adds a private object holding one
Utf8BoundedPreparationCache. Its Count and GetOrAdd forward to that cache. Create
creates the real cache, then allocates/admission-charges the wrapper. Every regex
constructor eagerly creates it, including expressions only used for IsMatch.

There is no separate public API, lifetime rule, synchronization or eviction
policy in this wrapper. Reference tracing finds only the owning facade's field,
constructor, GetReplacementPreparation and debug count accessor. The existing
cache already owns the relevant policy and concurrency behavior.

Recommendation: consider storing that existing cache directly and removing the
wrapper, its forwarding methods and its wrapper-only construction accounting.
Retain the real cache's capacity, conservative retained charges, failed-preparer
handling and admission before major allocations. Fewer actual allocations can
legitimately lower versioned construction charges; do not replace the deleted
helper-object charge with another descriptor or fixed estimate. This is a small
local simplification, not a new lazy-owner or retention redesign.

## Checks and boundaries

- Independent .NET comparison is limited to deliberate ASCII overlap: 39 patterns,
  54 subjects, both directions, unlimited/all-finite policies. All 8,424 controls
  agree on first match, Count, groups, sparse numbers, repeated capture histories,
  lookarounds, atomic groups, balancing groups and conditional references. No
  construction rejection or comparison issue. This does not qualify Unicode
  code-unit semantics, all grammar, packages or operating cost.
- Python case/class/identifier data have an owned generator with explicit CPython
  and Unicode pins. Grapheme data identify their pinned inputs/hashes and license;
  regeneration evidence remains in the existing historical receipt. No new data
  provenance or generator framework is justified by these inspected paths.
- UTF-8 replacement/destination overlap has explicit staging/borrowing handling;
  durable cursor aliases have explicit lifetime/concurrency contracts. No new
  defect was reproduced there. The completed wrapper/ledger cleanup and closed
  matching/accounting/checked-access campaigns are not reopened.
- Fresh immutable packages/downstream consumers and actual-package cumulative
  performance remain the existing delivery gates, not new review findings.

`cache-witness.json` and its isolated driver preserve cache/representation and
allocation observations; `overlap-witness.json` and its driver preserve the ASCII
comparison. Only scratch projects referenced existing Release libraries. No
shipped rebuild, full suite, engine throughput campaign, CI API request, package/
snapshot refresh, VM access, external repository edit or PLAN change occurred.
Integrity manifests verify unchanged plans, ten library DLL/PDB copies and all
18 protected historical snapshot/RC1/original-handoff files.
