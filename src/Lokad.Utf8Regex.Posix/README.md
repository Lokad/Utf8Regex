# Lokad.Utf8Regex.Posix

The optional POSIX ERE adapter for .NET 10/11 matches Unicode scalars over strict
UTF-8, with leftmost-longest selection and hierarchical capture priority.
It is included in the [four-package dev80 candidate](../../bench/Experiments/DeliveryDev80-20261113/README.md),
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
regenerates offline from `bench/Snapshots/Posix.json`.
The `projection/short-*` and `projection/unicode-prefix-*` cases isolate capture
coordinate scaling with one/four captures. Their native comparisons use the
same requested coordinates and the valid-text guarantee in both lanes.

## Subject text contract

Subjects are already-valid UTF-8 text, borrowed unchanged while in use. Validate
external bytes at the application's text boundary. The adapter has no subject
validation option/property and does not require malformed-suffix rejection.
Pattern validation, scalar starts/selection, captures, coordinate projection,
resource admission and original cancellation/deadlines remain active.

The tables use the same trusted UTF-8 guarantee in both lanes. Native PCRE2 NFA
is an ordered matcher, not a POSIX implementation: ranges/captures must agree
before a ratio is reported; incompatible selection/history cases stay excluded.

<!-- BEGIN GENERATED POSIX BENCHMARKS -->
## Benchmarks

Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. These rows cover POSIX longest selection, capture participation and nested/nullable histories. Unicode and nonzero floors are part of a selection case. Core's generic search, validation, dense counting and scalar progression are not repeated here. Both lanes require existing valid unchanged UTF-8 text. Native PCRE2 NFA uses UTF mode with NO_UTF_CHECK, a prepared reusable match buffer and equivalent byte/UTF-16 result projection. Its ratios apply only to verified equal-result inputs, not general POSIX semantics. Native allocations below cover CLR traffic, excluding native workspace.

| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |
|---|---|---:|---:|---:|---|---:|
| `capture/empty` | Captures | 0.46 | 0.17 | 0 | Native faster | 2.80 |
| `capture/nullable` | Captures | 0.44 | 0.20 | 0 | Native faster | 2.23 |
| `capture/absent` | Captures | 0.31 | 0.16 | 0 | Native faster | 1.89 |
| `projection/short-1` | Captures | 0.16 | 0.12 | 0 | Native faster | 1.41 |
| `projection/short-4` | Captures | 0.24 | 0.22 | 0 | Native faster | 1.09 |
| `projection/unicode-prefix-1` | Captures | 135.54 | 270.84 | 0 | Managed faster | 0.50 |
| `projection/unicode-prefix-4` | Captures | 135.23 | 674.98 | 0 | Managed faster | 0.20 |
| `capture/ambiguous` | Captures | 1.12 | — | 680 | Excluded | — |
| `capture/nested` | Captures | 1.60 | — | 680 | Excluded | — |
| `capture/unicode-longest` | Captures | 1.18 | — | 680 | Excluded | — |
| `count/ambiguous` | Count | 0.38 | — | 0 | Excluded | — |
| `cursor/ambiguous` | Cursor | 1.92 | — | 1040 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

### Bounded history growth

The same `(a|aa)*` capture operation at three lengths exposes ambiguity cost. Earlier iterations prefer `aa`; ordered native captures differ, so no native ratio is reported. Working peak is observed in untimed funded preflight with a finite policy; it is a conservative admission charge, not exact heap size.

| Case | Input bytes | Utf8Regex (µs) | Managed B/op | Peak working B |
|---|---:|---:|---:|---:|
| `history/length-4` | 4 | 1.40 | 680 | 2320 |
| `history/length-8` | 8 | 2.30 | 680 | 2320 |
| `history/length-12` | 12 | 3.42 | 1920 | 5904 |

### Historical adapter control

SKU is an unambiguous consumer control, not a POSIX selection claim. One timed batch performs 4,096 independent Match calls with reused caller capture storage; the table divides time and allocations by 4,096. Every record's captures and checksum are checked outside timing.

| Case | Utf8Regex / call (µs) | Native NFA / call (µs) | Managed B/call | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `capture/sku007` | 0.64 | 0.16 | 0 | 3.95 |

### Representative policy costs

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `capture/empty` | 0.65 µs | 0.75 µs | 0.84 µs | 1.62 |
| `capture/sku007` | 3479.80 µs | 3959.88 µs | 4408.56 µs | 1.52 |
| `cursor/ambiguous` | 2.37 µs | 2.58 µs | 2.95 µs | 1.34 |

Only the empty-capture, longest-cursor and SKU control rows time all policy lanes; SKU costs here remain per 4,096-call batch. Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `81CB28908D80BD3FAA489831759E4A9CAE32087CB87351377A4CA308BAB57DC0`.
<!-- END GENERATED POSIX BENCHMARKS -->
