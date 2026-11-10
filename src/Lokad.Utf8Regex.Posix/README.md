# Lokad.Utf8Regex.Posix

The optional POSIX ERE adapter for .NET 10/11 matches Unicode scalars over strict
UTF-8, with leftmost-longest selection and hierarchical capture priority.
It is included in the qualified [four-package 0.5.0-dev.66 candidate](../../bench/Experiments/DeliveryDev66-20261110/README.md),
and is not available in the published 0.4.0 release. See the [supported ERE contract](../../docs/SPEC-POSIX.md).
The dev66 candidate includes `inputValidation`; earlier frozen candidates retain
their original APIs and package bytes.

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
coordinate scaling with one/four captures. Their native comparisons await the
next refresh; the frozen tables below retain their original measured scope.

## Subject validation policy

Construct with `inputValidation: Utf8InputValidation.AssumeValid` only when the
host guarantees valid UTF-8 and unchanged borrowed storage. Read-only
`InputValidation` reports the choice; `Validate` is default and checks the whole
subject at most once per logical operation or cursor lifetime. Matching, counting,
replacement/splitting where supported and cursors inherit the engine policy.
Pattern/replacement validation, checked accesses, scalar/byte semantics,
cooperative cancellation/deadlines and admission remain. Required coordinate or
output work may still inspect input. See the [shared contract](../../docs/SPEC.md#subject-input-validation).
Existing benchmark tables use Validate unless explicitly labeled otherwise.

<!-- BEGIN GENERATED POSIX BENCHMARKS -->
## Benchmarks

Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. These rows cover POSIX longest selection, capture participation and nested/nullable histories. Unicode and nonzero floors are part of a selection case. Core's generic search, validation, dense counting and scalar progression are not repeated here. Native PCRE2 NFA uses a prepared reusable match buffer, UTF validation and equivalent byte/UTF-16 result projection. Its ratios apply only to verified equal-result inputs, not general POSIX semantics. Native allocations below cover CLR traffic, excluding native workspace.

| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |
|---|---|---:|---:|---:|---|---:|
| `capture/empty` | Captures | 0.61 | 0.16 | 64 | Native faster | 3.83 |
| `capture/nullable` | Captures | 0.59 | 0.20 | 56 | Native faster | 2.99 |
| `capture/absent` | Captures | 0.43 | 0.15 | 48 | Native faster | 2.88 |
| `projection/short-1` | Captures | 0.20 | 0.12 | 32 | Native faster | 1.72 |
| `projection/short-4` | Captures | 0.29 | 0.22 | 56 | Native faster | 1.31 |
| `projection/unicode-prefix-1` | Captures | 271.67 | 271.80 | 32 | Equivalent | 1.00 |
| `projection/unicode-prefix-4` | Captures | 271.76 | 678.71 | 56 | Managed faster | 0.40 |
| `capture/ambiguous` | Captures | 1.01 | — | 56 | Excluded | — |
| `capture/nested` | Captures | 2.92 | — | 64 | Excluded | — |
| `capture/unicode-longest` | Captures | 1.13 | — | 56 | Excluded | — |
| `count/ambiguous` | Count | 0.30 | — | 0 | Excluded | — |
| `cursor/ambiguous` | Cursor | 0.94 | — | 336 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

### Bounded history growth

The same `(a|aa)*` capture operation at three lengths exposes ambiguity cost. Earlier iterations prefer `aa`; ordered native captures differ, so no native ratio is reported. Working peak is observed in untimed funded preflight with a finite policy; it is a conservative admission charge, not exact heap size.

| Case | Input bytes | Utf8Regex (µs) | Managed B/op | Peak working B |
|---|---:|---:|---:|---:|
| `history/length-4` | 4 | 2.05 | 48 | 960 |
| `history/length-8` | 8 | 14.04 | 48 | 1952 |
| `history/length-12` | 12 | 105.59 | 48 | 1952 |

### Historical adapter control

SKU is an unambiguous consumer control, not a POSIX selection claim. One timed batch performs 4,096 independent Match calls with reused caller capture storage; the table divides time and allocations by 4,096. Every record's captures and checksum are checked outside timing.

| Case | Utf8Regex / call (µs) | Native NFA / call (µs) | Managed B/call | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `capture/sku007` | 0.79 | 0.16 | 96 | 4.88 |

### Representative policy costs

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `cursor/ambiguous` | 1.31 µs | 1.43 µs | 1.75 µs | 1.53 |
| `capture/empty` | 0.78 µs | 0.90 µs | 1.01 µs | 1.46 |
| `capture/sku007` | 4127.01 µs | 4601.27 µs | 5119.69 µs | 1.42 |

Only the empty-capture, longest-cursor and SKU control rows time all policy lanes; SKU costs here remain per 4,096-call batch. Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `2A860DBA323F2D7DB8F236884F85C09B5C069BF3DC4451EF49D09ADAA8C4F66A`.
<!-- END GENERATED POSIX BENCHMARKS -->
