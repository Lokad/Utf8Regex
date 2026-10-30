# Python scalar misses before workspace acquisition

Producer **157492e6f183696e27f21cdb90c26f65353f40b2**, baseline production
**e658c1e100d7a03b34596d2f1088772c10fcf3cf**. One bounded public A/B, then
one official-CPython qualification per diagnosed catalog row. No confirmation.

The existing Core rendering already retains a proved mandatory exact literal for
these expressions. Python scalar state now borrows that metered search after full
strict validation and before acquiring slots/attempt workspace. Absence enters
the existing finished state; presence still uses the original Python verifier.
There are no new fields, retained roots, construction, matcher or mode. Candidate
eligibility moves behind rejection. Original work/token/deadline authority covers
validation, screening and publication. Tight working/depth/result limits can now
admit a scratch-free miss. [Contract](../../../docs/SPEC-PYTHONRE.md).

Release checks pass on both runtimes: complete PythonRe **4249/runtime**, affected
Core **487/runtime**, PCRE2 **13/runtime**. Final test-only additions pass all
**28 focused cases/runtime**, including the actual catalog patterns below.
They cover nullable/negative/folded/atomic semantics, prefix lookbehind context,
scalar offsets, strict malformed tails, current/original cancellation, native
search-phase work/deadline denial, funded loans and retained captures after close.
An initial new boundary fixture expected ArgumentException; the existing precise
type is ArgumentOutOfRangeException. Failed and passing logs remain archived.
[Verification](verification.txt).

The [public A/B](comparison.json) took **58.35s** under a 120s cap, minimum sample
**50.84ms**. Quiet Ubuntu 24.04.4/Xeon Platinum 8573C VM, .NET 11 RC3, CPU mask 4.
Four independent lanes: before/after PythonRe Search, diagnostic compiled .NET
predecoded/decode Search on overlapping semantics. Each gets 750ms warmup,
80ms calibration target and three alternating samples. Result digests are retained
and checked after timing. Finite/live producer policies are not emulated by BCL.

| Public Search | Before / after | Change | Allocation B/op before / after |
| --- | --- | --- | --- |
| Actual greedy miss `a+b` / `aaaa` | 2.622 / 0.320 us | 8.19x faster | 3552 / 0 |
| Related absent-tail `a++b` miss | 2.652 / 0.318 us | 8.35x faster | 3552 / 0 |
| Related absent-tail `(?>a+)b` miss | 1.587 / 0.200 us | 7.92x faster | 3552 / 0 |
| Finite/live `a+b` miss | 1.831 / 0.334 us | 5.47x faster | 3672 / 120 |
| Positive `a+b` hit | 0.736 / 0.798 us | +8.43% | 3552 / 3552 |
| Tail present, no match | 1.629 / 1.676 us | +2.90% | 3552 / 3552 |
| Captured hit | 0.923 / 0.982 us | +6.43% | 5744 / 5744 |
| Folded control | 0.778 / 0.803 us | +3.15% | 3552 / 3552 |
| Unicode hit | 0.965 / 1.046 us | +8.48% | 3552 / 3552 |
| Literal control | 0.112 / 0.110 us | -1.90% | 0 / 0 |
| Finite/live hit | 0.991 / 1.063 us | +7.23% | 3672 / 3672 |

The positive costs are accepted operating costs, not timing equivalence. Existing
Core metadata retention is unchanged; [probes](actual-case-proof.jsonl) show that
the actual targets already retained Core. No construction/retention saving is
claimed. VM build initially failed because the system temp filesystem was full;
an owned /dev/shm task TMPDIR fixed setup without deleting files or stopping
servers. Exact failed/successful build logs are archived.

The pilot's possessive and atomic examples were initially mistaken for the
published miss cases. This scope error was caught by the real-catalog CPython
qualification. **Only the published greedy gap is addressed by this change.**
The actual possessive row is `a++a` on `aaaa`; its necessary `a` is present and
the miss depends on possessive consumption. The actual atomic row is `(?>ab+)c`
on `xxabbbdzz`; its selected mandatory literal is `a`, also present. Neither row
benefits from absence rejection. Their measured results remain evidence, without
crediting the proxy examples as catalog improvements.

| Actual catalog case | Current strong ratio to CPython | Qualification |
| --- | --- | --- |
| `greedy/search-miss` | 7.25x -> 0.822x, interval 0.802–0.903 | Inconclusive: 4.33% lane-order effect; 0 B |
| `possessive/search-miss` | 7.575x | CPython faster; 3552 B; gap remains |
| `atomic/search-miss` | 6.405x point estimate | Inconclusive: 2.00% order effect; 3552 B; gap remains |

Each case uses the existing raw selective refresh and nine-pair qualification
against official CPython 3.13.2, with clean matching source/PDB/binary provenance.
Strong ratios are paired public measurements; the separate raw medians are not
substituted into them or into the A/B. CPU policy is the protocol's existing
least-contended allowed processor, retained in each row. No retry to improve a
classification. [Snapshot scope](snapshot-scope.txt) confirms exactly these three
diagnosed cases changed; all other cases, lifecycle and scaling remain identical.
Generated [benchmark page](../../../src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md)
verification passed. No full catalog/package matrix was repeated.

Immutable evidence under artifacts/experiments/python-required-miss-20261030/:

- required-literal-157492e6.tar.gz: exact before/after Core/PythonRe DLL/PDB,
  benchmark/profile binaries, source bundle, raw driver/measurements, CPython
  qualification, failed/fixed VM build and final snapshot/page. SHA256
  `E6F8DAC49521D966015D9710A004D05349E3211FD5F9EB6FD339F8159149E3F0`.
- correctness-logs.zip: local probes, full/focused build/test logs and correction
  witnesses. SHA256
  `DAEDCDE5C67DB606127666655077AF5F3E22D649EC17AE562A8A273618955B4C`.

Next investigate the real possessive gap. Fixed-a40bda03 cumulative cost, a current
complete sub-hour refresh, Windows PCRE2 uncertainty and final exact package/actual
consumer acceptance remain open. Do not reopen this pilot's timing campaign.
