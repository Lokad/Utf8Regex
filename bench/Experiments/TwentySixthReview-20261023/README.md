# Grapheme properties, public value invariants and small cleanup

Source: `86933ad5` (graphemes), `df07d337` (value guards), `29386831`
(forwarder removal), `3dea7dec` (Python checkers). Development remains
`0.4.0-rc.2-dev`; no new product package candidate was made.

## Delivered changes and contracts

PCRE2 `\X` uses one immutable lookup of pinned Unicode 16.0 break, pictographic
and Indic-conjunct properties. It replaces general-category/broad-range guesses;
the existing segmentation loop and bounded 256-scalar resource checkpoints stay.
Repeated ZWJs now clear the pictographic predecessor correctly. ASCII dispatch
and exact Hangul arithmetic remain. There is no new executor or policy mode.
The PCRE2 package includes the existing Unicode license for its new data.

Before/after public witnesses show Latin-letter/virama over-joining and Thai
spacing-mark under-joining corrected. The supported Unicode Indic-conjunct rule
remains, including its intentional difference from native PCRE2 10.47. Native
PCRE2 also differs on the regional-indicator/ZWJ case; it is not assumed to
implement every selected Unicode rule. SPEC-PCRE2.md records that scoped contract.

The lookup has 683 disjoint ranges (5,464 embedded data bytes). All 1,114,112
code-point lookups match the three pinned input files, including exact Hangul
arithmetic. The recipe and input SHA256 identities are preserved here. The data
check and grapheme-after witness came from the initial focused build, whose
producer label is the preceding checkout; they are not package provenance. Final
committed source is covered by the complete affected suites and source CI below.

`Utf8ValueMatch` keeps its 16-byte packed layout. Successful public constructions
now reject negative active coordinates with the appropriate parameter name.
Unsuccessful values ignore all coordinates; unaligned values ignore byte fields.
Default/no-match values, legal extrema and valid engine results retain behavior.
XML docs and MIGRATION.md describe the revised argument contract.

The existing Python tools use explicit failures for fixture comparisons and
CPython/Unicode version admission, including under `-O`/`PYTHONOPTIMIZE`. No data
was regenerated. The redundant 16-line `Utf8SearchStrategyExecutor` is deleted;
its one caller directly invokes the same production backend with identical
arguments, output and invocation authority.

## Validation

- Focused Release PCRE2: 87 pass, including public ranges, Count, captures,
  substitution/destination results, repeat/backtracking routes and original
  in-cluster cancellation/deadline/work-denial tests.
- Full affected Release Core: 39,617 pass in 70 seconds; PCRE2: 6,092 in 7 seconds.
  Core focused checks: 128 pass, including public API/dependency inventory.
- Valid capture/scalar-data checks pass normally and under optimized Python.
  Corrupted copied capture fixtures reject in both modes. Synthetic mismatched
  CPython/Unicode versions reject under `-O --write` before data acquisition.
  Version-info injection leaves the real interpreter display string unchanged;
  it does not claim a second interpreter was installed or exercised.
- One final benchmark/library closure builds friend tooling. Matching binaries
  verify generated README/PythonRe/PCRE2 pages without snapshot writes.
- Existing Windows/Linux CI both completed successfully for `3dea7dec`:
  [run 39939033774](https://github.com/Lokad/Utf8Regex/actions/runs/39939033774).
  It builds the solution once per platform and tests all four source fixtures.
- Four snapshots, nine frozen RC1 files and five original TODOs retain exact
  bytes; generated Python data and the original capture corpus also stay intact.

## Bounded public source cost comparison: closed

The same driver loaded frozen `6af80c63` before and `3dea7dec` after DLLs into
separate load contexts. Release .NET 10.0.13, Windows processor 20, 2.5-second
warmup per version/lane, 40 ms calibration target. Nine alternating pairs plus
the sole 17-pair reversed-first confirmation; no concurrent builds/tests.
Selected subjects have identical expected results in both versions. This tests
changed property/value paths; it does not reopen the prior metering experiment.

| Public operation | Initial change | Confirmation | Bytes before/after |
| --- | ---: | ---: | ---: |
| Core literal Match, unlimited | +2.45% | -3.33% | 0 / 0 |
| Core supplementary Match, four finite controls | -0.82% | +1.39% | 112 / 112 |
| PCRE2 short-grapheme Count, unlimited | +7.71% | +4.32% | 0 / 0 |
| PCRE2 long-cluster IsMatch, unlimited | -26.23% | -25.12% | 0 / 0 |
| PCRE2 long-cluster IsMatch, four finite controls | -26.72% | -26.18% | 112 / 112 |
| Unchanged Core Count miss, 1 MiB | +6.07% | +7.17% | 0 / 0 |

Both observations' affected medians fit the +10% allowance, with unchanged
allocations. Wall times: 36.52/40.29 seconds. Minimum actual samples:
33.55/34.00 ms, below the calibration target; spreads reach 3.26x. Control drift
and outliers limit causal precision and individual-row certification. The long
cluster improvement reproduced, but these observations do not establish a
release-wide gain or cumulative <=10% acceptance. No third run follows.

Raw samples, DLL/PDB hashes and the reused host/driver are preserved here. These
are public before/after costs, not isolated-kernel estimates or new BCL/native
comparative rankings. Fresh immutable final packages, isolated consumers and
actual-package/host finite-control plus fixed-baseline cumulative acceptance
remain the release boundary. No full local package/platform matrix, normal
build-server shutdown, VM campaign or replacement qualification framework.
