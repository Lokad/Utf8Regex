# Lokad.Utf8Regex.Posix

The optional POSIX ERE adapter for .NET 10/11 matches Unicode scalars over strict
UTF-8, with leftmost-longest selection and hierarchical capture priority.
The adapter is part of the upcoming 0.5.0 release.
[Supported ERE contract](../../docs/SPEC-POSIX.md)

The adapter depends only on Core. Execution is fully managed and checked,
without a native or BCL regex fallback.

```csharp
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Posix;

var regex = new Utf8PosixRegex("(a|aa)(a*)");
Span<Utf8CaptureRange> groups = stackalloc Utf8CaptureRange[3];
var match = regex.Match("aaa"u8, captures: groups);
// Whole match: [0,3); group 1: [0,2); group 2: [2,3), all byte ranges.
```

`IsMatch`, `Match`, `Count` and a disposable memory cursor accept cancellation
and optional caller-owned working admission. Constructors also accept finite
work/depth/working/result policies and a timeout through `Utf8RegexPolicy`, shared
with PythonRe. `Compile(pattern, options, policy, cancellationToken)` adds a separate
construction token; both creation forms accept construction bounds/deadlines.
`ProgramMemoryAllowance` admits retained program storage before publication.
Dispose funded expressions: new calls reject after close, while admitted calls
and cursors retain funding until they finish. Null working admission inherits
program funding; an explicit working allowance overrides it. Unfunded disposal
is a no-op. Values and caller-owned capture
spans survive later calls/disposal. Keep borrowed cursor input alive and unchanged;
dispose on early termination to refund workspace and any closed program funding.
Advance with `cursor.MoveNext(out var match, captures)`; aliases share progress
and disposal. No finalizer reclaims admission.

