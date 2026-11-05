# Common 0.5.0-dev.32 delivery checkpoint

Frozen `7f152e64a646f1e6efa53abd94a9f0b221fe64c1`, all three packages with
net10.0/net11.0 assets and exact adapter-to-Core pins. Packages and symbols are
staged in artifacts/nuget; publication is separate. Prior bytes remain immutable.

This batch reuses the existing mandatory ASCII prefix proof before scalar Count
VM entry, and replaces structural reverse-suffix search with checked span IndexOf.
The now-unused per-suffix prepared-search descriptor is removed. See the
[Ruff evidence](../RuffStraightPrefix-20261105/README.md) and
[suffix evidence](../StructuralSuffixSpan-20261105/README.md).
Each change had one bounded separate-process public A/B with unchanged controls.
Ruff's actual public Count improves 56.26% ordinary/56.64% compiled and 55.60%
finite/live; suffix Count improves 78.59% ordinary/78.65% compiled. Matching
allocations remain unchanged. Dense suffix hits retain +0.87% (about0.57us).
These are isolated causal comparisons; a subsequent catalog refresh is separate.
Local costs remain documented trade-offs for substantial gains, without applying
the completed checked-access allowance as a universal release gate.

Strict complete-subject UTF-8/scalar semantics, captures, RTL/NonBacktracking,
retained aliases, original cancellation/deadlines and useful resource admission
remain. Execution is owned, managed and checked; actual Unsafe.SizeOf is retained.
Suffix preparation/cache charges can decrease with removed storage/work, while
major admission remains. No new selector, execution mode or accounting framework.

Source CI41984534078 exposed one stale structural test requiring the deleted
suffix-search descriptor. The correction checks borrowed literal storage and zero
preparation, with nine Release checks passing on each target. Frozen source
CI41986160368 passes Windows/Linux; no producer change follows the correction.

Windows qualification passed in489.3978439s: two affected library-root builds,
one pack per profile, six assets and eight detached fixtures/109096 checks.
Linux consumed the exact Windows bytes without repacking: eight fixtures/109096
checks, six assets and24 feed/cache/lock audits. Copied original Awk19/Lython118
checks pass on each platform/runtime; public native/funded probes finish with zero
root loans. The1565 original files differ only at three exact version pins.
Original Windows compilation evidence includes464 documents/generated inputs;
twelve original Windows restore audits match. All six staged package/symbol
files match the original qualification hashes. Compatibility remains separate
from downstream adoption and optional POSIX.

One **2587.8161431s = 43m07.82s Full** passed under the original3600s outer cap.
The [manifest](full-refresh.json) records Full/Complete/terminal0 and exactly83
expected step names in order, all Passed. One Release build, eight source
fixtures/109136 checks across.NET10/11, disabled dynamic code on both, all.NET11
catalogs, paired comparators, lifecycle/scaling, resources and verifiers passed.
Normal eight-CPU startup precedes CPU2 affinity, using pinned.NET11 RC3 and official
CPython3.13.2. Readiness was zero seconds with9246112KiB available under unchanged
8GiB RAM/1GiB repository-space guards. Package/consumer clocks remain separate.

The full archive was verified offVM before import. Seven generated files were
hash-imported; four blocks, six descending tables and178 ratios verify. Only the
root introduction's duration/receipt link was then updated. Matching binaries pass
six snapshot hosts/ten checks with SkipBuild. No rebuild, repeat source/package
matrix, catalog replay or favorable rerun follows.

[Fresh rankings](ratio-summary.json) retain exclusions and uncertainty. Catalog
movement is separate from the isolated causal A/B comparisons above:

- Core117wins/one tie candidate/54gaps/six unqualified. Chinese literal4.78x/91.18us,
  compiled alternation4.16x/150.93us, compiled literal3.77x/91.15us,
  compiled imports2.76x/12.37us/80B, email2.69x/1226.40us remain gaps.
  Ruff tweaked is0.556x/0.964x ordinary/compiled; suffix is0.038x/0.470x, all Win.
  Twain, river-window and symbol Leipzig rows in both modes are unqualified under
  the unchanged provenance/sample/per-lane20ms requirements. Preserve all six.
- Python37ManagedFaster/35CpythonFaster/13Inconclusive/fiveUnqualified.
  Unicode Split3.58x, early literal3.00x, date miss2.81x, captured FindAll2.43x.
  Current floor exclusions are alternation miss, ASCII-option hit, multiline hit,
  search-offset literal hit and Unicode-category miss. Earlier exclusions remain
  in earlier receipts; no new equivalence claim is inferred from summary counts.
- PCRE2 has78ManagedFaster/12NativeFaster/oneEquivalent/tenInconclusive/25Excluded.
  Date hit4.42x, subroutine-prefix-digits4.18x and date miss4.14x remain gaps.
  Date miss now qualifies as NativeFaster in this fresh paired run; retain the
  previous run's lane-order exclusion. No historical classification is rewritten.

Fresh32 Linux fixtures2174809169B and duplicate archive808103288B were retired
once after off-VM SHA verification and exact tar comparison. Never retry those
targets. Canonical raw evidence remains intact.

Immutable artifacts/experiments/delivery-dev32-20261105:

- windows-evidence.zip: `4CA4D346C2917A413AB7C68E6444E355973FBD8C5E8DE513A84A976B9C782EE3`.
- windows-restore-inputs.zip: `3F0A98156D8E7E262326F9B6235F1A11E2159EDB0A1483993A5A359A9A652E28`.
- windows-compilation-inputs.zip: `57A285076DB305B629B495089C19D646F6BA482E4BE270B5634E87937D121C2A`.
- linux-evidence.tar.gz: `3B304B0F3FCAA83C1E3512AD8195C05384BFD1A65CD0A76A7A66875296D47632`.

- full-evidence.tar.gz: `E1292A659C023735E30171513AE88B207292B880823B39CE4AC606A2ED2136BB`.
- post-refresh-guards.zip: `FC35B481D75A6F336C3314FD1A9DD00A4F750DC235315DFC39EE9BBA2D69A3DD`.

The previous [dev31 receipt](../DeliveryDev31-20261105/README.md), all earlier
packages/symbols and raw measurements remain immutable. This staged checkpoint
is compatibility/performance evidence; adoption, broader profiles and optional
POSIX remain separate. It does not authorize NuGet publication.
