# Managed PCRE2 performance qualification

`PCRE2.Benchmarks.json` is the authoritative snapshot. The accepted
2026-08-17 snapshot has SHA-256
`A565BD95D4B152CF1CFB07F02C88576DAAD557EE2DC33E0EAEAE98669AE2BA25`.
It contains 126 operation rows in ten top-line sections and 66 points in 16
scaling families. Every row and point was measured from source `9f3e6bcfb29d`
with `TrackedDirty=false`.

## Measurement protocol

- Release build and sequential commands. Operation rows use 20,000 requested
  iterations and seven samples; the calibrator lowers costly cases to keep one
  timed sample near 250 ms. Scaling uses 20 requested iterations and five
  samples with family-specific input floors.
- A complete 20-iteration/five-sample operation pass was rejected before
  publication because short PCRE2 VM rows had not reached a stable JIT tier.
  For example, `common/email-miss` reported 34.656 us there, but a 9,375-call
  selective rerun and the final catalog pass report 3.758 and 3.989 us. The
  higher-cap rerun replaced every operation row coherently.
- Every row records its effective iteration count.
- PCRE2 refresh uses input/operation-specific maximums plus a warm pilot that
  caps each timed sample at approximately 250 ms. Fast rows retain enough
  iterations to tier; the slowest whole-document rows still use one.
- Untimed priming is twice the effective count, bounded to 1–256 calls. This
  replaces the generic helper's 65,536-call minimum, which multiplied
  millisecond-scale global operations into minutes of discarded work.
- Sections checkpoint after every case, so an interruption never discards the
  measurements already completed.
- Construction time/allocation, first-call allocation, warm temporary
  allocation, and warm operation time are separate fields. Decode-then-Regex
  and predecoded-Regex baselines remain present wherever semantics overlap.

`Dry` was not used to accept these values.

## Scaling result

The warm ratios below compare each successive doubling. Input ratios are shown
where input is the varied dimension; the Cartesian family instead doubles its
alternative count at each point.

| Family | Scale ratios | Warm-time ratios | Warm allocation |
|---|---:|---:|---:|
| Long flat patterns | 2× pattern/input | 1.03×, 1.02×, 1.03× | 0 / 0 / 0 / 0 B |
| Cartesian literal families | 2× alternatives | 1.43×, 1.82×, 2.45× | 0 / 0 / 0 / 0 B |
| Dense+sparse candidates | catalog density progression | 2.04×, 1.03×, 2.43×, 1.00×, 0.28× | 0 / 0 / 1 / 1 / 1 / 1 B |
| Candidate-heavy misses | 2× input | 1.89×, 1.74×, 2.05× | 0 / 0 / 1 / 1 B |
| Required-literal all-`a` miss | 2× input | 1.28×, 1.51×, 1.79× | 1 / 1 / 1 / 1 B |
| Branch/repeat | 2× input | 1.78×, 1.82×, 0.78× | 1 / 1 / 1 / 1 B |
| Dense non-ASCII coordinates | 2× input | 0.36×, 1.95×, 1.33× | 0 / 0 / 1 / 1 B |
| Excluded-ASCII repeat Count | 2× input | 1.32×, 1.51×, 1.76× | 1 / 1 / 1 / 1 B |
| Dense character classes | 2× input | 1.69×, 1.71×, 1.77× | 0 / 1 / 1 / 1 B |
| Zero-width iteration | 2× input | 2.00×, 1.80×, 1.45× | 0 / 0 / 1 / 1 B |
| Capture rollback | approximately 2× input | 1.73×, 1.90×, 1.90× | 0 / 0 / 0 / 0 B |
| Replacement growth | 2× input/output | 1.96×, 1.81×, 1.91× | 34,408 / 68,456 / 136,552 / 272,744 B |
| Literal-family global Replace | 2× input/output | 1.82×, 1.92×, 1.85× | 841 / 1,609 / 3,145 / 6,217 B |
| Branch-reset coordinate projection | 2× input | 1.95×, 1.98×, 1.98× | 0 / 0 / 1 / 0 B |
| Single-token-repeat VM | 2× input | 1.91×, 2.20×, 1.80× | 0 / 0 / 0 / 0 B |
| Leading-boundary run candidates | 2× input | 1.48×, 1.07×, 1.18× | 0 / 0 / 0 / 0 B |

