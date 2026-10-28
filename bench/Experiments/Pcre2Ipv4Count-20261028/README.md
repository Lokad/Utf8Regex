# Reuse proved IPv4 counting under the original invocation

bcfc318c retains one complete-language fact in the existing PCRE2 slice program,
without retaining a second Core regex/root. Default-option, uncaptured Count
reuses the existing checked token finder. Captures and explicit match options
retain their established executors; Match, enumeration and result lifetimes are
unchanged. The language has two/three-digit octets and no word boundaries; this
does not specialize arbitrary IPv4 syntax.

A shared neutral counter also serves finite/token Core Count. Each admitted
window is at most 2,048 bytes, including 14 bytes of lookahead to finish every
crossing token. The existing finder chooses earliest ordered matches. All calls
meter bounded work, including unlimited PCRE2 calls; no separate invocation or
deadline starts. Original validation, token/deadline, sticky work denial and
publication remain. The existing unlimited Core fast path is untouched.

The counter needs no VM scratch or result payload. One-byte heap/working/result
and depth-one policies can therefore admit its hits; meaningful work remains
bounded. This intentional route-cost change is documented by the existing
route-dependent resource contract, clarified in SPEC-PCRE2.md. It is not a
process-memory guarantee or removal of general admission.

[Raw comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; immediately prior
75ef5759 versus bcfc318c. Four public Count lanes, 500 ms warmup, three alternating
approximately 50 ms samples, equal-result preflight and timed checks.
One comparison took **29.56 seconds**, within its 120-second cap; no confirmation.
a40bda03 remains the fixed cumulative release baseline.

| Public Count | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| PCRE2 full corpus, 6,839,410 bytes / 5 results | 1080124.000 | 4756.782 | 227.1x faster | 3568 / 0 |
| PCRE2 32 KiB, finite limits and live token | 5929.800 | 8.159 | 726.8x faster | 3680 / 112 |
| Dense mixed-scalar hits | 104.626 | 4.544 | 23.0x faster | 3568 / 0 |
| Dense dot miss, 4 KiB | 694.436 | 43.751 | 15.9x faster | 3568 / 0 |
| Captured scalar control | 110.192 | 111.771 | +1.4% elapsed | 5760 / 5760 |
| Unchanged PCRE2 literal Count | 210.194 | 206.157 | -1.9% elapsed | 0 / 0 |
| Unchanged unlimited Core fast path | 2651.147 | 2654.168 | +0.1% elapsed | 0 / 0 |
| Core 32 KiB, finite limits and live token | 5932.644 | 6.775 | 875.7x faster | 3680 / 112 |

The [five-sample catalog refresh](pcre-ipv4-refresh.txt) and independent
[nine-pair native comparison](pcre-ipv4-qualification.txt) replace only this
Count row. Native qualification uses CPU3 (mask 0x8), chosen by the existing
Linux processor selector, with both lanes on the same processor. It reports
**49.58x -> 0.221x native PCRE2**, interval **0.219-0.223**, ManagedFaster;
managed 4835.688 us, native 21857.550 us. This is distinct from the CPU2
before/after experiment. The paired allocation probe reports zero; the separate
catalog allocation probe reports 2 B/op. Preserve both measurements.
[Generated-page verification](pcre-ipv4-verify.txt) and
[qualification consistency](pcre-ipv4-consistency.txt) pass. Other rows and
lifecycle/scaling evidence retain their identities.

[Focused both-runtime PCRE2 checks](pcre-focused.txt), [complete both-runtime
PCRE2 checks](pcre-both.txt), [Core](core.txt) and affected
[PythonRe](python-net11.txt) pass. Coverage includes aliases, every crossing
position/tail, scalar byte floors, original global progression, captured/option
exclusions, complete invalid-tail rejection, cumulative work cutoffs, retained
captures, bounded dense-dot/no-dot interruption and final-poll cancellation.
No API, grammar, unchecked read, native shipped dependency or new engine.

Exact baseline/candidate DLLs/PDBs, driver, raw results and generated snapshots
are retained in the
[immutable archive](../../../artifacts/experiments/pcre2-ipv4-count-20261028/parity-pcre-ipv4-bcfc318c.tar.gz),
SHA256 `62F70B91B29593A0F28F5FDC6635E4021098325D2817294D079A5C0FB9991D56`.
No complete catalog or package matrix was repeated.
