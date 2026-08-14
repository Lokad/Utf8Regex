using Lokad.Utf8Regex.Internal.Planning;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiStructuralIdentifierFamilyPlan
{
    public AsciiStructuralIdentifierFamilyPlan(
        byte[][] prefixes,
        string? separatorSet,
        int separatorMinCount,
        string identifierStartSet,
        string identifierTailSet,
        int identifierTailMinCount,
        int identifierTailMaxCount,
        byte[]? suffixUtf8,
        AsciiStructuralSuffixPart[]? suffixParts,
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary)
    {
        Prefixes = prefixes;
        SeparatorSet = separatorSet;
        SeparatorMinCount = separatorMinCount;
        IdentifierStartSet = identifierStartSet;
        IdentifierTailSet = identifierTailSet;
        IdentifierTailMinCount = identifierTailMinCount;
        IdentifierTailMaxCount = identifierTailMaxCount;
        SuffixUtf8 = suffixUtf8;
        SuffixParts = suffixParts is { Length: > 0 }
            ? suffixParts
            : suffixUtf8 is { Length: > 0 }
                ? [AsciiStructuralSuffixPart.CreateLiteral(suffixUtf8)]
                : [];
        LeadingBoundary = leadingBoundary;
        TrailingBoundary = trailingBoundary;
        VerifierProgram = AsciiStructuralVerifierProgram.Create(this);
        SeparatorCharClass = TryCreateAsciiCharClass(separatorSet, out var separatorCharClass)
            ? separatorCharClass
            : null;
        IdentifierStartCharClass = TryCreateAsciiCharClass(identifierStartSet, out var identifierStartCharClass)
            ? identifierStartCharClass
            : null;
        IdentifierTailCharClass = TryCreateAsciiCharClass(identifierTailSet, out var identifierTailCharClass)
            ? identifierTailCharClass
            : null;
        CompiledSuffixParts = CreateCompiledSuffixParts(SuffixParts);
        HasAsciiUpperWordTailKernel =
            SuffixParts.Length == 0 &&
            IdentifierStartCharClass is not null &&
            IdentifierTailCharClass is not null &&
            HasSamePositiveDefinition(IdentifierStartCharClass, static b => b is >= (byte)'A' and <= (byte)'Z') &&
            HasSamePositiveDefinition(IdentifierTailCharClass, static b =>
                b is >= (byte)'A' and <= (byte)'Z' ||
                b is >= (byte)'a' and <= (byte)'z' ||
                b is >= (byte)'0' and <= (byte)'9' ||
                b == (byte)'_');
    }

    public byte[][] Prefixes { get; }

    public string? SeparatorSet { get; }

    public int SeparatorMinCount { get; }

    public string IdentifierStartSet { get; }

    public string IdentifierTailSet { get; }

    public int IdentifierTailMinCount { get; }

    public int IdentifierTailMaxCount { get; }

    public byte[]? SuffixUtf8 { get; }

    public AsciiStructuralSuffixPart[] SuffixParts { get; }

    public Utf8BoundaryRequirement LeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingBoundary { get; }

    public AsciiStructuralVerifierProgram VerifierProgram { get; }

    public AsciiCharClass? SeparatorCharClass { get; }

    public AsciiCharClass? IdentifierStartCharClass { get; }

    public AsciiCharClass? IdentifierTailCharClass { get; }

    public AsciiStructuralCompiledSuffixPart[] CompiledSuffixParts { get; }

    public bool HasAsciiUpperWordTailKernel { get; }

    private static AsciiStructuralCompiledSuffixPart[] CreateCompiledSuffixParts(AsciiStructuralSuffixPart[] suffixParts)
    {
        if (suffixParts.Length == 0)
        {
            return [];
        }

        var compiled = new AsciiStructuralCompiledSuffixPart[suffixParts.Length];
        for (var i = 0; i < suffixParts.Length; i++)
        {
            var part = suffixParts[i];
            compiled[i] = new AsciiStructuralCompiledSuffixPart(
                part.LiteralUtf8,
                part.SeparatorSet,
                TryCreateAsciiCharClass(part.SeparatorSet, out var charClass) ? charClass : null,
                part.SeparatorMinCount);
        }

        return compiled;
    }

    private static bool TryCreateAsciiCharClass(string? runtimeSet, out AsciiCharClass charClass)
    {
        return FrontEnd.DotNetAsciiCharClassProjector.TryProjectWholeClass(runtimeSet, out charClass);
    }

    private static bool HasSamePositiveDefinition(AsciiCharClass charClass, Func<byte, bool> predicate)
    {
        if (charClass.Negated)
        {
            return false;
        }

        for (var i = 0; i < 0x80; i++)
        {
            if (charClass.Contains((byte)i) != predicate((byte)i))
            {
                return false;
            }
        }

        return true;
    }
}

internal readonly struct AsciiStructuralSuffixPart
{
    private AsciiStructuralSuffixPart(byte[]? literalUtf8, string? separatorSet, int separatorMinCount)
    {
        LiteralUtf8 = literalUtf8;
        SeparatorSet = separatorSet;
        SeparatorMinCount = separatorMinCount;
    }

    public byte[]? LiteralUtf8 { get; }

    public string? SeparatorSet { get; }

    public int SeparatorMinCount { get; }

    public bool IsLiteral => LiteralUtf8 is { Length: > 0 };

    public bool IsSeparator => !string.IsNullOrEmpty(SeparatorSet);

    public static AsciiStructuralSuffixPart CreateLiteral(byte[] literalUtf8) => new(literalUtf8, null, 0);

    public static AsciiStructuralSuffixPart CreateSeparator(string separatorSet, int separatorMinCount) => new(null, separatorSet, separatorMinCount);
}

