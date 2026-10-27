# Documentation

The development version is **0.4.0-rc.3-dev**. Frozen RC2 is unpublished;
its cumulative performance gate remains inconclusive. Start with the
[release notes](../CHANGELOG.md) for the distinction between source and candidate.

| Need | Guide |
| --- | --- |
| Upgrade from the UTF-16-oriented line | [Migration](MIGRATION.md) |
| Configure limits, failures and lifetimes | [Host integration](HOST-INTEGRATION.md) |
| Core semantics and implementation boundaries | [Core contract](SPEC.md) |
| Python grammar, captures and result shaping | [PythonRe contract](SPEC-PYTHONRE.md) |
| Managed PCRE2 behavior and exclusions | [PCRE2 contract](SPEC-PCRE2.md) |
| Understand measured costs and open uncertainty | [Performance evidence](CURRENT-PERFORMANCE.md) |
| Understand retained migration/access machinery | [Scalar cleanup](SCALAR-CLEANUP.md), [managed access](UNCHECKED-ACCESS.md) |
| Read historical scoped experiments | [Runtime comparison](RUNTIME-COMPARISON.md), [capture assessment](CAPTURE-COST.md) |

Exact APIs live in the profile test snapshots; raw measurements and decisions
remain in [immutable experiment receipts](../bench/Experiments). Historical
versions/paths describe their original checkpoints, not current qualification.