No family has an unexplained quadratic curve. Candidate misses, branching,
coordinate projection, character classes, zero-width progress, and capture
rollback track input growth approximately linearly. The dense/sparse family
uses a constant pattern across points so the curve measures input growth rather
than size-dependent planner changes. Cartesian execution grows faster than its
last alternative-count doubling but remains well below quadratic growth.
Replacement allocation is the required returned byte array and scales with
the doubled output, while non-result operations allocate zero bytes per warm
invocation apart from measurement rounding of at most one byte. The 2.45×
final Cartesian point and 2.20× middle single-token point remain
subquadratic; they are retained as tiering and breadth sentinels rather than
normalized away.

Construction storage is bounded by pattern/program size. The long-flat
construction time grows 1.14×, 1.29×, and 1.47× for successive 2× pattern growth;
the other fixed-pattern families remain flat. Per-instance replacement plans
are capped at 16 cache entries.

## Current qualification summary

Against decode-then-Regex where equivalent work exists, the current snapshot
records 6 wins/20 losses for IsMatch with 18.650 us total positive excess,
12 wins/11 losses for Count with 35.180 ms excess and -24.454 ms net time,
0 wins/5 losses for enumeration with 24.150 us excess, 0 wins/5 losses for
`MatchMany` with 5.030 us excess, and 1 win/5 losses for Replace with 4.110 us
excess. Count remains the only material aggregate lane; compatible non-Count
positive excess totals 51.940 us.

Against the initial P0 snapshot, compatible positive excess fell from 82.464
ms to 35.232 ms (-57.3%). IsMatch fell from 90.423 us to 18.650 us (-79.4%),
Count from 81.773 ms to 35.180 ms (-57.0%), enumeration from 29.179 us to
24.150 us (-17.2%), `MatchMany` from 14.740 us to 5.030 us (-65.9%), and
Replace from 556.882 us to 4.110 us (-99.3%). Count wins increased from 8 to
12 of 23 cases and its net time moved from +43.984 ms to -24.454 ms.

The largest remaining compatible Count losses are generic VM work, not facade
overhead. URI accepts 5,301 matches through 700,575 VM steps and 33,959
workspace rents; river-window accepts 2 through 309,161 steps and 12,950
rents; email accepts 92 through 15,924 steps and 991 rents. Direct workspace
replay is 453.2 us for URI and 58.17 us for river-window, while resource
metering contributes only 0.13 us and 0 us respectively. Further progress on
these rows therefore requires a reusable semantic-search mechanism, not
another wrapper or pooling adjustment.

The largest PCRE2-specific operation is captured-repeat `\K` replacement at
8.216 us, down from the initial special-operation maximum of 36.945 us.
E97 replaced two temporary capture endpoint arrays with one stack/pool staging
buffer, and E98 projects top-level final capture slots before pooled VM state is
returned. One-, two-, and four-slot detailed matches now allocate exactly the
required public group arrays (56, 80, and 136 B). Positive assertions retain
only the raw nested capture ranges required for merge; scalar conditional and
recursive IsMatch paths remain zero-allocation. These changes are internal and
do not alter the PCRE2 public API or managed package boundary.

The sections below retain the chronological checkpoint evidence that led to
the current snapshot. Their per-case values and test totals are historical,
not replacements for the qualified summary above.

## Qualified P0 result

The snapshot is evidence, not a semantic parity claim. Candidate facts and
flavor-neutral direct literal-family execution removed the former multi-second
P0 losses:

| Case | `Utf8Pcre2` | Decode-then-Regex | Ratio |
|---|---:|---:|---:|
| `industry/leipzig-name-family-count` | 2.960 ms | 13.672 ms | 0.22× |
| `industry/leipzig-river-window-count` | 20.274 ms | 28.650 ms | 0.71× |
| `industry/mariomka-email-count` | 11.852 ms | 7.194 ms | 1.65× |
| `industry/mariomka-ip-count` | 13.782 ms | 13.103 ms | 1.05× |
| `industry/mariomka-uri-count` | 18.908 ms | 6.487 ms | 2.91× |
| `industry/rust-sherlock-word-holmes-count` | 1.170 ms | 5.144 ms | 0.23× |
| `industry/rust-sherlock-holmes-window-count` | 2.196 ms | 0.365 ms | 6.02× |
| `industry/rust-sherlock-ing-count` | 6.320 ms | 7.177 ms | 0.88× |
| `common/matches-words` | 77.630 us | 77.800 us | 1.00× |

