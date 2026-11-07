# Positive Unicode class starts on ASCII subjects

The Python cursor's existing bounded ASCII-start proof can reuse a positive
class's mask when its only extra term is one positive non-ASCII range. For an
authoritatively validated ASCII subject, that range contributes no starts.
Other class shapes/non-ASCII subjects keep their existing routes; the original
scalar VM verifies every candidate, capture and assertion. No new field, helper,
engine, allocation or case-folding implementation is added. Numerical work and
needed VM scratch can fall; SPEC-PYTHONRE documents that route-dependent contract.

The original target, catalog `ignorecase/search-miss`, **does not improve**.
The initial helper probe showed a declined ASCII hint but did not trace the whole
public route. The [public-route probe](public-route-before.jsonl) establishes that
`sherlock` already retains Core candidate guidance, then Python VM verification;
it is not complete native matching. That route bypasses the changed helper.
The actual changed public route is captured `(?P<v>[éa]+)`: Core candidate guidance
declines, and the [new ASCII hint](public-route-after.jsonl) admits a..a.

ONE [public comparison](cost-summary.json), **67.403496667s**, Default-process
ABBA, 13 workloads/260 samples, minimum72.2046ms. One unchanged caller and exact
Core48886FDE bytes; PythonAE970911 -> BE851B19 on source02dfc547 plus the archived
patch. Full hashes/scope were [bound before timing](comparison-declaration.json).
Each worker warms750ms/min32, targets75ms with one calibration confirmation and
checks all results after timing. No repeat comparison, timed .NET/CPython lane,
new catalog ratio or qualification Status is claimed.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Captured class FindAll,8192 ASCII nonmembers then aaa |836.97|0.90|-99.89%|128/128|
| Actual folded Search miss |0.40|0.40|+0.57%|0/0|
| Same miss finite/live |0.57|0.58|+1.70%|112/112|
| Same miss deadline/live |1.01|1.03|+2.13%|160/160|
| Dense false starts |20.78|20.92|+0.72%|0/0|
| Anchored FullMatch control |0.29|0.30|+3.16%|0/0|

The captured-class gain is933.23x, with disjoint ranges and both candidate processes
faster. Retain the anchored control's persistent/disjoint3.16%/9.29ns local cost
for that gain; its operation never uses this hint, so no causal mechanism is
asserted. The other rows, including ordinary/finite/deadline short misses and
unchanged Core/Unicode controls, have interleaving ranges. Allocations are
unchanged in every measured row. This adjacent gain does not close the catalog's
short-miss gap; continue investigating that gap separately.

Focused Release checks pass225 per runtime on both Windows/Linux, including17new
cases: exact projection/declines, public Search/FindAll/Replace/Split, nonzero
floors inside/outside runs,2047/2048/8192 boundaries, strict invalid tails,
Unicode partners, finite work policies, work denial, original token/deadline and
parent loans. Four direct public-route cases were added after timing; only tests
changed, and the measured producer bytes remain identical. Initial221 checks also
remain in the raw archive. One CRLF patch preflight failed before mutation;
normalization preceded the single initial Linux test run. No benchmark repeated.

Canonical off-VM evidence in `artifacts/experiments/python-ascii-folded-starts-20261107/`:

- evidence.tar.gz: `EC08533282D9DA6F85FD7F87230AD66D131F3F855EA7519439FA040BE1A606A2`,3957398B.
- validation-final.tar.gz: `056CFAB30440F5BC5D2C49755B5AA343D86287FC845B0896B04F2E349EA656A3`,39509B.

No package/full refresh follows this experiment. Published/staged dev.48 and all
older bytes remain immutable. The remaining delivery and parity plan stays active.
