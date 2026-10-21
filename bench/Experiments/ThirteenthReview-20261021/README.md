# Thirteenth cleanup and deferred PythonRe eligibility refinement

2026-10-21, development `0.4.0-rc.2-dev`. Original checkout `533d5a39`,
compiled producer `bf042c93`. Measured prototype `2c2e4d94`; final source
`b11afb82` restores the PythonRe production refinement after the cost gate fails.
No package is rebuilt, qualified or published. Original artifacts remain immutable.

## Delivered

- Removed the 14-overload Utf8SearchKernel forwarding facade. Core, PCRE2,
  benchmarks and tests call the same prepared search/AsciiSearch implementations
  directly. Algorithms/SIMD remain; the architecture assertion requiring that
  file is gone while dependency and primitive-result checks stay.
- Removed unused runtime/interpreter/prepared-find/validation/PCRE2/writer entry
  points, preserving live overloads and dynamic IL roots. Deleted the write-only
  cursor UTF16 total and balance-entry Slot/PushBalance parameter. Real derived
  coordinates, balance heads, trail Slot, capture/pop/rollback/RTL/lifetimes stay.
- Actual balance entry size falls 16 → 12 B. The 128-entry chunk's admitted
  capacity falls 2,080 → 1,568 B. The selected balancing Count lanes allocate
  512 fewer bytes per call. Actual Unsafe.SizeOf stays; all work/depth/state,
  major allocation, cancellation and deadline controls remain. Overall cursor
  size stays 1,440 B: no cursor-size saving is claimed.
- Fixed 16 links into ignored local artifacts and nine source README links that
  only resolved in the package layout. Public evidence points to tracked receipts
  or stable repository documents; local-only paths are labeled. Historical raw
  receipts, frozen packages and snapshots remain unchanged.

## PythonRe refinement — investigated, rejected and deferred

The seven reviewed patterns still retain unused Core preparations in the final
source. The prototype shared actual capture/nullability/available-iterator gates
with retention and removed those roots. Tests identified two additional **live**
consumers that must be preserved: canonical FindAll string counting for wrapped
Unicode literals and preparation-free empty-literal FullMatch. Both were restored
before prototype measurement; its complete Windows/Linux source CI was green.

Despite that correctness qualification, captured-class public FindAllToStrings
cost rises **16.77% initially and 11.57% in the only confirmation**, above the
10% allowance twice. The prototype is therefore not accepted. `b11afb82` restores
all three affected PythonRe production files to their exact original git blobs;
`restored-source.json` records this. Public-result and real-candidate regression
fixtures remain without assertions preserving dead backend existence.

There is no extra mode, compatibility switch, new classifier or replacement
framework. Original native/literal/canonical/empty routes and prior completed
retention simplifications remain. Transient Core construction was never removed.
No post-restoration timing run or throughput qualification is claimed. Further
root pruning needs a materially different bounded proposal; this experiment is
closed, rather than another timing or optimization campaign.

**Correction to the twelfth receipt:** `foo(?=bar)bar` retaining Core did not prove
a live lookahead hint. Its FixedDistanceAsciiSets search is declined by the actual
Python candidate predicate. `needle(?=bar)` is a genuine accepted hint, retained
and tested with finite/unlimited controls here. Canonical literal counting and
empty-literal values are separate consumers, not evidence of native iteration.
Historical raw observations are not rewritten or reclassified.

## One bounded comparison and one confirmation

Fixed original versus `2c2e4d94`, identical driver DLL bytes, local Windows and
.NET 10.0.13, default tiers, one logical CPU. At least 1.5 seconds warmup per lane
plus call floors; 35 ms sample floor/45 ms target, five alternating samples;
60-second child caps. The sole confirmation reverses process order with unchanged
bytes/inputs/protocol. These are public operation costs, not isolated kernels.

| Operation | Initial change | Confirmation | Original → prototype B/op |
| --- | ---: | ---: | ---: |
| Python captured-class construction | +32.69% | −8.54% | 56,968 → 56,968 |
| Python captured-class Count | +10.85% | +6.58% | 3,872 → 3,872 |
| Python captured-class finite-four Count | +19.90% | +6.28% | 3,984 → 3,984 |
| Python captured-class FindAllToStrings | +16.77% | +11.57% | 6,456 → 6,456 |
| Python class FindAll | +8.54% | −2.57% | 7,976 → 7,976 |
| Python canonical-literal FindAllToStrings | +14.37% | −3.46% | 536 → 536 |
| Core balancing Count | +16.85% | +8.53% | 9,976 → 9,464 |
| Core balancing finite-four Count | +20.41% | +5.49% | 10,088 → 9,576 |
| PCRE2 literal Match | +13.16% | +0.58% | 0 → 0 |
| Unchanged predecoded Regex Count | −3.30% | +0.70% | 0 → 0 |
| Unchanged decode-then-Regex Count | +18.27% | +5.26% | 1,048 → 1,048 |

Controls drift and sample spreads reach 1.75x. These observations do not identify
the causal method, prove precise equivalence or establish a final-candidate 10%
bound. Deferral is the conservative gate decision for the Python refinement.
The Core/PCRE2 source is unchanged after measurement; layout/allocation evidence
is independent of throughput. Raw runs, identities/facts and summary are alongside
this receipt. Driver/launcher `.txt` files are evidence, outside shipped tooling.

## Validation scope

Focused Release Core/search/balancing/replacement checks and PCRE2 literal/resource
checks pass. One complete local source boundary caught four empty-literal route
failures and the obsolete 2,080-byte accounting pin. The route was repaired and
the pin updated to the actual smaller entry/chunk, not disabled. The complete
Python suite and affected Core bounds/assertion fixtures pass after repair; the
complete Python suite also passes after source restoration. Diff and PCRE2 passed
the local boundary unchanged. Exact final source CI is in `validation.json`.

[CI 39614774610](https://github.com/Lokad/Utf8Regex/actions/runs/39614774610)
passes at exact final `b11afb82` on Windows/Linux: Core 39,585, Diff 1,504,
PythonRe 3,910 and PCRE2 5,974 on each platform, no failure/skip. The measured
prototype's separate CI 39613417852 is also green; correctness did not override
its failed public-cost gate.

Existing generated page/parity and read-only PE/PDB/source checks pass. Builds
were incremental affected experiments, one local solution boundary, and matching
benchmark closure builds for the prototype and restoration. No normal server
shutdown, repeated package matrix, new qualification framework or skipped fixture.

Four snapshots and nine frozen RC1 files retain their hashes. Frozen common RC1
stays unpublished with known PCRE2 defects. Current immutable packages, final API/
isolated Awk/Lython/PCRE2 consumers, actual-package/host cost acceptance and
publication remain separate deferred delivery work.
