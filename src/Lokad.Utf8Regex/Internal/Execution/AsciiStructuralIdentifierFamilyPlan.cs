using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiStructuralIdentifierFamilyPlan
{
    public AsciiStructuralIdentifierFamilyPlan(
        byte[][] prefixes,
        AsciiCharClass? separatorClass,
        int separatorMinCount,
        AsciiCharClass? identifierStartClass,
        AsciiCharClass? identifierTailClass,
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
            HasPrefixOverlap = (overlap & Utf8AlternateLiteralOverlapKind.SameStartPrefix) != 0;
            // A proper-start overlap inside an ASCII word cannot satisfy the
            // leading boundary. Same-start alternatives still need scalar retry
            // when a shorter prefix's suffix fails before a longer one succeeds.
            HasUnresolvedPrefixOverlap = overlap != Utf8AlternateLiteralOverlapKind.None &&
                (HasPrefixOverlap || leadingBoundary != Utf8BoundaryRequirement.Boundary ||
                 !HaveOnlyWordPrefixes(prefixes, owner));
            HasSeparator = separatorClass.HasValue;
            SeparatorMinCount = separatorMinCount;
            HasIdentifier = identifierStartClass.HasValue;
            IdentifierTailMinCount = identifierTailMinCount;
            IdentifierTailMaxCount = identifierTailMaxCount;
            SuffixParts = suffixParts is { Length: > 0 }
                ? suffixParts
                : suffixUtf8 is { Length: > 0 }
                    ? CreateLiteralSuffixPart(suffixUtf8, owner)
                    : [];
            LeadingBoundary = leadingBoundary;
            TrailingBoundary = trailingBoundary;
            SeparatorCharClass = separatorClass.GetValueOrDefault();
            IdentifierStartCharClass = identifierStartClass.GetValueOrDefault();
            IdentifierTailCharClass = identifierTailClass.GetValueOrDefault();

            VerifierProgram = AsciiStructuralVerifierProgram.Create(this);
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

    public bool HasUnresolvedPrefixOverlap { get; }

    public bool HasPrefixOverlap { get; }

    public bool HasSeparator { get; }

    public int SeparatorMinCount { get; }

    public bool HasIdentifier { get; }

    public int IdentifierTailMinCount { get; }

    public int IdentifierTailMaxCount { get; }

    public AsciiStructuralSuffixPart[] SuffixParts { get; }

    public Utf8BoundaryRequirement LeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingBoundary { get; }

    public AsciiStructuralVerifierProgram VerifierProgram { get; }

    public AsciiCharClass SeparatorCharClass { get; }

    public AsciiCharClass IdentifierStartCharClass { get; }

    public AsciiCharClass IdentifierTailCharClass { get; }

    public bool HasAsciiUpperWordTailKernel { get; }

    private static AsciiStructuralSuffixPart[] CreateLiteralSuffixPart(byte[] literal, Utf8ConstructionBudget owner)
    {
        var parts = owner.AllocateArray<AsciiStructuralSuffixPart>(1, false);
        owner.ChargeWork(1); parts[0] = AsciiStructuralSuffixPart.CreateLiteral(literal);
        return parts;
    }



    private static bool HaveOnlyWordPrefixes(byte[][] prefixes, Utf8ConstructionBudget owner)
    {
        foreach (var prefix in prefixes)
        foreach (var value in prefix)
        {
            owner.ChargeWork(1);
            if (!AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(value)) return false;
        }
        return true;
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
    private readonly PreparedSubstringSearch _literalSearch;
    private readonly AsciiCharClass _separatorCharClass;

    private AsciiStructuralSuffixPart(
        AsciiStructuralSuffixPartKind kind,
        PreparedSubstringSearch literalSearch,
        AsciiCharClass separatorCharClass,
        int separatorMinCount)
    {
        Kind = kind;
        _literalSearch = literalSearch;
        _separatorCharClass = separatorCharClass;
        SeparatorMinCount = separatorMinCount;
    }

    public AsciiStructuralSuffixPartKind Kind { get; }

    public byte[] LiteralUtf8 => _literalSearch.Needle ?? [];

    internal PreparedSubstringSearch LiteralSearch => _literalSearch;

    public AsciiCharClass SeparatorCharClass => _separatorCharClass;

    public int SeparatorMinCount { get; }

    public bool IsLiteral => Kind == AsciiStructuralSuffixPartKind.Literal;

    public bool IsSeparator => Kind == AsciiStructuralSuffixPartKind.Separator;

    public static AsciiStructuralSuffixPart CreateLiteral(byte[] literalUtf8) =>
        new(AsciiStructuralSuffixPartKind.Literal, new PreparedSubstringSearch(literalUtf8, false), default, 0);

    public static AsciiStructuralSuffixPart CreateSeparator(AsciiCharClass separatorCharClass, int separatorMinCount) =>
        new(AsciiStructuralSuffixPartKind.Separator, default, separatorCharClass, separatorMinCount);
}

