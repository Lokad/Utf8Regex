# Performance evidence and frozen measurements

The staged common release is **Core/PythonRe/PCRE2 0.4.0-rc.1**,
producer `2e97ac974e6e8c60aac95db1e4808cbda2ff9736`.
[Release handoff](artifacts/utf8regex-0.4.0-rc.1-20261020/HANDOFF.md) records
its exact immutable bytes, supported scope and Windows/Linux consumer checks.
It introduces no new release-wide timing claim. Development is **0.4.0-rc.2-dev**;
source cleanup validation does not qualify new packages or refresh old measurements.

The [second cleanup observation](bench/Experiments/SecondSimplification-20261020/README.md)
has a separate, bounded local construction/public-operation scope. The completed
checked-access campaign and historical benchmark decisions remain closed.

The [sixth-review cleanup receipt](bench/Experiments/SixthReview-20261020/README.md)
records a bounded source comparison separately. Frozen RC1 is unpublished and its
PCRE2 bytes have known defects; original package and benchmark evidence stays
historical. This page does not qualify the development engine for release.

The [seventh-review receipt](bench/Experiments/SeventhReview-20261021/README.md)
records removal of unused PCRE2 preparation and conservative facade admission.
Selected public Count medians changed −0.89%/+0.40%, with unchanged allocation;
construction observations and their noisy control are reported separately.
Windows/Linux source CI is green at `884b156b`. This bounded local comparison
does not refresh historical benchmark Status or resolve earlier delivery costs.

The [eighth-review receipt](bench/Experiments/EighthReview-20261021/README.md)
records bounded optimizer admission and construction/public observations with
identical driver bytes. Selected public median increases are at most 1.63%,
with unchanged allocation. Finite Count's apparent gain has a changing control
and is not attributed to the engine. The comparison uses unlimited workloads;
finite controls have separate correctness checks. Historical Status and deferred
package/finite-overhead qualification remain unchanged.

The [ninth-review receipt](bench/Experiments/NinthReview-20261021/README.md)
records the PCRE2 DFA overlap correction and removal of unused input machinery.
One bounded comparison needed its single allowed noise confirmation. With longer
warmup and sample floors, selected public medians show no regression and allocations
are unchanged; improving controls prevent an attributed speedup claim. The selected
1 KiB float IsMatch costs about 2.85 µs unlimited versus 78.05 µs and 343,304 B per
call with all four finite controls. These different routes disclose a continuing
operating cost, not a new default or a package-wide overhead qualification.
Construction medians rose 5–8% while allocations fell. Timing is closed.

The [tenth-review receipt](bench/Experiments/TenthReview-20261021/README.md)
records bounded partial preparation and removal of replacement metadata/literal
wrappers. One local comparison needed its single reversed-order confirmation.
Confirmation public medians increase at most 3.45%, with unchanged affected
allocations, but moving controls and spreads up to 3.12x prevent an attributed
speedup or precise equivalence claim. Selected construction allocations fall;
construction timing is separate and noisy. Timing is closed, snapshots/Status
are unchanged, and current-package/host costs remain deferred.

The [eleventh-review receipt](bench/Experiments/EleventhReview-20261021/README.md)
records removal of obsolete PCRE2 cursor modes, unused retained trees and name-copy
collections. Named-slot getters fall 1,104/1,192 → 0 B/call; the x64 cursor falls
1,632 → 1,576 bytes. The one comparison and its single reversed-order confirmation
are inconclusive: controls move substantially and some affected timings rise.
No precise 10% bound or general throughput change is established. Timing is closed;
actual-package/host cost acceptance remains deferred, with raw results preserved.

The [twelfth-review receipt](bench/Experiments/TwelfthReview-20261021/README.md)
records deletion of Core's unexecuted instruction vector and unused PythonRe
Core roots. The 64-capture construction witness allocates 82,760 fewer bytes;
ordinary matching allocations are unchanged in the selected lanes. Its fixed
original comparison and single reversed-order confirmation are closed with
inconclusive timing: individual public lanes cross 10% in different runs, while
an unchanged control moves 12.15%. No precise 10% bound or package cost claim is
established. Avoiding transient declined Core construction remains later work.

## Frozen scalar.119 package costs

The observations below apply to **Core/PythonRe scalar.119**, producer `ca0c8a6f`.
[Handoff](artifacts/core-python-scalar119-20261020/HANDOFF.md),
[actual-byte costs](artifacts/core-python-scalar119-20261020/vm-cost/RESULT.md),
[frozen fixture/raw observations/identities](bench/Experiments/Scalar119Delivery-20261020/README.md).
Same immutable DLLs qualify on Windows/Linux against original producer handoff
assertions and Unicode/resource/identity checks. One 106-second quiet-VM observation
uses .NET 10.0.12 with default tiers, 40 public lanes and 200 samples of at least52.891 ms.

Public-operation medians in microseconds:

