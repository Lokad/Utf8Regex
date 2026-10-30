# Lokad.Utf8Regex

**0.5.0 development**, .NET 10 and .NET 11. Regular expressions over strict UTF-8
`ReadOnlySpan<byte>` / `Span<byte>`, matching Unicode scalars with primary byte
coordinates. Dot matches 😀 once across four bytes. UTF-16 coordinates and
string APIs are derived interoperability conveniences.

```csharp
using Lokad.Utf8Regex;

var regex = new Utf8Regex(".");
int count = regex.Count("A😀"u8); // 2
var match = regex.Match("😀"u8); // LengthInBytes == 4
```

The owned managed engine supports documented .NET-style grammar, captures/history,
backreferences, assertions, RTL and a supported NonBacktracking subset, with
native UTF-8 search paths. It does not execute BCL Regex matchers/results or
promise UTF-16 parity. CultureInvariant is the primary supported mode. Whole
subjects must be valid UTF-8, including suffixes after an early match. Core uses
ordered alternatives, not POSIX leftmost-longest matching.

Optional IL preparation is skipped when the host disables dynamic code,
including with `Compiled`; non-emitted execution preserves results. NativeAOT
publishing and trimming have not been qualified.

Replacement callbacks can pass local stack buffers and formatting slices to
`Utf8ReplacementWriter.Append`. Its scoped byte/char span inputs are copied or
encoded before returning; the writer remains valid only during the callback.

UTF-8 replacement templates parse/cache directly as bytes. Warm queries borrow
the supplied span; cold keys are copied after admission. Prepared plans retain
byte literals and syntax flags rather than parser/text graphs. Native capture
discovery stays eligible, with byte output through the existing writer and only
final text results decoded. String templates retain their fragment-local
surrogate repair and .NET-style reference grammar.

Instances support finite `Utf8ResourcePolicy` work, depth, working-capacity and
result-capacity limits. **Defaults and static cached calls are unlimited.**
Original cancellation and absolute deadlines cover validation, matching, required
shaping and cursor advances. Explicit `Compile` factories offer separate
construction controls. Memory accounting admits modeled major capacity before
allocation; it is not total heap/RSS, all preparation, callbacks or retained-host
storage containment. See [resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits).

Keep borrowed subjects unchanged and their owners alive. Owned snapshots remain
valid after advance/failure/disposal. Copied value cursors retain independent
positions while sharing allowance/deadline. A host must separately account for
retained results and its own storage.

Optional `Lokad.Utf8Regex.PythonRe` and `Lokad.Utf8Regex.Pcre2` **0.5.0-dev**
require exactly this Core version. Both are managed profile adapters with explicit
compatibility boundaries; no native PCRE2 binary is shipped.

Read [release notes](../../CHANGELOG.md) and [migration guide](../../docs/MIGRATION.md).
The [repository specification](../../docs/SPEC.md)
defines the target contract; qualification fixtures establish implemented scope.
Performance snapshots retain their recorded source/package/runtime identities.
