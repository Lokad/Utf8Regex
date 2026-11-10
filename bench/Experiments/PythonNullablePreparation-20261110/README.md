# Skip Core preparation that nullable Python programs cannot consume

Accepted producer is baseline `d371b1ad1b8edff9416970fabfcefdba32d057a6`
plus the archived patch. All Python findall forms select scalar progression for
nullable patterns, and Core candidate/result/IPv4 proofs require nonempty
matches. Optional Core rendering/compilation is therefore skipped for nullable
nonliteral programs. Exact empty literals keep their live Core value path;
independent FullMatch kernels and all nonnullable preparation remain.

The change replaces one eligibility condition and clarifies its invariant.
No new analysis walk, field, matcher, policy switch, cache or unchecked read.
Scalar instruction/mask hashes are identical. Pattern semantics are still
validated independently. Construction admission covers work/storage actually
performed, so these expressions can fit smaller construction budgets; working,
result and lifetime contracts retain their existing authorities.

One **86.836s** Linux .NET11 RC3 ordinary-process ABBA under180s,15 public
lanes/five samples/process: **300 samples >=55.429ms**. Construction and first
Count are separate; warm complete capture strings/unchanged empty-literal Count
cover both policies. Dispose and consume every result after timing. Common
accepted Core/caller, distinct Python DLL/PDBs,8 CPUs then CPU2 affinity;
default runtime settings. No confirmation or tuned variants.

| Public operation | Before µs | After µs | Change | Bytes/op before → after |
|---|---:|---:|---:|---:|
| construct/nullable-capture | 125.290 | 10.122 | -91.92% | 53696 → 9760 |
| construct-first-count/nullable-capture | 56.566 | 11.119 | -80.34% | 54496 → 10560 |
| construct/nullable-alternation | 66.534 | 15.957 | -76.02% | 60552 → 16144 |
| construct-first-count/nullable-alternation | 67.712 | 17.385 | -74.33% | 61080 → 16672 |
| construct/nullable-dotall | 49.802 | 8.172 | -83.59% | 47520 → 8512 |
| construct-first-count/nullable-dotall | 52.709 | 8.842 | -83.22% | 47520 → 8512 |
| construct/nullable-two-captures | 74.437 | 13.739 | -81.54% | 64320 → 12376 |
| construct-first-count/nullable-two-captures | 77.697 | 15.249 | -80.37% | 65120 → 13176 |
| construct/byte-pattern | 61.588 | 10.874 | -82.34% | 53736 → 9800 |
| control/construct-nonnullable | 68.539 | 113.738 | +65.95%, overlapping | 50353 → 50352 |
| warm/nullable-captured-strings/Validate | 387.462 | 396.558 | +2.35%, overlapping | 111520 → 111520 |
| control/warm-empty-literal-count/Validate | 143.506 | 143.558 | +0.04%, overlapping | 0 → 0 |
| warm/nullable-captured-strings/AssumeValid | 395.847 | 407.640 | +2.98%, overlapping | 111520 → 111520 |
| control/warm-empty-literal-count/AssumeValid | 144.271 | 145.520 | +0.87%, overlapping | 0 → 0 |
| control/core-construction | 62.506 | 78.087 | +24.93%, overlapping | 49344 → 49344 |

All affected construction/first-use ranges are disjoint. Construction allocates
about73–82% less. Warm captured strings+2.35/+2.98% overlap;111520B/op unchanged.
Empty-literal Count stays0B; no warm-throughput or native-parity gain is claimed.

Keep adverse controls explicit: unchanged nonnullable Python construction has
+65.95% pooled median (+45.20µs), with process medians70.730/66.348 before and
125.126/102.349 after; sample ranges53.1–81.4 and54.7–148.8µs overlap. Unchanged
Core construction also moves+24.93% (+15.58µs), overlapping. This bounded experiment
does not resolve those lifecycle movements or establish that unchanged operations
are unaffected by the changed warmup/JIT context. Accept the clear removed work,
allocation and affected gains; retain the uncertainty and check ordinary lifecycle
controls at the coherent delivery milestone. Do not run a tuning/confirmation
campaign or normalize the controls away.

Windows/Linux Release PythonRe **5086 per.NET10/11** pass. Focused134 per target
also pass locally. Orthogonal new checks cover nullable capture/alternation/dotall
and two-capture shapes, scalar-safe empty progression across supplementary input,
replacement, FullMatch, default strict malformed rejection, original cancellation
and both policies. Existing resource, funded-owner, cursor-alias and capture suites
remain unchanged. Exact empty-literal preparation/value behavior stays verified.
No API/package/version change, full refresh or package requalification.

Raw protocol/caller/graphs/PDBs/patch/shapes/samples:
`artifacts/experiments/python-nullable-preparation-20261110/evidence.tar.gz`, SHA256
`38356732AA347431A3AE824753641C305DEBC19EC172A2F6B2A90AB28F13D7AC`.
Full derived observations/process identities/uncertainty are beside the archive.
The before graph is exactly the accepted preceding ASCII-escape after graph,
with common Core `F65EB5D4D176E34691D7711DD8C95116411FA4A736841611F81CB4D257EB6F63`
and Python `E2986B90DE9C9C931769F6780DD926E76BDD9B48B0C38DF00D7524600AB31574`.
The runner verifies the accepted source patch/hash before advancing the VM;
producer metadata records build time, with archived patches/checksums authoritative.
Linux checks archive SHA256
`73A9D57AD997D09A7358FAF48BB77F602DB56E22C31D0E39D9A47270D6F10CCC`.
Both archives are offVM/hash verified; all workers closed. CI is a separate source
gate. Published/staged bytes stay immutable. Continue the performance roadmap.
