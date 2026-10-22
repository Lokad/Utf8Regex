# Zero-safe results, selected-profile qualification and borrowed templates

2026-10-22. Reviewed checkout `63f891ec`, matching producer `009edf3a`.
Accepted source: `0c43a021` (default results), `d68e22c3` (profile independence),
`5ee10614` (template borrowing). Development stays `0.4.0-rc.2-dev`; no new package
is qualified or published. Frozen RC1, four snapshots and five original TODOs
retain exact bytes. PLAN/history remain ignored.

## Delivered behavior and removed work

- Core's two packed result words now complement legal coordinates and reserve
  zero for failure. Default/new/zeroed-array results report no match, consistently
  with NoMatch. Genuine empty matches and legal large coordinates are preserved;
  actual Unsafe.SizeOf remains 16. The old behavior was a default-result defect,
  not evidence of ordinary engine false positives. No cursor-layout redesign.
- Core inventories its consumed Core assembly; each adapter inventories its own
  consumed assembly. The existing typed PE member/reference policy is one linked
  test source. Negative/positive compiled fixtures, owner/payload checks, parser
  retirement and profile integration guards remain. No shipping dependency or
  matching algorithm changes.
- Candidate preparation builds only selected library roots, with Core included
  by adapter builds. Core-only and Core/PythonRe no longer build PCRE2 or the
  benchmark host. The manifest records actual library build invocations.
  Exact package graph/pin/provenance/feed/cache/lock/DLL/symbol checks remain;
  SkipBuild still requires matching binaries and candidates remain immutable.
- Removed the native literal-template ToArray helper. Allocating Replace borrows
  its synchronous template; TryReplace copies only for template/destination
  overlap. Existing input-overlap staging and scalar/controlled routes remain.
  Strict input/template validation, capacity-failure behavior, original tokens,
  deadlines and useful resource admission are preserved.
- Condensed CURRENT-PERFORMANCE and unreleased release notes around actual
  consumer behavior and open delivery gates. Original prose remains in git
  history and ignored history; all raw receipts remain. Updated SPEC/XML default
  semantics and build instructions; corrected stale linked-source/CI comments.

## Focused public costs: one comparison, then stop

`cost-pair.json` contains the sole 23.47-second Release comparison on Windows,
.NET 10.0.13/default tiers, one pinned logical processor. One identical driver
loads the two frozen Core DLLs in separate load contexts within the same process;
750 ms warmup per version/lane precedes calibration and nine alternating paired
samples. Minimum observed sample is 30.78 ms. Public checksum/range witnesses
agree. The erroneous default sentinel itself is not timed. No benchmark command,
production mode or qualification framework was added.

| Public operation | Before ns | After ns | Median change | Before/after B/op |
| --- | ---: | ---: | ---: | ---: |
| Literal Match | 161.51 | 148.18 | -8.25% | 0 / 0 |
| Scalar Match | 229.11 | 227.14 | -0.86% | 0 / 0 |
| Finite captured-reference Match | 875.71 | 830.79 | -5.13% | 2,304 / 2,304 |
| Scalar enumeration | 2,013.94 | 1,998.28 | -0.78% | 192 / 192 |
| TryReplace, no hit, 4 KiB template | 1,208.19 | 705.17 | -41.63% | 4,120 / 0 |
| TryReplace, hits | 764.61 | 719.47 | -5.90% | 32 / 0 |
| TryReplace, overlapping template | 750.96 | 721.40 | -3.94% | 32 / 32 |
| UTF-8 Replace, Unicode subject | 839.72 | 802.69 | -4.41% | 64 / 32 |
| Unchanged literal Count miss, 128 KiB | 6,942.09 | 6,874.54 | -0.97% | 0 / 0 |

Selected public medians show no regression above the 10% allowance. Outliers and
spreads up to 5.17x prevent precise equivalence or an attributed throughput gain;
these results do not establish a cumulative/package-wide bound. Allocation
removal is direct: isolated public no-hit TryReplace with 0/4,096/1,048,576-byte
templates allocates 0/4,120/1,048,600 bytes before and zero after. Inputs/templates/
destinations and warm calls are outside that counter. Overlap still allocates
the required copy. Accept these bounded changes; no confirmation/retiming.

## Validation and remaining delivery work

Local Release: Core 39,598 (81 s), PythonRe 3,911 (14 s), PCRE2 6,006 (9 s), all
passing without skips. Focused value 26, replacement/validation/resource 501,
Core inventory 12, PythonRe inventory 1 and PCRE2 source/inventory 5 also pass.
Existing linked-source consumer rewrite and actual qualifier build statements
pass three profile/eight selection+SkipBuild cases with external dotnet calls
recorded. This source-copy/build-selection check does not claim new package
qualification. Shared source links remain inside detached consumer trees.

[Windows/Linux CI](https://github.com/Lokad/Utf8Regex/actions/runs/39780440603)
is green at `5ee10614350d139ee7cef3891fc7178b4beb4e6c` across all four fixture
projects. Final producer/PE/PDB/compiled-source and generated README/adapter-page
checks pass. Experiments build affected projects and reuse matching binaries;
one final benchmark/library closure supplies the unchanged generated checks.
No build-server shutdown, package matrix, VM timing or immutable-byte rewrite.

Remaining release blockers are a fresh immutable candidate with exact Core pins,
final downstream/package acceptance, and representative actual-package/host
finite-limit costs against the preserved fixed correctness-equivalent baseline.
The earlier `844dcbad` cumulative baseline is not replaced by this focused pair.
Broader PCRE2 completion must not block scoped Core/PythonRe delivery. Completed
checked-access/accounting campaigns and rejected layout/PCRE2 Count trials stay
closed. See `validation.json`, `profile-workflow.json`, `compiled-documents.json`,
`immutable-files.json`, frozen identities and original driver sources here.
