# Prepared Boolean execution requests facts only when needed

Accepted producer `4212d709`, compared with `56f2da7f` on .NET11 GA. The existing
internal helper now accepts bytes and returns the definitive Boolean at offset
zero. Remove the always-handled Try/out/offset plumbing and eager descriptions
in its two callers. The owned scalar branch consumes no description; remaining
compiled branches request their existing facts after definitive byte kernels.
Public APIs, native UTF checks and nonzero/options/finite fallbacks remain.

Route inspection proves the regular FTP-line pattern uses the owned scalar
FallbackRegex kind with SupportsPreparedOffsets. Its no-offset facade already
avoided descriptions; the explicit zero-offset public overload still derived
unused whole-subject UTF-16 metadata. The comparison measures that overload.

One bounded public IsMatch comparison used default native UTF checking and equal
case-sensitive, anchored, default-dot options. Native PCRE.NET reused its match
buffer; both sides returned Booleans. Compilation/input construction were outside
timing. Expected outcomes0/0/1 and exact byte lengths262148/262148/13 were asserted
before timing. Unicode is written as source escapes, and task files use explicit
UTF-8 encoding. Nine samples per lane/case exceeded117ms; each process was capped
at120s, retained int samples at4MiB. Outputs were consumed after timing.

| Public explicit-zero IsMatch | Before µs | After µs | After/before, lower better | B/op before/after |
|---|---:|---:|---:|---:|
| ASCII early rejection | 2.992 | 2.965 | 0.991 | 0 /0 |
| Short Unicode match | 0.244 | 0.229 | 0.940 | 0 /0 |
| Unicode early rejection | 66.051 | 13.827 | 0.209 | 0 /0 |

The large rejection controls moved−0.69/−0.31%; retain the clear Unicode benefit
and effectively unchanged ASCII cost without confirmation. The short-match
native control moved+5.73%, so no precise small gain or general parity claim
follows. Both processes used the same allowed CPU; staged Core/Pcre2 DLL/PDB and
source checksums matched, and caller/native control identities stayed unchanged.

Focused Release Core culture/input and PCRE2 regular/architecture/input/limit
checks pass both runtimes. They cover valid borrowed views, Unicode/scalars,
default/trusted policy, malformed checked suffixes, trailing newlines, nonzero
starts/options, finite denial and original interruption. No execution algorithm,
public validation token, ownership mechanism or switch was added.

Raw samples, identities, caller, route proof and checks are in
`artifacts/experiments/pcre-boolean-preparation-20261113/`. The immutable
[dev81 packages/full catalogs](../DeliveryDev81-20261113/README.md) are unchanged
and precede this source-only improvement. No package or Full matrix was repeated.
