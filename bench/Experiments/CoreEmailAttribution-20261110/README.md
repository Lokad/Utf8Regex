# Core email Count attribution

Read-only investigation on clean `90d18c2fe1907b4348947003c5308765c0cfccc7`.
One existing `--measure-compiled-microcost-case industry/mariomka-email-count 1000`
command, within the120s cap; one sample per lane. An existing non-timed
`--inspect-pattern` confirms `Utf8WordDelimitedTokenCount`. No producer change,
confirmation, new instrumentation, status promotion or package/Full repeat.

The subject has6,839,410 UTF8 bytes/6,194,653 UTF16 units; all Count lanes return92.
Public compiled Count1196.340µs, strict well-formed validation838.069µs,
validation-core-only838.238µs, internal token count275.683µs; predecoded compiled
.NET454.035µs and decode-then-compiled .NET8408.979µs. Full coordinate validation
3427.081µs and BCL Utf8.IsValid2740.504µs are separate diagnostics, not the public
Count route. These single-lane observations do not qualify a new parity ratio.

Source trace: `CountCore` selects the owned scalar program because execution kind
is FallbackRegex. `CanUseNativeScalarTokenSearch` proves this no-capture, forward,
case-sensitive token grammar. `OwnedSliceCountFromByte` validates without UTF16
coordinates, then calls `Utf8WordDelimitedTokenExecutor.TryCountWellFormed` under
the same original invocation. The internal compiled-count diagnostic reaches the
same token executor through the existing count router; its phase timing excludes
validation. Legacy/FallbackRegex runtime labels describe an owned managed router,
not a BCL regex engine. The public diagnostics route label is currently `<none>`;
the source/control proof, rather than that label, establishes ownership.

Required whole-subject validation dominates this workload. Token matching is
already faster than the predecoded reference in this observation; no additional
email matcher or weaker validation is justified. The gap remains, sharing the
validation bottleneck identified for Chinese literals. Phase times are not a
composed public speedup or a reason to reopen the checked-access campaign.

Current Core DLL `43DC7D1A390BDE1FA6A37A20A77FA17AD2B66CAC11CDA3B3D7C994E48F227BF1`;
benchmark DLL `99A57D020D1CF55593050C2B24AE8756AB4B9CF097C5B27BC744ABC858771EC2`.
Build log, identities/PDB hash, full observations, scripts and inspection verified
offVM at `artifacts/experiments/core-email-attribution-20261110/evidence.tar.gz`,
SHA-256 `72F54B6F388D3D562C943650080B020D89652860C0FBE16DD1652476574CC71E`.
Immutable dev63 packages/snapshots remain unchanged.
