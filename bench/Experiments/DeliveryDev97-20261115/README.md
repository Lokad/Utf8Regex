# dev.97 capture preparation and POSIX delivery

Frozen producer source `0e10d86b6038772d724d7bfc828712b919720eb5`, common
`0.5.0-dev.97`, all four packages and symbols for .NET10/11 in
`artifacts/nuget/`. Adapters pin the exact Core version. Earlier0.5.0 and all636
previous feed assets remain byte-identical; eight new files total644.
No publication or downstream adoption was performed.

Includes the accepted [finite Match preparation](../FiniteMatchPreparation-20261115/README.md),
[capture-prefix projection](../DetailedPrefixProjection-20261115/README.md),
[strict detailed preparation](../DetailedPreparationFacts-20261115/README.md)
and [POSIX IgnoreCase](../PosixInvariantCase-20261115/README.md) changes.
The latter preserves longest whole/hierarchical capture selection, original
bytes and invariant single-scalar lowercase equivalence. Frozen0.5.0 excludes
these later improvements; use the common dev.97 graph for this candidate.

Qualification:

- Windows: ten detached fixtures/112908 checks in327.11s, no project references.
- Linux: the SAME Windows package bytes pass the same ten fixtures/112908 checks
  without rebuilding or repacking producers. Source/API/PDB/DLL/feed/cache/lock
  audits agree;14 TRXs and28 restore audits pass.
- Original copied Awk19/Lython118 per target and public native/funded probes pass
  on both platforms, root loans0. Only three producer pins among1565 original
  source inputs changed; consumer runtime/provider code and real checkouts stay
  untouched. Isolated host setup is the existing qualification scaffolding.
- One completed **Full refresh:47m02s**,2821.7091163s, all85 stages pass under
  3600s. One build, both-runtime correctness, disabled dynamic code, all.NET11
  catalogs, paired comparators, lifecycle/scaling, finite controls and verification.
  [Full receipt](full-refresh.json). No extra policy matrix or timing confirmation.
- All four README presentations import these actual measurements. Existing offline
  writers and both-target snapshot guards pass with qualified producer pairs
  unchanged. Source/presentation commita35ef179 is pushed.
- Final source CI43761412643 ona35ef179 is GREEN on Windows/Linux; result retained
  and closed. The later receipt-only commit is excluded by the existing CI paths.

Current ratios are observations of this frozen implementation, not causal gains
over dev.96. Core Chinese literal3.75x/~70.9us, compiled Chinese alternatives
2.62x/~95.7us, email1.89–2.05x/~915–924us and Russian1.69–1.83x/~160–162us
remain slower than predecoded.NET. Python captured strings1.33–1.39x/~299–461us,
captured split1.25x/~317us and callbacks~1.20x/~330–336us remain slower than
CPython. Canonical slower PCRE2/POSIX single operations have no qualified>=100us
row; the SKU batch contains4096 calls. Tiny/noisy rows remain qualified as such;
finite ratios measure same-producer overhead, and native POSIX comparisons cover
only verified overlap. The separate folded POSIX capture cost has a bounded
proof investigation; it is not a general native parity claim.

Contracts remain Core Validate by default/AssumeValid for caller-valid unchanged
text; PythonRe/POSIX require existing valid unchanged text; PCRE2 follows its UTF
window and trusted bypass. Scalar matching, captures, original interruption and
modeled admission remain. Memory controls are not exact heap/RSS limits.

Raw evidence under `artifacts/experiments/delivery-dev97-20261115/`:

- Full `9C90925F1CB6B32922B79CC583078314BE42C99F069207411CC5132A204F716F`.
- Linux `7F3982358869896DD2D4170476C2904BF364A7E73BDD53F0709A1D717D825EC7`.
- Actual Windows compiler inputs `8BA00ACD1D7C671B43586B24EAA247A3FCF8915D813C6D3D9389E36DA1328E0E`.

All original worker/collector handles are terminal0. Evidence was hash-verified
offVM before keeper release. Owned Full and consumer tmpfs scratch was retired
only after zero live process/cwd/FD/map references; caches, packages and canonical
raw remain. Local validation records retain exact assets, outputs and scope.
