# Scoped writer inputs and truthful PCRE2 partial decisions

Final implementation source `7650e7a5`, development `0.4.0-rc.2-dev`.
Source commits are pushed; Windows/Linux source CI passes in
run 40330818731,
including both disabled-dynamic-code steps. This receipt preserves the review
at checkout `36d3980a`, existing product `575eeea6`, and the bounded implementation.
No package, published benchmark snapshot, VM or external consumer was changed.
Frozen unpublished RC1 remains unchanged and must not be released.

## Delivered contracts and removal

- `bc244204`: the two public `Utf8ReplacementWriter.Append` span parameters
  declare `scoped` borrowing. Core/PCRE2 callbacks can append local byte/char
  stack buffers and formatting slices without array staging solely for escape
  analysis. Existing copying/encoding bodies, callback lifetime, reset/aliases,
  completed-output UTF-8 validation and invocation admission remain.
- The existing API parameter formatter records ScopedRefAttribute. Its mutation
  fixture detects removal; the Core snapshot records the two lifetime contracts
  in the formatter's ordinal order. No compatibility analyzer or qualification
  framework was added.
- `a98222fd`: remove generic Hard FullMatch and partial NoMatch-from-MARK
  decisions that ordinary matching cannot justify. The existing ordinary
  fallback remains for proven **Soft FullMatch** results, including captures and
  MARK. None-mode matching and literal/curated partial drivers remain.
  Unsupported decisions use the existing NotSupportedException; no general
  partial VM or compatibility mode was added.
- `22bc3695` documents the profile boundary and scoped inputs in public XML,
  packaged READMEs, host/specification and release notes. Matching timeouts use
  Pcre2MatchException/Timeout; eager construction retains its separate
  RegexMatchTimeoutException contract. Migration guidance explains the stricter
  partial boundary and that an unsupported decision is not NoMatch.
- `7650e7a5`: retarget two prior fixture expectations uncovered by the complete
  PCRE2 suite. Ordinary marked-failure inspection uses None and retains MARK;
  the unsupported partial call asserts rejection. The large nullable-pattern
  test retains IsMatch/Count/detailed/None/Soft behavior and tree-release checks,
  while Hard explicitly rejects. No fixture disabled or budget raised.

Strict whole-subject UTF-8/scalars, captures, result/cursor lifetimes, native
paths, synchronous original tokens, absolute deadlines and useful work/depth/
working/result controls remain. A rejected unsupported Hard call can do less
execution work; this does not add valid-input support or exact portable units.

## Compiler and semantic acceptance

Before: real byte/char stack spans in a callback fail CS8352/CS8350; an allocated
array control compiles. After: four external consumers (Core/PCRE2 × byte/char)
compile and return `ok`. Production fixtures additionally use nonzero formatting
slices, mutate the local buffers immediately after Append, and verify the scalar
output under unlimited/all-four-finite policies. Existing callback/resource tests
retain reset, aliases, cancellation/deadlines and malformed-output coverage.

Both Append overloads have identical emitted IL bytes, max stack and locals
before/after. Only ScopedRefAttribute is added to the relevant parameter.
`writer-il.json` records the comparison; no separate writer throughput campaign
was warranted. The real API formatter's scoped/unscoped fixtures previously
produced identical snapshots; they now differ (`api-scope-*.json`).

The existing benchmark-private PCRE.NET 1.5.0/native PCRE2 10.47 oracle comparison
uses nine patterns, six ASCII subjects, three probe modes and unlimited/four-finite
policies. All 324 observations are retained. Before: 26 wrong accepted kinds and
96 explicit rejections. After: zero wrong accepted kinds and 148 explicit
rejections. All 26 wrong outcomes now reject. Another 26 formerly correct ordinary
answers also reject because ordinary matching cannot prove their partial answer;
this intentional support restriction is documented, not counted as new support.
Generic Soft full matches, supported captures/MARK and None controls remain.
Native comparison is confined to ignored tooling, not shipped dependencies.

Affected Release projects built incrementally with matching binaries reused:

- Complete Core: 39,655 pass, zero failed/skipped, 96 s at source `22bc3695`. No Core
  runtime/fixture inputs change in the final fixture-only commit `7650e7a5`.
- Complete final PCRE2: 6,165 pass, zero failed/skipped, 9 s. Its first complete
  run exposed the two prior fallback expectations described above; the affected
  suite was rerun after their contract-specific corrections.
- PythonRe API/member inventory: 2 pass. Initial writer/API focus: 24 pass;
  partial/callback/API focus: 29 pass. Final combined CI covers all source/profile suites.
- No repeated local solution/platform/package matrix or build-server shutdown.

## One bounded public comparison and one confirmation

Frozen product `575eeea6` versus final `7650e7a5`, same SDK 10.0.401/.NET 10.0.14.
Isolated load contexts use the same driver; all 16 public outputs/coordinate
checksums pass before timing. The eight lanes cover preserved generic Soft full
matches/captures/MARK, finite invocation controls, curated/literal partials,
ordinary marked misses and unchanged Core Count. Incorrect old partial outcomes
are not benchmarked as equivalent work. No package/cumulative ranking is claimed.

One 50.20 s comparison, nine alternating pairs, 2.5 s warmup per version/lane and
calibration toward 40 ms. All allocations are unchanged. A single metadata-only
snapshot probe overlapped the initial comparison's early warmup; no builds or
test suites ran during timing. The literal partial row is +11.84% while the
unchanged Core control is +14.78%, with large spreads. Other preserved probe
medians range -5.21% to +5.72%; this does not establish exact equivalence.

Sole 30.65 s confirmation, 17 reversed-first pairs on the uncertain rows:

| Public row | Initial change | Confirmation change |
| --- | ---: | ---: |
| Finite generic Soft full capture | +5.35% | -3.55% |
| Literal Hard partial | +11.84% | -13.49% |
| None-mode marked miss | +5.72% | -2.37% |
| Unchanged Core Count | +14.78% | +15.56% |

The confirmation's unchanged control spreads 5.83x/6.15x; other confirmed probe
spreads reach 3.29x. Confirmed affected medians are within the allowance, but the
disturbed control prevents a settled <=10% performance or causal speedup claim.
Correctness/usability changes remain; cost evidence is explicitly inconclusive.
No third timing run, quiet-sample selection, code tuning campaign or snapshot
Status update. Raw samples and actual binary identities are preserved.

## Remaining delivery work

The fresh immutable common-version candidate, final Windows/Linux package and
Awk/Lython/PCRE2 consumers, actual-package/host costs and cumulative <=10% gate
against fixed `844dcbad` remain deferred. PCRE2 broader completion does not block
Core/PythonRe delivery. Undefined execution option values and optional MatchMany
overlap guards remain deferred; closed checked-access/accounting/layout/retention
campaigns stay closed. All 18 protected files and six frozen baseline DLL/PDB files
match their recorded hashes. PLAN/history remain ignored.
