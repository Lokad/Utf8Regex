# Lokad.Utf8Regex.Pcre2

`Lokad.Utf8Regex.Pcre2` is a `net10.0`, UTF-8-first managed companion to
`Lokad.Utf8Regex`. `Utf8Pcre2Regex` implements the repository's selected PCRE2
10.47 standard-matcher profile over `ReadOnlySpan<byte>` and `Span<byte>`.
`Utf8Regex` remains exclusively the .NET 10 `Regex` profile.

The package does not bind to native PCRE2. Its only non-BCL implementation
dependency is `Lokad.Utf8Regex`; there is no P/Invoke, native loader, RID
payload, or external matcher executable.

Release notes for the core and companion packages are tracked in the
[repository changelog](https://github.com/Lokad/Utf8Regex/blob/master/CHANGELOG.md).

## Supported profile

The generic PCRE2 compiler/runtime covers the admitted literal, character,
branching, repetition, capture, backreference, assertion, scoped-option,
branch-reset, duplicate-name, recursion/subroutine, conditional, atomic,
backtracking-control, `\K`, Unicode, and opted-in `\C` families. Normal
operations include `IsMatch`, `Match`, `MatchDetailed`, `Count`,
`EnumerateMatches`, `MatchMany`, and PCRE2 substitution.

`Probe` implements a curated partial-match profile and rejects unsupported
partial shapes explicitly. The DFA matcher, invalid-UTF subject mode,
callouts, split, full capture history, and substitution forms outside the
selected profile are not included. The executable corpus and backlog ledgers
are the authoritative capability boundary; support is never inferred from
similarity to .NET behavior.

Patterns and subjects must be well-formed UTF-8. Public start positions and
raw match ranges use byte offsets. UTF-16 coordinates are exposed only when
the reported range is exactly projectable. Opted-in `\C` can return a byte
slice that splits a scalar, and opted-in lookaround `\K` can report a
non-monotone range; affected operations follow the explicit dispositions in
[`SPEC-PCRE2.md`](../../SPEC-PCRE2.md).

## Example

```csharp
using System.Text;
using Lokad.Utf8Regex.Pcre2;

var regex = new Utf8Pcre2Regex(
    @"(?<scheme>https?)://(?<host>[A-Za-z0-9.-]+)",
    Pcre2CompileOptions.None);

ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(
    "Visit https://example.com and http://localhost.");

foreach (var match in regex.EnumerateMatches(input))
{
    if (match.Success && match.IsByteAligned)
    {
        Console.WriteLine(
            Encoding.UTF8.GetString(input.Slice(match.IndexInBytes, match.LengthInBytes)));
    }
}

byte[] redacted = regex.Replace(input, "<$host>");
```

Replacement strings use PCRE2 substitution syntax, not .NET replacement
syntax. The API also exposes UTF-8 replacement patterns, callback replacement,
`TryReplace`, `ReplaceToString`, detailed numeric-slot/name-table results, and
explicit compile and execution settings.

## Contract and development evidence

The public surface uses explicit overloads rather than default-valued
parameters. Its exact frozen form is
[`PublicApi.Shipped.txt`](../../tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
The package contract is [`SPEC-PCRE2.md`](../../SPEC-PCRE2.md), internal
ownership is described in [`ARCHITECTURE.md`](ARCHITECTURE.md), and release
evidence is recorded in [`QUALIFICATION.md`](QUALIFICATION.md).

- Project: [`Lokad.Utf8Regex.Pcre2.csproj`](Lokad.Utf8Regex.Pcre2.csproj)
- Facade: [`Utf8Pcre2Regex.cs`](Utf8Pcre2Regex.cs)
- Result types: [`Utf8Pcre2CoreTypes.cs`](Utf8Pcre2CoreTypes.cs)
