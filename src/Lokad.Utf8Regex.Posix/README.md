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

Use `--measure-posix-case capture/sku007 9 AssumeValid` for a focused trusted-input
comparison, including its finite/live/funded lanes. Omit the final argument for
`Validate`. The command prints labeled raw evidence without changing these tables;
the native comparator keeps UTF validation enabled.

<!-- BEGIN GENERATED POSIX BENCHMARKS -->
## Benchmarks

Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. These rows cover POSIX longest selection, capture participation and nested/nullable histories. Unicode and nonzero floors are part of a selection case. Core's generic search, validation, dense counting and scalar progression are not repeated here. Native PCRE2 NFA uses a prepared reusable match buffer, UTF validation and equivalent byte/UTF-16 result projection. Its ratios apply only to verified equal-result inputs, not general POSIX semantics. Native allocations below cover CLR traffic, excluding native workspace.

| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |
|---|---|---:|---:|---:|---|---:|
| `capture/empty` | Captures | 0.49 | 0.17 | 64 | Native faster | 2.85 |
| `capture/nullable` | Captures | 0.45 | 0.21 | 56 | Native faster | 2.17 |
| `capture/absent` | Captures | 0.31 | 0.15 | 48 | Native faster | 2.07 |
| `projection/short-1` | Captures | 0.16 | 0.12 | 32 | Native faster | 1.33 |
| `projection/short-4` | Captures | 0.26 | 0.24 | 56 | Native faster | 1.08 |
| `projection/unicode-prefix-1` | Captures | 270.57 | 271.27 | 32 | Equivalent | 1.00 |
| `projection/unicode-prefix-4` | Captures | 270.92 | 675.47 | 56 | Managed faster | 0.40 |
| `capture/ambiguous` | Captures | 0.62 | — | 56 | Excluded | — |
| `capture/nested` | Captures | 0.95 | — | 64 | Excluded | — |
| `capture/unicode-longest` | Captures | 0.73 | — | 56 | Excluded | — |
| `count/ambiguous` | Count | 0.29 | — | 0 | Excluded | — |
| `cursor/ambiguous` | Cursor | 0.83 | — | 336 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

### Bounded history growth

The same `(a|aa)*` capture operation at three lengths exposes ambiguity cost. Earlier iterations prefer `aa`; ordered native captures differ, so no native ratio is reported. Working peak is observed in untimed funded preflight with a finite policy; it is a conservative admission charge, not exact heap size.

| Case | Input bytes | Utf8Regex (µs) | Managed B/op | Peak working B |
|---|---:|---:|---:|---:|
| `history/length-4` | 4 | 0.76 | 48 | 960 |
| `history/length-8` | 8 | 1.08 | 48 | 960 |
| `history/length-12` | 12 | 1.45 | 48 | 960 |

### Historical adapter control

SKU is an unambiguous consumer control, not a POSIX selection claim. One timed batch performs 4,096 independent Match calls with reused caller capture storage; the table divides time and allocations by 4,096. Every record's captures and checksum are checked outside timing.

| Case | Utf8Regex / call (µs) | Native NFA / call (µs) | Managed B/call | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `capture/sku007` | 0.67 | 0.17 | 96 | 4.02 |

### Representative policy costs

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `capture/empty` | 0.69 µs | 0.80 µs | 0.90 µs | 1.65 |
| `cursor/ambiguous` | 1.20 µs | 1.32 µs | 1.66 µs | 1.60 |
| `capture/sku007` | 3755.99 µs | 4215.08 µs | 4758.45 µs | 1.53 |

Only the empty-capture, longest-cursor and SKU control rows time all policy lanes; SKU costs here remain per 4,096-call batch. Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `7D3A7A0C19676CB509167D2C14FF680A212A8C171E77C937DABA85B03623E342`.
<!-- END GENERATED POSIX BENCHMARKS -->
