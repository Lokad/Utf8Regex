namespace Lokad.Utf8Regex.PythonRe;

internal abstract record PythonReNode;

internal sealed record PythonReAlternationNode(IReadOnlyList<PythonReNode> Branches) : PythonReNode;

internal sealed record PythonReSequenceNode(IReadOnlyList<PythonReNode> Elements) : PythonReNode;

internal sealed record PythonReLiteralNode(char Value) : PythonReNode;

internal sealed record PythonReRawNode(string Text, PythonReRawKind Kind) : PythonReNode;

internal sealed record PythonReCharacterClassNode(
    bool IsNegated,
    IReadOnlyList<PythonReCharacterClassItem> Items) : PythonReNode;

internal sealed record PythonReQuantifierNode(
    PythonReNode Inner,
    int Min,
    int? Max,
    PythonReQuantifierFlavor Flavor) : PythonReNode;

internal sealed record PythonReGroupNode(
    PythonReGroupKind Kind,
    PythonReNode Inner,
    string? Name,
    PythonReCompileOptions AddOptions,
    PythonReCompileOptions RemoveOptions) : PythonReNode
{
    internal int GroupNumber { get; init; }
}

internal sealed record PythonReNamedBackreferenceNode(string Name) : PythonReNode
{
    internal Func<int, int>? LowerScalar { get; init; }
}

internal sealed record PythonReNumericBackreferenceNode(int GroupNumber) : PythonReNode
{
    internal Func<int, int>? LowerScalar { get; init; }
}

internal sealed record PythonReConditionalNode(
    PythonReConditionalReference Reference,
    PythonReNode YesBranch,
    PythonReNode? NoBranch) : PythonReNode;

internal readonly record struct PythonReConditionalReference(
    PythonReConditionalReferenceKind Kind,
    int GroupNumber,
    string? GroupName)
{
    public static PythonReConditionalReference ForGroupNumber(int groupNumber) => new(PythonReConditionalReferenceKind.GroupNumber, groupNumber, null);

    public static PythonReConditionalReference ForGroupName(string groupName) => new(PythonReConditionalReferenceKind.GroupName, 0, groupName);
}

internal enum PythonReConditionalReferenceKind
{
    GroupNumber,
    GroupName,
}

internal enum PythonReRawKind
{
    Escape,
    Dot,
    StartAnchor,
    AstralLiteral,
    EndAnchor,
}

internal abstract record PythonReCharacterClassItem;

internal sealed record PythonReCharacterClassLiteralItem(int Scalar) : PythonReCharacterClassItem;

internal sealed record PythonReCharacterClassRangeItem(int StartScalar, int EndScalar) : PythonReCharacterClassItem;

internal sealed record PythonReCharacterClassPredefinedItem(string RegexClassFragment, bool IsStandaloneAtom) : PythonReCharacterClassItem;

internal enum PythonReQuantifierFlavor
{
    Greedy,
    Reluctant,
    Possessive,
}

internal enum PythonReGroupKind
{
    Capturing,
    NamedCapturing,
    NonCapturing,
    PositiveLookahead,
    NegativeLookahead,
    PositiveLookbehind,
    NegativeLookbehind,
    Atomic,
    ScopedFlags,
}

internal readonly record struct PythonReWidth(UInt128 Min, UInt128 Max)
{
    // CPython 3.13.2 getwidth clamps at 2^64, including the unbounded sentinel.
    // UInt128 holds that endpoint and the intermediate sums/products exactly.
    internal static UInt128 Maximum => (UInt128)ulong.MaxValue + 1;
    public bool IsFixed => Max == Min;
}
