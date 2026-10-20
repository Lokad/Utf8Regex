# Release notes

## 0.4.0-rc.1

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
