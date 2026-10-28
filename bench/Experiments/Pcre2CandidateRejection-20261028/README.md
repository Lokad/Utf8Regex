# Share necessary rejection with PCRE2 IsMatch

3b961b73 lets scalar PCRE2 IsMatch borrow the admitted candidate-rejection check
already used by Count. One profile-local helper owns the unchanged necessary
leading-set/run-literal proof. It rents no VM scratch on an impossible suffix;
all possible candidates still run ordinary scalar verification. Non-None match
options, empty languages and RTL retain their existing fences. No preparation,
matcher, execution mode or unchecked memory operation was added.

[Raw comparison](comparison.json), [console](comparison.txt),
[driver](compare.cs.txt), [project](compare.csproj.txt),
[five-sample row refresh](refresh.txt), [native paired comparison](qualification.txt),
[generated-page verification](verify.txt).

Same quiet VM, .NET 11 RC3, CPU2, VM-built frozen a40bda03 versus 3b961b73.
The earlier Core changes do not serve this PCRE2 slice operation; the preceding
[required-literal experiment](../RequiredLiteralMiss-20261028/README.md) retains
its actual no-improvement evidence and disproved Core-delegation hypothesis.
Four public-operation lanes use independent calibration, 250 ms warmup, three
alternating samples and approximately 50 ms batches. Results agree with .NET
before and during timing. One comparison completed in **9.84 seconds**, within
its 120-second cap. No confirmation or favorable rerun.

| Public PCRE2 IsMatch | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| One-node miss, 52 bytes | 35.199 | 0.168 | 209.1x faster | 3552 / 0 |
| Finite limits and original live token | 38.371 | 0.237 | 162.1x faster | 3664 / 112 |
| Literal present but no match | 37.564 | 37.474 | -0.2% elapsed | 3552 / 3552 |
| Mixed-scalar Unicode hit | 1.168 | 1.244 | +6.6% elapsed | 3552 / 3552 |
| Unchanged English literal | 30.555 | 30.298 | -0.8% elapsed | 0 / 0 |

The hit overhead is within the allowed 10% public-cost gate. These are public
operation costs, not private-kernel estimates. The benchmark-only delegate thunk
binds each loaded assembly's enum outside timing; the finite timed action calls
the actual public overload with its live token, start zero and None options.

The separate native paired row reports 0.164 us managed / 0.076 us native,
**2.15x**, against the previous 432.70x estimate. Its verdict is **Inconclusive**:
lane-order medians differ by 3.03%, above the existing gate. Retain that verdict,
raw samples and nine-pair interval 2.05-2.21x; no retry merely for classification.
The five-sample .NET row is independently measured: 0.188 us / 0.619 us
predecoded. Neither is substituted for the paired before/after costs above.

Focused Release checks pass on [.NET 10](pcre-net10.txt) and
[.NET 11](pcre-net11.txt): actual routes, byte offsets/scalar tails, false
candidates, nullable/option exclusions, strict validation, cumulative work,
heap admission before live attempts, original-token cancellation/deadlines and
retained captures. Existing Count shares the same check with unchanged semantics.

Measured DLLs/PDBs, driver, raw results and exact generated snapshots are retained
in the [local immutable archive](../../../artifacts/experiments/pcre2-candidate-rejection-20261028/parity-pcre-3b961b73.tar.gz),
SHA256 `0A7C96E2147D1E0B481C10B354EB4C4AF53FECB2FADA6988DC467FE4CA564395`.
Only the affected catalog case was refreshed; no package or complete catalog
qualification was repeated.