| Workload | Unlimited | Finite | Live token + finite | Deadline + finite | Finite B/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| Core pair Count, 13 KB / 2,000 hits |55.926|167.149|199.613|511.708|112|
| Core RTL strict-end captured-reference miss, 16 KB |211.905|238.151|234.560|250.375|105928|
| Core RTL empty EOF, 64 KiB |2.901|2.788|2.793|3.033|112|
| Core RTL impossible floor, 16 KB |0.530|0.640|0.699|0.835|176|
| Core native literal Count, 64 KiB |3.615|3.699|—|—|112|
| Python UTF-8 values, 3,072 words |685.898|849.992|—|—|190256|
| Python UTF-8 tuples, 1,024 two-group matches |381.050|518.659|—|—|133408|
| Python captured Count, 1,024 hits |35.368|20.243|—|—|112|

All four limits are finite together: 50 million work steps, depth 1,024, working
capacity 64 MiB and result capacity 128 MiB.
These are experiment allowances, not defaults. Live tokens are uncanceled; deadline
lanes allow 10 s per call. Public costs include whole-input strict UTF-8 validation.
Eager values/tuples are retained and verified. Both strict-decode and predecoded
.NET controls check equivalent overlapping ASCII results; all medians and
allocations are recorded. These observations do not qualify CPython Status or current .NET 11 behavior.

Limits retain material costs: pair Count finite/live/deadline are about 3.0x/3.6x/
9.1x unrestricted; eager UTF8 words/tuples about 24%/36% higher finite. Different
eligible routes mean these ratios are not isolated accounting overhead. Most
producer spreads are at most 1.047x, but empty EOF is 1.690x unrestricted / 1.225x finite, native
finite literal 1.099x, some .NET controls 1.469x/1.528x. Empty-row rankings, universal
overhead, causal 118-to-119 gains and per-row performance certification are unsupported.
One run only; all uncertainty retained, no confirmation/retiming campaign.

Included source changes have separate FullOpts before/after evidence:

- [Controlled exact-pair discovery](bench/Experiments/ControlledPairCandidate-20261020/README.md):
  finite hit 283.137 → 262.544 µs, miss 366.975 → 337.360 µs; dense false-prefix costs roughly
  halve, Core allocations unchanged; max measured public regression 3.26%.
- [Mandatory RTL strict-end Count](bench/Experiments/RtlStrictEndCount-20261020/README.md):
  long finite misses 1820.650 → 276.920 µs / 1782.256 → 238.934 µs, unchanged allocations;
  empty EOF 6042.257 → 2.760 µs; impossible floor 766.270 → 0.699 µs and 240 → 176 B.
  Max Core regression 2.29%; original terminal authority, captures and limits remain.

Those comparisons support implementation changes; they do not substitute for the
actual-package observation or establish default-tier before/after gains. Numeric
work/working requirements intentionally fall where real attempts/scans disappear.
The ordinal-reference span prototype is [rejected/reverted](bench/Experiments/OrdinalBackreference-20261020/README.md):
unchanged unlimited literal Count repeatedly slowed 37–41% despite faster long refs.
Keep byte loops, retained regression tests and the closed decision; no further timing.

Eight frozen 117 PythonRe rows keep their exact product/uncertainty. Detailed
iteration and named replacement qualify opposite winners, detailed search is
Unqualified, five others Inconclusive. [Detailed](bench/Experiments/PythonReDetailedFour-20261020/README.md),
[UTF8/callback](bench/Experiments/PythonReUtf8CallbackFour-20261020/README.md).
This does not establish 118/119 CPython Status or freshness for the full 90-row catalog.
Newest versioned .NET 10/11/generated lanes remain frozen at Core 114; the original
comparative-lane deliverable is complete. [Exact runtime scope](RUNTIME-COMPARISON.md).
No new framework or repeated closed package/checked-access qualification.

Four additional [frozen119 literal Search rows](bench/Experiments/PythonReLiteralFour119-20261020/README.md)
now have qualified public CPython comparisons: ASCII late hit 3.104/22.663 µs,
ASCII miss 2.706/22.375 µs, Cyrillic hit 0.118/0.174 µs, Cyrillic miss 0.832/0.255 µs
(managed/predecoded CPython). Zero managed allocation. Nine pairs each, no extension
or retiming; all managed/CPython samples at least 48.099/49.829 ms. Both ASCII bytes controls
qualify managed wins; Cyrillic remains Not engine-comparable/NotApplicable.
The 3.26x Cyrillic miss is a current public-operation lead, not an attributed engine
or removable-validation gap. CPython harness-floor bounds and full provenance are
preserved. The other 86 catalog rows, including eight frozen 117 rows, are unchanged.
The .NET timings elsewhere in those rows retain historical scope; finite controls
remain covered by the separate actual-byte observation above.

## Earlier history

Prior scalar.118 and earlier checkpoint prose, rejected experiments and their
uncertainty remain in the local gitignored PLAN.HISTORY.md and
[repository history](https://github.com/Lokad/Utf8Regex/blob/d0fd579bc4eb3005a017ea8682b6a1e96f30a08a/CURRENT-PERFORMANCE.md).
Versioned experiment directories and original package handoffs remain immutable.
[Runtime comparisons](RUNTIME-COMPARISON.md) and each generated benchmark page
retain their own source/runtime identities; none is promoted to current-engine
performance by this documentation cleanup. No historical timing is rerun here.
