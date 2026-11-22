# Lokad.Utf8Regex

**0.5.0 development**, .NET 10 and .NET 11. Regular expressions over strict UTF-8
`ReadOnlySpan<byte>` / `Span<byte>`, matching Unicode scalars with primary byte
coordinates. Dot matches 😀 once across four bytes. Results expose byte ranges;
callers derive character positions when needed.

```csharp
using Lokad.Utf8Regex;

var regex = new Utf8Regex(".");
int count = regex.Count("A😀"u8); // 2
var match = regex.Match("😀"u8); // StartOffsetInBytes == 0, EndOffsetInBytes == 4
```

The managed engine supports documented .NET-style grammar, captures and capture
history, backreferences, assertions, right-to-left matching and a NonBacktracking
subset. Core uses ordered alternatives. CultureInvariant is the primary supported
mode.

Optional IL preparation is skipped when the host disables dynamic code,
including with `Compiled`; non-emitted execution preserves results. NativeAOT
publishing and trimming have not been qualified.

Pass UTF-8 replacement templates to `Replace` for owned bytes or `TryReplace`
for caller-owned storage. Use `CreateMatchCursor` to compute custom replacements
and write the output in the host.

Instances support finite `Utf8ResourcePolicy` work, depth, working-capacity and
result-capacity limits. Defaults are unlimited. The original cancellation token
and absolute deadline cover matching, template preparation, result shaping and
cursor advances. Explicit `Compile` factories offer separate construction
controls. Memory accounting admits modeled major capacities before allocation.
See the [resource contract](../../docs/SPEC.md#cancellation-deadlines-and-resource-limits).

Constructors and `Compile` accept `Utf8RegexPolicy`, combining execution and
construction limits/timeouts with optional `ProgramMemoryAllowance`. Both funded
and unfunded factories return `Utf8Regex`; dispose funded expressions. The bounded
replacement cache is conservatively prepaid before publication (4MiB payload plus
64KiB metadata), without eagerly allocating it. The UTF8-template
`TryReplace` writes into caller-owned storage; cold preparation and output share
working admission, while cached plans remain covered by the program allowance.
Temporary loans refund synchronously and copied output survives owner disposal.
With shared working admission this uses the controlled scalar replacement route, preserving RTL, captures,
NonBacktracking, cancellation and numeric limits.

Keep borrowed subjects unchanged and their owners alive. Owned snapshots remain
valid after advance/failure/disposal. Copied value cursors retain independent
positions while sharing numeric allowance/deadline. Funded span enumeration borrows
an open regex and rejects later advances after close; Current values remain valid.
Use the memory cursor for traversal surviving close or shared working admission.
APIs exposing a working allowance inherit program funding when it is null.
The host accounts for its own storage and retained results.

Optional `Lokad.Utf8Regex.PythonRe`, `Lokad.Utf8Regex.Pcre2` and
`Lokad.Utf8Regex.Posix` adapters require
the exact matching Core package version. Keep selected packages at the same
version. They are managed profile adapters with explicit compatibility boundaries;
no native PCRE2 binary is shipped.

See the [release notes](../../CHANGELOG.md) for upgrades and version history.
The [repository specification](../../docs/SPEC.md)
defines the target contract; qualification fixtures establish implemented scope.
Performance snapshots retain their recorded source/package/runtime identities.

## Subject input contract

All four profiles require valid, unchanged UTF-8 subjects. Validate external bytes
where they become text, for example with `System.Text.Unicode.Utf8.IsValid`.
Regex operations expose no validation policy or token. Malformed subjects have
unspecified matching results; managed bounds checking and resource controls remain.
Pattern/template checks are independent. Necessary search, output and eligibility
work can still inspect input. See the [shared contract](../../docs/SPEC.md#subject-input-contract).
Frozen performance snapshots retain their recorded input contracts.
