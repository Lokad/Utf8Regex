# Core compilation architecture

`Lokad.Utf8Regex` compiles each pattern through one directional pipeline:

```text
parse -> analyze -> prepare -> immutable prepared regex -> invocation runtime
```

- `Internal/FrontEnd` owns parsing, semantic normalization, feature analysis,
  and optional optimization classification. Analysis describes the pattern; it
  neither observes subject bytes nor constructs an invocation runtime.
- `Internal/Planning` owns inert plans and `Utf8PreparedRegex`, the single
  immutable handoff from compilation to execution. `Utf8RegexPreparer` creates
  search state, verifier programs, fallback metadata, and backend provenance
  once. Plan constructors and properties do not execute input or create
  runtimes.
- `Internal/Execution` owns subject-byte execution and runtime factories. It
  consumes prepared artifacts and may create operation-specific runtime state;
  it does not re-run front-end analyzers to rediscover compile decisions.

The semantic tree deliberately contains no selected-backend provenance.
Backend and fallback-route decisions belong to `Utf8PreparedRegex`, because
they describe lowering rather than regex meaning. Optional specialized family
plans are optimization results, not a general regex IR and not an integration
contract for other regex flavors.

Speculative lowering is semantics-neutral and bounded. Every Cartesian
literal expansion and finite-repeat expansion reserves checked work and output
capacity before allocation. When a budget is exhausted, preparation declines
that optimization and retains the authoritative fallback route. Successful
`TryPrepare` operations return the prepared object that execution will reuse.

Cross-layer imports must be explicit. Do not recreate project-wide internal
global usings, put executor methods on plan values, construct runtimes from
plans, or call front-end analyzers from execution. Architecture tests enforce
these source-level boundaries in addition to semantic and benchmark coverage.
