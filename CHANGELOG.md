# Release notes

## Unreleased after 0.4.0-rc.2

Development is 0.4.0-rc.3-dev; no new package candidate is staged.
Removed the test-only legacy fallback-reason scanner. Detached package fixtures
read their staged API snapshot and leave repository-only source/layout/snapshot
guards to source CI. Matching, public APIs and resource controls are unchanged.
PythonRe match/group text and detailed capture/name collections now expose empty
values for default and unsuccessful results, as their non-null contracts require.
Supplied result arrays retain their identity; no match discovery or admission
policy changes accompany this correction.
RC2 correctness/identity/consumer evidence remains valid; its cumulative cost
acceptance is withdrawn as inconclusive. Frozen packages and ZIP are unchanged;
see the [dated correction](https://github.com/Lokad/Utf8Regex/blob/master/bench/Experiments/ReleaseCandidate2-20261026/ERRATA.md).

## 0.4.0-rc.2

Common prerelease for **Lokad.Utf8Regex**, **Lokad.Utf8Regex.PythonRe** and
**Lokad.Utf8Regex.Pcre2**, targeting .NET 10. Both adapters require exactly
Core **[0.4.0-rc.2]**. Upgrade them together. Frozen, unpublished RC1 artifacts
are superseded and must not be published.

This is a breaking migration from UTF-16 matching to **Unicode scalars over
strict UTF-8**, with byte offsets and lengths as the primary coordinates.
Dot matches an emoji once across its four bytes. Classes, repetition, captures,
references, assertions, RTL and empty-match progression respect scalar boundaries.
Malformed UTF-8 anywhere in a subject is rejected, including after an early match.
UTF-16 coordinates and string helpers remain derived interoperability APIs.

- **Owned managed execution:** Core provides .NET-style grammar and a supported
  NonBacktracking subset; PythonRe provides the documented Python str profile;
  PCRE2 provides a selected managed PCRE2 profile. No BCL matcher/result execution
  or native PCRE2 implementation is shipped. Complete profile equivalence,
  NativeAOT and trimming are not promised.
- **Usable invocation controls:** original synchronous cancellation and one
  absolute deadline cover validation, matching, required shaping and cursor
  advances. Positive finite work/depth/working/result policies admit modeled
  major capacities before allocation. Defaults and static cached calls remain
  unlimited. Construction has separate Compile controls and fixed expansion
  backstops. These controls do not bound total heap/RSS, arbitrary callbacks,
  standalone getters, separate preparation or host-retained storage.
- **Explicit lifetimes:** owned snapshots survive advance/failure/disposal;
  borrowed subjects must remain alive and unchanged. PythonRe memory-cursor
  aliases share one disposable lifetime; Core value-cursor copies share the
  allowance/deadline. Callback writers accept scoped stack spans and are valid
  only within their callback. Null evaluators reject even on misses.
- **Core corrections:** default match/capture values have empty coordinates;
  successful value matches reject negative active coordinates. Detailed misses
  avoid unnecessary coordinate maps. Hosts that disable dynamic code use
  non-emitted execution, including with Compiled.
- **PythonRe corrections:** Unicode capture names cannot collide with generated
  names; opening-order numbering and backreferences remain distinct. Native
  collection avoids discarded coordinate maps. Fixed finite policies omit
  optional preparation those instances cannot execute.
- **PCRE2 corrections:** grammar admission, overlapping repeated-class discovery,
  CRLF wildcard starts, branch-reset analysis and Unicode 16 grapheme boundaries
  are corrected. Long graphemes observe interruption and work limits. TryReplace
  stages overlapping output under admission. Replacement-cache syntax sharing
  preserves per-call unset/unknown behavior and independent cancellation.
  Generic unsupported Hard partial decisions and marked partial misses reject
  explicitly; full partial-profile completion remains out of scope.
- **Simpler internals:** superseded UTF-16/fallback engines, unchecked reads,
  forwarding owners and diagnostic-only shipping code are removed. Actual
  Unsafe.SizeOf queries remain. Construction/cache charges are conservative
  modeled capacities; thresholds may change and shared storage may count twice.
  PCRE2 MatchMany requires disjoint input/result storage; overlap is unchecked.

Read [MIGRATION.md](MIGRATION.md) and [HOST-INTEGRATION.md](HOST-INTEGRATION.md)
before upgrading. Each package README and profile specification describes its
limits. [current performance evidence](https://github.com/Lokad/Utf8Regex/blob/master/CURRENT-PERFORMANCE.md) separates measured
evidence from release acceptance; this version does not refresh historical
benchmark Status or claim universal throughput gains.

Detailed development notes remain in
[repository history](https://github.com/Lokad/Utf8Regex/blob/ee917133/CHANGELOG.md)
and [experiment receipts](https://github.com/Lokad/Utf8Regex/tree/master/bench/Experiments).

## 0.4.0-rc.1

Frozen and unpublished. Review found PCRE2 grammar/construction defects; do not
publish those PCRE2 bytes. A corrected release requires a fresh immutable candidate
and final qualification. The notes below describe the original staged migration.

This prerelease moves all three packages to one common version. PythonRe and
PCRE2 each depend on exactly `Lokad.Utf8Regex [0.4.0-rc.1]`. It is a breaking
semantic and host-integration change from the earlier UTF-16-oriented release.

- **Unicode scalars are the matching unit.** Dot, classes, repetition, captures,
  references, assertions, RTL and empty-match progression use complete scalars.
  An emoji is one match, even when its UTF-8 encoding occupies four bytes.
  Byte offsets/lengths are primary; UTF-16 coordinates are derived conveniences.
- **Strict UTF-8 is required throughout the subject.** An early match does not
  permit a malformed suffix. Invalid input is rejected with byte-offset
  diagnostics; validation, cancellation and resource denial retain their defined
  precedence. A controlled invocation can exhaust its allowance before reaching
  a later malformed byte.
- **Owned managed execution replaces BCL matching fallbacks.** Core has its own
  scalar backtracking and supported NonBacktracking execution, native UTF-8
  searches and shared captures. PythonRe targets the documented CPython `str`
  profile; PCRE2 supplies a selected managed PCRE2 profile without native imports.
- **Cancellation and deadlines cover the invocation.** Validation, matching,
  required result shaping and cursor advances observe the original token and
  absolute deadline. Waiting between advances does not restart the clock.
- **Finite controls are usable across profiles.** Instances can bound modeled
  work, live depth, major active working capacity and cumulative result/staging
  capacity. Major covered buffers require admission before allocation. Result
  charges do not refund on publication; shared storage may be counted again.
  Defaults/static cached calls remain unlimited. Numeric charge thresholds are
  implementation dependent and are not portable across versions/profiles.
- **Construction is controlled separately.** Explicit `Compile` factories accept
  work/depth/instruction/byte limits, timeout and cancellation. Fixed expansion
  backstops remain. Cache/program retention, cold preparation, incidental helper
  objects, callbacks, standalone getters and host-retained storage have distinct
  scope; these policies are not total managed-heap or process-memory limits.
- **Results have explicit lifetimes.** Owned snapshots survive cursor movement,
  EOF, failure and disposal. PythonRe's memory cursor requires disposal when
  stopped early; aliases share its position/lifetime. Core value copies retain
  independent positions while sharing the invocation allowance.

The release also removes superseded UTF-16 execution machinery and unchecked
memory access. Actual `Unsafe.SizeOf<T>()` size queries remain. Matching and
resource admission retain managed safety; no optional unsafe mode is provided.

See [MIGRATION.md](MIGRATION.md) for upgrade examples and
[HOST-INTEGRATION.md](HOST-INTEGRATION.md) for exact control/ownership scope.
Profile limitations are documented in each package README and specification.
This candidate does not claim full .NET, Python or PCRE2 compatibility, safe
finite defaults, complete host memory containment or POSIX matching semantics.
Historical development evidence remains in repository history and versioned
experiment receipts; it is not additional release functionality.