At the initial P0 checkpoint, the remaining material compatible limitation was
`industry/rust-sherlock-nonnewline-count` (`[^\n]*`): 18.373 ms versus
1.206 ms decode-then-Regex, or 15.23×. Only this one family passed the residual
VM-cost threshold. Email and URI still trailed the predecoded baseline by 28.09×
and 11.92× respectively, but stay below 3× decode-then-Regex; decoding is part
of the comparable UTF-8 operation and PCRE2 semantics remain authoritative.

## Single-token repeat follow-up

The residual non-newline case justified one reusable direct execution shape:
an exact AST containing one consuming PCRE2 token under one quantifier. The
executor preserves greedy, lazy, possessive, bounded, Unicode, `\R`, `\X`, and
`\C` behavior, shares the global cursor used by enumerate, `MatchMany`, and
replacement, and retains the backtracking VM whenever resource metering is
requested. It reduced the public Count case from approximately 18.2 ms to
4.0 ms without repeat workspace rents.

A second and final variant recognizes an exact greedy unbounded negated-ASCII
class and counts its global stream without constructing match projections. The
qualified snapshot records 112.386 us versus 748.589 us for decode-then-Regex:
6.66× faster, zero warm allocation, and 99.4% below the pre-experiment PCRE2
time. The implementation and evidence are `cef416e`, `8aafc32`, and `1db3bd6`.
All 1,612 PCRE2 tests pass; allocation sentinels run as one non-parallel
measurement cohort so unrelated pooled work cannot contaminate their counters.

PCRE2-specific global rows are substantially smaller in the curated inputs;
the largest is `literal/empty-unicode` replacement at 93.625 us. Future
performance work should use `--emit-pcre2-priority-report` to select a family,
then the appropriate case/scaling drilldown, and refresh only the affected
snapshot rows.

## Compatible candidate-search follow-up

The P1 rerank selected `common/one-node-backtracking`, whose
`[^a]+\.[^z]+` pattern previously disabled the existing
`LeadingRunThenLiteral` plan because the leading run can also consume the dot.
That exclusion was unnecessarily broad for an unbounded final leading run:
the candidate search enumerates dot occurrences, retreats to the beginning of
the run, and still delegates final match semantics to the PCRE2 backtracker.
Bounded runs continue to use their prior bounded-window route.

At 20,000 iterations and seven samples, the accepted implementation moves the
no-dot miss from 24.903 us to 0.405 us (-98.4%). It is faster than the 1.155 us
decode-then-Regex comparator and close to the 0.353 us predecoded lower bound.
The selective five-sample snapshot refresh records 0.402 us, 0 B warm
allocation, and 20,000 effective iterations for the same PCRE2 operation.
Greedy, lazy, possessive, adjacent/multiple delimiter, start-offset, Unicode,
malformed-input, resource-metering, Count, enumeration, `MatchMany`, and
replacement controls pass. The PCRE2 10.47 corpus passes 1,623/1,623 tests and
the full solution passes 2,975/2,975. The implementation and evidence are in
`7fe0cace`.

The same rerank identifies a distinct next mechanism rather than an extension
of this candidate change. On the 16,013,977-byte
`industry/leipzig-symbol-count` input, public PCRE2 `\p{Sm}` Count takes
38.942 ms while the core UTF-8 category route takes approximately 1.13--1.20
ms. Raw PCRE2 and public PCRE2 are equivalent within noise, so wrapper
validation and projection are not the cause. The existing PCRE2 character
runner decodes and dispatches every scalar, whereas the core category counter
has a vectorized ASCII math-symbol kernel. `\p{L}` is only 1.43x slower than
the core route (1.549 ms versus 1.080 ms), so future work should first prove a
narrow exact-category shared kernel rather than generalize all property
semantics.

The committed pre-experiment snapshot row on source `5bc1476d` records
44.825 ms PCRE2, 1.592 ms core UTF-8, 18.510 ms decode-then-Regex, and 12.264
ms predecoded Regex at four effective iterations. Use that row for persisted
before/after evidence and the longer direct run above for attribution.

Semantic admission is backed by upstream PCRE2 rather than the managed
comparators. PCRE2 10.47 `testinput4` lines 933--938 specifies that
`^\p{Sm}+` matches `+<|~¬⁄` and rejects `X` and U+09F2. Commit `e47f8b9f`
normalizes that vector into an active `\p{Sm}` Count row with result 6; it
passes through the generic PCRE2 character runner before the proposed shared
kernel is enabled. The pre-experiment full solution passes 2,977/2,977.