The default is case-sensitive, with scalar ranges and fixed ASCII named
classes. `IgnoreCase` uses invariant Unicode 16.0 simple-lowercase equivalence
without changing longest-match or capture priority. For example, `(a|aa)(a*)`
on `AAA` captures `AA` then `A`; `(k|kk)` on `KK` selects four original bytes.
The relation is single-scalar, with no normalization or multi-scalar folds;
see the [exact relation and class behavior](../../docs/SPEC-POSIX.md#input-and-syntax).
`Newline` enables LF line anchors and excludes LF from dot/negated
brackets. Locale collation, backreferences, lookarounds and non-ERE extensions
are rejected. Matching is resource-metered; worst-case linear time is not promised.
Result-byte policy does not cap caller-owned destinations; these APIs return
values and allocate no producer-owned result payload.

Use `tools/bench.ps1 -CommandArgs '--inspect-posix-cases'` for result preflight,
or `tools/refresh-benchmarks.ps1 -Phase Posix` for the bounded .NET 11 catalog.
Raw measurements stay under `artifacts/benchmark-refresh/current/`; Markdown
regenerates offline from `bench/Snapshots/Posix.json`.
Results and capture destinations use `Utf8CaptureRange`: `Success`,
`StartOffsetInBytes` and exclusive `EndOffsetInBytes`. They have no UTF-16 fields
or alignment flags. Publication does not scan the subject for coordinates.
The refreshed benchmark lanes return equivalent byte-only ranges on both sides.
The `projection/short-*` and `projection/unicode-prefix-*` IDs identify workloads
with short matches and Unicode prefixes.

## Subject text contract

Subjects are already-valid UTF-8 text, borrowed unchanged while in use. Validate
external bytes at the application's text boundary. The adapter has no subject
validation option/property and does not require malformed-suffix rejection.
Pattern validation, scalar starts and selection, byte captures, resource admission,
and original cancellation and deadlines remain active.

The tables use the same trusted UTF-8 guarantee in both lanes. Native PCRE2 NFA
is an ordered matcher, not a POSIX implementation: ranges/captures must agree
before a ratio is reported; incompatible selection/history cases stay excluded.

<!-- BEGIN GENERATED POSIX BENCHMARKS -->
## Benchmarks

Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. These rows cover POSIX longest selection, captures and nested/nullable histories, including Unicode floors, invariant folding and LF rewind. Core's generic search, dense counting and scalar progression are not repeated here. Both lanes require valid unchanged UTF-8. Native PCRE2 NFA uses UTF+NO_UTF_CHECK, matching case/newline options, a reusable match buffer and equivalent requested capture output. Newline rows use positive classes: native negated brackets do not implement POSIX's LF exclusion. Ratios cover verified equal-result inputs only, not general POSIX selection or folding equivalence. Native allocations cover CLR traffic, excluding native workspace.

| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |
|---|---|---:|---:|---:|---|---:|
| `capture/ignorecase-disjoint` | Captures | 0.15 | 0.13 | 0 | Native faster | 1.13 |
| `capture/nullable` | Captures | 0.17 | 0.18 | 0 | Inconclusive | 0.98 |
| `capture/newline-rewind` | Captures | 0.14 | 0.15 | 0 | Managed faster | 0.94 |
| `capture/empty` | Captures | 0.10 | 0.12 | 0 | Managed faster | 0.83 |
| `capture/absent` | Captures | 0.10 | 0.12 | 0 | Managed faster | 0.83 |
| `projection/short-1` | Captures | 0.08 | 0.10 | 0 | Managed faster | 0.82 |
| `projection/unicode-prefix-1` | Captures | 0.08 | 0.10 | 0 | Managed faster | 0.82 |
| `projection/short-4` | Captures | 0.12 | 0.16 | 0 | Managed faster | 0.72 |
| `projection/unicode-prefix-4` | Captures | 0.12 | 0.16 | 0 | Managed faster | 0.71 |
| `capture/ambiguous` | Captures | 1.03 | — | 680 | Excluded | — |
| `capture/nested` | Captures | 1.44 | — | 680 | Excluded | — |
| `capture/unicode-longest` | Captures | 1.03 | — | 680 | Excluded | — |
| `count/ambiguous` | Count | 0.37 | — | 0 | Excluded | — |
| `cursor/ambiguous` | Cursor | 1.67 | — | 1040 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

### Bounded history growth

The same `(a|aa)*` capture operation at three lengths exposes ambiguity cost. Earlier iterations prefer `aa`; ordered native captures differ, so no native ratio is reported. Working peak is observed in untimed funded preflight with a finite policy; it is a conservative admission charge, not exact heap size.

| Case | Input bytes | Utf8Regex (µs) | Managed B/op | Peak working B |
|---|---:|---:|---:|---:|
| `history/length-4` | 4 | 1.21 | 680 | 2320 |
| `history/length-8` | 8 | 2.06 | 680 | 2320 |
| `history/length-12` | 12 | 3.09 | 1920 | 5904 |

### Historical adapter control

SKU is an unambiguous consumer control, not a POSIX selection claim. One timed batch performs 4,096 independent Match calls with reused caller capture storage; the table divides time and allocations by 4,096. Every record's captures and checksum are checked outside timing.

| Case | Utf8Regex / call (µs) | Native NFA / call (µs) | Managed B/call | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `capture/sku007` | 0.12 | 0.12 | 0 | 1.01 |

### Representative policy costs

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `capture/empty` | 0.12 µs | 0.14 µs | 0.19 µs | 1.41 |
| `capture/ignorecase-disjoint` | 0.19 µs | 0.21 µs | 0.25 µs | 1.41 |
| `capture/sku007` | 555.98 µs | 666.53 µs | 865.97 µs | 1.36 |
| `cursor/ambiguous` | 1.89 µs | 2.13 µs | 2.16 µs | 1.27 |

Only the empty-capture, longest-cursor and SKU control rows time all policy lanes; SKU costs here remain per 4,096-call batch. Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `251B711C1EB08B6FD42FE000983C6A5CB9362ED8FDB07AF5ACD001FCD043ABB6`.
<!-- END GENERATED POSIX BENCHMARKS -->
