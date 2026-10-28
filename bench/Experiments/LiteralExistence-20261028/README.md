# Complete suffix existence inside owned IsMatch

97b3f20e reuses the admitted dot-star/exact-ASCII-suffix proof inside the existing
owned IsMatch invocation. Dot-star can consume nothing, so IsMatch is exactly
the complete suffix's existence. Its prepared required literal must span that
whole suffix; a shorter fragment from separate captures remains insufficient.
No new payload, preparation or top-level dispatch was added.

Strict whole-subject validation precedes bounded search from the requested byte
boundary. The original work/depth/working/result authority, token/deadline and
final publication check remain. RTL, NB and folding keep their existing routes.
Count, Match and captures are not reduced to literal matching. A Boolean call
can now succeed with a one-byte working cap because it needs no VM scratch;
it still pays validation/search/publication work. Numerical costs remain
route/version dependent as documented in SPEC.md.

[Raw comparison](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt),
[five-sample README refresh](refresh.txt), [verification](verify.txt).
Same quiet VM, .NET 11 RC3, CPU2. **Incremental baseline 0a4072d9**, candidate
97b3f20e: the prior engine already contains the earlier required-literal miss
rejection. Do not misattribute that miss improvement to this new success proof.
a40bda03 remains the original cumulative release baseline.

Four independent public IsMatch lanes, 250 ms warmup and three alternating
approximately 50 ms samples. Equal-result preflight and timed results pass.
One comparison completed in **11.59 seconds**, within its 120-second cap;
no confirmation or favorable rerun.

| Public IsMatch | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Common backtracking hit, ordinary | 4.471 | 0.186 | 24.0x faster | 5744 / 0 |
| Compiled | 4.467 | 0.186 | 24.0x faster | 5744 / 0 |
| Finite limits and live token | 4.861 | 0.215 | 22.6x faster | 5856 / 112 |
| 4 KiB miss, already screened by prior engine | 0.717 | 0.654 | -8.7% elapsed | 0 / 0 |
| 4 KiB mixed-scalar hit | 115.657 | 0.668 | 173.1x faster | 175048 / 0 |
| Unchanged English literal IsMatch | 36.017 | 36.623 | +1.7% elapsed | 0 / 0 |

The independent five-sample README row reports **4.34x compiled** against
predecoded .NET (was 99.71x), **0.22x ordinary**, with 0.190/0.185 us costs.
Those costs and verdicts remain separate from the paired comparison above.

Focused Release checks pass on [Core .NET 10](core-net10.txt) and
[Core .NET 11](core-net11.txt): partial captured suffixes, newline/Unicode context,
offsets and malformed tails, unchanged Count/Match ranges, exact work admission,
original-token cancellation during search and from the final observed deadline
poll, deadlines and retained captures. Affected [PythonRe](python-net11.txt),
[Boolean/offset cases](python-boolean-net11.txt), [PCRE2](pcre-net11.txt) and
[Boolean/probe cases](pcre-boolean-net11.txt) pass. No grammar/API contract change.

Exact candidate DLLs/PDBs, driver, raw results and generated snapshots are in the
[local immutable archive](../../../artifacts/experiments/literal-existence-20261028/parity-literal-existence-97b3f20e.tar.gz),
SHA256 `9FED51F52C6C8DF54D84A4DC1EF8B1144282D6E176FE7AE3EFCE1533A6729727`.
The incremental baseline's exact bytes remain in the
[preceding archive](../../../artifacts/experiments/nb-rows-20261028/parity-nb-row-0a4072d9.tar.gz).
No complete catalog or package matrix was repeated.
