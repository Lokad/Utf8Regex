# Release notes

## Unreleased development after 0.4.0-rc.1

These changes are development source, currently 0.4.0-rc.2-dev. Frozen RC1
artifacts remain unchanged and unpublished; they must not be released. A fresh
candidate and final downstream/package/cost qualification are still required.

- Zero-initialized Core value matches now report no match, consistently with
  explicit NoMatch. Successful empty matches remain distinct. The result stays
  16 bytes and preserves byte and derived UTF-16 coordinates.
- Native UTF-8 literal replacement borrows the synchronous template instead of
  copying it unconditionally. TryReplace retains a copy when the template
  overlaps the destination; input overlap, capacity failures, strict validation,
  cancellation and finite resource behavior retain their contracts.
- Null text patterns now throw ArgumentNullException across all profiles.
  Existing option/policy/timeout and explicit Compile pre-cancellation ordering
  remain. Empty text and default UTF-8 spans still compile empty patterns.
- Fixed PCRE2 boolean DFA false negatives for overlapping repeated classes and
  later literals/classes, such as ^\d*1$ on 11. Finite controls retain owned
  execution. Unsupported PCRE2 patterns no longer enter unrestricted Core grammar;
  invalid bounds/options/escapes are rejected in context, complete escape operands
  are consumed consistently, and construction fails if every compiler declines.
- PCRE2 analysis now reports parsed branch-reset usage and conservative positive
  byte bounds for character and single-token-repeat programs. IsFullyNative
  describes owned managed execution. The documented profile remains partial;
  these fixes do not promise full .NET, Python or PCRE2 equivalence.
- Construction admits major buffers, instructions/state expansion and lexical
  storage before acquisition, with bounded interruption/depth checks. PCRE2's
  backtracking recursive-depth backstop is 256 frames; smaller caller limits
  tighten it. Cached Core facades use a conservative 512-byte charge. Coarse
  metadata charges may double-count shared storage and refuse/evict earlier;
  removing unused storage/work can also lower thresholds. These are modeled
  capacities, not total managed-heap/process limits or stable numeric charges.
- Removed unused plans, forwarding executors, retained construction metadata and
  diagnostic-only shipping code. Shared native preparation and name storage avoid
  duplicate acquisition. Scalar semantics, captures, cursor copies/owned snapshots,
  cancellation, deadlines and useful invocation limits remain. See
  [host contracts](HOST-INTEGRATION.md) and the
  [PCRE2 specification](SPEC-PCRE2.md) for scope and exclusions.
- Package preparation now builds only selected library roots. Each profile
  inventories its actual consumed assembly, including detached package tests;
  Core qualification no longer needs adapter DLLs or the benchmark executable.
  Exact dependency pins, provenance, isolated consumers and immutable bytes remain
  required. Combined source CI still covers all profiles.

[Performance acceptance and historical evidence](CURRENT-PERFORMANCE.md) separate
source checks from package qualification and measured operating cost. Detailed
cleanup receipts remain under [bench/Experiments](bench/Experiments), with earlier
release-note prose preserved in repository history.

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
