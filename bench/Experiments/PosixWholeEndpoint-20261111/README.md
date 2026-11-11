# POSIX whole-only EOF completion — accepted

Baseline4536ec9b. A whole-only acceptance at EOF proves the greatest possible end
at the current earliest start; capture histories cannot affect ERE acceptance.
Return immediately using the existing completion path. Captured operations and
non-EOF endpoints keep their competition, validation and original invocation.
No new storage, selector, executor or public policy.

One **55.245s** .NET11 RC3 quiet-VM ABBA per policy,8 public rows/192 samples,
minimum52.609ms. Explicit distinct POSIX DLL/PDBs; identical Core/caller/controls;
one ordinary process per producer,8CPU startup thenCPU2, normal tiering/PGO.
Complete coordinates checked after timing. No confirmation or tuned variant.

| Public operation on12 scalars | Validate before → after (µs) | AssumeValid before → after (µs) |
|---|---:|---:|
| Whole-only Match |43.984 →0.449|43.447 →0.448|
| Count, including terminal empty |43.471 →0.497|43.011 →0.509|
| Whole-only cursor lifecycle, finite/live/funded |55.917 →0.746|55.266 →0.705|

All ranges are disjoint. Allocations remain32/32/896B per operation and modeled
funded peak/refunds stay unchanged. Captured-history/SKU/captured-cursor and
unchanged Core/.NET controls overlap. Strict captured-history−26.78% is noisy and
not a claimed gain; other control movements, including trusted Core−6.73%, remain
in raw observations without normalization.

Full POSIX Release **253 checks per.NET10/11** pass on Windows/Linux. The existing
independent oracle covers EOF/non-EOF, nullable/astral/nested alternatives and
all output sizes. Finite32-scalar cases now also cover whole-only Match, Count,
cursor aliases switching to captures, terminal empty progression and retained
values. Existing original cancellation/deadlines, refusal and refund tests pass.
Specifications document early completion; numerical work requirements can change.

Hash-verified offVM evidence under `artifacts/experiments/posix-eof-whole-20261111/`:

- Primary/source/pairs/Linux: `9853CB2F04B4249DF83AEB4AFFA6B9E5485A6283B39CCA1B90EBDB2F0F1C8745`.
- Windows checks: `28E07C37C477604F03CCB7E0398E18BC01D1EE2F8978B06C450FF5C6E5250EB0`.

Other endpoints still require longest-match competition. Immutable packages and
generated catalog tables remain unchanged; no full/package qualification here.
