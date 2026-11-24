# Core compilation architecture

```text
parse -> analyze -> prepare -> immutable instructions -> invoke
```

- `Internal/FrontEnd` parses the pattern and derives semantic facts, capture
  numbering and candidate-search information. It works independently of the
  subject and its invocation state.
- `Internal/Planning` lowers the analyzed pattern into `Utf8PreparedRegex`.
  The handoff carries the scalar program, capture catalog, and prepared
  literal, search and cursor plans. It borrows the capture catalog and
  releases the parse tree after compilation.
- `Internal/Execution` selects an engine from the prepared capabilities.
  The scalar interpreter covers general patterns. Core with `Compiled` can
  emit bounded forward programs to verify Boolean, whole-range and Count results.
  A proven delimiter can let ordered alternatives share an emitted
  continuation. The admitted extent can bound a decimal run with a proved
  endpoint. Search applies invocation checks at each admitted origin.
  Eligible ordinary Core calls accumulate visits from complete short
  absolute-origin proofs and from unanchored checks admitted to run the
  entire emitted proof. Search and Count contribute their totals once per
  scan, including checks that entered the general matcher.
  Construction admits separate tier state for eligible programs and keeps
  the lowered program fields immutable. One caller claims bounded
  compilation and publishes the delegate for future eligible calls.
  Funded and interruptible calls retain their original paths.
  The scalar interpreter handles capture output. The supported
  `NonBacktracking` subset uses its own ordered traversal. Eligible literal
  families also use emitted delegates.

The matcher consumes caller-valid, unchanged UTF-8 and publishes byte ranges.
Candidate discovery can narrow a search or prove an endpoint. The invocation
still owns match order, capture publication, cursor progress, cancellation and
resource limits. Shared search kernels serve Core and the adapters.

Preparation admits major buffers and bounded expansion before allocating.
Optional search preparation can decline an optimization when its allowance is
insufficient. Retention caches charge conservatively and may count shared
storage more than once. See the [resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits).

Keep execution independent of front-end analysis. Plans carry prepared data,
and execution owns runtime construction and subject handling. Use explicit
cross-layer imports. Source architecture tests check these boundaries, while
public fixtures check matching, resource and lifetime behavior.
