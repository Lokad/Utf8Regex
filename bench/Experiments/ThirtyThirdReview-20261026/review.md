# Thirty-third review — 2026-10-26

Checkout `99bea745ad862a66582db779537df54ae6d44711`; existing Release products
identify source `4d89398a4ba2bdee7fdae8b890d6bc672a8f6aac`, version
`0.4.0-rc.2-dev`. Review only: product source and PLAN/history are unchanged.
Only the scratch probe was built, against the existing three library DLLs.

This pass approached the repository through public API truthfulness, aliased
buffers, coordinate-projection complexity, and package delivery rather than
another accounting or checked-access campaign. One live algorithmic issue and
one documentation correction merit a bounded follow-up. An additional buffer
footgun is recorded separately because the existing unchanged-subject rule
already excludes its use.

## 1. PCRE2 slice enumeration restarts coordinate projection for each result (P2)

`Pcre2SliceGlobalMatchCursor.ProjectCurrent` in
`src/Lokad.Utf8Regex.Pcre2/Pcre2SliceDriver.cs:131` has two implementations:

- Unmetered: `Pcre2GroupData.FromByteOffsets` calls
  `Encoding.UTF8.GetCharCount` over both complete prefixes (`Utf8Pcre2CoreTypes.cs:49`).
- Controlled: creates a new `Utf8ProjectionCursor` at zero and projects the
  current match through `Pcre2GlobalCursorProjection`.

`Utf8Pcre2ValueMatchEnumerator.MoveNext` calls this for every slice result
(`Utf8Pcre2CoreTypes.cs:746`). `MatchMany` uses that enumerator when its direct
cursor selector declines (`Utf8Pcre2Regex.cs:985`). This is therefore live public
work, including when callers only read byte ranges. In the controlled case,
ASCII input has constant-time projection, but mixed UTF-8 repeatedly decodes
its prefix. Unmetered prefix character counting also repeats on ASCII input.

Witness: `(?:ab|cd)+` over repetitions of `éaabcd `, eight UTF-8 bytes per unit.
Both unlimited and four-finite instances select `Enumerate=Pcre2Slice`; the
entry dispatch was traced as well as inspecting the retained plan. Every
enumeration and disjoint `MatchMany` result has the correct byte/UTF-16 ranges
and count. Finite policy: 10 million work steps, depth 1,024, working 64 MiB,
result 128 MiB; infinite timeout, no token.

| Subject | Matches | Unmetered prefix extents | Controlled prefix extents |
| --- | ---: | ---: | ---: |
| 4,096 B | 512 | 2,098,176 B | 1,050,112 B |
| 8,192 B | 1,024 | 8,390,656 B | 4,197,376 B |
| 16,384 B | 2,048 | 33,558,528 B | 16,783,360 B |

These are **source-derived sums of the scan ranges**, computed from verified
public match endpoints, not instrumented byte counters, timing results or a
promised speedup. The ranges grow quadratically for this dense-match family.
`projection-route-witness.json`, `projection-scale-witness.json` and the adjacent
probe source preserve route/results and make that distinction explicit. Other
native/direct routes in the route witness are controls; no slice-rescan claim
is made for those rows. Direct and literal-family global cursors already retain
an incremental `_projection` field (`Pcre2ExecutionArchitecture.cs:2862/3287`).

Recommended bounded investigation: retain the existing incremental projection
cursor in the slice cursor and use the existing controlled projector for
successive results. Check monotone endpoint requirements, same-position empty
retry and repeated projection calls; do not introduce another map, ownership
system or execution mode. Remove the duplicate per-result prefix route there;
`FromByteOffsets` still has a real single-match consumer and is not globally dead.
Keep derived coordinates and complete validation, not just byte-only results.

Focused acceptance: public enumeration/MatchMany at nonzero starts with mixed
BMP/supplementary input, empty retries, copied cursors and retained Current;
strict malformed-tail validation, original cancellation, deadlines and each/all
finite controls. Actual work charges may fall as scans disappear; document that
versioned accounting change while preserving original cumulative authority.
One bounded before/after public comparison with an unchanged control should
cover the live Unicode slice route plus an ASCII control under unlimited and
finite policies. Apply the existing 10% allowance; one noisy confirmation only.
This is distinct from the closed Count experiment about projecting values that
Count never reads. Here public result coordinates are required and remain.

