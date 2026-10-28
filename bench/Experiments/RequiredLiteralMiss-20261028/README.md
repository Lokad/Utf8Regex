# Required-literal rejection on unlimited Core calls

1cd988f0 removes the unlimited-invocation exclusion from the existing admitted
required-literal rejection. All remaining eligibility fences are unchanged.
The exact literal can disprove a match; its presence never proves success.
Strict full-subject validation still precedes the check. Scalar execution,
captures, RTL/context restrictions and original interruption authority remain.

[Raw comparison](comparison.json), [console](comparison.txt),
[driver](compare.cs.txt), [project](compare.csproj.txt),
[selective README refresh](readme-refresh.txt).
Same quiet VM, .NET 11 RC3, CPU2, VM-built a40bda03 and 1cd988f0.
Four independent public-operation lanes, 250 ms warmup, three alternating
samples and independently calibrated 50 ms batches. Results agree with .NET
before and during timing. One comparison completed in **15.38 seconds**, within
its 120-second cap; no confirmation or favorable rerun.

| Public IsMatch | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Core one-node miss, ordinary | 67.061 | 0.367 | 183.0x faster | 3552 / 0 |
| Core one-node miss, compiled | 66.445 | 0.366 | 181.4x faster | 3552 / 0 |
| Core finite/live-token miss | 0.381 | 0.352 | -7.4% elapsed | 112 / 112 |
| Core literal present, no match | 37.387 | 37.805 | +1.1% elapsed | 3552 / 3552 |
| Core Unicode hit | 1.043 | 1.104 | +5.8% elapsed | 3552 / 3552 |
| Unchanged compiled English literal | 36.728 | 36.744 | +0.04% elapsed | 0 / 0 |
| PCRE2 one-node miss | 35.021 | 35.440 | +1.2% elapsed | 3552 / 3552 |
| PCRE2 Unicode hit | 1.154 | 1.199 | +3.9% elapsed | 3552 / 3552 |

The independent five-sample README refresh reports Core compiled **2.15x**
against predecoded .NET (was 390.51x), ordinary **0.30x**. Its 0.190/0.189 us
costs remain separate from the paired measurements above. Finite calls already
used this helper before the change.

The initial PCRE2 delegation hypothesis was wrong. A readonly reflection
[probe](probe.cs.txt), [project](probe.csproj.txt), [output](route.txt) proves this
case uses **Pcre2SliceDirectProgram**, with no unmetered Core IsMatch program.
The public measurements agree: this change delivers no PCRE2 improvement.
PCRE2's existing published evidence is unchanged and its 432.70x native-comparator
gap remains active.

Focused Release checks pass on [Core .NET 10](core-net10.txt),
[Core .NET 11](core-net11.txt), [PythonRe](python-net11.txt) and
[PCRE2](pcre-net11.txt). Coverage includes unlimited/finite modes, literal absence
and presence, Unicode, offsets/assertions, malformed tails and interruption.

Measured DLLs/PDBs, driver binaries, raw outputs and selective snapshots are in
the [local immutable archive](../../../artifacts/experiments/required-literal-miss-20261028/parity-required-1cd988f0.tar.gz),
SHA256 `9DF727A0CC6CD08EC0604B382C9882949FBFFD7A477C2E9D94CCF0E37B5B156E`.
No package matrix or complete catalog refresh was repeated.
