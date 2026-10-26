# Performance evidence and frozen measurements

Development is **0.4.0-rc.2-dev**. Current source is not a newly qualified package
and has no new release-wide timing claim. The unpublished common **0.4.0-rc.1**
[release identities](bench/Experiments/ReleaseCandidate-20261020/release.json)
remain immutable; those PCRE2 bytes have known defects and must not be published.

The next release needs one fresh immutable common-version candidate, final
package/downstream checks and representative actual-package/host costs, including
finite controls. The cumulative public-operation allowance is at most 10%
slowdown against a fixed correctness-equivalent baseline. Per-change timings,
private attribution and source test counts do not establish that acceptance.

The completed checked-access campaign stays closed. Source cleanup preserves
scalar matching, strict UTF-8 validation, captures and interruption/resource
contracts; it does not refresh historical benchmark Status. Relevant receipts:

| Evidence | Scope and remaining limits |
| --- | --- |
| [Incremental PCRE2 slice projection](bench/Experiments/ThirtyThirdReview-20261026/README.md) | Measured guard-containing candidate `eec34354` versus `4d89398a`: dense Unicode enumeration/MatchMany medians -86.05% to -93.00%, allocations unchanged. Sole noisy confirmation reports native MatchMany +18.78%; optional overlap guard is removed in final `575eeea6`. Exact final-byte, package and cumulative costs remain unqualified. |
| [Finite adapter preparation and Python names](bench/Experiments/ThirtySecondReview-20261026/README.md) | Source `9ea498ad` versus `4d89398a`: captured-literal IsMatch medians -72.40% unrestricted/-26.20% finite, allocations 2,192 to 0 B / 2,304 to 112 B. Finite-family constructors allocate about 44 KB less. Count/control timing remains noisy after one confirmation; no cumulative/package qualification. |
| [Host/callback/preparation cleanup](bench/Experiments/ThirtyFirstReview-20261026/README.md) | Source `56042eb1` versus `9ea498ad`, same .NET 10.0.14: eight public median changes -4.37% to +3.83%, unchanged allocations. One noisy-row confirmation: PCRE2 +0.01%, preparation -1.61%, control -8.83%. Noise limits causal claims; construction is separate. No cumulative/package qualification. |
| [Twenty-first cleanup](bench/Experiments/TwentyFirstReview-20261022/README.md) | One callback writer and typed PCRE2 probing roots. Confirmation callback/probe medians -4.47% to +6.35%; noisy controls/spreads remain. Construction costs are separate; no cumulative/package qualification. |
| [Twentieth cleanup](bench/Experiments/TwentiethReview-20261022/README.md) | Default-result correction, selected-profile qualification and native template borrowing; focused public costs and allocation witnesses are separate from release qualification. |
| [Nineteenth comparison](bench/Experiments/NineteenthReview-20261022/README.md) | Package/member guards delivered. Fixed-baseline comparison and sole confirmation were inconclusive; PCRE2 Count prototype restored. No cumulative 10% qualification. |
| [Fifteenth cleanup](bench/Experiments/FifteenthReview-20261021/README.md) | Selected public medians -5.09% to +6.10%, unchanged matching allocations; compiled identifier construction allocates 61,696 fewer bytes. Bounded source evidence only. |
| [Ninth PCRE2 correction](bench/Experiments/NinthReview-20261021/README.md) | Historical 1 KiB float IsMatch: about 2.85 us unlimited versus 78.05 us and 343,304 B with all four finite controls. Different routes; no universal accounting-overhead claim. |
| [Rejected sixteenth prototype](bench/Experiments/SixteenthReview-20261022/README.md) | Window/preparation/value-layout changes restored after inconclusive costs. Prototype savings are not delivered. |

Other cleanup observations and their uncertainty remain in the versioned
[experiment receipts](bench/Experiments) and
[earlier page](https://github.com/Lokad/Utf8Regex/blob/63f891ec5cdeceb34e95b8f445f8b01f12246433/CURRENT-PERFORMANCE.md).
No historical evidence is promoted to current-engine performance or rerun here.

## Frozen scalar.119 package costs

The observations below apply to **Core/PythonRe scalar.119**, producer `ca0c8a6f`.
[Frozen fixture, actual-byte costs and identities](bench/Experiments/Scalar119Delivery-20261020/README.md)
provide the tracked receipt; detailed local handoffs remain under
`artifacts/core-python-scalar119-20261020/` and are not part of a source checkout.
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
