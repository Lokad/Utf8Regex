# Managed PCRE2 performance qualification

`PCRE2.Benchmarks.json` is the authoritative snapshot. The accepted
2026-08-14 snapshot has SHA-256
`A8B039A5FA785D8664DA7CAD2BF6E92577DA745020060AC8BE6F4A467CD993A7`.
It contains 126 operation rows in ten top-line sections and ten scaling
families with four 1×/2×/4×/8× points each.

## Measurement protocol

- Release build, sequential commands, 20 requested iterations, three samples.
- Dense/sparse candidate and capture-rollback scaling rows were rerun with 100
  requested iterations and seven samples after the base refresh exposed noisy
  nonmonotone timings.
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
| Dense+sparse candidates | 2× input | 1.79×, 1.65×, 2.19× | 0 B |
| Candidate-heavy misses | 2× input | 1.99×, 1.99×, 2.00× | 0–1 B |
| Branch/repeat | 2× input | 2.00×, 1.94×, 2.03× | 1 B |
| Dense non-ASCII coordinates | 2× input | 1.99×, 1.99×, 1.97× | 0–1 B |
| Dense character classes | 2× input | 1.95×, 1.99×, 1.92× | 0–1 B |
| Zero-width iteration | 2× input | 1.99×, 1.99×, 2.09× | 0–1 B |
| Capture rollback | approximately 2× input | 1.74×, 1.62×, 0.66× | 0 B |
| Replacement growth | 2× input/output | 1.78×, 1.73×, 1.83× | 42,088 / 83,816 / 167,272 / 334,184 B |

No family has an unexplained quadratic curve. Candidate misses, branching,
coordinate projection, character classes, zero-width progress, and capture
rollback track input growth approximately linearly. The dense/sparse family
uses a constant pattern across points so the curve measures input growth rather
than size-dependent planner changes. Cartesian execution grows faster than its
last alternative-count doubling but remains well below quadratic growth.
Replacement allocation is the required returned byte array and scales with
the doubled output, while non-result operations allocate zero bytes per warm
invocation apart from measurement rounding of at most one byte.

Construction storage is bounded by pattern/program size. The long-flat
construction time grows 1.15×, 1.27×, and 1.42× for successive 2× pattern growth;
the other fixed-pattern families remain flat. Per-instance replacement plans
are capped at 16 cache entries.

## Accepted priority gaps

The snapshot is evidence, not a parity claim. The generic PCRE2 verifier is
still much slower than specialized core/.NET routes on several complex
whole-document `Count` workloads:

| Case | `Utf8Pcre2` | Ratio to predecoded Regex |
|---|---:|---:|
| `industry/leipzig-name-family-count` | 4.013 s | 359.59× |
| `industry/leipzig-river-window-count` | 3.308 s | 75.70× |
| `industry/mariomka-email-count` | 2.547 s | 5,155.35× |
| `industry/mariomka-ip-count` | 2.251 s | 225.25× |
| `industry/mariomka-uri-count` | 2.218 s | 1,078.18× |

These are the next optimization priorities: semantic-tree-derived candidate
search and compatible-subset delegation, not fixture-shaped execution. Their
poor constants do not hide an allocation leak or quadratic curve—the frozen
candidate/search scaling families remain linear—but they do rule out claiming
general throughput parity in 0.2.0.

PCRE2-specific global rows are substantially smaller in the curated inputs;
the largest is `literal/empty-unicode` replacement at 93.625 us. Future
performance work should use `--emit-pcre2-priority-report` to select a family,
then the appropriate case/scaling drilldown, and refresh only the affected
snapshot rows.
