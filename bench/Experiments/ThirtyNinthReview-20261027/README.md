# FullMatch ownership investigation and invariant cleanup

Review baseline 45af53e5; prototype e5d58c1b and measured candidate ff6a5f6c;
final retained source 427f6b34, development 0.4.0-rc.3-dev. No new package,
published snapshot, consumer pin or immutable release artifact was created.

## Delivered and deferred work

- **Defer and revert FullMatch program sharing.** The prototype reused the
  owned VM's endpoint constraint for root EOF acceptance, removing the derived
  instruction copy, Lazy owner and cold construction/interruption machinery.
  Focused semantics and complete PythonRe passed, but representative public
  lazy FullMatch costs increased 16–25%, exceeding the declared 10% allowance.
  The implementation and its proposed contract/documentation changes are
  completely reverted. The existing eager/lazy preparation, admission, retry,
  cancellation and lifetime contracts remain. No dormant alternative or switch
  is retained. [Rejected implementation](https://github.com/Lokad/Utf8Regex/commit/e5d58c1b);
  the complete rejected patch also remains in local artifacts.
- **Test useful cursor invariants.** PCRE2 cursor tests still prohibit embedding
  the large candidate-search value payload and bound actual cursor sizes to
  64 pointer widths (512 B on tested x64). Exact private field-name, wrapper-class
  identity and readonly-field pins are removed. Current reference ownership and
  algorithms remain. A direct raw-program embedding would fail the field/size
  checks, while another reference wrapper is allowed.
- **Remove stale references.** Maintained source/test/benchmark comments no
  longer depend on obsolete IDs in the gitignored PLAN. Their substantive
  invariants remain. The historical PCRE2 ledger links to canonical current
  qualification/performance status instead of calling rc.2-dev current. Original
  historical identities and receipts are untouched.
- **Keep useful boundary coverage.** Nine extra rows in the existing FullMatch
  backtracking fixture cover word-repeat tails/maxima, captured and uncaptured
  Unicode, dot/newlines and lookbehind, through value/context/data/text entry
  points. Prototype-specific ownership/admission tests are reverted with it;
  original lazy construction/resource/authority tests remain.

There is no retained matching algorithm, resource-policy, public-API or lifetime
change. The final shipped-source diff from the review baseline contains comments
only. Strict whole-subject UTF-8/scalars, captures, NB/RTL, measured native routes,
synchronous original-token cancellation, absolute deadlines and useful
work/depth/working/result controls retain their prior implementation.

## One bounded public-cost comparison, closed

[Predeclared protocol](PREDECLARED.md), [raw samples](public-cost.json),
[public preflight](preflight.json), [exact measured identities](measured-identities.json).
The existing [ThirtySixthReview host](../ThirtySixthReview-20261026/cost-host.cs.txt)
and [host project](../ThirtySixthReview-20261026/cost-host.csproj.txt) were reused
without changes. The new lane source/project are beside this receipt. No permanent
benchmark framework was added.

Quiet Ubuntu 24.04.4 VM / .NET 10.0.12, default tiers, one selected CPU7 (0x80).
One 66.62-second comparison: 2.5-second warmup per version/row, 40 ms calibration
target, nine alternating sample pairs. Full capture/text results and public
checksums agree before timing; timed checksums agree. Samples exceed 43.56 ms.
This work launched no concurrent benchmark.

| Public operation | Unlimited change | All finite + live token change |
| --- | ---: | ---: |
| Short captured alternation FullMatch | +9.18% | +1.86% |
| Unicode lazy FullMatchDetailedData | **+19.51%** | **+16.40%** |
| Long terminal word FullMatch | -1.77% | -2.15% |
| Lazy FullMatchToString | **+25.03%** | **+24.05%** |

Unchanged native Python literal FullMatch is -3.35%; unchanged Core compiled
literal Count is -0.39%; the default Core VM Match control is +2.07%. Within-row
max/min spreads are at most 1.07x, with the adverse lazy text rows at about 1.02x
or less. This demonstrates a public regression above the allowance. It is not
an isolated-kernel rejection or an inconclusive confirmation requiring more runs.
No mechanism attribution, confirmation, tuning or third comparison follows.

Lazy/captured call allocations are unchanged. The terminal-word prototype saves
1,680 B/call, but this does not waive other public regressions. Two warmed then
eight retained constructor observations show 61,392 -> 61,280 B/constructor and
62,048 -> 61,360 B/explicit Compile for the selected captured pattern. These are
allocation observations, not constructor timings, retained-heap/RSS bounds or a
claim about every pattern. The allocation savings are reverted with the prototype.

Frozen RC2 cumulative cost remains independently **inconclusive** under its
[existing erratum](../ReleaseCandidate2-20261026/ERRATA.md). This comparison neither
repeats nor discharges that gate, qualifies packages or refreshes old Status.

## Validation and immutable inputs

Prototype local Release: complete PythonRe 3,933 passed; focused PythonRe 394,
Core 959 and PCRE2 1,269 passed. Initial full source CI 40488760469 additionally
found eight Core guard/harness failures per platform: the new optional parameters
violated the mandatory-internal-contract guard, and seven wide-repeat fixtures
could no longer bind their private VM delegate. No matching outcome defect was
established by those binding failures. The prototype was already rejected for
public cost; its signatures are restored without weakening either guard.

After reversion, complete local PythonRe **3,946** passed with zero failures/skips,
including the original preparation/interruption tests and new public endpoint
rows. Final [Windows/Linux source CI 40489364017](https://github.com/Lokad/Utf8Regex/actions/runs/40489364017)
is green at 427f6b34, including complete relevant suites and both
disabled-dynamic-code checks; [job/step evidence](ci-source.json) is retained. A new
source CI run was necessary after the demonstrated prototype rejection, without
another local solution/package/platform matrix. Affected-project builds and
matching binary reuse were used throughout; no build-server shutdown.

[Protected input checks](immutable-inputs.json) preserve original TODO handoffs,
benchmark snapshots and historical candidate/package/symbol/archive bytes.
Rejected DLLs/PDBs, complete local logs and the remote comparison directory remain
under the ignored artifact/VM paths. No package qualification or publication.
