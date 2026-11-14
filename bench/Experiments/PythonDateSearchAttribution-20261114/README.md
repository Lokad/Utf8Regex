# Date-miss search attribution — deferred

One existing `--measure-pythonre-search-attribution common/date-miss 200000 5`
command, **29.624s** under120s cap, current source `8af3a569`, .NET11GA.
Owned Core `f9fc88ad` and Python `8af3a569` producer DLL/PDBs remain unchanged;
only the existing benchmark caller was built. No new comparison/framework,
package/catalog refresh or implementation. [Acceptance](acceptance.json).

The47-byte valid ASCII subject contains two near-date candidates; all public
controls return false. The scalar profile still verifies bounded repeats and word
boundaries. The shared prefilter does not reject this subject. Attributed medians:
validity0.003–0.006us, prefilter0.072us, adapter public1.649us; translated Core
public26.477us. These are diagnostic observations, NOT a version speedup or new
CPython ratio. Core's control uses default Validate, Python consumes caller-valid
text. Timed phases are not additive; no validation cost is subtracted from a
public operation. The selected Core route has no separable kernel lane here.

No unnecessary whole-subject coordinate/validity pass was identified. Replacing
profile verification with translated Core would be counterproductive on this
fixture and would need a broader semantic proof anyway. Without a simple live
cause, defer the small absolute gap. Canonical dev92 retains1.913us/2.276x CPython;
this differently warmed attribution does not replace it. Move to material search
costs instead of another date-specific matcher or preparation mode.

One private wrapper failure occurred before the diagnostic: its hash helper
received string paths instead of Path objects. Failure raw
E7C4F45F63DE75408C6ADB83E44BE145B93C94DB15A8688C5827D7DF85BC8970 is preserved.
The successful6.61s caller build was reused; no producer or timing run was repeated.
Raw verified offVM before keeper release:
`artifacts/experiments/python-date-search-attribution-run-20261114/evidence.tar.gz`,
SHA256 `26A167856565BEA296ED89AEC360072EF235F07836691F9B04F57E64263934D0`.
All workers/collectors/keepers terminal. This bounded investigation is CLOSED.
