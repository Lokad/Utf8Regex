# Branch lowering for compound optional bodies

**Accepted**, sources `7521e81f` and `49b8f2b1`. Backtracking zero-or-one repeats
of compound bodies lower to existing Fork/Jump instructions. Greedy/lazy branch
order and normal capture/assertion rollback remain; these optionals no longer need
a repeat id or counter-journal mutations. NonBacktracking lowering and single-atom
repeats retain their existing paths, including constant-space terminal kernels.
There is no new opcode, runner, retained state, switch or unchecked access.

The actual imports Count program is owned scalar execution in both binaries.
Its optional `shared` clause now contributes a fork: 45 → 43 instructions and
five → four repeat ids. Existing major-buffer/depth admission, instruction work,
original synchronous cancellation/deadline and publication checks remain. Numeric
construction/work/working costs depend on the lowered program version, as recorded
in [the contract](../../../docs/SPEC.md). Captures, nullable completion, assertions,
RTL, strict whole-subject validation and retained cursor results are preserved.

One **84.2631404-second** public comparison covers 16 cases. Baseline Core is the
accepted `cc0bcb40`; candidate Core is `49b8f2b1`. Only Core DLL/PDB changes; the six
adapter/benchmark companions are byte-identical frozen dev.24 Full files. Normal
eight-processor startup precedes CPU2 affinity. Managed warmup is 1.5s/minimum 128
calls, diagnostic comparators 250ms/minimum 64. Each lane calibrates independently
to 60ms, followed by five alternating samples. All 160 owned samples exceed 20ms,
minimum **56.0014ms**. Complete Count/range results are consumed after timing.
No confirmation, variant, catalog refresh or package qualification follows.

| Public operation | Before us | After us | Change | Allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| ASCII control | 2.501 | 2.516 | +0.59% | 0 → 0 |
| Actual imports Count, ordinary | 14.495 | 14.350 | -0.99% | 3584 → 3568 |
| Actual imports Count, compiled | 14.452 | 14.195 | -1.78% | 3584 → 3568 |
| Actual imports Count, finite/live | 17.446 | 17.391 | -0.32% | 3696 → 3680 |
| Actual imports Count, deadline/live | 19.087 | 18.372 | -3.75% | 3744 → 3728 |
| Greedy optional Count | 63.160 | 63.111 | -0.08% | 0 → 0 |
| Lazy optional Count | 1348.620 | 1052.605 | -21.95% | 3552 → 1872 |
| Nullable optional Count | 130.986 | 131.697 | +0.54% | 0 → 0 |
| Optional inside repeated body, Count | 148.166 | 102.962 | -30.51% | 3552 → 3552 |
| Optional value-Match | 0.301 | 0.301 | +0.04% | 0 → 0 |
| Unicode optional Count | 702.941 | 516.747 | -26.49% | 3552 → 1872 |
| Scalar single-atom optional control | 45.160 | 45.189 | +0.06% | 0 → 0 |
| Literal single-atom optional control | 38.196 | 38.306 | +0.29% | 0 → 0 |
| NonBacktracking control | 160.905 | 163.872 | +1.84% | 133296 → 133296 |
| PythonRe optional Count | 141.097 | 141.006 | -0.06% | 0 → 0 |
| PCRE2 optional Count | 101.670 | 100.608 | -1.04% | 0 → 0 |

[All costs and identities](cost-summary.json). The three large gains have tight
sample ranges; actual imports moves only slightly. Keep the NonBacktracking cost
without asserting a compiler cause: its lowering is unchanged. This supports a
small general simplification with useful public gains, not a closed imports gap,
new parity ratio or cumulative release claim. The user's local-cost/major-gain
policy applies; the old checked-access 10% gate is not a universal release rule.

Before timing, single-atom bodies were excluded to preserve their fused paths.
Focused final Release tests pass on Windows/Linux, .NET10/11: Core 223,
PythonRe 914 and PCRE2 765 per target. Earlier broader checks covered Core 11598,
PythonRe 1549 and PCRE2 1023 per target. Windows initially exposed two depth fixtures
whose 8192 optional nodes no longer allocated counters; they now use `{0,2}` to
retain their original pre-allocation-denial purpose, and the corrected tests pass.
The initial new tests also had cursor/enum API spelling errors, corrected before
these runs. Logs retain both failures. The final eligibility narrowing was then
checked separately, without repeating unchanged package/Full matrices.

Coverage includes ordinary/compiled/RTL operations, nullable/nested captures,
backreferences, conditions, atomic/balancing scopes, assertions, scalar boundaries,
borrowed retained results, every work cutoff with parent-loan cleanup, original
interruption and malformed tails. Single-atom optionals still Count with 1B working.
The initial final preflight used astral nullable controls whose .NET code-unit
empty progression differs from scalar progression. Only those two comparator
subjects changed to BMP before measurement; their failed preflight is preserved.

Immutable evidence under artifacts/experiments/optional-repeat-branch-20261104/:

- evidence.tar.gz: `F0BE611989AFCE13C6E48B2D69F1B3CB9236641401AC046E3CD14D40D5585BAA`.
- windows-correctness.zip: `C2EE884CD49D65680964BF9DA9F2F4496E98D6D1EA03F7F5052534961D46E9ED`.

Raw evidence includes both frozen graphs, driver/binaries/corpus, stage-one
preflights/source identities, the final source patch, all samples and Linux logs.
Published/staged dev.24 bytes and its source 00a97561 Full catalog remain unchanged.
