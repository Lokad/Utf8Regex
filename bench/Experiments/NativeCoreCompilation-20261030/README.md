# Core native pattern compilation

Producer `63a9955577ce57c670faa5d8f720b2e8b3650a80`, frozen text-compilation
baseline `b9adddda5706f7d05afdb52f523e35e4333ef795`. This qualifies the connected
public UTF-8 compilation flow, including funded ownership, rather than a parser
helper. It makes no package, comparator Status or cumulative release-baseline claim.

Core accepts borrowed strict UTF-8 through the authoritative grammar and existing
scalar/native programs. Retained source/literals own admitted bytes. Optional
pattern text is absent during compilation; explicit `Pattern` projects a string.
Value enumerators retain the regex for lazy timeout diagnostics. Construction
timeouts omit native pattern text after expiry. Text-null tests now disambiguate
the added span overload explicitly. See [Core](../../../docs/SPEC.md) and
[migration](../../../docs/MIGRATION.md) for contracts.

ASCII literal lowering, Cyrillic Count preparation and the balanced-literal query
consume byte payloads directly. Text-only prefix proofs stop at an unrepresented
byte fragment, preserving completeness and branch coverage. Native capture
projection uses the existing scalar program instead of UTF-16 literal maps.
Complete native prefix optimization, UTF-8 metadata access and adapter/template
bridges remain separate unfinished work.

## Correctness

Windows Release: complete Core **40745/runtime**, affected PythonRe **970/runtime**
and PCRE2 **1015/runtime**, on .NET 10/11. Quiet Linux VM: **278** focused .NET 11
checks covering public native compilation, Cyrillic/native routes, resource
admission, Core API inventory and memory cursors. See [verification](verification.txt).

Public fixtures compare grammar/selection, Boolean/count, legacy results and
byte-range capture histories against the existing text surface, after overwriting
the source buffer. They include supplementary/combining/NUL text, scoped folding,
NB/RTL, full-prefix assertions, scalar-aligned byte starts, strict malformed input,
syntax units, original/current cancellation, all four construction limits,
independent construction/matching deadlines and funded program/cursor/result
aliases across owner close and awaits. No unchanged package matrix was repeated.

## Bounded public costs

Quiet Ubuntu 24.04.4 VM, Xeon Platinum 8573C, affinity mask 4, pinned
.NET 11.0.0-rc.3.26522.148. Existing paired-driver protocol: independent 80 ms
calibration, three alternating samples, live result/checksum consumption after
timing, allocation counters and synchronous funding-balance checks. Each launch
has a 120-second outer cap. Numeric policy: 16M work, depth 4096, 4 MiB working
and result capacity. This receipt makes no live-token performance claim.

The [first run](comparison.json) took **73.375s** with 2s warmup/lane; its shortest
sample was **50.644ms**. Medians below are microseconds per public operation.
Funded construction includes explicit close; pattern encoding is outside timing.

| Operation | Before text | After text | After UTF-8 | After funded UTF-8 |
| --- | ---: | ---: | ---: | ---: |
| Literal Count control | 0.547 | 0.547 | 0.548 | 0.710 |
| Scalar Count | 90.094 | 89.600 | 89.893 | 89.931 |
| Unicode common-family Count | 123.295 | 127.744 | 105.113 | 103.203 |
| Finite scalar Count | 91.084 | 90.679 | 90.613 | 90.767 |
| Scalar Compile | 49.980 | 51.027 | 50.623 | 54.005 |
| Unicode common-family Compile | 171.301 | 172.235 | 122.855 | 133.261 |
| Cyrillic Count | 3.315 | 3.707 | 3.785 | 287.374 |

Unfunded Count allocations are unchanged at 0/3872/1872/3992/0 B per operation,
respectively. Funded operations use 144/4016/2016/4016/144 B. Scalar construction:
55392/55400/51840/51952 B; Unicode common-family: 253816/253824/97728/97840 B.
The extra text-facade field costs 8 B. Lower native construction allocation also
reflects text-only optional proofs declining; it is not evidence that their
optimization migration is finished.

Cyrillic samples drift sharply within the first run (before 2.21–3.47 us; after
text 2.40–3.78 us). The [single confirmation](confirmation.json) used 4s warmup,
the unchanged literal control, and a before-text funded control: **47.038s**,
minimum sample **56.868ms**. Cyrillic medians were **2.363/2.403/2.396 us**
(before text / after text / after UTF-8), with unchanged zero allocation.
Funded before-text/after-native medians were **147.441/147.683 us**, both 144 B.
The funded path's substantial metered-route cost therefore predates native
compilation and remains visible; it is not a native-compilation regression.
Literal controls were 0.542/0.505/0.416 us, with funded before/after 0.437/0.445 us.
Run-to-run variation prevents treating these points as precise equivalence or a
catalog speedup. Stop measuring this slice; no further confirmation is authorized.

A driver-edit encoding failure briefly launched the unchanged first driver in
place of the intended confirmation. Its exact process/cwd was verified and it
was stopped; partial output is archived as `aborted-unchanged-driver.*`, not used
as qualification. The corrected confirmation above is the only completed one.

## Immutable evidence

Raw builds, Linux correctness, both DLL/PDB pairs, source bundles, driver builds,
preflight, measured samples and the aborted launch are retained in
`artifacts/experiments/native-core-compilation-20261030/`:

- `native-core-compilation-63a99555.tar.gz` SHA256
  `8D4465A02A4ED902FB3969FB118967434E0AE61AA399CA3C8497C1490106BD47`.
- `correctness-logs.zip` SHA256
  `70FA48AA5A3AE01D3A391A435B6854DAC295D8C61ACB7E79C5A76C6B2546720C`.

Both raw JSON files identify loaded binary hashes. The immutable archive includes
the exact [first](Compare-first.cs.txt) and
[confirmation](Compare-confirmation.cs.txt) driver variants. Socket files in the
SDK temporary directory are intentionally not archive data.
