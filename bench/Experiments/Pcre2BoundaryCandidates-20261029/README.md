# Deferred PCRE2 boundary-prefix candidate filter

**Reverted; no production or snapshot change is promoted.** ba82a2ac let the
existing leading-ASCII-byte collector skip up to 64 leading word/nonword boundary
tokens per sequence, reusing the predicate already used for literal prefixes.
The original VM still verified each retained start. Existing request, match-option
and controlled-scan gates remained. No runtime kind, frame or meter was added.

[Initial inspection](initial-route.txt) found CandidateSearch=None and 42 VM
attempts over the 47-byte date miss. The [candidate inspection](candidate-route.txt)
shows LeadingAsciiSet, 11 attempts and 65 VM steps, versus 111 steps before.
These cold diagnostic timings are not public throughput measurements.

The [first public A/B](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt) and [project](driver.csproj.txt) compare exact c9d9b685
baseline with ba82a2ac on the quiet VM, .NET 11 RC3, CPU2. One **61.01s** run
under a 120s cap; thirteen public IsMatch/Count workloads, one-second warmup,
independent calibration and three alternating ~50ms samples. All timed samples
exceed 48ms. .NET diagnostic lanes are excluded for the Unicode default-word
case; PCRE2 ASCII-word semantics differ. Allocations remain unchanged in all rows.

| Public operation | Before us | Candidate us | Before / candidate B/op |
| --- | ---: | ---: | ---: |
| Date IsMatch hit | 1.893 | 0.795 | 0 / 0 |
| Date IsMatch miss | 6.904 | 3.536 | 0 / 0 |
| Finite limits/live token, hit | 2.968 | 3.138 | 1592 / 1592 |
| Finite limits/live token, miss | 9.801 | 10.355 | 1592 / 1592 |
| No-digit miss, 4096 bytes | 429.101 | 0.330 | 0 / 0 |
| Dense false date candidates | 598.152 | 433.709 | 0 / 0 |
| Dense date Count | 81.618 | 54.400 | 0 / 0 |
| Unicode default-word IsMatch | 0.967 | 0.897 | 0 / 0 |
| Captured date Count | 84.417 | 56.142 | 0 / 0 |
| Nonword-boundary Count | 150.572 | 97.396 | 0 / 0 |
| UCP IsMatch control | 2.980 | 3.374 | 0 / 0 |
| Existing candidate control | 1.018 | 1.035 | 1680 / 1680 |
| Literal control | 0.216 | 0.225 | 0 / 0 |

Default date gains were about 2.4x/2x, but the excluded UCP control cost +13.2%
and finite controls +5.7%/+5.6%. The single allowed
[confirmation](confirmation.json), [console](confirmation.txt),
[filtered driver](confirmation-driver.cs.txt) took **34.38s** over seven rows
with the same source/binaries/settings. Date gains remain, while UCP is **+16.2%**,
finite hit **+8.3%**, finite miss **+10.9%**, candidate control **+5.3%** and
literal control **+6.6%**. No third comparison was run. Eligibility for UCP and
controlled matching remained excluded; the operating-cost effect is unresolved.
The repeated public penalties justify deferral without claiming their cause.

[Correctness checks](verification.txt) passed the complete PCRE2 suite on .NET
10 and 11. New prototype tests compared VM-only controls at every scalar offset,
including boundaries, captures, options, UCP exclusions, the proof bound, retained
aliases, work denial and strict malformed-tail/pre-cancellation handling. Initial
fixture errors around stack-only comparisons and no-match string projection were
corrected; those logs remain archived. These passes do not override the measured
costs.

98e18ca5 reverts the prototype, its dedicated fixtures and changelog claim.
`src/`, `tests/` and CHANGELOG.md match accepted 14ed10d9 exactly; that source
already passed Windows/Linux/both-runtime/disabled-dynamic-code CI 40885532167.
Existing native date ratios **11.80x/11.70x** remain unchanged. No native
qualification, snapshot rewrite, package or full catalog matrix was run for this
rejected candidate. Keep this experiment closed during the current bounded pass.

Immutable archives under `artifacts/experiments/pcre2-boundary-candidates-20261029-deferred/`:

- `parity-pcre2-date-ba82a2ac-deferred.tar.gz`: exact baseline/candidate DLL/PDBs,
  benchmark host, source bundle, original/confirmation drivers and binaries,
  preflight/build/route evidence and both raw comparisons. SHA256
  `580C3C789049F2D53D66D5A9BFD11F5F4E4E60254E7D90CD3DB86DFA90C3A0E8`.
- `correctness-and-revert.zip`: initial/final fixtures/checks and the empty
  accepted-source diff after reverting. SHA256
  `0A3A8856F432B79328DAF65E465BFD40822963AE8BDEFA0C2D0FB26E7AF55A7E`.
