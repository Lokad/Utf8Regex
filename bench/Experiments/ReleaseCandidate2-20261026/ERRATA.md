# RC2 acceptance correction — 2026-10-27

The earlier cumulative performance acceptance is **withdrawn: inconclusive**.
RC2 package identity, Windows/Linux correctness, original-consumer assertions,
API/member inventory and source CI evidence remain valid. The packages and ZIP
remain immutable and unpublished. This correction does not assert a matcher defect.

The original finite Python captured Count observation is +10.38%:
18.582 us before / 20.512 us after. The sole confirmation is +0.06%:
12.039 us before / 12.047 us after. It filters the nineteen original lanes to
four, so this row runs first instead of after other warmed operations; it also
reverses pair order. Absolute costs change substantially. The evidence does not
isolate the cause or establish the cumulative <=10% requirement.

The predeclaration says uncertainty is not a pass. "No repeatable regression"
was too weak a substitute for that acceptance criterion. Preserve both runs and
their original decisions as historical evidence; do not select the favorable
median, pool unlike observations, or conduct a third run of this closed campaign.
The cumulative release-cost gate remains open for future bounded delivery work.

Original evidence:
- Protocol (d58ce915)
- Initial observations (d58ce915)
- Sole confirmation (d58ce915)

The original summary.json Decision and frozen ZIP still contain the superseded
acceptance. This dated erratum supersedes that conclusion without rewriting those
bytes. NuGet publication and external consumer pin changes have not occurred.

ZIP navigation: its root HANDOFF.md refers to performance/RESULT.md; the actual
entry is evidence/RESULT.md. Root CHANGELOG.md also links repository-only pages.
Use evidence/README.md for the bundled evidence and the repository for current
specifications/performance status. These navigation corrections do not change the
feed or package hashes. Reusable NuGet.Config and all six package/symbol entries
remain present at the ZIP root/feed.
