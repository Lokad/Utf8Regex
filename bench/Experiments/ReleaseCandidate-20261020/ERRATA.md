# Superseded RC1: do not publish the PCRE2 bytes

Navigation consolidation, 2026-11-10. This retains the rejection previously
documented in the profile qualification guide; it is not a new defect report.
The original receipt below described acceptance before the subsequent review.

The unpublished `0.4.0-rc.1` producer
`2e97ac974e6e8c60aac95db1e4808cbda2ff9736` has known PCRE2 defects:

- Backtracking construction can bypass depth/instruction admission.
- Fixture-specific validation misses neighboring invalid syntax, while lexical
  guards can reject valid classes with delimiter-like escape operands.
- Compilation can publish a program without an executable backend.

Do not publish these PCRE2 bytes. Preserve the original packages, symbols,
archive and [identities](release.json). Later fixes require their own candidate
and qualification; the original passing suites did not establish their absence.
Construction, grammar and ownership corrections have their separate
[sixth-review](../SixthReview-20261020/README.md),
[seventh-review](../SeventhReview-20261021/README.md) and
[eighth-review](../EighthReview-20261021/README.md) evidence. Later performance
uncertainty remains recorded in the [RC2 erratum](../ReleaseCandidate2-20261026/ERRATA.md).
