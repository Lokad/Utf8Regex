# Managed PCRE2 performance qualification

`PCRE2.Benchmarks.json` is the authoritative snapshot. The accepted
2026-08-15 snapshot has SHA-256
`CC3FAEC536E166C4FCD8F2879100C230B723862EA71D647BF668B9C8DE376D20`.
It contains 126 operation rows in ten top-line sections and eleven scaling
families with four 1×/2×/4×/8× points each.

## Measurement protocol

- Release build and sequential commands. The original catalog used 20
  requested iterations and three samples; the final selective qualification
  used five samples for affected rows and scaling families.
- Candidate-heavy scaling was rerun with 100 requested iterations and seven
  samples after its shorter run exposed tiering noise.
- Every row records its effective iteration count.
- PCRE2 refresh uses input/operation-specific maximums plus a warm pilot that
  caps each timed sample at approximately 250 ms. Fast rows retain up to 1,000
  iterations; the slowest whole-document rows use one.
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
| Long flat patterns | 2× pattern/input | 1.01×, 1.03×, 1.08× | 0 B |
| Cartesian literal families | 2× alternatives | 1.71×, 1.82×, 2.87× | 0 B |
| Dense+sparse candidates | 2× input | 1.96×, 1.78×, 2.23× | 0–1 B |
| Candidate-heavy misses | 2× input | 0.47×, 1.77×, 2.03× | 0 B |
| Branch/repeat | 2× input | 2.00×, 1.94×, 2.03× | 1 B |
| Dense non-ASCII coordinates | 2× input | 1.99×, 1.99×, 1.97× | 0–1 B |
| Dense character classes | 2× input | 1.95×, 1.99×, 1.92× | 0–1 B |
| Zero-width iteration | 2× input | 1.99×, 1.99×, 2.09× | 0–1 B |
| Capture rollback | approximately 2× input | 1.74×, 1.62×, 0.66× | 0 B |
| Replacement growth | 2× input/output | 1.78×, 1.73×, 1.83× | 42,088 / 83,816 / 167,272 / 334,184 B |
| Required-literal all-`a` miss | 2× input | 1.29×, 1.46×, 1.62× | 1 B |

No family has an unexplained quadratic curve. Candidate misses, branching,
coordinate projection, character classes, zero-width progress, and capture
rollback track input growth approximately linearly. The dense/sparse family
uses a constant pattern across points so the curve measures input growth rather
than size-dependent planner changes. Cartesian execution grows faster than its
last alternative-count doubling but remains well below quadratic growth.
Replacement allocation is the required returned byte array and scales with
the doubled output, while non-result operations allocate zero bytes per warm
invocation apart from measurement rounding of at most one byte. The first
candidate-heavy point was measured before the process settled on its warmed
tier, making the first ratio artificially sublinear; the subsequent doublings
are 1.77× and 2.03× and show the relevant steady-state envelope.

Construction storage is bounded by pattern/program size. The long-flat
construction time grows 1.15×, 1.27×, and 1.42× for successive 2× pattern growth;
the other fixed-pattern families remain flat. Per-instance replacement plans
are capped at 16 cache entries.

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
