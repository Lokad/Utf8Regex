# Native PythonRe compilation and discovery

Public operating-cost checkpoint for producer `c0361d44` against fixed
`c4bb82d7`, before the PythonRe parser/renderer migration. The baseline is
correctness-equivalent for these selected cases. The separately fixed scoped-
verbose-space defect is excluded from timing.

The quiet Ubuntu VM used .NET 11 RC3, Release, processor affinity mask 4.
One capped 120-second run completed in **86.767s**, including ten cases and
34 lanes. Each lane warmed for two seconds, calibrated independently toward
80 ms and retained three alternating samples. Every measured batch exceeds
20 ms. Public results, complete literal string values and funding balances
were checked outside timing. No confirmation or further campaign was needed.

Warm Count/string-result calls reuse explicitly compiled expressions; compilation
lanes include eager FullMatch preparation and close funded ownership. Source
encoding is prepared outside timing in both versions. This is producer A/B
evidence, not a BCL/CPython catalog refresh or a cumulative release comparison.

| Public operation | Before text / native (us) | After text / native (us) | Allocation before / after (B/op) |
| --- | ---: | ---: | ---: |
| core-literal-count-control | 0.515 | 0.513 | 0 → 0 |
| core-prefix-compilation | 161.944 | 169.806 | 253824 → 253144 |
| core-prefix-count | 116.204 | 113.317 | 1872 → 1872 |
| python-unicode-compilation | 158.982 / 159.579 | 163.332 / 167.879 | 85584 / 85680 / 85760 → 84736 / 84800 / 84880 |
| python-guided-count | 27.695 / 27.903 | 27.819 / 27.682 | 3552 → 3552 |
| python-unicode-count | 161.087 / 154.784 | 147.371 / 155.597 | 4064 → 4064 |
| python-finite-count | 207.026 / 206.593 | 204.047 / 203.998 | 3992 → 3992 |
| python-literal-string-findall | 4.944 / 4.949 | 5.009 / 5.007 | 536 → 568 |
| python-comment-count | 27.832 / 27.786 | 27.596 / 27.946 | 3552 → 3552 |
| python-funded-finite-count | 105.874 (funded native) | 103.633 (funded native) | 4016 → 4016 |

For Unicode compilation, funded native compile+close is separately
**174.002 → 183.041 us**, with **85,760 → 84,880 B/op**.
The three unfunded before/after increases are 2.7% (Python text), 5.2%
(Python native) and 4.9% (Core text). Matching allocations are unchanged.
Explicit literal FindAll string output adds **32 B/op** and about 1.2–1.3%
in this workload: the canonical string is now decoded once on actual demand
per invocation, instead of retained during program construction. This is an
intentional cost/lifetime change, documented in the PythonRe specification.

Descriptive medians do not establish precise equivalence or a general speedup.
Unicode Count samples vary; no catalog Status is changed from these results.
This checkpoint preserves native guided discovery and finite/funded public
paths without reopening the closed Core compilation experiment.

## Source correctness and CI

Final local Release source checks passed Core 40,750, PythonRe 4,303 and PCRE2
6,272 tests per runtime (.NET 10/11); the benchmark project builds both targets.
Coverage includes native source reuse, mixed scalars, complete prefix proofs,
captures, limits, cancellation, lazy timeout context and retained cursor aliases.
CI `41079124833` passes Linux/both runtimes/disabled dynamic code. Windows
passed all producer suites but failed a two-second BCL compiled-oracle probe
while cold JIT ran under concurrent load. No producer failure occurred.
The oracle-only allowance was raised to ten seconds; the bounded step count
and producer assertions are unchanged. The complete diff suite subsequently
passed 1,503/runtime locally. Follow-up CI is recorded in PLAN.md.

## Evidence

- [Raw samples](comparison.json) and [exact driver](Compare.cs.txt).
- `artifacts/experiments/python-native-compilation-20261030/` holds the immutable
  VM archive `utf8regex-python-native-c0361d44.tar.gz`, including both matching
  Core/PythonRe DLLs and PDBs, driver/executable, preflight, build logs and raw output.
  SHA-256: `61E6C74AB93E74739E5E87E9327226951F1A3D4FDD2D7BB1B6B40918F6C18A19`.
- The companion correctness/source archive preserves the source bundle, logs
  actually retained by the tools, CI failure and VM identity. Some local test
  runners wrote their pass summaries directly to the session rather than log
  redirection; the counts above record those observed outputs.

No package bytes, benchmark catalogs or historical artifacts were regenerated.
PCRE2 native parsing, metadata/templates, final immutable consumers and current-
source cumulative performance/full refresh remain open delivery work.
