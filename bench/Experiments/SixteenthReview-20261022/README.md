# Bounded cleanup and deferred execution prototypes

2026-10-22. Original checkout `1a4018eb`, matching producer `08cb2ed6`.
Prototype `36ae0e01` is **rejected for delivery and reverted**. Final source is
`d011e9b6`, development `0.4.0-rc.2-dev`. No package was made or published.

## Delivered

- Removed Utf8FallbackSearchPlan and its unread derived FallbackSearch property.
  Canonical prefilter/candidate storage and execution remain. Tests use real
  sources; the prefilter view test now checks production accept/reject outcomes
  under zero construction capacity rather than preserve the unused wrapper.
- Removed unused exact forwarding overloads: anchored validator(segments),
  BackrefNode(slot,foldCase), PythonReScalarMatchState with token/limits, and
  the five-argument value-enumerator constructor. Its now-unused five-argument
  cursor constructor is also gone. The four-argument invocation cursor remains
  live through mapped/split entry points and is preserved.
- Removed the unread internal Utf8RegexProgram.Compile timeout parameter and
  updated callers. Public timeout APIs and absolute invocation deadlines remain.
- Corrected value-match enumerator remarks: the cursor borrows subject bytes;
  its coordinate-only current result does not retain them. Streamlined obsolete
  raw-dotnet-run prose in AGENTS.md around the existing bench.ps1 workflow.

These deletions remove no live matching algorithm, payload field, SIMD path,
resource meter or ownership mechanism. Scalar/capture/RTL/NonBacktracking,
strict full-subject UTF-8, synchronous original-token cancellation, deadlines,
work/depth/working/result/state controls and retained-result/alias lifetimes
keep their existing contracts. Actual Unsafe.SizeOf queries remain.

## Investigated, then deferred

The prototype removed the unreachable ordered-window emitter and its lowering,
window-only reflection roots/plan fields and route labels. Public operations use
the direct window executor or scalar route first. It also removed unused search
payload copies from operation dispositions, reading classification from the
canonical search plan, and removed newly orphaned candidate-view variants.
A shared Count helper cut beside the window-only methods was restored after the
first build error, before correctness checks. The prototype passed affected and
full Core checks plus both source CI platforms.

Untimed actual x64 layouts in that prototype were operation 896 -> 4 B,
cursor 1,440 -> 544 B, and prefilter candidate 440 -> 424 B; SearchPlan stayed
2,720 B. Identifier kernel sharing/live emission and literal-family emission
remained. Selected compiled window construction allocated 92,248 -> 57,336 B;
boundary literal-family construction allocated 147,152 -> 143,584 B.
These were **prototype-only observations, not delivered savings**.

The bounded public cost evidence did not qualify the prototype. Both changes,
their newly orphaned variants, layout sources and capability tests are restored
to the original source. Do not keep the smaller layout or window-emitter deletion
under a claim of equivalent performance. Their static cleanup rationale remains
valid, but they are deferred together because this one bundled experiment cannot
attribute the cost to either change. No replacement framework, mode, mitigation,
third timing run or new package campaign follows.

## One cost pair and sole confirmation, closed

Linux/.NET 10.0.12, CPU0/default tiers, identical driver DLL bytes; fixed original
DLL versus prototype DLL. Five alternating samples after bounded warmup and
calibration. The comparison uses real public operations and same-size finite/
unlimited literal-family Count. Finite controls: work1M/depth2048/working16MiB/
result16MiB/infinite timeout/no token. Two unchanged BCL controls remain separate.
Both versions preflight results before timing. Construction uses exactly 16 new
instances per expression per process, one warm instance and five batches of 3;
its short observations are separate from the matching gate.

The VM is reachable but root storage is full and background logging services
are busy. New drivers/output used a separate RAM directory and no existing files
or services were changed. CPU preflight selected the most idle observed core;
that snapshot does not establish quiet conditions throughout measurement.

| Public operation/control | UTF-8 input bytes | Initial median change | Reversed confirmation |
| --- | ---: | ---: | ---: |
| core/window-compiled/Count | 464 | +145.34% | +72.19% |
| core/window-compiled/Match | 464 | +65.52% | +83.37% |
| core/bounded-window-compiled/Count | 408 | +66.55% | +65.99% |
| core/literal-family-compiled/Count | 176 | +76.22% | +71.06% |
| core/literal-family-compiled-finite-four/Count | 176 | +70.72% | +77.61% |
| core/literal-family-compiled/Match | 176 | +74.16% | +75.32% |
| core/literal-family-compiled/EnumerateMatches | 176 | +51.23% | +74.46% |
| core/identifier-compiled/Count | 464 | +80.27% | +71.15% |
| core/identifier-compiled/IsMatch-late | 4,115 | +26.48% | +55.35% |
| control/predecoded-Regex/Count | 464 | -2.00% | +38.36% |
| control/decode-then-Regex/Count | 464 | -2.46% | +41.81% |

All selected public medians exceed the 10% allowance in both comparisons. The
confirmation's unchanged controls also worsen 38.36%/41.81%; initial control
medians move about -2%. Raw spreads reach 3.17x. These data do not isolate an
engine cause or prove a specific source-level regression, and cannot be blamed
solely on the VM. They do not establish the required <=10% bound. Conservative
decision: reject/defer the bundled execution prototypes and restore baseline
preparation/layout code. Timing closes after the sole unchanged-protocol,
reversed-process-order confirmation. No further measurement is active.

Matching allocations were unchanged in these rejected runs. The smaller final
orphan/docs cleanup was **not timed again** and makes no performance-acceptance,
construction-saving, RSS or package-cost claim. Old receipts/snapshots and
immutable package bytes retain their original identities and status.

## Validation and remaining work

Prototype: 873 affected Core checks, full Core 39,582 without skips (1m36s), and
1,002 affected PythonRe checks pass. Source CI 39745270379 at 36ae0e01 passes all
four fixture projects on Windows/Linux. After restoration, the full local Core suite passes 39,584 without skips in
1m17s. Final CI 39746403628
is green at exact d011e9b6 on Windows/Linux, all four fixture projects. Untimed
static size delegates confirm restored operation 896 B / cursor 1,440 B /
SearchPlan 2,720 B; no further operation timing. Exact identities are in
validation.json.
Existing generated README parity and PythonRe/PCRE2 benchmark pages verify
unchanged. Read-only producer/PE/PDB/compiled-source preflight accepts matching
final d011e9b6 binaries. Four snapshots/nine frozen RC1 files retain hashes/lengths.
Rebuilding final affected source is required by the rollback, not repeated
qualification of unchanged packages. No normal server shutdown or new framework.

Final source has the previous live engine/preparation/layouts plus the verified
unused-overload/view/docs cleanup. Fresh immutable packages, final API and
isolated Awk/Lython/PCRE2 consumers and package/host operating-cost acceptance
remain the separate delivery boundary. Broader PCRE2 completion and rejected
PythonRe root pruning stay deferred. Historical checked-access/cost campaigns
are not reopened. Frozen RC1 remains unpublished and untouched.
