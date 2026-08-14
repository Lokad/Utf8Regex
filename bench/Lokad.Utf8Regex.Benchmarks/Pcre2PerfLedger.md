# Managed PCRE2 performance qualification

`PCRE2.Benchmarks.json` is the authoritative snapshot. The accepted
2026-08-14 snapshot has SHA-256
`14E9635C4E34DFC36AE2BD85A9E41BCC4E3CECF647B714BF7A549EFD7FC57287`.
It contains 126 operation rows in ten top-line sections and ten scaling
families with three points each.

## Measurement protocol

- Release build, sequential commands, 20 requested iterations, three samples.
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

The warm ratios below compare point 2 / point 1 and point 3 / point 2. Input
ratios are shown where input is the varied dimension; the Cartesian family
instead doubles its alternative count at each point.

| Family | Scale ratios | Warm-time ratios | Warm allocation |
|---|---:|---:|---:|
| Long flat patterns | 4×, 4× pattern/input | 0.84×, 0.92× | 0 B |
| Cartesian literal families | 2×, 2× alternatives | 1.13×, 1.88× | 0 B |
| Dense+sparse candidates | 8×, 4× input | 1.53×, 2.66× | 0–1 B |
| Candidate-heavy misses | 8×, 4× input | 9.86×, 3.15× | 0–1 B |
| Branch/repeat | 8×, 4× input | 6.91×, 4.00× | 0–1 B |
| Dense non-ASCII coordinates | 8×, 4× input | 7.85×, 3.95× | 0–1 B |
| Dense character classes | 8.2×, 4× input | 6.74×, 2.43× | 0–1 B |
| Zero-width iteration | 8×, 4× input | 7.84×, 3.99× | 0–1 B |
| Capture rollback | 3.9×, 4× input | 3.06×, 3.61× | 0 B |
| Replacement growth | 4×, 4× input/output | 3.84×, 3.17× | 21,224 / 83,816 / 334,184 B |

No family has an unexplained quadratic curve. Candidate misses, branching,
coordinate projection, character classes, zero-width progress, and capture
rollback track input growth approximately linearly. The first candidate-miss
step is 9.86× for 8× input and returns to 3.15× for the following 4× step.
Cartesian compilation/execution remains below the semantic alternative growth.
Replacement allocation is the required returned byte array and scales with
the doubled output, while non-result operations allocate zero bytes per warm
invocation apart from measurement rounding of at most one byte.

Construction storage is bounded by pattern/program size. The long-flat
construction time grows 1.20× then 2.25× for successive 4× pattern growth;
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
