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
  the owned scalar program, native search/verifier plans and route provenance
  once. Native instruction preparation is retained only for live consumers.
  Plan constructors and properties do not execute input or create runtimes.
- `Internal/Execution` owns subject-byte execution and runtime factories. It
  consumes prepared artifacts and may create operation-specific runtime state;
  it does not re-run front-end analyzers to rediscover compile decisions.

The semantic tree contains no selected-backend provenance. Route decisions
belong to `Utf8PreparedRegex`, because they describe lowering rather than regex
meaning. The owned scalar program supplies general execution, including the
supported NonBacktracking route; native literal and structural plans accelerate
their admitted subsets. Internal `FallbackRegex` classification names refer to
owned execution, not BCL matching. Optional family plans are optimization results,
not a general regex IR or an integration contract for other regex flavors.

Engine selection reads prepared capabilities; it does not lower a kernel merely
to label it. The structural linear runtime prepares its optional emitted kernel
once. For compiled identifier families the program borrows that same immutable
kernel for direct Count; ordinary construction still prepares its direct kernel.
A declined preparation is not retried by the program. Invocation state, budgets
and result ownership are separate from this shared implementation. There is no
separate structural-family runtime or emitted-kernel engine kind; actual emitted
capabilities are reported by the selected runtime.

Search dispositions derive from canonical candidate sources, confirmation and
projection. Count, first-match and enumeration control their actual exit and
advancement behavior in execution; duplicate descriptive flags are not stored.

Speculative lowering is semantics-neutral and bounded. Every Cartesian
literal expansion and finite-repeat expansion reserves checked work and output
capacity before allocation. When a budget is exhausted, preparation declines
that optimization and retains the authoritative owned scalar route. Successful
`TryPrepare` operations return the prepared object that execution will reuse.

Cross-layer imports must be explicit. Do not recreate project-wide internal
global usings, put executor methods on plan values, construct runtimes from
plans, or call front-end analyzers from execution. Architecture tests enforce
these source-level boundaries in addition to semantic and benchmark coverage.

Construction admits major buffers and bounded expansion before allocation.
Selected prepared/scalar/IL carriers and small runtime/verifier wrappers use
conservative metadata allowances instead of mirrored transfer descriptors.
Remaining smaller construction carriers retain their existing factories; real
algorithmic state vectors remain separate. Retention caches use a conservative bounded
payload policy; shared storage can be charged again. These modeled limits do not
bound total CLR/JIT memory or arbitrary host retention. See
[HOST-INTEGRATION.md](../../docs/HOST-INTEGRATION.md) for scope and lifetime contracts.
Benchmark-only candidate-count replay lives in the benchmark project; production
search helpers remain in the library where public execution consumes them.
