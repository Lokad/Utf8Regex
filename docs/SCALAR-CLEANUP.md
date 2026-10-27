# Scalar migration: retained architecture

Core matches complete scalars over strict UTF-8. The UTF-16-parity migration is
complete for the qualified paths; current development and release acceptance are
separate in [release notes](../CHANGELOG.md) and [performance evidence](CURRENT-PERFORMANCE.md).

## Removed

- BCL matcher/result execution and whole-subject decoded fallback ownership.
- Surrogate-half units/VM routing and compatibility capture engines.
- Unreachable ByteSafeLinear, CompiledFallback, SearchGuidedFallback and emitted
  fallback runtimes, their exclusive preparation/selectors and existence tests.
- Retained-object graph accounting, GC-timed lease refunds and obsolete forwarding
  executors. Benchmark-only census/replay logic lives outside shipped libraries.
- Verified orphan helpers and explicit/generated unchecked reads; actual SizeOf
  queries remain.

[Dependency/architecture fixtures](../tests/Lokad.Utf8Regex.Tests) check consumed
assemblies and source boundaries. Scalar, astral, capture, RTL, NonBacktracking,
resource/interruption and lifetime fixtures establish behavior; deleting a name
alone does not.

## Required survivors

| Area | Purpose |
| --- | --- |
| UTF-16 maps/fields and string helpers | Derived interoperability, not subject matching units |
| Pattern/replacement UTF-16 parsing | C# string grammar and per-fragment encoding |
| General/Fallback diagnostic labels | Owned scalar verification or declined specialization; no hidden BCL engine |
| Shared candidate search/native kernels | Live optimizations with scalar verification and complete context |
| PCRE2 CodeUnit | Explicit opted-in `\C` byte behavior, isolated from Core/PythonRe |
| Historical snapshots/receipts | Original evidence, without implying current performance |

Construction, invocation controls and cache accounting are implemented within their
documented modeled scope; total retention/RSS containment and safe defaults are
not delivered. See [host integration](HOST-INTEGRATION.md).
No dormant second engine, replacement accounting framework or new cleanup
campaign is implied by this summary.

Detailed deletion inventories and checkpoint-specific validation remain in the
[original report](https://github.com/Lokad/Utf8Regex/blob/91973e3a/SCALAR-CLEANUP.md)
and [versioned receipts](../bench/Experiments).
