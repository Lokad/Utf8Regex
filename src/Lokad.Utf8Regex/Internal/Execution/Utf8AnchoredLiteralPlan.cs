namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8AnchoredLiteralKind : byte
{
    None,
    AnchoredAsciiLiteralPrefix,
    AnchoredAsciiLiteralSuffix,
    AnchoredAsciiLiteralSuffixStrictEnd,
    AnchoredFoldedAsciiLiteralPrefix,
    AnchoredFoldedAsciiLiteralSuffix,
    AnchoredFoldedAsciiLiteralSuffixStrictEnd,
}

// One owned literal and its existing anchor/case semantics. Nonliteral grammars
// execute the scalar program; they retain no separate payload or selector.
internal readonly struct Utf8AnchoredLiteralPlan(Utf8AnchoredLiteralKind kind, byte[] literalUtf8)
{
    private readonly byte[]? _literalUtf8 = literalUtf8;

    internal Utf8AnchoredLiteralKind Kind { get; } = kind;
    internal byte[] LiteralUtf8 => _literalUtf8 ?? [];
    internal bool HasValue => Kind != Utf8AnchoredLiteralKind.None;
    internal bool IsExact => Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or
        Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix or
        Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffixStrictEnd;
}
