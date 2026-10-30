# PythonRe current-capture scalar coordinates

Producer **5a91f9c4c5e973258af511d6479c16ab6b57e7cb**, baseline
**0128afc2b927c073e0d8293cbf41026332d8b610**. One public cursor query;
no new match-result family, projection framework or Core map fields.

`TryGetScalarRange(number, out start, out end, token = default)` queries current
captures after detailed/range/retained advances. Scalar positions keep primary
bytes and existing UTF-16 units distinct. Successful empty captures return true;
unmatched/absent groups return false/-1. Copied integers survive closure.
ASCII uses byte offsets. Mixed input lazily admits one prefix count per 256 bytes,
then scans at most 255 checked bytes per lookup. Subjects under 256 bytes need no
array. The index counts starts over already validated UTF-8, even across block
cuts. One metered checkpoint per 32-byte scan unit preserves original cumulative
work, creation/current-call tokens and the fixed deadline. EOF/failure/disposal
refund the index synchronously. Depth and result traffic retain their meanings.
See the [contract](../../../docs/SPEC-PYTHONRE.md).

The initial dense UTF-16-map proposal was rejected during source inspection:
it would allocate approximately four bytes per UTF-16 unit just to get scalar
positions. The chosen array is about 1.6% of input bytes plus its modeled header,
with no source copy, captured text, prefix rescans growing with subject length,
manual size estimates or unchecked APIs. Byte-only calls never build it.

Release evidence: **4223 full PythonRe checks per runtime**, including 25 new
coordinate cases and API/source guards; an additional combined hosting case
passes on both runtimes after a test-only addition. No production changes followed
the measured producer. Initial 22 cases also pass; the later test build caught an
underscore lambda parameter shadowing discards, corrected without production
changes. [Verification](verification.txt) summarizes the runs.

Tests cover mixed ASCII/BMP/supplementary/combining text, byte blocks at
253–257/511 bytes, offsets, UTF-16 projection, lookbehind/lookahead, empty/unmatched
groups, alias lifetimes and rejected reentrancy. Half a million ASCII **and Unicode**
scalar queries complete with **32 KiB live working admission**, without text or
a UTF-16 map. Repeated queries add no historical loans. Finite work/working denial,
caller-account refusal, cancellation/deadline during index construction and
result-traffic independence are exercised. Both original/current-call token
identities and the independence of completed move/query tokens are verified.

The combined public host case overlaps two funded programs, replacement caches,
two active cursors, a retained range and retained substitution output on one
allowance across async waits. Invalid compilation/template preparation preserve
other loans and valid caches. Queries work through program closure, then cursor
EOF releases program/index/cache charges while output receipts remain funded.
The exact remaining balance uses only public ChargedBytes and an independent
host loan. Explicit release preserves escaped arrays/integers. No new acceptance
framework or full package/platform repeat.

One [VM comparison](comparison.json) took **16.41s**, minimum sample **79.48ms**,
under a 120s cap. No confirmation. .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4,
Xeon Platinum 8573C, CPU mask 4. Independent 750ms warmup/80ms calibration per
lane, three alternating samples. Public cursor creation, range projection,
consumer arrays, every requested group query and disposal are timed. All arrays
remain alive until after timing; independent expected coordinates/checksums are
checked afterward. All accounts balance after program disposal.

| Public cursor workload | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Byte-only ASCII control | 14.704 | 14.644 | -0.40% | 7664 / 7680 |
| Byte-only Unicode control | 19.612 | 20.086 | +2.42% | 10808 / 10824 |
| Query scalar positions, ASCII/named capture | 21.670 | 20.613 | -4.88% | 13312 / 13328 |
| Query scalar positions, Unicode/alternative captures | 20.504 | 35.987 | +75.51% | 11832 / 11848 |
| Query scalar positions, 3328-byte Unicode document | 296.571 | 621.015 | +109.40% | 76184 / 76280 |
| Query scalar positions, finite/live token | 22.653 | 40.382 | +78.26% | 10808 / 10824 |

Controls perform equal public operations. Scalar rows add queries and return
different coordinate units from the baseline byte-range operation; this is a
feature-cost observation, **not an equal-unit speedup, parity or catalog Status
claim**. Every numbered group is queried, including unmatched alternative slots.
Both lanes are checked against their respective independent expected units.
The ASCII timing improvement is an observation, not a claimed kernel speedup.
Two cursor fields add 16 actual bytes; the longer mixed subject additionally
acquires an 80-byte array. The small mixed subjects are under 256 bytes and
allocate no index. Repeated bounded scans have a real Unicode cost; retain this
evidence for actual consumer-driven optimization rather than hiding it or starting
another speculative index/cache campaign. Existing match algorithms/SIMD remain.

[Driver](driver.cs.txt), [project](driver.csproj.txt), [console](comparison.txt)
and [preflight](preflight.txt) are retained. Immutable local evidence under
artifacts/experiments/python-scalar-coordinates-20261030/:

- python-scalar-coordinates-5a91f9c4.tar.gz:
  `311FE221EBAC2852BC71A07426FB261EE8A42DF53480F59FF8210FFEF6B9A82C`.
- correctness-logs.zip:
  `1EB6438C924002AA42AA996037297F826F18D56B6F84197D9E9C040805FF008B`.

Archives preserve the source bundle, both DLL/PDB pairs, driver/binary/build/raw
output and correctness logs, including the final combined host test source.
Published/staged packages are unchanged. Hosting source acceptance is separate
from actual consumer/package and final cumulative/current-refresh release gates.
