# Repeated-segment proof and endpoint correctness

**Accepted correctness fix, source 47dbeeeb.** Investigation of the remaining
capitals Count gap found three existing defects in the ASCII repeated-segment
fast path. [The same public probe](probe.cs.txt) against accepted 362ba340 and
the corrected Core demonstrates them in None and Compiled modes:

| Pattern / subject | Before | Required and corrected |
| --- | --- | --- |
| `(?:[A-Z][a-z]+\s*){2,4}?` / `Aa Bb Cc Dd Ee Ff Gg Hh` | Count 2; first range 0/12 | Count 4; first range 0/6 |
| `(?:[a-z][a-z]+\s*){2,4}` / `aaaa` | No match | Count 1; first range 0/4 |
| `(?:[A-Z][a-z]+\s*){2,4}` / `Aa BbC Dd Ee` | First range 0/6 | First range 0/5 |

[Before](probe-before.txt) and [after](probe-after.txt) record Count, value Match
and detailed Match. The ASCII expectations overlap .NET scalar semantics here;
they do not introduce UTF-16 matching or a BCL fallback.

The existing structural proof now requires greedy repetition and pairwise-
disjoint leading, trailing and separator classes. Other shapes use the existing
scalar program. The segment consumer advances a local position and publishes it
only after the complete segment succeeds. A failed extra segment cannot extend
the last successful range. Valid native shapes remain admitted. No allocation,
engine, ownership abstraction or resource-accounting mechanism was added.

[Focused Release checks](verification.txt) pass on both .NET 10 and .NET 11:
public Count/IsMatch/value/detailed/cursor behavior, captures and copied aliases,
outer/inner/separator laziness, overlapping classes, failed tails/separators,
nonzero slices, supplementary decoration, RTL/NonBacktracking, malformed UTF-8,
original-token cancellation and finite resource/structural admission. Relevant
Core/PythonRe/PCRE2 resource and route guards pass. An initial missing test import
and its correction are archived. Package and full benchmark matrices were not
repeated for this fix.

[One public A/B](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt) and [project](driver.csproj.txt) compare exact 362ba340
and 47dbeeeb DLLs in symmetric load contexts on the quiet VM: .NET 11 RC3,
CPU2, 500ms warmup, independent calibration, three alternating samples, a 120s
outer cap. Elapsed **30.33s**, minimum timed batch **47.29ms**. Every timed row
has equal results in producer and predecoded/decode-then-.NET lanes. Incorrect
baseline lazy/overlap/range cases are separate correctness evidence, never
claimed speedups.

| Public Count | Before us | After us | Change | Before / after B/op |
| --- | ---: | ---: | ---: | ---: |
| Full capitals, ordinary | 133405.300 | 129334.000 | -3.1% | 11880 / 11880 |
| Full capitals, compiled | 133320.200 | 128161.800 | -3.9% | 11880 / 11880 |
| 32 KiB, finite limits/live token | 576.551 | 563.730 | -2.2% | 3680 / 3680 |
| Unicode hit | 102.999 | 100.491 | -2.4% | 18208 / 18208 |
| Dense native, incomplete extra segment | 15.537 | 15.573 | +0.2% | 0 / 0 |
| Short required-separator miss | 0.269 | 0.298 | +10.7% | 0 / 0 |
| Literal-prefix control | 11341.960 | 11059.880 | -2.5% | 3600 / 3600 |
| ASCII native control | 3.897 | 3.938 | +1.0% | 0 / 0 |
| Literal control | 69.994 | 71.255 | +1.8% | 0 / 0 |

The separator-miss point costs about **0.03us more**. Preserve that explicit
correctness-fix cost; do not claim a universal <=10% bound or reject corrected
results to retain the old execution. Negative variation in full capitals and
controls is not attributed as a speedup. There is no confirmation, snapshot
refresh or new parity verdict. The capitals 9.64x compiled gap remains active.

[Corpus census](whitespace-census.txt) also finds 1280 non-ASCII scalars,
including nine whitespace scalars, U+00A0/U+2028. An ASCII-only assumption cannot
justify a future mixed-input fast path. Any such work must prove exact scalar
separator semantics, derived coordinates and bounded interruption/metering.

Immutable files under `artifacts/experiments/repeated-segment-proof-20261029/`:

- `parity-repeated-proof-47dbeeeb.tar.gz`: exact before/after Core DLL/PDBs,
  host, source bundle, driver/binaries, public probe and raw cost evidence.
  SHA256 `503FCB5B9291CC9D0773344D777E3DA1F8F1C06836EC7EB25B1BC7B861566629`.
- `correctness-and-probes.zip`: initial/final checks, probe source/output,
  whitespace census, preparation and measurement logs.
  SHA256 `9E792712CCAFA8CFA5269ED674FBC1C6E24E60F18BE85AC27F3D68EDB07DC9F9`.
