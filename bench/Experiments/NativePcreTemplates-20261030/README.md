# Direct PCRE2 UTF-8 template preparation

Producer `78f19fd4`, fixed baseline `dfcc96ad`. The existing PCRE2 byte-template
Replace/TryReplace APIs now parse/cache source bytes without a whole-template
decode. One simple/prepared grammar and the existing literal/global/detailed
drivers serve both source representations. Text conveniences remain available.

The quiet-VM comparison took **52.487s**, twelve cases/24 lanes, under a 120s
cap. The sole confirmation took **48.496s**, six cases/12 lanes, under a 90s cap.
Release .NET 11 RC3, affinity mask 4; independent lane calibration, alternating
samples and full output-batch checks after timing. Setup is excluded; public
operations include validation, first preparation, matching and output. Cold
cycles use 32 equal-size templates through the existing 16-entry cache; warm
lanes reuse one. The destination case reuses one fixed caller buffer with
invariant output. Finite controls and a live token remain enabled where labeled.

Initial warmup: 1.5s/at least 1,000 calls/lane, 80ms calibration, three samples.
Confirmation: 2.5s/at least 1,000 calls/lane, 150ms calibration, five samples.
Minimum samples are **75.947ms / 133.218ms**. This experiment is closed.

## Public operation costs

Initial medians, with actual allocations:

| Operation | Before (us) | After (us) | B/op before -> after |
| --- | ---: | ---: | ---: |
| Unchanged Core literal Count | 0.5439 | 0.5397 | 0 ->0 |
| Unchanged PCRE2 literal Count | 0.4864 | 0.5360 | 0 ->0 |
| Warm text simple template | 1.7732 | 1.8312 | 48 ->48 |
| Cold text capture template | 8.0841 | 8.2625 | 2856 ->2856 |
| Cold text Unicode ~2KiB | 87.7857 | 89.0002 | 33504 ->33504 |
| Warm native simple template | 1.1596 | 1.1570 | 88 ->48 |
| Warm native capture template | 2.5001 | 2.5106 | 512 ->472 |
| Cold native capture template | 7.9661 | 8.4051 | 2896 ->3120 |
| Cold native Unicode ~2KiB | 102.7982 | 85.5335 | 35584 ->43944 |
| Warm native extended template | 4.6112 | 4.5576 | 528 ->480 |
| Warm native finite/live template | 8.4846 | 8.5605 | 4056 ->4016 |
| Native simple caller destination | 2.0554 | 2.0071 | 40 ->0 |

Confirmation medians:

| Operation | Before (us) | After (us) | B/op before -> after |
| --- | ---: | ---: | ---: |
| Unchanged Core literal Count | 0.5619 | 0.5607 | 0 ->0 |
| Unchanged PCRE2 literal Count | 0.5459 | 0.5412 | 0 ->0 |
| Warm text simple template | 1.1959 | 1.2195 | 48 ->48 |
| Cold native Unicode ~2KiB | 96.8845 | 81.7822 | 35584 ->43944 |
| Warm native extended template | 4.6507 | 4.6637 | 528 ->480 |
| Warm native finite/live template | 8.6550 | 8.6028 | 4056 ->4016 |

The initial unchanged PCRE2 Count +10.2% is timing drift, not an established
producer regression: confirmation is -0.9%, with stable samples. Warm text has
large timing-level changes, so do not claim precise equivalence or derive a
matching-throughput improvement. Native warm operations remove 40–48 B/op of
source projection, including zero sampled destination allocation.

Cold native Unicode is consistently cheaper here (-15.6% in confirmation), but
adds **8,360 B/op**. The existing compiler's bounded position tables use byte
offsets rather than UTF-16 offsets; that allocation/earlier-admission tradeoff is
intentional and documented. Small cold native calls cost +5.5%/+224 B/op in the
first run. No sparse position index, new accounting precision, alternative engine
or extra performance campaign was introduced to hide these feature costs.

## Preserved contracts and validation

- Complete strict template validation precedes route selection, even on no
  match or SubstituteLiteral. Unicode scalar escapes, captures/MARK, extended
  quoting/case forcing and option-driven execution keep their existing routes.
  Syntax errors remain deferred instructions, preserving no-match, conditional
  validation and unselected-fallback behavior. String fragments retain surrogate
  repair. No UTF-16 template, final subject string or alternate matcher is needed
  for the native path.
- Warm cache queries borrow the supplied span. Cold keys are admitted/copied
  before retention; their temporary working loan overlaps parser/output capacity
  and refunds with the original operation. Text/native entries share one bounded
  cache but remain distinct. Only Extended separates grammar variants; execution
  flags and actual match/reference resolution stay with each invocation.
- Matching/cursor algorithms are unchanged. Original cancellation/deadlines and
  meaningful work/depth/heap/result limits remain authoritative. Input/template
  destination aliases retain staging/canary guarantees. No new memory governor,
  lifetime protocol, profile framework or source/package qualifier.
- Full PCRE2 Release **6,415 tests/runtime** pass on .NET 10/11, including native/
  text instruction equivalence, production preparation cutoffs, original token/
  deadline injection, key immutability, borrowed warm queries, strict errors,
  aliases, independent grammar-option identity and deterministic temporary refunds.
- An uncacheable fixture increased from 1MiB to 2MiB because smaller native keys
  can fit the old cache limit. It now measures the actual source representation's
  work and still excludes a second compilation. Range-growth fixtures warm the
  native production plan and prove **160/448-byte** peaks, replacing 208/496:
  the former 48-byte decoded-template loan is removed. Producer limits were not
  weakened. Initial failed builds/tests and successful focused/full runs are kept.
- Public package APIs are unchanged. Specifications/package README describe byte
  key ownership, source-unit capacity and the remaining funded replacement gap.
  Latest source CI `41101650853` is queued behind `41099880406` at this checkpoint;
  no cross-platform green claim is made from local tests alone.

## Evidence and remaining work

[First raw samples](comparison.json), [sole confirmation](confirmation.json),
[first driver](Compare.cs.txt), [confirmation driver](Confirm.cs.txt).
The confirmation's inherited Scope mentions destination calls; that case was in
the initial run only. Its six selected row identities and actual driver calls
define confirmation scope. No measurements were rewritten after execution.

Immutable exact DLL/PDB, measured drivers, raw/preflight/build/runtime records:
`artifacts/experiments/pcre2-native-templates-20261030/utf8regex-pcre2-templates-78f19fd4.tar.gz`.
SHA-256: `45230B598E4C5EB16E3B501FA9C812838AB32D1E7FCF07A1DC10756FDDE83E44`.
Companion source/correctness archive `correctness-source-logs.zip`:
`F6CEF1A146151C9FDF3D8D87260A4237711F2A86F05AFDCFA3D4415EF402C668`.

Core native templates, Core/PCRE2 funded replacement/results, final immutable
packages/actual consumers, current-source full refresh and cumulative release
operating cost remain delivery work. No published/staged artifacts or benchmark
catalogs changed. This closes selected public-operation evidence, not release
package acceptance or catalog parity qualification.
