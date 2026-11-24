# Lokad.Utf8Regex

Managed regular expressions for .NET 10 and 11. Match Unicode scalars
directly over UTF-8 and return byte ranges.

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

`Utf8RegexPolicy` can also supply a shared `ProgramMemoryAllowance`. Dispose
funded expressions when finished. Operations with an optional working allowance
inherit the program account by default. [Program and working allowances](../../docs/SPEC.md#program-and-working-allowances)

Keep borrowed subjects unchanged and their owners alive. Span enumerator
copies have independent positions and share limits. Each advance needs an
open funded regex when program admission is in use. Use a memory cursor
for traversal across calls or after closing the regex. Published snapshots
survive advances and disposal. [Cursor and result lifetimes](../../docs/SPEC.md#cursor-and-result-lifetimes)

Optional `Lokad.Utf8Regex.PythonRe`, `Lokad.Utf8Regex.Pcre2` and
`Lokad.Utf8Regex.Posix` adapters require
the exact matching Core package version. Keep selected packages at the same
version. They are managed profile adapters with explicit compatibility boundaries;
no native PCRE2 binary is shipped.

[Core specification](../../docs/SPEC.md) · [Release notes](../../CHANGELOG.md)

## Subject input contract

All four profiles require valid, unchanged UTF-8 subjects. Validate external bytes
where they become text, for example with `System.Text.Unicode.Utf8.IsValid`.
Matching trusts this input guarantee. Pattern and template validation are
independent. [Subject input contract](../../docs/SPEC.md#subject-input-contract)
