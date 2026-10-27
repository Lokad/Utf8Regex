# PCRE2 replacement preparation and bounded output investigation

Library implementation `01d04a5f`; additional scalar-fragment fixtures
`39a7012f`, development `0.4.0-rc.2-dev`. Before DLLs are source `7650e7a5`.
This executes the review at checkout `9459ebc1`. No package or published snapshot
was refreshed; frozen unpublished RC1 remains unchanged and must not be released.

## Delivered simplification

- Delete Pcre2ReplacementComponent and store its existing bounded cache directly.
  Its forwarding methods, allocated owner and nested construction scope disappear.
  Capacity stays 16 with the existing conservative retained policy, pending-entry
  concurrency and independent invocation failure/retry. Actual major allocations
  retain admission; removed helper/scope capacity is no longer charged. Exact
  construction requirements remain version-dependent.
- Replacement cache identity uses only the template text and Extended grammar.
  Execution flags remain with each invocation. The literal-driver's original
  eligibility predicate is retained separately, so sharing syntax does not send
  runtime-flag calls into the range ledger. No second cache, new representation
  mode, fake flag, compatibility switch or accounting descriptor is added.
- The real contention fixture now observes the cache itself. Public tests cover
  bytes/text/UTF8-template/destination, runtime unset/unknown errors, grammar
  differences, cache reuse, scalar/lone-surrogate fragments and finite controls.

The `(a)`/`$1` witness goes from four byte-identical prepared programs in four
cache entries to one shared program/entry. The literal `$0` flag variants retain
their respective execution routes and allocations. All observed matching-call
allocations are unchanged. A fixed batch of eight constructors per version, after
two warm instances, records 1,632 to 1,528 B/instance. This is allocation evidence;
it is not a constructor-time or universal performance claim.

## Rejected broader streaming prototype

A four-line route change sent allocating native results through the existing
template writer while retaining backtracking and caller-destination paths.
Its scalar/progress focus passed 285 checks, but complete PCRE2 exposed five
failures in existing output/text capacity tests. In particular, 17 expanding
matches that the ledger serves within its 2,032-byte working extent could fail
under streaming growth/final-copy overlap. Other old boundary expectations also
changed where the new route required less capacity.

No budget was raised, admission weakened or fixture disabled to accept the
prototype. The route change and its prototype-only fixture are removed completely;
the patch/test text, rejected binaries locally, and failure observations are
preserved. Existing native discovery/output and destination preflight remain.
Allocation savings on a dense identity example do not establish a general CPU or
working-capacity improvement. No streaming implementation was performance-tuned.

## Focused Release validation

- Final implementation: complete PCRE2 6,181 pass, zero failed/skipped, 9 s.
  Eight additional scalar-fragment cases were subsequently added to the existing
  public matrix; final focused run 55 pass, zero failed/skipped. Tests are added
  after the full run; shipped implementation source is unchanged.
- Core API/formatter 12 and member inventory 12 pass; PythonRe API/member inventory
  one each pass, using matching existing binaries. Complete combined source CI
  40359868301 passes
  on Windows/Linux, including both disabled-dynamic-code steps, at `39a7012f`.
- Incremental affected-project builds and matching binary reuse. The full suite
  was repeated only to verify removal of the demonstrated failing prototype.
  No local solution/platform/package matrix or build-server shutdown.
- All 18 protected snapshot/RC1/original-handoff hashes match. No public API,
  strict UTF-8/scalar, captures, NB/RTL, cursor/result lifetime, original-token,
  absolute-deadline or useful work/depth/working/result/state bounds are removed.

## One public cost comparison and sole confirmation

The reused isolated-load-context driver exercises final retained code: 11 public
lanes, small/dense/sparse and Unicode byte/text output, all-finite controls,
runtime flags, unchanged backtracking/destination routes and a Core Count control.
Full outputs are checked before timing; timed lanes retain outputs and check
length/endpoints. SDK10.0.401/.NET10.0.14, existing processor-selection scope,
2.5 s warmup per version/lane, 40 ms calibration target, alternating nine pairs.
No builds/tests ran during measurement. Initial run: 67.96 s.

The sole 41.84 s confirmation uses 17 reversed-first pairs on uncertain rows:

| Public row | Initial change | Confirmation change |
| --- | ---: | ---: |
| Unlimited short bytes | +32.19% | +19.37% |
| Unlimited dense bytes | +8.01% | +50.64% |
| Unlimited Unicode text | +9.26% | -3.86% |
| Finite runtime-flag streaming | -15.38% | -17.81% |
| Unchanged backtracking ledger | -5.20% | +1.64% |
| Unchanged Core Count control | +0.94% | -0.85% |

The confirmation's short spreads reach 9.07x/9.96x, dense spreads 5.20x/5.89x;
the finite flag row is 1.06x/1.17x. Allocation observations remain exact/unchanged.
The short/dense medians are adverse observations and are not hidden or declared
equivalent. **Overall operating-cost evidence remains inconclusive; there is no
settled <=10%, package or cumulative qualification.** No third local timing run
or tuning campaign follows. The ownership/cache change is retained with original
native algorithms; the output-route conversion failed its separate admission
gate and was not promoted. Actual-package/cumulative costs remain a release gate.

Raw samples, selected affinities, producer versions and DLL/PDB hashes accompany
this receipt. Scalar semantics are qualified separately from measurements. The
existing release boundary remains: fresh common-version packages, exact source/
symbol/API/platform and original Awk/Lython/PCRE2 consumer checks, plus actual
package costs against fixed correctness-equivalent `844dcbad`. Broader profile
completion and closed accounting/checked-access/layout campaigns are not reopened.
