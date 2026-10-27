# Managed access boundary

The checked-access campaign is complete and closed.
Shipped libraries exclude unchecked loads/reads, generated staging accesses,
native imports and BCL matcher/result execution. Actual
`Unsafe.SizeOf<T>()` queries remain permitted: they do not bypass memory bounds
and must not be replaced by manual size estimates. Lython accepts this contract.

Consumed-assembly/source inventories enforce this boundary. Historical package
evidence is specific to the exact DLLs:
[RC1 identities](../bench/Experiments/ReleaseCandidate-20261020/release.json),
[RC2 qualification](../bench/Experiments/ReleaseCandidate2-20261026/README.md).
RC1 is superseded; RC2 is unpublished, and its separate cumulative cost gate is
[inconclusive](../bench/Experiments/ReleaseCandidate2-20261026/ERRATA.md).
Neither inventory proves performance acceptance of development source.

The conversion allowance was up to 10% public-operation slowdown against a fixed
correctness-equivalent original baseline. Isolated kernel timings did not decide
public acceptance. Preserve algorithms/SIMD, scalar semantics, strict validation,
resource admission and interruption. No safe/unsafe mode or new timing campaign
is needed for documentation cleanup.

Detailed accepted/rejected conversions, code-generation evidence and receipts
remain in the
[closed inventory](https://github.com/Lokad/Utf8Regex/blob/0310b50a2d33dc64bd6bc188a1e850df9014c8f8/UNCHECKED-ACCESS.md)
and [versioned experiments](../bench/Experiments).
