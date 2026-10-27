# Fixed-policy preparation and maintenance convergence

Development remains `0.4.0-rc.2-dev`. Source commits: SDK `a687446a`, fixed-policy
preparation `c4b2eb53`, cleanup `8a08a228`, diagnostic call sites `28b31f70`.
No product package or benchmark snapshot was created or refreshed. The ignored
PLAN/history retain the active/deferred boundary; this receipt preserves evidence.

## Delivered scope

Core instances with any fixed finite numeric invocation limit omit the verifier,
unmetered compiled runtime and direct emitted structural kernel. Existing nullable
program slots express absence. The immutable policy cannot later become unlimited
on that instance. Scalar/semantic programs, shared captures and native search
payloads remain; public Compiled/Options identity is unchanged. Unlimited
instances retain real runtime and emitted consumers. Tokens and deadlines alone
do not suppress preparation on unlimited instances.

Removed the private verifier forwarding property and changed existing test
assertions to inspect its actual program owner. Read-only inspection reports
missing runtimes honestly; it does not create a diagnostic replacement engine.
Internal construction diagnostics explicitly request their unrestricted runtime.

HOST-INTEGRATION and CHANGELOG document lower construction charges: some patterns
previously denied may now be admitted. Major retained preparation still receives
construction admission and fixed work/depth/instruction/byte backstops. This is
not exact GC, CLR/JIT/native storage or process-memory accounting.

Strict complete-subject UTF-8/scalars, captures, byte/derived coordinates, RTL,
NonBacktracking, result/cursor alias lifetimes, original-token synchronous
cancellation, deadlines and work/depth/working/result/state controls remain.
No matcher, public mode, ownership system or qualification framework was added.

Deleted four identical ExactLiteral/LiteralFamily IsMatch/Match wrappers and
combined equivalent switch arms with identical arguments. Count helpers and real
native selection remain. Deleted the unreferenced six-line PCRE2 debt ledger;
corrected obsolete single-caller ceilings, BCL RTL/folded fallback and unfinished
NonBacktracking comments. Parser layout and production algorithms are unchanged.
The original review/witnesses are preserved beside this file.

## Deliberate SDK selection

global.json now requests stable SDK `10.0.401`, disables roll-forward/prereleases,
and uses the built-in `.dotnet` / `$host$` search paths. CI reads the same file.
An ignored repository-local Windows SDK was installed from the official archive
after SHA512 verification; the system installation was not changed. Ordinary
local `dotnet` resolves 401. SDK/compiler selection and runtime selection remain
separate: this comparison runs on .NET 10.0.13.

The quiet VM is reachable and already has SDK401. A tiny temporary config on its
RAM filesystem proves the same policy resolves 401 through its existing host.
The first default-temp attempt failed because the VM root filesystem is full;
no existing VM files were deleted or installation/build/benchmark attempted. VM stdout,
check source, selected official archive metadata and local dotnet-info are saved.
The SDK archive itself stays in ignored artifacts. Historical binaries/results
were neither rebuilt nor reclassified as current.

## Focused acceptance and source CI

- New finite-policy checks cover each numeric cap separately, ordinary/Compiled,
  supplementary captures/references, RTL, NonBacktracking, malformed input,
  cancellation identity, preserved Options and construction denial.
- Preparation/resource focus: 347 pass. Updating verifier-owner assertions:
  495 pass. Full affected Release Core: 39,633 pass in 61 seconds.
  The first full run failed 44 reflection assertions against the removed private
  property; direct program-owner assertions replace them. No public operation
  behavior failure was observed in that run.
- Final source API/inventory/routes/RTL/preparation focus: Core 142 pass.
  Adapter resources/cancellation/native/cursor/API/construction: PythonRe 115 and
  PCRE2 65 pass. Affected projects build incrementally; matching binaries reused.
- Final benchmark/library closure and all three generated-page verifiers pass.
  Closure found two benchmark construction callers missing the new required
  internal argument; `28b31f70` fixes them. The earlier source CI failed on those
  callers. Final Windows/Linux source CI passes at `28b31f70` in
  run 39989918574,
  also recorded in `ci-source.json`. CI builds once per platform and tests all
  four existing source fixtures; no package/platform matrix was added here.
- Four snapshots, nine frozen unpublished RC1 files and five original TODOs
  retain exact SHA256 identities. PLAN.md, PLAN.HISTORY.md and .dotnet are ignored.

## One bounded comparison

`binary-identities.json` freezes same-SDK before/after Core DLL/PDB identities.
Before C# is `9579aab0`, rebuilt with SDK401 before C# edits; after is `28b31f70`.
The SDK change therefore does not confound the preparation comparison. Public
driver references only Core and invokes public production operations in separate
assembly load contexts on one logical processor. No isolated kernel costs are
reported as public costs.

Constructor comparison uses fixed four-instance batches: one warm batch and
three alternating paired samples, sixteen constructions per version/pattern.
No self-calibrating dynamic-code construction loop. Allocated bytes are:

| Constructor | Before B | After B | Reduction |
| --- | ---: | ---: | ---: |
| Finite compiled simple | 62,080 | 51,560 | 16.9% |
| Finite compiled date | 86,000 | 69,720 | 18.9% |
| Finite compiled structural family | 113,472 | 53,312 | 53.0% |
| Unlimited compiled date control | 86,000 | 86,000 | 0% |

Constructor elapsed samples are descriptive; this short comparison does not
establish a stable construction speedup or measure CLR/JIT/native allocations.

Public operations receive 2.5 seconds warmup per version/lane, individual
calibration toward 40 ms, and nine alternating paired samples. Checksums are
verified before and during timing. Total comparison wall-time is 37.34 seconds.

| Public operation | Before ns | After ns | Change | Allocated B/op |
| --- | ---: | ---: | ---: | ---: |
| Finite compiled simple IsMatch | 377.34 | 388.10 | +2.85% | 1,792 |
| Finite compiled date Count | 267.17 | 278.00 | +4.05% | 112 |
| Finite compiled structural family Count | 44,209.80 | 43,772.40 | -0.99% | 3,664 |
| Finite compiled scalar captured MatchDetailed | 1,976.14 | 1,950.14 | -1.32% | 6,408 |
| Unlimited compiled date Count control | 247.91 | 249.69 | +0.72% | 0 |
| Unlimited compiled literal Count control | 1,383.67 | 1,390.98 | +0.53% | 0 |

All allocation counts are unchanged. Accept within the 10% affected public-cost
allowance. Raw samples include noisy outliers, including one captured-Match
baseline outlier; the reported statistics are medians. Controls stay below 1%
change. No confirmation or further timing campaign was needed. This is bounded
Windows source evidence, not statistical proof of every workload/platform cost.

## Remaining release boundary

Fresh immutable common-version packages with exact Core pins, selected platform/
API/package/provenance and isolated Awk/Lython/PCRE2 consumers remain deferred.
Actual-package/host finite costs and cumulative <=10% execution against fixed,
correctness-equivalent `844dcbad` remain separate acceptance gates. Source timings,
test counts and this receipt do not satisfy those gates. Frozen RC1 must not
publish. Closed checked-access/accounting/layout campaigns stay closed; another
broad review is not a release prerequisite.
