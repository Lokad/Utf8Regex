# Reject constant false literals within admitted packed rows

Packed NonBacktracking preparation already admits at most64 instruction cells
before each row, with direct original-token and absolute-deadline checks. It now
rejects an unfolded literal whose operand differs from the current scalar within
that cell quantum. Feasible/folded atoms and every complex-class predicate keep
their direct charges. Wide rows and streaming single search are unchanged.

No new arrays, metadata, execution mode, engine, unchecked access or framework.
Scalar matching, complete strict validation, captures/priority, aliases/lifetimes,
major-buffer admission, working/depth/result limits and basic work bounds remain.
The [numeric contract](../../../docs/SPEC.md) intentionally revises preparation
work: subtract exactly the number of skipped false literal predicates. These
constant rejections remain metered by their row, even on unlimited calls. This is
separate from the rejected atom-poll relocation and cross-row allowance pilots.

The [current census](census.json) found46400 impossible literal calls among92926
candidate atoms on the canonical46429-byte corpus. Existing /// search is a
presence proof with CanGuideFallbackStarts=false; the ordinary whitespace helper
does not apply to lowered NB Fork/Jump. Ten diagnostic windows occupy260bytes,
but one enclosing range occupies37113bytes. Neither establishes a general safe
clipping algorithm, so no window ownership/dispatch changes were made.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json) took90.719s
on the quiet Ubuntu VM, .NET11RC3, CPUmask4. Fourteen cases/four equal-result lanes,
1s/min64 warmup, independent60ms calibration,5alternating samples (minimum59.767ms).
Full UTF-8 validation is included, completed batches consumed after timing.
Frozen60563cb3 binaries before, parentae8cadca plus archived runtime patch after.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| region-ordinary | 1333.042 | 1220.995 | -8.41% |
| region-compiled | 1394.434 | 1264.322 | -9.33% |
| region-live | 1397.781 | 1243.106 | -11.07% |
| region-finite-live | 1517.916 | 1348.003 | -11.19% |
| region-deadline-live | 2948.722 | 2178.588 | -26.12% |
| unicode-nb | 341.875 | 317.342 | -7.18% |
| wide-nb | 2312.947 | 2316.206 | +0.14% |
| nullable-nb | 78.779 | 73.145 | -7.15% |
| dense-ascii-nb | 307.801 | 313.368 | +1.81% |
| dense-unicode-nb | 222.171 | 222.112 | -0.03% |
| category-count-deadline-live | 626.716 | 608.873 | -2.85% |
| category-single-search-deadline-live | 1109.939 | 1136.189 | +2.36% |
| non-nb-scalar-control | 4.682 | 4.661 | -0.45% |
| literal-control | 0.395 | 0.398 | +0.72% |

**Promote.** Unchanged allocations on every lane. Retain dense ASCII+1.81%,
unchanged category IsMatch+2.36%, wide+0.14% and literal+0.72%. All are below10%;
no confirmation or variant campaign. [Pairs/spreads](summary.json) retain all
observations. These incremental public costs are not a cumulative parity claim.

[Release checks](verification.json):713 focused Core,729 additional resource/
budget/allowance/lifetime/cancellation and28 differential checks per runtime pass.
Some focused/resource selections overlap; do not add them as unique test counts.
Four new theory rows exercise real rejecting-row and final-publication token/
deadline checks and13-byte loan refunds. Every finite cutoff and packed/wide
boundary retains refusal before unadmitted work and synchronous table cleanup.
[371 production probes](state-verification.json) preserve all fields except the
independently counted false-literal work reduction (covered delta0..26).
The [canonical after census](census-after.json) also verifies140790->94390 work,
exact delta46400, unchanged candidates/viability-derived counts and byte matches.

CIae8/41296824912 passed Linux; Windows failed a grapheme deadline test's recovery
assertion AFTER its diagnostic-clock scope, where a loaded runner could exceed
the real20ms timeout. Separate commit30da6059 keeps only that recovery under a
constant clock;37 grapheme checks/runtime pass. Main timeout kind/third in-cluster
checkpoint witnesses remain. No shipped deadline behavior was changed for CI.

- [Frozen binaries, patch, harnesses and raw measurements/census](../../../artifacts/experiments/nb-literal-rejection-20261101/evidence.tar.gz):
  `194F7FB6A192A0391AB7AD90518CA0DA7495F1951454CC62DFDDF86151E694BB`.
- [Exact source, correctness logs and original CI failure](../../../artifacts/experiments/nb-literal-rejection-20261101/correctness-source.zip):
  `776B5B5B35FF13166732A7F3D0CAD74FF5836C409EFCDF73AD1BF961679F0CA3`.

No package/platform/full-catalog qualification was repeated. Published/staged
artifacts remain immutable; final-source release acceptance stays separate.