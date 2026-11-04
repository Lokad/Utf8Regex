# Reuse necessary ASCII starts for first-match search

**Accepted**, source `078cbba9`. The shared first-match runner borrows Count's
existing bounded leading-ASCII proof and seeker. A mandatory exact byte/range and
optional mandatory neighbour reject impossible starts; every survivor still runs
the original VM. No search metadata, overload, engine, switch or unchecked API is
added. Initial beginning-anchor discovery, NonBacktracking, RTL, nullable,
contiguous-anchor and folded/non-ASCII heads retain their existing paths.

Controlled gap searches cover at most 2048 bytes per checkpoint. Rejected
neighbours consume their own step, exhaustion checks original interruption, and
zero-byte working admission observes enclosing loans before avoiding local
scratch. Captures, rollback, offsets, retained cursor aliases, replacement and
PCRE2 byte-range callers keep their semantics. Work totals can decrease by
skipping failed VM attempts; [the contract](../../../docs/SPEC.md) records this.

The preceding public control exposed a 281.61us Core value-Match on 4105 bytes.
An untimed probe confirms owned scalar execution with no native/direct family,
match 4096+9, and the existing a/b start proof. The old first-match loop attempted
each scalar start. This is separate from the closed PCRE2 date and prefix pilots.

One **100.1055239-second** public comparison uses accepted `bde712e7` Core versus
`078cbba9`; six adapter/benchmark companions remain byte-identical dev.24 Full
files. Normal eight-processor startup precedes CPU2 affinity, with 1.5s/minimum 128
managed and 250ms/minimum 64 diagnostic warmup, independent 60ms calibration and five
alternating samples. All 190 owned samples qualify, minimum **42.1063ms**. The
typed bridge introduces no boxing or additional result allocation. All numeric
and complete string/tuple outputs are consumed after timing. BCL projected lanes
are diagnostics, not a new catalog/CPython ratio or cumulative release claim.

| Public operation | Before us | After us | Change | Allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Native ASCII control | 2.517 | 2.521 | +0.19% | 0 → 0 |
| Actual prefix Match | 259.234 | 0.600 | -99.77% | 2256 → 2256 |
| Actual finite/live Match | 262.298 | 0.887 | -99.66% | 2368 → 2368 |
| Actual deadline/live Match | 302.644 | 1.134 | -99.63% | 2416 → 2416 |
| Early prefix Match | 0.521 | 0.503 | -3.40% | 2256 → 2256 |
| Dense rejected-neighbour Match | 280.492 | 32.939 | -88.26% | 2256 → 2256 |
| Dense false-prefix miss | 151.576 | 35.102 | -76.84% | 2256 → 2256 |
| Missing-head Match | 257.590 | 0.224 | -99.91% | 128 → 64 |
| Positive-range Match | 479.498 | 0.958 | -99.80% | 2256 → 2256 |
| Unicode-offset Match | 129.984 | 0.748 | -99.42% | 2256 → 2256 |
| Branch-root control | 380.901 | 430.489 | +13.02% | 4128 → 4128 |
| Folded-root control | 295.726 | 322.151 | +8.94% | 2256 → 2256 |
| RTL control | 433.299 | 445.605 | +2.84% | 5744 → 5744 |
| Nullable-root control | 0.592 | 0.584 | -1.36% | 4064 → 4064 |
| Contiguous-root control | 264.830 | 298.892 | +12.86% | 128 → 128 |
| NonBacktracking control | 171.700 | 176.802 | +2.97% | 1416 → 1416 |
| Python FindAll control | 474.121 | 439.347 | -7.33% | 112944 → 112944 |
| PCRE2 Count control | 75.471 | 67.897 | -10.04% | 2256 → 2256 |
| Non-ASCII-root control | 277.313 | 305.863 | +10.30% | 2256 → 2256 |

[Full costs, spreads and identities](cost-summary.json). The branch/contiguous/
non-ASCII regressions are demonstrated public costs, with tight sample ranges.
Keep them explicitly: approximately 50/34/29us versus 259–479us saved on the primary
and range cases. Folded +8.94%, RTL +2.84% and NonBacktracking +2.97% also remain.
These excluded roots do not use the new seeker; no compiler attribution is asserted.
Unchanged-route improvements receive no causal credit. The user's local-cost/
major-gain policy supports this trade-off; the old checked-access 10% allowance is
not a universal gate. No confirmation, tuning variant or favorable catalog refresh.

Focused Release checks pass on Windows/Linux, .NET10/11: Core 1586,
PythonRe 1274 and PCRE2 815 per target. Coverage includes public value/detailed
matching, offsets, replacement, cursors/aliases, branch/capture/backreference
rollback, malformed tails, every work cutoff, enclosing loans, dense false-head
interruption and final-miss original token/deadline checks. A PCRE2 cursor witness
asserts its actual shared-slice route. The initial 75-test run passed but used a
2KiB subject in an all-offset helper; that redundant case was removed before the
final focused builds, retaining separate boundary/slice witnesses. No test failure.

Canonical evidence under artifacts/experiments/core-first-candidate-20261104/:

- evidence.tar.gz: `D2ADED4DB86C502117E2BBE04076A63174C3859A58CC1CD2E83F86DB4FBF1CC3`.
- windows-correctness.zip: `5176F07F3679C158B61C259E8BF59358043B86C3669C2D727137C4C7685C3A48`.

Raw evidence retains both graphs, driver/binaries/corpus, untimed probe/source,
predeclaration, all samples, preflights, source patch and Linux logs. Staged package
bytes and the source 00a97561 Full catalog remain immutable; no package/Full matrix
was repeated for this pilot.