## 2. Core's public specification describes three absent APIs (P3)

`SPEC.md:356` says Core exposes `MatchMany`. Section 4.3 at402 describes
`MatchMany`, `Analyze` and `CopyRequiredLiterals` as public extensions. All three
have zero public methods on the actual Core assembly. Current Core source and
`tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt` agree with reflection.

PCRE2 has three `MatchMany` overloads and `Analyze`, correctly listed in its API
snapshot. `CopyRequiredLiterals` is absent from all shipped profiles. This sits
immediately below the specification's statement that the reviewed API snapshot
is authoritative and no second partial signature catalog is maintained.

SPEC is a target contract, not implementation proof, so an absent target API is
not itself an engine defect. The concrete issue is present-tense "Core exposes"
and the failure to identify these as future or profile-specific. Reconcile the
text with the reviewed current surfaces, or explicitly label a still-required
future target with a consumer need. Do not add three APIs to satisfy obsolete
prose or invent overload symmetry. Validation needs a source/API comparison and
working documentation links; no package build or timing campaign is justified.
The reflection counts are in `buffer-alias-witness.json`.

## 3. MatchMany has no local non-overlap rule (P3, contract hardening only)

Safe `MemoryMarshal.Cast<byte, Utf8Pcre2MatchData>` can make the caller's result
destination share its subject array. `MatchManyCore` validates once, then writes
snapshots before discovering later matches (`Utf8Pcre2Regex.cs:943–1035`). There
is no overlap rejection or staging. With pattern `a`, 128 original ASCII `a`
bytes and a five-snapshot destination, disjoint output starts are `0,1,2,3,4`;
overlapping output starts are `0,24,48,72,96`. Four simple native/captured/family
patterns reproduce the mutation. The managed snapshot size is queried with
`Unsafe.SizeOf`, not a manual layout estimate.

This is **not a managed memory-safety failure or a demonstrated supported-input
semantic violation**: the packaged README already requires borrowed subjects
to remain unchanged. The MatchMany XML, however, does not state its local
non-overlap precondition, and the library itself performs the destructive write.
`TryReplace` explicitly supports overlapping buffers; that different contract
should not be inferred for MatchMany.

A small follow-up may explicitly require disjoint storage in MatchMany XML and
the borrowing guidance, preferably rejecting overlap before any writes with a
checked span overlap test. Preserve original pre-cancelled-token precedence,
zero-capacity behavior, finite admission and valid disjoint partial-output rules.
Avoid result staging or a new overlap abstraction just for this unsupported use.
If retaining only the existing caller obligation is intentional, make it clear
locally and treat this as documentation hardening, not a release blocker.

## Reviewed mechanisms that do not justify a new cleanup

- Package qualification builds selected roots, packs once, verifies exact Core
  dependencies, source/PE/PDB identities and detached fixture/feed/cache/lock
  results, then finalizes one new candidate by moving a completed attempt.
  These checks protect real delivery invariants; no demonstrated failure here
  warrants replacing them or repeatedly qualifying unchanged bytes.
- Current cache identity includes effective culture, options/Compiled and
  timeout. No cache semantic defect was established in this pass.
- Direct/global projection cursors already use incremental state; do not replace
  those implementations because the separate slice route repeats scans.
- The prior requested orphan names remain absent from shipped source. A bounded
  lexical check found no private-method names occurring only once across tracked
  C#; this is a search heuristic, not a proof that all methods are reachable.
- PythonRe's current parser-name-map documentation still describes real metadata
  consumers; it is not evidence that the removed generated alias map survives.
- Frozen package/snapshot evidence is identified as historical. The known fresh
  candidate, platform/downstream and cumulative cost gates remain deferred;
  this review neither satisfies nor duplicates them. The known archived-project
  BDN discovery problem is already recorded in PLAN, not a new finding.

No product rebuild, full-suite rerun, throughput campaign, package/snapshot
refresh, VM operation, external consumer change or GitHub API request occurred.
`integrity-before.json`/`integrity-after.json` verify unchanged plans, six product
DLL/PDB files and all 18 protected snapshot/RC1/handoff files. Only ignored review
artifacts were added. No tracked edits or PLAN changes were made.
