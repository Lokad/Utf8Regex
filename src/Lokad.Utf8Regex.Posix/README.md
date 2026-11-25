# Lokad.Utf8Regex.Posix

A managed implementation of selected POSIX extended regular expressions
for .NET 10 and 11. It uses leftmost-longest selection and hierarchical
capture priority. The adapter requires the exact matching Core package.

## Subject text contract

Pass valid UTF-8 and keep borrowed bytes unchanged during use. Validate
external bytes as they become text. Matches consume Unicode scalars, and
start offsets and capture ranges use byte coordinates.
[Subject and start offsets](../../docs/SPEC-POSIX.md#subject-text-contract)

## Supported scope

`IgnoreCase` uses invariant Unicode 16 simple-lowercase equivalence.
`Newline` enables LF line anchors and excludes LF from dot and negated
brackets. The profile excludes locale collation, backreferences and
lookaround. [Grammar and options](../../docs/SPEC-POSIX.md#input-and-syntax)

## Example

```csharp
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Posix;

var regex = new Utf8PosixRegex("(a|aa)(a*)");
Span<Utf8CaptureRange> groups = stackalloc Utf8CaptureRange[3];
var match = regex.Match("aaa"u8, captures: groups);
// Whole match: [0,3); group 1: [0,2); group 2: [2,3), all byte ranges.
```

`Match` writes as many slots as fit, starting with slot zero for the whole
match. Use `IsMatch` for existence and `Count` or `CreateMatchCursor` for
nonoverlapping matches. Copied ranges remain valid after advancing or
disposing the cursor. [Selection and captures](../../docs/SPEC-POSIX.md#selection-and-captures)

## Controls and lifetimes

`Utf8RegexPolicy` sets execution limits and timeouts. `Compile` also accepts
a construction token. Matching and cursor advances observe the original
token and deadline. Result limits govern producer-owned storage. Capture
slots supplied by the caller remain caller-owned.
[Interruption and admission](../../docs/SPEC-POSIX.md#interruption-and-admission)

Cursor aliases share progress and disposal. Dispose an abandoned cursor to
release its workspace. Dispose a funded regex when finished. Admitted calls
and cursors can finish after it closes.
[Public surface and lifetimes](../../docs/SPEC-POSIX.md#public-surface-and-lifetimes)

[Benchmark procedure](../../docs/BENCHMARKS.md)

<!-- BEGIN GENERATED POSIX BENCHMARKS -->
## Benchmarks

Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. These rows cover POSIX longest selection, captures and nested/nullable histories, including Unicode floors, invariant folding and LF rewind. Core's generic search, dense counting and scalar progression are not repeated here. Both lanes require valid unchanged UTF-8. Native PCRE2 NFA uses UTF+NO_UTF_CHECK, matching case/newline options, a reusable match buffer and equivalent requested capture output. Newline rows use positive classes: native negated brackets do not implement POSIX's LF exclusion. Ratios cover verified equal-result inputs only, not general POSIX selection or folding equivalence. Native allocations cover CLR traffic, excluding native workspace.

| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |
|---|---|---:|---:|---:|---|---:|
| `capture/ignorecase-disjoint` | Captures | 0.14 | 0.14 | 0 | Equivalent | 1.01 |
| `capture/newline-rewind` | Captures | 0.15 | 0.16 | 0 | Managed faster | 0.93 |
| `projection/short-1` | Captures | 0.08 | 0.10 | 0 | Managed faster | 0.85 |
| `projection/unicode-prefix-1` | Captures | 0.08 | 0.10 | 0 | Managed faster | 0.85 |
| `capture/absent` | Captures | 0.11 | 0.13 | 0 | Managed faster | 0.84 |
| `capture/nullable` | Captures | 0.15 | 0.18 | 0 | Managed faster | 0.83 |
| `capture/empty` | Captures | 0.11 | 0.13 | 0 | Managed faster | 0.83 |
| `projection/unicode-prefix-4` | Captures | 0.12 | 0.17 | 0 | Managed faster | 0.72 |
| `projection/short-4` | Captures | 0.12 | 0.17 | 0 | Managed faster | 0.71 |
| `capture/ambiguous` | Captures | 0.87 | — | 680 | Excluded | — |
| `capture/nested` | Captures | 1.14 | — | 680 | Excluded | — |
| `capture/unicode-longest` | Captures | 0.88 | — | 680 | Excluded | — |
| `count/ambiguous` | Count | 0.38 | — | 0 | Excluded | — |
| `cursor/ambiguous` | Cursor | 1.52 | — | 1040 | Excluded | — |

The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.

### Bounded history growth

The same `(a|aa)*` capture operation at three lengths exposes ambiguity cost. Earlier iterations prefer `aa`; ordered native captures differ, so no native ratio is reported. Working peak is observed in untimed funded preflight with a finite policy; it is a conservative admission charge, not exact heap size.

| Case | Input bytes | Utf8Regex (µs) | Managed B/op | Peak working B |
|---|---:|---:|---:|---:|
| `history/length-4` | 4 | 0.93 | 680 | 2320 |
| `history/length-8` | 8 | 1.43 | 680 | 2320 |
| `history/length-12` | 12 | 2.13 | 1920 | 5904 |

### Historical adapter control

SKU is an unambiguous consumer control, not a POSIX selection claim. One timed batch performs 4,096 independent Match calls with reused caller capture storage; the table divides time and allocations by 4,096. Every record's captures and checksum are checked outside timing.

| Case | Utf8Regex / call (µs) | Native NFA / call (µs) | Managed B/call | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `capture/sku007` | 0.12 | 0.12 | 0 | 1.00 |

### Representative policy costs

| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |
|---|---:|---:|---:|---:|
| `capture/ignorecase-disjoint` | 0.17 µs | 0.20 µs | 0.26 µs | 1.45 |
| `capture/empty` | 0.13 µs | 0.15 µs | 0.21 µs | 1.43 |
| `capture/sku007` | 598.05 µs | 709.82 µs | 978.39 µs | 1.41 |
| `cursor/ambiguous` | 1.77 µs | 1.98 µs | 2.07 µs | 1.31 |

Only the empty-capture, longest-cursor and SKU control rows time all policy lanes; SKU costs here remain per 4,096-call batch. Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.

[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `E289B9BD334330D4B7F6F2D0385DE28BDE2E87AAF978EFF6B1C2C38439ABFD28`.
<!-- END GENERATED POSIX BENCHMARKS -->
