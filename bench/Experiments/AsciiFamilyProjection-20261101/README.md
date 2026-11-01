# Reuse proven ASCII coordinates for exact literal families

Source `1fb3bea0`, frozen baseline `32cefcf5`. Exact literal-family cursors now
use the same ByteOnly projection already used by folded families on proven ASCII
subjects. Byte and UTF-16 coordinates coincide; inter-match gap scans add no
information. Whole-subject strict validation, search/branch ordering, captures,
mixed Unicode projection, lifetimes and search/per-hit/output controls remain.
No new mode, API, unchecked access or allocation. Eliminated gap scans no longer
consume validation-work units; [the contract](../../../docs/SPEC.md) records this.

One [public comparison](comparison.json), **84.703 seconds**, .NET11RC3, quiet
Ubuntu VM/CPU2. Ten equal-result cases/four lanes, warm1.5s/min64, independent60ms
calibration, five alternating samples, minimum **62.894ms**; outputs checked
after timing. [Preflight](preflight.txt) proves the actual family's projection
changes Utf16BoundaryMap ->ByteOnly; mixed input retains its projection and
finite/live/deadline calls retain scalar selection. No confirmation.

| Public replacement | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual common/replace-words | 68.926 | 65.188 | -5.42% | 13648 |
| Same, compiled | 68.581 | 64.964 | -5.27% | 13648 |
| Native template | 70.033 | 65.801 | -6.04% | 13648 |
| Native destination | 70.124 | 70.975 | +1.21% | 4120 |
| Finite limits/live token | 173.091 | 177.093 | +2.31% | 57072 |
| Deadline/live token | 277.364 | 276.666 | -0.25% | 57120 |
| Mixed Unicode control | 68.217 | 69.291 | +1.57% | 13664 |
| Overlapping alternatives | 138.988 | 133.097 | -4.24% | 22920 |
| Dense adjacent matches | 125.572 | 123.568 | -1.60% | 21576 |
| Unchanged Count | 0.426 | 0.421 | -1.24% | 0 |

All five actual ordinary and compiled pairs improve about5%. Destination and
mixed-control regressions are stable and retained, within the allowance.
Allocations are unchanged in every case. [Driver](driver.cs.txt) and
[summary](summary.json) retain all observations. BCL experiment lanes materialize
the same UTF-8 outputs, including encoding/copy for destination; native templates
are already strings for BCL. Their costs differ from catalog checksum comparators
and do not imply equivalent finite-resource policies.

One [clean selective refresh](selective-refresh.txt) qualifies compiled ratio
**3.71x ->3.55x** (still Gap), ordinary **0.89x ->0.86x**, allocations13648B.
Only [the selected two section entries](snapshot-diff.json) change; all unselected
case values/identities are preserved. README regeneration, ordering and parity
verification pass. Producer A/B effects and independent catalog/comparator point
movement are separate observations, not a cumulative cost claim.

[Release checks](verification.txt) pass per runtime: Core9994+3120, PythonRe1518,
PCRE2 2137 and snapshot/API/dependency16. The existing exhaustive cutoff test now
also covers ASCII exact/folded cursors, byte/UTF-16 coordinates and retained
caller reservations. Public offset, aliases, captures, replacement, original
cancellation and deadline tests remain. No full solution/package qualification.

Immutable `artifacts/experiments/ascii-family-projection-20261101/` archives:

- `evidence.tar.gz`: exact DLL/PDBs, bundles, patch, driver, raw comparison and
  clean selective snapshots, SHA256
  `AD06CCB79D290B887DCA3954554B51509B35901F13D668604D9261A77E9459F9`.
- `correctness-source.zip`: source/test diff, bundles, driver and complete logs,
  SHA256 `0DE2E904F8CE7A3C2241E087EC8D2AFE7B665AB65855897BD3E9F4732D876B5A`.

The final fixed `a40bda03` cumulative comparison, complete current-source sub-hour
refresh and fresh immutable package/consumer boundary remain separate gates.
Published and staged packages are unchanged.
