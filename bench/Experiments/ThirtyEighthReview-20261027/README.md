# Review 38: truthful cost status, dead-code removal, detached fixtures

Development is 0.4.0-rc.3-dev. Frozen RC2 packages/ZIP, their identity/platform/
consumer evidence, raw timing observations and all protected originals remain
unchanged. No new package, benchmark campaign or external consumer change.

## Delivered

- Corrected the earlier cost sign-off. The canonical [RC2 erratum](../ReleaseCandidate2-20261026/ERRATA.md)
  withdraws cumulative acceptance as inconclusive: finite Python Count +10.38%
  initially / +0.06% in a reduced-lane confirmation, with substantial absolute
  timing changes. Both runs and original JSON decisions remain historical.
  Current entry points disclose the open gate; no third run occurred.
- Deleted the unused shipped LegacyFallbackReason instruction scanner (71 lines,
  including its comments) and its sole self-comparison fixture (88 lines).
  The real front-end classifier and every live semantic flag remain. There is
  no changed matching/execution path or public API, so no timing was warranted.
- Detached package fixtures filter the ordinary Category=SourceOnly trait.
  Repository source/layout and historical snapshot checks still run in ordinary
  source CI. Mixed mandatory-parameter/null-suppression checks were separated:
  binary assertions remain in detached runs; source assertions stay in source CI.
  No matcher, resource or binary dependency check was disabled.
- Core/PythonRe/PCRE2 stage their existing PublicApi.Shipped.txt next to the test
  DLL. The shared reader uses that exact file; ancestor lookup is deleted.
  The existing package qualifier/helper is retained, without a new framework,
  project, helper mode or package build.
- Specification and source-version headers now distinguish development from
  frozen RC2 correctness evidence and its inconclusive cost gate. Future packaged
  CHANGELOG links use repository URLs. The external erratum explains the frozen
  ZIP's evidence/RESULT.md path; the ZIP itself is not rewritten.

## Validation and delivery boundary

Focused Release source checks pass: Core 164 / PythonRe 20 / PCRE2 27, including the
source guards and API/member/route/scalar coverage. Those runs build only their
affected projects incrementally; no solution rebuild or server shutdown.

The changed detached helper runs once per profile in a fresh directory outside
the checkout, against unchanged RC2 packages: Core 39,582 / PythonRe 3,936 / PCRE2 6,183
pass with no failed/skipped cases. Actual DLL hashes match RC2, staged API
baselines match reviewed source, and consumed API/member checks are present.
The binary half of the mixed architecture check remains; source-only checks are
excluded from this slice. Full detached TRX and logs remain under the local
artifacts/thirty-eighth-implementation-20261027 directory.

No repeated provenance/source/package matrix or pack was performed. The helper
change justified this outside-checkout execution, rather than requalifying new
product bytes. No timing was repeated: the only shipped-code removal had no
production caller. Complete source CI, including source guards and all profiles,
is the final platform boundary; its result is recorded separately.

[Validation](validation.json), [outside-checkout coverage](detached-coverage.json),
[document navigation](document-checks.json), [preserved hashes](immutable-inputs.json).

Cumulative release cost remains inconclusive and blocks claiming full delivery
acceptance. This bounded cleanup does not resolve or silently waive that gate.
Broader profile completion, accounting/checked-access/retention work and rejected
replacement streaming consolidation remain deferred.

Final source a2f73b38 is green on Windows/Linux in
[CI 40481258226](https://github.com/Lokad/Utf8Regex/actions/runs/40481258226),
including both disabled-dynamic-code steps. The initial scheduling failure is
preserved; the correction retained its five-second termination guard. No further
package or timing run was performed after it.
