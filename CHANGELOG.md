# Release notes

## Unreleased: 0.4.0-rc.3-dev

No new candidate is staged. Development after RC2 includes:

- PythonRe default/missed detailed results now return empty text and capture/name
  arrays as promised by their non-null contracts. Successful arrays keep their
  identity; matching and admission are unchanged.
- Detached fixtures use staged API snapshots; repository-only guards stay in
  source CI. Focused and detached test runs fail when no tests are discovered.
- Guides are consolidated under docs/; the changelog remains at repository root.
  Packaged guide filenames remain unchanged.

Frozen RC2 correctness/identity/consumer evidence retains its recorded scope.
Its cumulative performance acceptance was withdrawn as **inconclusive**:
[dated correction](bench/Experiments/ReleaseCandidate2-20261026/ERRATA.md).
Package/symbol/ZIP bytes remain unchanged. Development fixes require fresh
candidate bytes at a future release boundary.

## 0.4.0-rc.2: frozen, unpublished

Common .NET 10 candidate for Core, PythonRe and PCRE2. Adapters require exactly
Core [0.4.0-rc.2]; upgrade selected packages together.

- **Breaking scalar migration:** dot, classes, repetition, captures/references,
  assertions, RTL and empty progress use complete Unicode scalars over strict
  UTF-8. Byte coordinates are primary. The entire subject must be valid,
  including suffixes after early matches; UTF-16 coordinates are derived.
- **Owned managed execution:** .NET-style Core, Python str and selected PCRE2
  profiles execute without BCL matcher/results or native PCRE2. The supported
  NonBacktracking subset remains a separate linear mode.
- **Usable controls:** synchronous original-token cancellation, one absolute
  invocation deadline and positive finite work/depth/working/result limits.
  Defaults/static calls are unlimited. Compile has separate construction
  controls/backstops. Modeled major-capacity admission is not total heap/RSS,
  callback, arbitrary retention or complete preparation containment.
- **Lifetimes:** owned snapshots survive advance/failure/disposal; borrowed
  subjects stay alive/unchanged. PythonRe memory-cursor aliases share disposal;
  copied value cursors share allowance/deadline. Scoped callback writers accept
  local spans. Null evaluators reject even on misses.
- **Corrections:** Core defaults/active coordinates and disabled-dynamic-code
  execution; Python numbering/name isolation and finite preparation; PCRE2 grammar,
  numeric-name rejection, overlapping discovery, newline/options, Unicode 16
  grapheme boundaries and controlled replacement/projection. Unsupported partial
  decisions reject explicitly. MatchMany requires disjoint input/result storage.
- **Cleanup:** superseded UTF-16/fallback engines, unchecked reads, forwarding
  owners and benchmark-only shipped code are removed. Actual Unsafe.SizeOf
  queries remain; conservative construction/cache charges may double-count.

Read [migration](docs/MIGRATION.md) and [resource controls](docs/SPEC.md#cancellation-deadlines-and-resource-limits) before
upgrading. Profile specifications define exclusions; full .NET/Python/PCRE2
equivalence, POSIX selection, NativeAOT/trimming and universal throughput gains
are not promised.

## Earlier candidates and evidence

RC1 is superseded, frozen and unpublished; its PCRE2 grammar/construction defects
make those bytes unsuitable for publication. Do not overwrite old candidates.

Detailed development evidence remains in [versioned receipts](bench/Experiments).
Benchmark snapshots retain their measured versions; release acceptance remains
separate from historical measurements.
