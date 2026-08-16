# Managed PCRE2 performance qualification

`PCRE2.Benchmarks.json` is the authoritative snapshot. The accepted
2026-08-16 snapshot has SHA-256
`2CBE983A03BDCC8E22F1ED3F3F9EB73D92AC942A3C123CE723DC992DAEB818BC`.
It contains 126 operation rows in ten top-line sections and 66 points in 16
scaling families. Every row and point was measured from source `c11ff939ceb6`
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
| Long flat patterns | 2× pattern/input | 1.02×, 1.01×, 1.04× | 0 B |
| Cartesian literal families | 2× alternatives | 1.50×, 1.66×, 2.68× | 0 B |
| Dense+sparse candidates | catalog density progression | 1.98×, 0.17×, 1.19×, 1.00×, 1.87× | 0–1 B |
| Candidate-heavy misses | 2× input | 1.93×, 1.76×, 2.08× | 0–1 B |
| Required-literal all-`a` miss | 2× input | 1.40×, 1.60×, 1.81× | 1 B |
| Branch/repeat | 2× input | 1.72×, 1.80×, 0.83× | 1 B |
| Dense non-ASCII coordinates | 2× input | 0.38×, 1.98×, 1.32× | 0–1 B |
| Excluded-ASCII repeat Count | 2× input | 1.33×, 1.58×, 1.67× | 1 B |
| Dense character classes | 2× input | 1.83×, 1.75×, 1.88× | 0–1 B |
| Zero-width iteration | 2× input | 1.99×, 1.94×, 1.29× | 0–1 B |
| Capture rollback | approximately 2× input | 1.73×, 1.86×, 1.94× | 0 B |
| Replacement growth | 2× input/output | 1.88×, 1.94×, 1.93× | 34,408 / 68,456 / 136,552 / 272,744 B |
| Literal-family global Replace | 2× input/output | 1.85×, 1.85×, 1.25× | 841 / 1,609 / 3,145 / 6,217 B |
| Branch-reset coordinate projection | 2× input | 1.97×, 1.97×, 2.64× | 0–1 B |
| Single-token-repeat VM | 2× input | 1.97×, 1.98×, 1.99× | 0–1 B |
| Leading-boundary run candidates | 2× input | 1.43×, 1.08×, 1.21× | 0–1 B |

No family has an unexplained quadratic curve. Candidate misses, branching,
coordinate projection, character classes, zero-width progress, and capture
rollback track input growth approximately linearly. The dense/sparse family
uses a constant pattern across points so the curve measures input growth rather
than size-dependent planner changes. Cartesian execution grows faster than its
last alternative-count doubling but remains well below quadratic growth.
Replacement allocation is the required returned byte array and scales with
the doubled output, while non-result operations allocate zero bytes per warm
invocation apart from measurement rounding of at most one byte. The 2.64×
final branch-reset point and 2.68× Cartesian point remain below the quadratic
gate and are retained as future breadth sentinels rather than normalized away.

Construction storage is bounded by pattern/program size. The long-flat
construction time grows 1.15×, 1.27×, and 1.42× for successive 2× pattern growth;
the other fixed-pattern families remain flat. Per-instance replacement plans
are capped at 16 cache entries.

## Current qualification summary

Against decode-then-Regex where equivalent work exists, the current snapshot
records 6 wins/20 losses for IsMatch with 18.318 us total positive excess,
13 wins/10 losses for Count with 29.600 ms excess, 0 wins/5 losses for
enumeration with 18.016 us excess, and 1 win/5 losses for Replace with 3.828 us
excess. Count remains the only material aggregate lane; the other compatible
operation excess totals 40.162 us.

The largest PCRE2-specific operation is captured `\K` replacement at 8.009 us.
E97 replaced two temporary capture endpoint arrays with one stack/pool staging
buffer, and E98 projects top-level final capture slots before pooled VM state is
returned. One-, two-, and four-slot detailed matches now allocate exactly the
required public group arrays (56, 80, and 136 B). Positive assertions retain
only the raw nested capture ranges required for merge; scalar conditional and
recursive IsMatch paths remain zero-allocation. These changes are internal and
do not alter the PCRE2 public API or managed package boundary.

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
