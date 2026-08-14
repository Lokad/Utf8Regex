using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>
/// PCRE2-owned construction boundary for the shared ASCII membership carrier.
/// It deliberately has no dependency on the vendored .NET character-class encoding.
/// </summary>
internal static class Pcre2AsciiByteSetFactory
{
    public static AsciiCharClass Create(ReadOnlySpan<byte> values) => Create(values, false);

    public static AsciiCharClass Create(ReadOnlySpan<byte> values, bool negated) =>
        AsciiCharClass.FromBytes(values, negated);

    public static AsciiCharClass CreateRange(byte low, byte high) => CreateRange(low, high, false);

    public static AsciiCharClass CreateRange(byte low, byte high, bool negated) =>
        AsciiCharClass.FromRange(Utf8InclusiveByteRange.Create(low, high), negated);

    public static AsciiCharClass CreatePredicate(Func<byte, bool> predicate) => CreatePredicate(predicate, false);

    public static AsciiCharClass CreatePredicate(Func<byte, bool> predicate, bool negated) =>
        AsciiCharClass.FromPredicate(predicate, negated);
}
