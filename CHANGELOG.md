# Release notes

## Unreleased development after 0.4.0-rc.1

- Removed obsolete PCRE2 value-enumerator modes and example generators. Direct
  and scalar cursors retain copy, lifetime, interruption and resource semantics.
  Patterns without a curated partial descriptor no longer retain their parsed
  backtracking tree; lowered option fallbacks remain. Named-slot span copying
  avoids temporary arrays, and optional Core ownership needs no wrapper object.
  Public APIs and conservative construction accounting are unchanged.

- Literal execution now uses its implementation directly, removing two
  forwarding runtime wrappers and one object per applicable construction.
  Split eligibility keeps the existing compiled-kind proof; algorithms, SIMD,
  scalar results and resource controls remain. Metadata admission is smaller.

- Removed unused native replacement capture snapshots, reference maps/vectors
  and destination forwarding code. Native templates keep group-zero operations;
  real captures still expand through the owned scalar/template implementations.
  Removed metadata and smaller ranges can reduce construction, working and cache
  charges; output/result admission and public replacement semantics remain.

- PCRE2 partial-probe preparation now uses bounded semantic recognition instead
  of serializing whole trees. Literal operands admit storage before acquisition;
  original construction cancellation, deadlines and work/depth limits apply.
  Covered construction can refuse earlier; the curated Probe profile is unchanged.

- Fixed PCRE2 boolean DFA false negatives when repeated classes overlap a later
  literal or class (for example, `^\d*1$` on `11`). Overlapping paths now merge
  before state publication. Finite controls retain the original owned fallback.
- Narrowed internal DFA construction to the flat ASCII-class operations emitted
  by its public caller. Removed synthetic nested/text-alternative input machinery
  and unused forwarding overloads. Construction work/depth requirements can fall
  as unused frames disappear; state, major allocation and interruption bounds stay.

- PCRE2 optional finite/ASCII expansion now admits major state and buffers and
  checks original interruption. Unreferenced capture-map copies and throwaway
  legacy program foundations are removed; operation publication is complete.
- Large prepared/scalar/IL carrier mirrors use direct construction with coarse
  admission. These conservative charges can refuse or evict earlier. Cache
  generation/cancellation guarantees remain; diagnostic mutation under its lock
  no longer requires a separate revision/retry protocol.
- Retired the completed README-to-JSON reverse migration, syntax-only source
  gates and an unassigned diagnostic route. Historical measurements remain intact.

- Null text patterns now report `ArgumentNullException` across all profiles.
  Existing policy/options/timeout and explicit Compile pre-cancellation ordering
  remain; empty text/default UTF-8 spans still compile empty patterns.
- PCRE2 lexical group storage/depth is admitted before growth, with bounded
  interruption and fewer irrelevant policy passes. Selected operations discard
  unused Core preparation while preserving shared slices and live Core fast paths.
- Cached Core facades use a conservative 512-byte admission instead of a complete
  field-layout mirror. This may cause earlier construction refusal/cache eviction;
  major payloads remain separately admitted.
- PCRE2 no longer accepts unsupported patterns through unrestricted Core grammar.
  Shared Core search remains behind a successful PCRE2 parse and eligibility proof;
  accidental .NET escape/property/balancing extensions are rejected.
- PCRE2 character compilation now admits major token/class/property storage,
  instruction expansion and bounded interruption. It removes unused Core
  preparation, syntax-node vectors, token offsets and retained literal atoms.
  Covered construction thresholds can change as storage/work disappears.
- PCRE2 lexical guards consume complete escape operands consistently, so control
  escapes cannot turn literal class contents into apparent callouts or names.
- PythonRe no longer retains construction-only translation state. Scalar-only
  patterns skip unused native rendering while preserving reference/lookbehind
  validation. Factory argument simplification preserves conservative admission.
- PCRE2 rejects invalid quantifier bounds, option flags and unknown escapes in
  context, rather than recognizing only particular error examples. If every
  compiler declines, construction fails instead of returning an unusable program.
- PCRE2 backtracking construction now admits major metadata/instruction buffers
  and checks work, interruption and recursive depth. Its fixed recursive-depth
  backstop is 256 frames; smaller caller limits tighten it. Conservative metadata
  charges can count shared storage twice and cause earlier construction refusal.
  See [SPEC-PCRE2.md](SPEC-PCRE2.md) for the modeled scope and remaining exclusions.
- Benchmark-only UTF-8 lead-byte sampling moves out of the shipped Core library.

These changes are development source; the frozen 0.4.0-rc.1 artifacts are unchanged.

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
