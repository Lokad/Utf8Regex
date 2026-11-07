# Bounded prefix pairs for mixed-width UTF-8 literals

Promote a BCL prefix search for selected 17–32-byte exact needles beginning with
a wider UTF-8 lead and containing an interior two-byte lead. It reuses the two
existing anchor integers. Search the prefix once, verify the complete literal,
then immediately use ordinary full-literal BCL search after a rejected candidate.
Reverse search mirrors that rule. No repeated custom prefix loop, counter, new
field, allocation, owner, mode or engine is added.

Earlier fused/correlated Count routes retain priority. Selected long anchors use
the existing prepared counter; short ASCII and other literal policies remain.
Strict whole-input validation, scalar results, captures, coordinates, lifetimes,
finite 2,048-byte windows and interruption/resource admission remain. Newly eligible
needles use the existing preparation scope: conservative `3 * length` work (at
most 96) and its logical construction depth admission. Tight construction budgets
may need readmission; modeled memory capacity is unchanged.

## Public costs

ONE separate-Default-process ABBA completed in **101.949 seconds**, under its
120-second cap: 20 public workloads, 400 samples, minimum 72.6697 ms. Identical
caller, one Core per ordinary process, 750 ms/minimum 32-call warmup, independent
75 ms calibration, five samples and 20 ms floor. Startup sees eight CPUs, then
affinity selects CPU2. Results are consumed after timing. All 20 allocation
medians remain unchanged.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Core actual CJK Count | 86.099 | 81.251 | -5.63% | 0 |
| Same finite/live | 122.688 | 113.237 | -7.70% | 112 |
| Same deadline/live | 130.714 | 120.525 | -7.79% | 160 |
| Same Compiled | 85.638 | 81.064 | -5.34% | 0 |
| PythonRe actual CJK Count | 86.006 | 81.223 | -5.56% | 0 |
| Same finite/live | 122.675 | 112.488 | -8.30% | 112 |
| Core same, RTL | 86.120 | 81.581 | -5.27% | 0 |

All seven gains persist in both processes with disjoint sample ranges. Finite
Core before-process medians vary 120.30/125.42 us, after 113.18/113.29; PythonRe
120.14/124.19 becomes 113.11/112.45. Keep effect-size uncertainty.

Retain overlapping adverse point estimates: dense hits +3.28%/+0.1194 us, PCRE2
CJK +2.09%/+27.18 us, PCRE ASCII +3.84%/+12.22 ns, false prefixes +1.19%/+13.21 ns,
Latin-1 opposite density +0.92%/+0.1007 us and partial-tail cases. BCL control
-3.29% overlaps and its after-process medians vary 25.25/23.69 us. Do not normalize
controls or assign causal credit to bypassed routes. No confirmation, tuning,
uniform-speedup claim or new catalog Status. [Summary](cost-summary.json) and
[acceptance](acceptance.json) retain every row and process observation.

The input is the frozen 813,478-byte LF fixture, SHA256
`F129E81928C58ECBBA0CCBB63B36679355345248DF057D1E9DED670D6E9C964B`,
also recorded in [attribution](attribution.json). The 23-byte needle is
`夏洛克·福尔摩斯`, with 30 matches. The untimed pair-predicate census is 751 endpoint
gates versus 51 first/interior gates; this is neither actual JIT candidate tracing
nor a speedup. Runtime source explains the endpoint choice. The new prefix path
uses BCL searching; it differs from closed leading-scalar/fused-prefix strategies.

## Validation and preservation

Release Core 330/PythonRe 253/PCRE2 170 checks per target pass on Windows/Linux
for .NET 10/11. Coverage includes lengths 16/17/32/33, borrowed offsets/tails,
both directions, rejected-prefix fallback, random-byte differential checks,
public malformed input, native/finite paths and original cancellation/deadlines.
Both 20-row public preflights and untimed FullOpts pass. No failed build, test,
preflight or timed attempt.

CountExactLiteral is 950→1110 bytes; IndexOfExactAnchored 377→646; the existing
SpanSearch helper remains 126 and exact dispatch 149. These are
[code-generation facts](codegen-summary.json), not public speedups. No unchecked
access, manual size estimates or shipped dependency is introduced.

No package/Full qualification was repeated. All staged dev.52 bytes and frozen
Full52 graphs remain immutable; this is later source work for a future common
candidate. Raw evidence is copied off VM and hash-verified:

- `artifacts/experiments/cjk-pair-attribution-20261107/evidence.tar.gz`:
  `1FE093B049F2BE395A41ACD36F59FD17CCA7D467009A735F8818C0EA63EF7F8D`.
- `windows-evidence.zip` in that directory:
  `31CDA11E220819FEB1E7601644755228863837AC6D4E907B3C3B740338203175`.

Parent `5e69d416`; Core before `00D7A259`, after `8AFEDEC7`; fixed PythonRe
`47870A68`/PCRE2 `3236E597`; caller `6FE4020B`. Exact hashes, source patch and all
samples are retained. No NuGet publication.

Source CI [passed on Windows and Linux](ci.json) for `72195191`.
