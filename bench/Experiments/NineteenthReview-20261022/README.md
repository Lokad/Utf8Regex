# Semantic dependency guards and bounded route/cost investigation

2026-10-22. Review checkout `34d147d8`, matching producer `2c73f6b4`.
Validation changes: `368dedbe`. PCRE2 Count trial: `d56c2b69`; completely restored
in `009edf3a`. Development remains `0.4.0-rc.2-dev`. No new package was qualified
or published. Frozen unpublished RC1, four snapshots and five original TODOs
retain their bytes. PLAN/history remain ignored.

## Accepted changes

- `Assert-PackageShape` reads the actual nuspec XML dependency/group graph.
  Core may declare no package dependency; each adapter must declare exactly its
  selected exact Core dependency in net10.0. Extra implementation dependencies,
  comment-spoofed/wrong pins, misplaced dependencies and other target groups
  fail. XML attribute order no longer decides acceptance. Existing archive,
  assembly/documentation and native-asset checks remain.
- The existing real-payload provenance fixture script also tests this helper
  using new copies. Core: nine provenance plus three shape cases; PCRE2: nine
  provenance plus six shape cases; original PythonRe manifest control accepted.
  No restore of the injected dependency, repack or package matrix occurred.
- The engine inventory reads typed PE type/member references, including static
  fully qualified BCL calls. It allows RegexOptions, Regex.InfiniteMatchTimeout
  and timeout-exception construction, and rejects matcher/result/escaping and
  BCL implementation types. It inspects consumed Core and built adapters without
  loading them for inspection. Seven in-memory compiled fixtures distinguish
  matching/escaping/construction/results from compatibility. These are dependency
  policy checks, not a security boundary against arbitrary reflective code.
- Removed redundant old receiver/constructor spelling inventories, source-only
  csproj dependency checks, exact adapter consumer filenames and broad BCL-name
  substring inventories. Kept engine-owner/payload, owned parser retirement,
  PythonRe integration-marker and separate PCRE2 isolation checks. Production
  algorithms, public APIs and resource contracts are unchanged in the final tree.

## Route correction

The eighteenth driver inserted Unicode in every shared Core subject. Scalar
dispatch selected the owned engine for its structural/boundary shapes. Even on
ASCII, `record|struct|class` has overlapping prefixes, so that identifier pattern
still selects scalar execution. A queried compiled kernel's availability does
not establish its use by the public facade. The folded-boundary pattern also
selects scalar execution. Earlier public measurements remain factual; their
coverage claim was too broad. The old receipt now explicitly links this correction.

Untimed original/trial Core rows agree. Actual alternatives include:

- `\b(?:record|class|interface)\s+[A-Z][A-Za-z0-9_]+`: no overlapping prefixes,
  selector declines scalar execution; public Count records the native emitted
  shared-prefix/suffix route. This does not prove every removed identifier wrapper
  executes; upper-word eligibility alone remains insufficient.
- ASCII `\bpublic\s+async\b`, compiled: selector declines scalar; Count records
  NativeOrderedLiteralWindow. Source routing takes the direct window FindNext
  for IsMatch/Match, which is the changed argument/forwarding chain.
- ASCII `ab.cd`, invariant ignore-case: native structural automaton, folded
  literal search plan, 585 search candidates for eight matches. Source FindNext
  calls the changed prepared literal filter.
- ASCII `foobar` Match, invariant ignore-case, both directions: literal runtime,
  folded search plan and no scalar selection. Source direct match reaches the
  changed first/last filtered literal search. The exact boundary Match lane also
  exercises the boundary filtering path. Public preflight checks ranges/lengths.

`None` diagnostics and lane names alone are not route proof. Ordinary/compiled
overlapping-identifier and finite-policy lanes remain scalar controls. This is
representative public coverage, not exhaustive certification of private helpers.

## Cost experiment closed: inconclusive

One fixed correctness-equivalent original `844dcbad` versus complete trial
`d56c2b69` pair; identical driver bytes within each pair. Each lane receives
750ms warmup, bounded 35–45ms calibration and five samples with alternating lane
order; child processes have 60s caps. The Linux pair used the bench VM,
.NET 10.0.12, CPU5/default tiering. Root storage was full and background services
busy; only a new RAM directory was used. Its staging vanished between SSH
sessions, so staging/execution/retrieval used one session. No prior artifacts or
services were changed. Initial staging failures performed no timing.

Linux unchanged-control medians shifted +47.4%, +49.0%, +67.4%; target spreads
were also wide. The sole permitted confirmation used Windows/.NET 10.0.13 CPU7,
the same protocol and existing lanes plus two preflighted native identifier lanes.
It is reported separately, not pooled with Linux. Windows controls shifted
+13.2%, +34.8%, +49.5%, with large within-lane spreads. This host was not isolated
from other activity, including small untimed probe builds. There is no further
timing run. Both pairs are retained, without selecting favorable medians.

Neither pair establishes the 10% cumulative public-cost gate or a causal
regression/speedup. Public operation timings include validation, matching and
required projection, with all four finite limits where labeled. They are not
isolated kernel estimates. Subsample allocation noise and changed pooling/tiering
conditions also preclude precise equivalence claims. No Status/snapshot was updated.

## PCRE2 Count: investigated and deferred

The direct backtracking Count loop produces group-zero UTF-16 coordinates it never
reads. Existing diagnostic replay records eight projections for eight Unicode
matches under unlimited and four-finite policies. The trial split shared
discovery/retry advancement from eager public projection: Count recorded zero
projections and still returned eight; public MoveNext retained eager projection,
retained values and failure ordering. Focused/full correctness checks passed.
Basic matching/interruption/storage accounting remained; only removed projection
work could lower charges. No execution switch or second matcher was introduced.

Cost acceptance was not established after the bounded pair and its sole
confirmation. The entire runtime/test/contract-document trial was therefore
restored. Current Count still projects eagerly: the restored producer replay
again records eight projections in each policy. No runtime optimization or
accounting-contract change from this trial is accepted. Final production source
is byte-for-byte identical to `2c73f6b4`; the accepted work simplifies validation.

## Validation and remaining delivery work

Engine inventory: 12 Release checks pass. Full Core: 39,586 pass in 1m23s.
Trial PCRE2 focused: 66 pass; full: 6,009 pass in 10s. After restoration, the
affected 62 Release checks pass; prior unchanged production PCRE2 suite has
6,005 cases. Final Windows/Linux CI and exact revisions are in validation.json.
The CI boundary runs all four fixture projects. No source test is skipped.

Final benchmark/library producer, PE/PDB and compiled-source preflight accepts
restored source. Generated README parity and PythonRe/PCRE2 pages are current.
Matching builds were reused; no local solution/package/platform matrix or build
server shutdown was repeated. This receipt preserves raw paired data, drivers,
dependency fixtures and route observations without another qualification system.

Still open at the next actual delivery boundary: a fresh common-version immutable
candidate, exact Core pins, final isolated Awk/Lython/PCRE2 consumer/package/API/
provenance checks, and useful actual-host/finite operating-cost evidence. Existing
source evidence is not a new package qualification. PCRE2 projection work stays
deferred; wider profile completion must not block Core/PythonRe delivery. No
completed checked-access, value-layout/window or broad accounting campaign reopened.
