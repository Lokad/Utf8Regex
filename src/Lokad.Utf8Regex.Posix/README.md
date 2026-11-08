# Lokad.Utf8Regex.Posix

The optional POSIX ERE adapter for .NET 10/11 matches Unicode scalars over strict
UTF-8, with leftmost-longest selection and hierarchical capture priority.
It is under qualification for 0.5.0, and is not available in 0.4.0 or the qualified
three-package dev.55 candidate. See the [supported ERE contract](../../docs/SPEC-POSIX.md).

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
| `capture/empty` | Captures | 2.16 | 0.15 | 240 | Native faster | 14.46 |
| `capture/sku007` | Captures × 4096 | 7977.30 | 599.98 | 1114286 | Native faster | 13.32 |
| `capture/nonzero` | Captures | 1.10 | 0.20 | 240 | Native faster | 5.50 |
| `capture/unicode` | Captures | 1.04 | 0.19 | 240 | Native faster | 5.48 |
| `capture/absent` | Captures | 0.74 | 0.17 | 232 | Native faster | 4.48 |
| `count/dense` | Count | 59.84 | 14.71 | 225 | Native faster | 4.07 |
| `cursor/absent` | Cursor | 1.42 | 0.58 | 408 | Native faster | 2.42 |
| `is-match/miss` | IsMatch | 0.34 | 0.20 | 256 | Native faster | 1.73 |
| `is-match/hit` | IsMatch | 0.35 | 0.24 | 256 | Native faster | 1.45 |
| `cursor/scalars` | Cursor | 0.88 | 0.71 | 360 | Native faster | 1.24 |
| `capture/ambiguous` | Captures | 1.69 | — | 256 | Excluded | — |
| `capture/repeated` | Captures | 4.20 | — | 232 | Excluded | — |
| `count/ambiguous` | Count | 1.05 | — | 208 | Excluded | — |
| `cursor/ambiguous` | Cursor | 1.20 | — | 384 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `capture/absent` | 1.04 µs | 1.19 µs | 1.37 µs | 1.61 |
| `cursor/scalars` | 1.29 µs | 1.38 µs | 1.97 µs | 1.57 |
| `is-match/miss` | 0.49 µs | 0.52 µs | 0.61 µs | 1.50 |
| `capture/unicode` | 1.40 µs | 1.56 µs | 1.81 µs | 1.50 |
| `capture/nonzero` | 1.47 µs | 1.64 µs | 1.89 µs | 1.49 |
| `cursor/ambiguous` | 1.60 µs | 1.77 µs | 2.08 µs | 1.48 |
| `is-match/hit` | 0.48 µs | 0.51 µs | 0.66 µs | 1.46 |
| `cursor/absent` | 1.89 µs | 2.04 µs | 2.49 µs | 1.44 |
| `capture/ambiguous` | 2.21 µs | 2.41 µs | 2.70 µs | 1.43 |
| `capture/sku007` | 10109.90 µs | 11164.21 µs | 12467.26 µs | 1.40 |
| `count/ambiguous` | 1.29 µs | 1.44 µs | 1.54 µs | 1.38 |
| `capture/empty` | 2.75 µs | 2.90 µs | 3.27 µs | 1.34 |
| `capture/repeated` | 4.99 µs | 5.24 µs | 5.58 µs | 1.25 |
| `count/dense` | 69.55 µs | 69.52 µs | 72.09 µs | 1.16 |

Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `83E954A46DF60CDC54BD3988B0DE6ECFF05024196E99F0A33BC7AE2EF70248F4`.
<!-- END GENERATED POSIX BENCHMARKS -->
