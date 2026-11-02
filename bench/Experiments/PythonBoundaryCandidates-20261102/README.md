# Positive-boundary candidate skipping: rejected

The sole **64.260s** public comparison rejects this candidate: the date miss
improved 17.99%, but the long Unicode `IsMatch` control slowed **10.89%**, beyond
the 10% allowance. Samples were tightly grouped. No confirmation or variant was
run. Runtime and tests are fully reverted locally and on the quiet VM.

The candidate used the exact production word predicate to reject ASCII word-
interior starts before entering the VM. It required a positive first boundary,
existing ASCII head eligibility, search intent and an already-initialized VM
workspace. First-attempt admission, original invocation, scalar advancement and
cleanup stayed intact. No field, plan, parser, matcher or execution mode was added.
This differs from the previously rejected category-mask experiment.

Before any runtime edit, an unchanged 8,246-byte Unicode control passed a bounded
calibration preflight: all 20 samples exceeded 20ms (minimum43.004ms). The reused
scratch driver capped retained boolean/int output values at ten million, rather
than capping transient allocation traffic, and wrote each completed row immediately.
This fixed the earlier IPv4 probe's measurement limitation without reopening it
or changing shipped benchmark instrumentation.

Quiet VM, .NET 11 RC3, normal eight-CPU startup then CPU2. Matching cb1d443e
benchmark DLL/PDBs were the baseline; the candidate rebuilt PythonRe only from
data-only parent5514a236 plus the archived patch. Core DLL/PDB bytes were identical.
Four equal-result public lanes include both BCL comparator forms. Five alternating
samples per lane, independently calibrated toward60ms; minimum **45.825ms** across
all300 measured samples. Full outputs were consumed after timing. The scoped
ASCII comparator used an explicit ASCII boundary formula.

| Public operation | Before us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Actual47B date miss, IsMatch | 3.05 | 2.50 | -17.99% |
| Finite limits/live token | 3.49 | 2.92 | -16.31% |
| Deadline/live token | 3.99 | 3.38 | -15.33% |
| Dense almost-dates | 72.46 | 53.62 | -26.00% |
| Dense word interiors | 102.41 | 55.86 | -45.46% |
| Long Unicode prefix | 586.99 | 650.91 | **+10.89%** |
| Scoped ASCII date miss | 2.74 | 2.23 | -18.62% |
| Existing boundary Count control | 176.27 | 189.06 | +7.26% |
| Unchanged Core Count control | 1.86 | 1.87 | +0.86% |

All allocation medians were unchanged. Other controls, offsets, captures and
anchored matching are in the [complete comparison](comparison.json) and
[summary](summary.json). The exact cause of the collateral runtime cost is not
proven; this receipt makes no code-generation attribution. Parent measurements
do not replace fixed-a40bda03 cumulative release acceptance or CPython qualification.

[Verification](verification.json): focused Release PythonRe71/runtime passes on
.NET10/11, including every work cutoff with a parent loan, first-attempt working
admission, scoped/negative boundaries, captures, aliases after program closure,
UTF-8 tails, offsets and original interruption. Three initial fixture failures
were corrected before timing; product code stayed unchanged. Restored canonical
builds and54/runtime focused tests pass. No full suite, package qualification or
catalog refresh was repeated after the rejection. The existing immutable dev.12
packages and published benchmark snapshots are unchanged.

Immutable archives under `artifacts/experiments/`:

- `python-boundary-candidates-20261102-rejected/evidence.tar.gz`: exact compared
  DLL/PDBs, driver, patch, preflights, complete measurements and restored VM build.
  SHA256 `EE9BCA4821E57A124FE4D479F0F66AFA77174D433DCF0A74BA642FFFB8C798F1`.
- `python-boundary-candidates-20261102-correctness.zip`: candidate source/test
  snapshots, declaration, local build/test logs and verification.
  SHA256 `64BBFD748D943E98F5D470CEBEE602307356CB572CEC79248A6F30075E4E1B54`.
