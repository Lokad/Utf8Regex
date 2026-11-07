# PCRE2 byte-only literal Count validation

Move the existing unmetered direct literal Count ahead of UTF-16 metadata
preparation, using strict whole-input validity-only validation. Delete its former
global-driver branch. Eligibility remains unchanged: no match options, unmetered
invocation without a checkpoint hook, nonempty unanchored literal and the existing
1,024-byte suffix threshold. Check scalar alignment after validation. Count returns
only an integer; result-producing operations retain complete metadata.

No field, subject mode, owner, engine, API or dependency is added. Finite/shared
allowances, live cancellation and deadlines retain their existing paths and useful
metering. Captures, retained aliases, result coordinates and interruption contracts
are unchanged. This does not solve controlled-path preparation cost.

## Public costs

ONE separate-Default-process ABBA completed in **101.939 seconds**, under its
120-second cap: 20 public rows, 400 samples, minimum 72.285 ms. Identical caller,
one producer per process, 750 ms/minimum 32-call warmup, independent 75 ms
calibration and five samples with a 20 ms floor. Startup sees eight CPUs, then
affinity selects CPU2. Checksums/results are consumed after timing. All 20
allocation medians are unchanged.

| Public PCRE2 Count | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual Chinese text | 1240.57 | 80.96 | -93.47% | 0 |
| Same, byte offset 4 | 1232.68 | 80.89 | -93.44% | 0 |
| Wide Unicode miss | 97.85 | 19.10 | -80.48% | 0 |
| Two-byte dense | 44.96 | 40.28 | -10.42% | 0 |
| Three-byte dense | 53.79 | 48.18 | -10.43% | 0 |
| Supplementary dense | 43.34 | 31.76 | -26.73% | 0 |
| ASCII literal | 0.40 | 0.18 | -53.94% | 0 |
| Chinese, finite/live | 2652.31 | 2731.18 | +2.97% | 112 |
| Chinese, live token | 2641.11 | 2733.41 | +3.49% | 112 |
| Chinese, deadline | 2654.15 | 2749.94 | +3.61% | 160 |

All ten intended gains, including partial tails and the 1,024/1,025-byte cases,
persist in both processes with disjoint sample ranges. Accept the three disjoint
controlled-path adverse observations alongside the substantial direct-route gains.
Their execution bodies retain the old route; no unmeasured causal explanation is
assigned. Controlled Count still costs about 2.7 ms here and needs further work.

Keep every observation: unchanged Core Chinese Count +6.56% overlaps (before
process medians 72.04/80.70 us, after 81.34/81.79); predecoded BCL -3.63% overlaps.
The excluded 1,023-byte, anchored and empty cases also improve despite bypassing
the new fast path; assign no causal credit. No normalization, confirmation,
producer tuning, uniform speedup or new catalog Status. [Summary](cost-summary.json)
and [acceptance](acceptance.json) retain all rows, allocations and process ranges.

The unchanged 813,478-byte LF Chinese input has SHA256
`F129E81928C58ECBBA0CCBB63B36679355345248DF057D1E9DED670D6E9C964B`.
The 23-byte needle `夏洛克·福尔摩斯` has 30 matches. The untimed public
[route probe](route.json) establishes the actual direct program and its eligibility.

## Validation and preservation

Release PCRE2 100 checks per target pass on Windows/Linux for .NET 10/11. Coverage
includes Unicode/ASCII boundaries, malformed input before invalid offsets,
nonoverlap, result coordinates/retention, options/empty/anchor exclusions, finite
resources, cancellation and deadlines. Both 20-row public preflights pass.

Untimed FullOpts CountCore grows 1127 to 1544 bytes: the new branch calls
validity-only validation and the existing CountWithMetrics before metadata; the
controlled metadata fallthrough remains. These are [route facts](codegen-summary.json),
not public timings. No unchecked access or manual size estimate is introduced.

The initial source placement in IsMatchCore produced a compiler error; it was
corrected to CountCore. The new supplementary fixture initially enumerated from
zero and correctly matched its supplementary prefix; enumerate from byte four
to test the intended coordinates. Both original failures are preserved. Final
tests/preflights and the sole timed attempt pass; no algorithm retuning followed.

No package or Full qualification was repeated. All 282 staged assets and frozen
Full52 graphs remain unchanged. Raw evidence is copied off VM and hash-verified:

- `artifacts/experiments/pcre-count-validation-20261107/evidence.tar.gz`:
  `0101C355D20C2E44EEC037FBF4F6173E096BD9A97A1A3EEBDB2B2363BDE5C91D`.
- `windows-evidence.zip` in that directory:
  `F3A11A0C46D8CFEE14B3B1003EC451E28964E34A1F2DDB6C28CE76FC505C22A8`.

Parent `519be860`; fixed Core `8AFEDEC7`/PythonRe `47870A68`; PCRE2
`3236E597` to `23FA2CBA`; caller `0E9A9311`. Exact hashes and source patch are
retained. This is source work for a future common candidate; no NuGet publication.

Source CI [passed on Windows and Linux](ci.json) for `328f1577`.
