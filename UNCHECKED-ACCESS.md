# Checked-access inventory: complete and closed

The frozen common **0.4.0-rc.1** Core/PCRE2 DLLs retain only permitted
`Unsafe.SizeOf<T>()` queries; PythonRe has none. No unchecked reads, generated
staging helpers, native imports or shipped BCL matcher/result execution remain
in those qualified bytes. Lython accepts SizeOf. Actual size queries perform no
unchecked memory access and must remain rather than manual size estimates.

The [RC1 handoff](artifacts/utf8regex-0.4.0-rc.1-20261020/HANDOFF.md) and
[original identities](artifacts/utf8regex-0.4.0-rc.1-20261020/final-integrity.json)
identify the immutable Windows/Linux-qualified inventory. RC1 is unpublished
and has separate known PCRE2 grammar/construction defects: do not publish those
PCRE2 bytes. Its inventory evidence does not qualify a corrected release.

Development is **0.4.0-rc.2-dev**. Source/member inventory checks continue to
enforce the managed boundary. A later source cleanup does not inherit RC1 package
qualification, reopen the checked-access campaign or refresh old performance
results. No new conversion or safe/unsafe execution mode is planned here.

## Preserved evidence

The full former execution ledger, original source/package identities, accepted
conversions, deferrals/rejections, cumulative costs and uncertainty remain in
local ignored PLAN.HISTORY.md and
[repository history](https://github.com/Lokad/Utf8Regex/blob/0310b50a2d33dc64bd6bc188a1e850df9014c8f8/UNCHECKED-ACCESS.md).
Versioned experiment receipts and immutable packages remain unchanged. Historical
"current" labels in that ledger describe their original checkpoints.

The final inventory permits SizeOf and rejects unchecked loads/reads and native
matcher dependencies. Preserve SIMD/algorithms, strict UTF-8/scalars, cancellation,
resource admission and managed safety. The former conversion allowance was up to
10% public-operation execution slowdown against one fixed correctness-equivalent
baseline; isolated kernel timings did not determine public acceptance. That
campaign is closed. Do not rerun closed timing or package matrices for prose cleanup.
