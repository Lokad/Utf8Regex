using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// Every state includes all stored fields of its AST type. Referenced strings,
// lists and arrays retain their separate ownership; this admits the node and
// its retaining descriptor before either object is acquired.
internal static class PythonReAstFactory
{
    internal static PythonReSequenceNode Empty { get; } = new([]);

    internal static PythonReAlternationNode Alternation(IReadOnlyList<PythonReNode> branches) =>
        Create(branches, static state => new PythonReAlternationNode(state));

    internal static PythonReSequenceNode Sequence(IReadOnlyList<PythonReNode> elements) =>
        Create(elements, static state => new PythonReSequenceNode(state));

    internal static PythonReLiteralNode Literal(char value) =>
        Create(value, static state => new PythonReLiteralNode(state));

    internal static PythonReRawNode Raw(string text, PythonReRawKind kind) =>
        Create((text, kind), static state => new PythonReRawNode(state.text, state.kind));

    internal static PythonReCharacterClassNode CharacterClass(bool isNegated, IReadOnlyList<PythonReCharacterClassItem> items) =>
        Create((isNegated, items), static state => new PythonReCharacterClassNode(state.isNegated, state.items));

    internal static PythonReQuantifierNode Quantifier(PythonReNode inner, int min, int? max, PythonReQuantifierFlavor flavor) =>
        Create((inner, min, max, flavor), static state => new PythonReQuantifierNode(state.inner, state.min, state.max, state.flavor));

    internal static PythonReGroupNode Group(PythonReGroupKind kind, PythonReNode inner, string? name,
        PythonReCompileOptions addOptions, PythonReCompileOptions removeOptions, int groupNumber) =>
        Create((kind, inner, name, addOptions, removeOptions, groupNumber), static state =>
            new PythonReGroupNode(state.kind, state.inner, state.name, state.addOptions, state.removeOptions) { GroupNumber = state.groupNumber });

    internal static PythonReNamedBackreferenceNode NamedReference(string name, Func<int, int>? lowerScalar) =>
        Create((name, lowerScalar), static state => new PythonReNamedBackreferenceNode(state.name) { LowerScalar = state.lowerScalar });

    internal static PythonReNumericBackreferenceNode NumericReference(int groupNumber, Func<int, int>? lowerScalar) =>
        Create((groupNumber, lowerScalar), static state => new PythonReNumericBackreferenceNode(state.groupNumber) { LowerScalar = state.lowerScalar });

    internal static PythonReConditionalNode Conditional(PythonReConditionalReference reference, PythonReNode yesBranch, PythonReNode? noBranch) =>
        Create((reference, yesBranch, noBranch), static state => new PythonReConditionalNode(state.reference, state.yesBranch, state.noBranch));

    internal static PythonReCharacterClassLiteralItem ClassLiteral(int scalar) =>
        Create(scalar, static state => new PythonReCharacterClassLiteralItem(state));

    internal static PythonReCharacterClassRangeItem ClassRange(int startScalar, int endScalar) =>
        Create((startScalar, endScalar), static state => new PythonReCharacterClassRangeItem(state.startScalar, state.endScalar));

    internal static PythonReCharacterClassPredefinedItem ClassPredefined(string fragment, bool isStandaloneAtom) =>
        Create((fragment, isStandaloneAtom), static state => new PythonReCharacterClassPredefinedItem(state.fragment, state.isStandaloneAtom));

    private static TNode Create<TState, TNode>(TState state, Func<TState, TNode> factory) where TNode : class =>
        Utf8ConstructionScope.CurrentBudget.AllocateObject(state, factory);
}
