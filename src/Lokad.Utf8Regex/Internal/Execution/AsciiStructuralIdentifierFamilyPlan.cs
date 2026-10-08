using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;

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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            Prefixes = prefixes;
            var overlap = Utf8SearchPlan.GetLiteralOverlap(prefixes);
            HasOverlappingPrefixes = overlap != Utf8AlternateLiteralOverlapKind.None;
            HasPrefixOverlap = (overlap & Utf8AlternateLiteralOverlapKind.SameStartPrefix) != 0;
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
                    ? CreateLiteralSuffixPart(suffixUtf8, owner)
                    : [];
            LeadingBoundary = leadingBoundary;
            TrailingBoundary = trailingBoundary;
            SeparatorCharClass = TryCreateAsciiCharClass(separatorSet, out var separatorCharClass)
                ? separatorCharClass
                : default;
            IdentifierStartCharClass = TryCreateAsciiCharClass(identifierStartSet, out var identifierStartCharClass)
                ? identifierStartCharClass
                : default;
            IdentifierTailCharClass = TryCreateAsciiCharClass(identifierTailSet, out var identifierTailCharClass)
                ? identifierTailCharClass
                : default;
            CompiledSuffixParts = CreateCompiledSuffixParts(SuffixParts);
            VerifierProgram = AsciiStructuralVerifierProgram.Create(this, CompiledSuffixParts);
            HasAsciiUpperWordTailKernel =
                SuffixParts.Length == 0 &&
                !IdentifierStartCharClass.IsEmpty &&
                !IdentifierTailCharClass.IsEmpty &&
                HasSamePositiveDefinition(IdentifierStartCharClass, static b => b is >= (byte)'A' and <= (byte)'Z') &&
                HasSamePositiveDefinition(IdentifierTailCharClass, static b =>
                    b is >= (byte)'A' and <= (byte)'Z' ||
                    b is >= (byte)'a' and <= (byte)'z' ||
                    b is >= (byte)'0' and <= (byte)'9' ||
                    b == (byte)'_');
            construction.Complete();
        }
        finally { owner.LeaveDepth(); }
    }

    public byte[][] Prefixes { get; }

    public bool HasOverlappingPrefixes { get; }

    public bool HasPrefixOverlap { get; }

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

    public AsciiCharClass SeparatorCharClass { get; }

    public AsciiCharClass IdentifierStartCharClass { get; }

    public AsciiCharClass IdentifierTailCharClass { get; }

    public AsciiStructuralCompiledSuffixPart[] CompiledSuffixParts { get; }

    public bool HasAsciiUpperWordTailKernel { get; }

    // PLAN.md D05: single construction owner for compiled suffix parts; the linear verifier shares this array instead of rebuilding it.
    internal static AsciiStructuralCompiledSuffixPart[] CreateCompiledSuffixParts(AsciiStructuralSuffixPart[] suffixParts)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            if (suffixParts.Length == 0)
            {
                return [];
            }

            var compiled = owner.AllocateArray<AsciiStructuralCompiledSuffixPart>(suffixParts.Length, false);
            for (var i = 0; i < suffixParts.Length; i++)
            {
                owner.ChargeWork(1);
                var part = suffixParts[i];
                compiled[i] = part.IsLiteral
                    ? AsciiStructuralCompiledSuffixPart.CreateLiteral(part.LiteralUtf8)
                    : AsciiStructuralCompiledSuffixPart.CreateSeparator(
                        part.SeparatorSet,
                        TryCreateAsciiCharClass(part.SeparatorSet, out var charClass) ? charClass : default,
                        part.SeparatorMinCount);
            }

            construction.Complete(); return compiled;
        }
        finally { owner.LeaveDepth(); }
    }

    private static AsciiStructuralSuffixPart[] CreateLiteralSuffixPart(byte[] literal, Utf8ConstructionBudget owner)
    {
        var parts = owner.AllocateArray<AsciiStructuralSuffixPart>(1, false);
        owner.ChargeWork(1); parts[0] = AsciiStructuralSuffixPart.CreateLiteral(literal);
        return parts;
    }

    internal static bool TryCreateAsciiCharClass(string? runtimeSet, out AsciiCharClass charClass)
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
            Utf8ConstructionScope.ChargeWork(1);
            if (charClass.Contains((byte)i) != predicate((byte)i))
            {
                return false;
            }
        }

        return true;
    }
}

internal enum AsciiStructuralSuffixPartKind : byte
{
    None = 0,
    Literal = 1,
    Separator = 2,
}

internal readonly struct AsciiStructuralSuffixPart
{
    private readonly byte[]? _literalUtf8;
    private readonly string? _separatorSet;

    private AsciiStructuralSuffixPart(
        AsciiStructuralSuffixPartKind kind,
        byte[] literalUtf8,
        string separatorSet,
        int separatorMinCount)
    {
        Kind = kind;
        _literalUtf8 = literalUtf8;
        _separatorSet = separatorSet;
        SeparatorMinCount = separatorMinCount;
    }

    public AsciiStructuralSuffixPartKind Kind { get; }

    public byte[] LiteralUtf8 => _literalUtf8 ?? [];

    public string SeparatorSet => _separatorSet ?? string.Empty;

    public int SeparatorMinCount { get; }

    public bool IsLiteral => Kind == AsciiStructuralSuffixPartKind.Literal;

    public bool IsSeparator => Kind == AsciiStructuralSuffixPartKind.Separator;

    public static AsciiStructuralSuffixPart CreateLiteral(byte[] literalUtf8) =>
        new(AsciiStructuralSuffixPartKind.Literal, literalUtf8, string.Empty, 0);

    public static AsciiStructuralSuffixPart CreateSeparator(string separatorSet, int separatorMinCount) =>
        new(AsciiStructuralSuffixPartKind.Separator, [], separatorSet, separatorMinCount);
}

