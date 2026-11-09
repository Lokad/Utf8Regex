# Lokad.Utf8Regex.Posix

The optional POSIX ERE adapter for .NET 10/11 matches Unicode scalars over strict
UTF-8, with leftmost-longest selection and hierarchical capture priority.
It is included in the qualified [four-package 0.5.0-dev.56 candidate](../../bench/Experiments/DeliveryDev56-20261108/README.md),
and is not available in the published 0.4.0 release. See the [supported ERE contract](../../docs/SPEC-POSIX.md).

The adapter depends only on Core. Execution is fully managed and checked,
without a native or BCL regex fallback.

```csharp
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Posix;

var regex = new Utf8PosixRegex("(a|aa)(a*)");
Span<Utf8ValueMatch> groups = stackalloc Utf8ValueMatch[3];
var match = regex.Match("aaa"u8, groups);
// Whole match: aaa; group 1: aa; group 2: a. Byte coordinates are primary.
```

`IsMatch`, `Match`, `Count` and a disposable memory cursor accept cancellation
and optional caller-owned working admission. Constructors also accept finite
work/depth/working/result policies and a timeout. Values and caller-owned capture
spans survive later calls/disposal. Keep borrowed cursor input alive and unchanged;
dispose on early termination to refund workspace. No finalizer is required.

The initial subset is case-sensitive, with scalar ranges and fixed ASCII named
classes. `Newline` enables LF line anchors and excludes LF from dot/negated
brackets. Locale collation, backreferences, lookarounds and non-ERE extensions
are rejected. Matching is resource-metered; worst-case linear time is not promised.
Result-byte policy does not cap caller-owned destinations; these APIs return
values and allocate no producer-owned result payload.

Use `tools/bench.ps1 -CommandArgs '--inspect-posix-cases'` for result preflight,
or `tools/refresh-benchmarks.ps1 -Phase Posix` for the bounded .NET 11 catalog.
Raw measurements stay under `artifacts/benchmark-refresh/current/`; Markdown
regenerates offline from `bench/Snapshots/Posix.json` once measurements exist.

<!-- BEGIN GENERATED POSIX BENCHMARKS -->
## Benchmarks

Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. Times are per catalog operation; sku007 performs 4,096 independent Match calls with one reused caller capture span. Native PCRE2 NFA uses a prepared reusable match buffer, UTF validation and equivalent byte/UTF-16 result projection. Its ratios apply only to verified equal-result inputs, not general POSIX semantics. Native allocations below cover CLR traffic, excluding native workspace.

| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |
|---|---|---:|---:|---:|---|---:|
| `capture/empty` | Captures | 1.59 | 0.14 | 208 | Native faster | 10.98 |
| `capture/sku007` | Captures × 4096 | 5324.71 | 613.18 | 983098 | Native faster | 8.74 |
| `capture/nonzero` | Captures | 0.93 | 0.20 | 200 | Native faster | 4.75 |
| `capture/unicode` | Captures | 0.88 | 0.19 | 200 | Native faster | 4.60 |
| `count/dense` | Count | 58.18 | 14.84 | 225 | Native faster | 3.91 |
| `capture/absent` | Captures | 0.64 | 0.17 | 200 | Native faster | 3.78 |
| `cursor/absent` | Cursor | 1.10 | 0.59 | 376 | Native faster | 1.85 |
| `is-match/miss` | IsMatch | 0.29 | 0.20 | 256 | Native faster | 1.50 |
| `is-match/hit` | IsMatch | 0.33 | 0.24 | 256 | Native faster | 1.42 |
| `cursor/scalars` | Cursor | 0.76 | 0.71 | 352 | Native faster | 1.06 |
| `capture/ambiguous` | Captures | 1.08 | — | 200 | Excluded | — |
| `capture/repeated` | Captures | 2.12 | — | 192 | Excluded | — |
| `count/ambiguous` | Count | 0.99 | — | 208 | Excluded | — |
| `cursor/ambiguous` | Cursor | 1.17 | — | 384 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `capture/absent` | 0.91 µs | 1.05 µs | 1.25 µs | 1.64 |
| `cursor/scalars` | 1.15 µs | 1.24 µs | 1.85 µs | 1.63 |
| `is-match/miss` | 0.44 µs | 0.47 µs | 0.59 µs | 1.61 |
| `cursor/absent` | 1.59 µs | 1.71 µs | 2.20 µs | 1.56 |
| `is-match/hit` | 0.48 µs | 0.51 µs | 0.67 µs | 1.54 |
| `capture/unicode` | 1.17 µs | 1.31 µs | 1.59 µs | 1.49 |
| `capture/ambiguous` | 1.46 µs | 1.60 µs | 1.91 µs | 1.49 |
| `cursor/ambiguous` | 1.60 µs | 1.74 µs | 2.11 µs | 1.48 |
| `capture/nonzero` | 1.23 µs | 1.37 µs | 1.67 µs | 1.47 |
| `count/ambiguous` | 1.28 µs | 1.42 µs | 1.56 µs | 1.43 |
| `capture/sku007` | 6910.16 µs | 7483.27 µs | 8748.21 µs | 1.41 |
| `capture/repeated` | 2.82 µs | 2.94 µs | 3.34 µs | 1.39 |
| `capture/empty` | 2.06 µs | 2.20 µs | 2.58 µs | 1.38 |
| `count/dense` | 66.30 µs | 66.62 µs | 69.67 µs | 1.15 |

Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `BB8DAD967BEA7E04FE242B5B9C73F87B27650C8702D8EDB5F59290533C11867E`.
<!-- END GENERATED POSIX BENCHMARKS -->
