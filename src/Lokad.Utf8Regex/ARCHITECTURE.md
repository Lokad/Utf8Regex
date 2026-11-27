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
  The scalar interpreter covers general patterns. The supported
  `NonBacktracking` subset uses ordered traversal. Eligible Core operations
  can emit managed code to prove candidates or whole results when dynamic code
  is available. Tiered compilation publishes a reusable delegate and leaves
  the prepared instructions immutable. Funded and interruptible calls keep
  their original invocation controls.

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
