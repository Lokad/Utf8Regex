# PCRE2 funded template destinations

Producer `0ed1f519`, fixed baseline `dc55e585`. The program owner now supports
native-template `TryReplace` with byte starts, substitution/match options and an
optional separate working allowance. It reuses literal/global/detailed/Extended
execution and native parsing, without another result type or matcher.

## Admission and output

Funded compilation prepays the existing lazy cache's maximum: **4MiB payload plus
bounded metadata**. This raises modeled program admission, even before use; it
does not allocate that payload eagerly. It avoids new per-entry leases or a later
accounting transfer. Ordinary cache bounds remain unchanged. The sampled complete
program loans are 4,260,400–4,299,968 bytes, including compilation metadata.

Cold key/parser capacity borrows the existing invocation ledger before allocation
and stays admitted through output. It is not funded by acquiring a peak after
compilation. Frozen keys do not receive a second working charge. Unlimited numeric
heap limits retain host admission. The existing construction work/depth/instruction
and preparation byte backstops remain, with original cancellation/deadlines.

Detailed/native-value destinations copy directly from the live output writer;
no-match destinations copy the validated subject. This removes the final array
and its former working-loan gap before copying. Ordinary destination APIs share
completion. The literal destination driver and measured native paths remain.
Short destinations stay unchanged, including required-length reporting when
requested. Interruption may leave partially copied output, as before. Removed
intermediates can lower route-dependent numeric result traffic; no control is
disabled. Retained cache state remains covered until program close and the final
active call; temporary loans refund synchronously.

## Bounded public costs

One quiet-VM Release/.NET 11 RC3 comparison, affinity mask 4, completed in
**54.379s** under a 120s cap: ten equal-result cases/26 lanes. No confirmation was
needed; this experiment is closed. Each lane warms 1.5s/at least 1,000 calls,
calibrates independently to 80ms and takes three alternating samples. Minimum
sample **60.463ms**. Compilation/setup is excluded; complete operations include
validation, native preparation, matching, copying and synchronous refunds.

Existing public operation medians:

| Operation | Before (us) | After (us) | B/op before -> after |
| --- | ---: | ---: | ---: |
| Unchanged Core Count | 0.5605 | 0.5594 | 0 ->0 |
| Unchanged PCRE2 Count | 0.5177 | 0.4997 | 0 ->0 |
| Text-template Replace control | 5.7467 | 5.7823 | 27336 ->27336 |
| Native-template Replace control | 6.0708 | 5.8635 | 27336 ->27336 |
| Native literal destination | 2.2509 | 2.2449 | 120 ->~120 |
| Native capture destination | 5.8373 | 5.8539 | 27336 ->27288 |
| Finite/live capture destination | 10.0451 | 9.8514 | 27336 ->27288 |
| Extended destination | 3.4335 | 3.3706 | 416 ->~384 |
| Cold 32 destination templates | 10.4388 | 10.0867 | 30280 ->30232 |
| No-match destination | 2.4464 | 2.3107 | 4024 ->3992 |

Existing timings vary approximately -5.6% to +0.7%; do not claim precise
equivalence or a matching-catalog speedup. Destination allocation savings are the
removed 32–48 byte final arrays on these subjects. Tiny fractional B/op readings
remain in the raw data rather than being silently normalized.

The funded API is new: there is no funded baseline. These are feature costs
against the candidate's existing unfunded live-token API, not claimed funded
speedups/regressions:

| Operation | Existing / new funded (us) | Existing / funded B/op |
| --- | ---: | ---: |
| Literal destination | 2.2449 /2.4523 | ~120 /~512 |
| Capture destination | 5.8539 /6.3321 | 27288 /27528 |
| Finite destination | 9.8514 /10.7385 | 27288 /27528 |
| Extended destination | 3.3706 /3.6932 | ~384 /~800 |
| Cold 32 templates | 10.0867 /11.7658 | 30232 /30336 |
| No match | 2.3107 /2.5785 | 3992 /4232 |

All substitutions use a live token; finite rows retain work/depth/heap/result
limits. Lightweight atomic program/working host counters admit real loans.
Working loans return to zero after every operation, program loans remain constant
across cache use, and close returns the program loan. Cold templates use five
positions of `${1}` versus `$001`: 32 distinct equal-size keys with identical
expansions. Caller buffers therefore retain invariant output, allowing complete
batch checks after timing without per-call snapshots or host conversions.

## Correctness and delivery scope

Local full PCRE2 Release: **6,439 passes/runtime** on .NET 10 and 11. Later
test-only additions (cache-admission publication, MARK and an unselected invalid
fallback) pass in the final **53/runtime** funded/API run. Intermediate focused
preparation/authority/resource runs also pass. Existing Core/PythonRe code is
unchanged; this is not another complete platform/package matrix.

Public tests cover text/native patterns, scalar/sparse/unmatched/empty captures,
Extended transformations, MARK, start context, match/substitution flags, lazy
no-match/unselected-branch errors, aliases, untouched short destinations,
overflow length, all numeric limits, original cancellation/deadlines, terminal
shared-root refusal, cache eviction, close during use and synchronous refunds.
A cold preparation/output numeric overlap is denied, then warm reuse succeeds
under the same cap. Cache admission refusal/interruption before owner publication
leaves earlier caller reservations intact. Docs and the API snapshot are updated.

H2/H3 producer replacement boundaries now exist across all three profiles. Fresh
immutable common-version packages/exact pins, actual consumer acceptance, current
full sub-hour refresh and fixed-baseline cumulative costs remain delivery work.
Do not repeat native-parser campaigns or hide the new funding cost. No catalog,
cumulative or package qualification claim is made by this receipt.

## Evidence

Adjacent [raw comparison](comparison.json) and [exact driver](Compare.cs.txt).
Immutable local archives in `artifacts/experiments/pcre2-funded-replacement-20261031/`:

- `utf8regex-pcre2-funded-0ed1f519.tar.gz`: before/after DLL/PDB/builds, driver,
  source bundle, preflight and raw outputs. SHA-256:
  `6C84B1560240039A9A8FF8064AFD9A0C61932EC2D6FE4B903C9FC307231A25C7`.
- `source-correctness-logs.zip`: source bundle and local build/full/focused logs.
  SHA-256: `A073F80859ADBAB03FB63AEFB9E34D3525B8B8CDA42CDA97A1C27CB4608A4C46`.

Published/staged package bytes are unchanged.
