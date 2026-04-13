# Lokad.Utf8Regex.Pcre2

`Lokad.Utf8Regex.Pcre2` is a `net10.0` side extension for `Lokad.Utf8Regex` that exposes a UTF-8-first PCRE2-flavored API through `Utf8Pcre2Regex`.

It is intended for workloads where the input already exists as UTF-8 bytes and you need a managed PCRE2 profile on top of the base `Lokad.Utf8Regex` library.

## Support Scope

- Semantic target: `Utf8Pcre2Regex` models the PCRE2 standard matcher profile, not `.NET Regex` semantics.
- Implementation model: the package is strictly managed .NET. It does not bind to native PCRE2.
- Dependency model: the only non-BCL implementation dependency is `Lokad.Utf8Regex`.
- Translation-first execution: when a PCRE2 pattern is semantically equivalent to the `Utf8Regex`/.NET profile, execution is translated onto `Utf8Regex`.
- PCRE2-only execution: when exact translation is not possible, `Utf8Pcre2Regex` uses explicit modeled PCRE2 behavior or rejects unsupported shapes by spec.
- Primary I/O model: the main API surface operates on UTF-8 `ReadOnlySpan<byte>` / `Span<byte>`.
- Match coordinates: PCRE2-style match values preserve UTF-16 coordinates and byte coordinates when they are well-defined for the match.
- Profile boundary: the package supports a documented PCRE2 subset. Some constructs are intentionally rejected rather than silently approximated.

For the detailed support contract, see [SPEC-PCRE2.md](../../SPEC-PCRE2.md).

## Public API

```csharp
using System.Text;
using Lokad.Utf8Regex;

var regex = new Utf8Pcre2Regex(
    pattern: @"(?<scheme>https?)://(?<host>[A-Za-z0-9.-]+)",
    options: Pcre2CompileOptions.None);

ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("""
    Visit https://example.com and http://localhost.
    """);

bool isMatch = regex.IsMatch(input);
int count = regex.Count(input);

Utf8Pcre2ValueMatch first = regex.Match(input);
if (first.Success && first.IsByteAligned)
{
    Console.WriteLine($"First match at byte {first.IndexInBytes}, length {first.LengthInBytes}");
}

foreach (var match in regex.EnumerateMatches(input))
{
    if (!match.Success || !match.IsByteAligned)
    {
        continue;
    }

    var slice = input.Slice(match.IndexInBytes, match.LengthInBytes);
    Console.WriteLine(Encoding.UTF8.GetString(slice));
}

byte[] redacted = regex.Replace(input, "<$host>");
Console.WriteLine(Encoding.UTF8.GetString(redacted));
```

Notes:
- Inputs must be well-formed UTF-8.
- Replacement strings follow PCRE2 substitution semantics, not `.NET` replacement semantics.
- `Utf8Pcre2Regex` also exposes `MatchDetailed(...)`, `Probe(...)`, `MatchMany(...)`, `ReplaceToString(...)`, and `TryReplace(...)`.
- Partial matching is exposed through `Probe(...)`, not through the normal match APIs.

## API Shape

The main public types are:
- `Utf8Pcre2Regex`
- `Utf8Pcre2ValueMatch`
- `Utf8Pcre2MatchContext`
- `Utf8Pcre2MatchData`
- `Pcre2CompileOptions`
- `Pcre2MatchOptions`
- `Pcre2SubstitutionOptions`
- `Utf8Pcre2CompileSettings`
- `Utf8Pcre2ExecutionLimits`

The package keeps the PCRE2 flavor separate from the base `Utf8Regex` type. `Utf8Regex` remains the `.NET Regex` profile; `Utf8Pcre2Regex` is the PCRE2 profile.

## Behavioral Notes

- Exact semantic parity with `Utf8Regex` is only used when the PCRE2 pattern can be translated without changing meaning.
- PCRE2-specific constructs such as branch-reset, duplicate names, `\K`, and recursive/subroutine families are handled by explicit PCRE2-side logic, not by silent translation.
- Unsupported public operations fail with explicit profile rejections rather than raw implementation exceptions.
- The package is intended as a side extension of `Lokad.Utf8Regex`, not as a native-PCRE2 binding.

## Development Notes

- Project: [Lokad.Utf8Regex.Pcre2.csproj](./Lokad.Utf8Regex.Pcre2.csproj)
- Main runtime: [Utf8Pcre2Regex.cs](./Utf8Pcre2Regex.cs)
- Core match/result types: [Utf8Pcre2CoreTypes.cs](./Utf8Pcre2CoreTypes.cs)
- Support spec: [SPEC-PCRE2.md](../../SPEC-PCRE2.md)
- Base library overview: [README.md](../../README.md)
