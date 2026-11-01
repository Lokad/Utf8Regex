# Bounded scalar-table preparation

Count scalar starts in already validated UTF8 with a checked ASCII-prefix search
and byte tests under<=64byte quanta. Fill exact boundary offsets with the existing
scalar decoder under<=64scalar quanta. All atom direct charges and row checks
stay; complete array admission still precedes allocation. No validator cache,
metadata widening, new SIMD kernel, matching mode or unchecked operation.

This intentionally revises numeric preparation work: size+offset charges become
ceil(bytes/64)+ceil(scalars/64), replacing2*scalars. Each group directly checks
the original token/deadline. [SPEC](../../../docs/SPEC.md) and
[migration guidance](../../../docs/MIGRATION.md) explain version-dependent costs.
Invocation working/result/depth storage and borrowed/retained lifetimes are unchanged.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json),**89.876s**,
quiet Ubuntu VM/.NET11RC3,CPUmask4. Frozen570c binaries before (7e only changed
data/receipt); candidate7e plus archived one-file patch after. Fourteen cases,
four equal-result lanes, warm1s/min64, independent60ms calibration, fivealternating
samples, minimum59.782ms. Complete results checked after timing. No confirmation.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Region Count ordinary | 2172.966 | 2020.678 | -7.01% |
| Region Count compiled | 2239.344 | 2072.275 | -7.46% |
| Region Count live | 2493.391 | 2173.772 | -12.82% |
| Region Count finite/live | 2776.213 | 2313.791 | -16.66% |
| Region Count deadline/live | 4965.463 | 4174.625 | -15.93% |
| Unicode Count | 508.263 | 468.862 | -7.75% |
| Wide Count | 2721.272 | 2354.919 | -13.46% |
| Nullable Count | 154.246 | 149.263 | -3.23% |
| Dense ASCII Count | 435.166 | 394.915 | -9.25% |
| Dense supplementary Count | 302.622 | 286.181 | -5.43% |
| Category Count deadline/live | 774.348 | 725.345 | -6.33% |
| Single-search category IsMatch control | 1192.567 | 1179.667 | -1.08% |
| Unchanged non-NB Count control | 4.739 | 4.731 | -0.18% |
| Unchanged literal Count control | 0.404 | 0.400 | -0.92% |

Allocations are unchanged for every lane. [All pairs/spreads](summary.json)
retain uncertainty: affected/control spreads<=1.05 except tiny literal<=1.13.
No observed adverse regression or universal parity claim. Full producer validation
is included; construction is excluded and unchanged by this one-file runtime change.

[Release checks](verification.json) pass on net10/net11. Fifteen new theory rows
cover scalar boundaries at64/65 bytes/scalars, split continuation/supplementary
sequences, size interruption before array admission, offset interruption after
admission, original-token cancellation/deadlines, loan refunds and strict public
malformed tails after early hits. The final fixture37/runtime passes separately.

[371 complete state/search probes](state-verification.json) match all fields
except the exact declared preparation-work reduction (0..148 in this dataset).
Full viability digests, complete single-search work/ranges/captures/peaks and13B
enclosing loans agree. No UTF16 subject map or new persistent ownership state.

- [Raw/source/harness/binaries](../../../artifacts/experiments/nb-scalar-preparation-20261101/evidence.tar.gz):
  `B4DA22B2F36171D8170FAC623B0565B5C172F5CFDD300E9150DCD42FA53AE980`.
- [Correctness/source](../../../artifacts/experiments/nb-scalar-preparation-20261101/correctness-source.zip):
  `B088BEE3157A24A443C6E619A4276BADD22CC8F4E1C61C33AA1CDF8963B703AB`.

No repeated package/fullcatalog/unchecked-access qualification. Existing immutable
8d/dev.2 full refresh and consumer evidence remain that source's old boundary.

Onlyregion rows were selectively refreshed from clean source `c8ed65bf`:
ordinary2012.360us/54.90x,compiled2004.924us/54.37x,557416B, bothGap (previous
60.79/60.15x). This is a separate observation, not the A/B percentage above.
All30raw samples>=47.999ms; other rows/statuses/identities/lifecycle/scaling remain.
Existing parity verification and3snapshot/hash/ratio guards perruntime pass.
[Selective raw/generated/binary evidence](../../../artifacts/experiments/nb-scalar-preparation-20261101/selective-evidence.tar.gz):
`E381B45AC3C8FF83EB3FA979B9AC4174C675F2D6FC97DF63F4B1888EBBF11720`.
Committed-source Core DLL SHA256:
`32E0E00E4F3F9957F6D36CE0B92C90ED70C06E24C4773D4C8F5043AE745992CB`.
