# Twelfth review: remove unexecuted preparation and redundant carriers

2026-10-21. Development source only, `0.4.0-rc.2-dev`. Original checkout
`cae703538bd6680b5cca5d43021e74e4bdde0755`, matching compiled producer
`cd9844388e970c48a766bcd1275657eb9603784c`. Final implementation
`bf042c93a46c43e7dc8404677e6c53cbfa7cb025`, preceded by `5e6105dc`,
`9180c676` and `552d3abc`. All are pushed. No package is rebuilt or qualified.

## Changes and limits

- Deleted Core's unexecuted Enter/Exit program, lowerer, instruction record/kind,
  retained references, forwarding arguments and exclusive ASCII projection helper.
  Backreference eligibility uses the existing owned-tree feature. Simple matching
  checks its actual branch plan. Real scalar/structural instructions, execution
  trees with capture/native consumers and interruption/admission remain.
- Removed the redundant `Utf8OperationMatch` wrapper and its always-zero branch
  tag. Split, replacement and literal runtime consumers use `Utf8ValueMatch`.
  Real literal-family branch identity remains. No heap saving is attributed to
  removing a value carrier that the JIT could already optimize.
- PythonRe retains optional Core only for eligible iteration, native literal
  results or an accepted scalar cursor hint. The existing hint predicate has one
  shared implementation. The five demonstrated class patterns no longer retain
  Core; literal, family and lookahead-hint controls retain theirs. Some roots are
  still constructed transiently to determine eligibility. This change does not
  eliminate their construction traffic or introduce a second classifier.
- Corrected active comments claiming BCL fallback execution, including parser
  failures and adapter/comparator fixtures. Historical receipts remain historical.

Strict UTF-8/scalars, captures, original synchronous cancellation/deadlines,
NonBacktracking, cursor/result lifetimes and useful execution limits are unchanged.
Construction/cache charges intentionally decrease with deleted work and storage;
they are conservative implementation costs, not stable per-pattern quotas.
Real instruction/state expansion and major allocations still admit before
acquisition. Actual `Unsafe.SizeOf` queries remain.

## Bounded cost evidence — closed, timing inconclusive

One fixed original and final candidate, identical driver DLL bytes, local Windows
and .NET 10.0.13, default tiers, one logical CPU. Each lane warms for at least
1.5 seconds plus its call floor. Calibration targets 45 ms with a 35 ms floor;
five samples alternate lane order. Each child has a 60-second outer cap.
The single permitted confirmation reverses candidate/original process order;
bytes, inputs and protocol stay fixed. No prior campaign is reopened.

Public medians, candidate relative to original:

| Operation | Initial | Confirmation | Allocated B/op, original → candidate |
| --- | ---: | ---: | ---: |
| Core 64-capture construction | +10.60% | −9.00% | 375,840 → 293,080 |
| PythonRe class construction | +1.72% | −9.22% | 36,160 → 36,144 |
| Core simple Count | +5.01% | +2.67% | 1,680 → 1,680 |
| Core simple Count, four finite controls | −3.71% | +3.35% | 1,792 → 1,792 |
| Core simple Replace | +6.98% | +6.84% | 8,816 → 8,816 |
| Core simple EnumerateSplits | −0.60% | +11.43% | 218,712 → 218,712 |
| PythonRe class Count | +5.94% | +3.81% | 0 → 0 |
| PythonRe captured-class FindAllToStrings | +13.03% | +6.31% | 7,736 → 7,736 |
| Unchanged predecoded Regex Count | +1.42% | +12.15% | 0 → 0 |
| Unchanged decode-then-Regex Count | +0.78% | +4.92% | 1,688 → 1,688 |

No affected lane exceeds +10% in both comparisons. This does **not** establish
a 10% bound, exact equivalence or a causal speedup: unchanged controls drift,
and sample spreads reach 2.00x. Both runs remain visible. No further timing run
is authorized by this experiment. Allocation/retention evidence is separate.

The untimed complete 64-capture construction, inside an existing owner, uses
28,322 → 25,993 work units, 453 → 193 instruction slots and 215,584 → 198,528 B
peak covered capacity. The deleted vector contained 260 × actual 64-byte records,
16,640 B of payload. The real 193 scalar instructions remain admitted. The
82,760 B whole-construction allocation reduction includes temporary traffic;
neither it nor modeled capacity is retained heap/RSS. PythonRe's five class
roots change from present to absent; positive native/hint roots remain present.

Raw samples, identities, facts and summary are alongside this receipt. The driver
and launcher are `.txt` evidence, outside the compiled benchmark command surface.

## Validation and delivery boundary

Focused Release checks cover owned lowering/deep preparation, native/Compiled
simple execution, finite invocation and construction, PythonRe retention/public
operations and PCRE2 cursors/resources. Meaningful tests remain; nine Core cases
exclusively checking deleted instructions/duplicate lowerer routes are removed.
Nine PythonRe retention/result cases are added, and stale backend-existence
assertions now describe actual ownership.

One complete local Release source boundary passes Core, Diff, PythonRe and PCRE2,
with no failure/skip. Final orphan-helper deletion is subsequently covered by the
exact final source CI recorded in `validation.json`. Affected experimental builds
were incremental. One benchmark closure build plus its orphan-deletion repair;
matching binaries serve page/parity and read-only PE/PDB/source checks. No normal
compiler/server shutdown, repeated package matrix or disabled fixture.

CI 39607485948
passes at exact final `bf042c93` on Windows and Linux: Core 39,585, Diff 1,504,
PythonRe 3,901 and PCRE2 5,974 on each platform, no failure/skip. The earlier
source run was canceled. Existing generated pages/parity and read-only
PE/PDB/producer/source preflight also pass.

Four benchmark snapshots and nine frozen RC1 file hashes are unchanged
(`immutable-files.json`). Frozen all-three RC1 stays unpublished with known PCRE2
defects. Fresh coherent packages, API/isolated Awk/Lython/PCRE2 consumer checks,
actual-package/host cost acceptance and publication remain deferred. Avoiding
transient declined Core construction is a later improvement, not a claimed result.
