using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal static partial class PythonReScalarCompiler
{
    // These spellings come only from the Python parser's semantic fragments.
    // Keep profile classes in the existing VM, without reparsing expanded data
    // or introducing a second character-class engine.
    private static Utf8SliceNode? ConvertPredefined(string text, Utf8ConstructionArena arena)
    {
        switch (text)
        {
            case PythonReParser.AsciiWordBoundary:
                return PythonReScalarNodeFactory.AsciiBoundary(true);
            case PythonReParser.AsciiNonWordBoundary:
                return RejectEmptySubject(PythonReScalarNodeFactory.AsciiBoundary(false), arena);
            case PythonReParser.UnicodeWordClass:
            case PythonReParser.UnicodeNonWordClass:
                return PythonReScalarNodeFactory.Class(PythonReScalarClassData.WordAscii,
                    text == PythonReParser.UnicodeNonWordClass, PythonReScalarClassData.IsWord, arena);
            case @"\d":
            case PythonReParser.UnicodeNonDigitComplement:
                return PythonReScalarNodeFactory.Class(PythonReScalarClassData.DigitAscii,
                    text == PythonReParser.UnicodeNonDigitComplement, PythonReScalarClassData.IsDigit, arena);
            case PythonReParser.UnicodeSpaceClass:
            case PythonReParser.UnicodeNonSpaceClass:
                return PythonReScalarNodeFactory.Class(PythonReScalarClassData.SpaceAscii,
                    text == PythonReParser.UnicodeNonSpaceClass, PythonReScalarClassData.IsSpace, arena);
            case PythonReParser.UnicodeWordBoundary:
            case PythonReParser.UnicodeNonWordBoundary:
                var boundary = text == PythonReParser.UnicodeWordBoundary;
                var word = PythonReScalarNodeFactory.Class(PythonReScalarClassData.WordAscii, false,
                    PythonReScalarClassData.IsWord, arena);
                var branches = arena.Allocate<Utf8SliceNode[]>(2, true);
                branches[0] = Pair(PythonReScalarNodeFactory.Lookbehind(word, true, 1),
                    PythonReScalarNodeFactory.Lookahead(word, !boundary), arena);
                branches[1] = Pair(PythonReScalarNodeFactory.Lookbehind(word, false, 1),
                    PythonReScalarNodeFactory.Lookahead(word, boundary), arena);
                var result = PythonReScalarNodeFactory.Alternation(branches);
                if (boundary) return result;
                return RejectEmptySubject(result, arena);
            default:
                return null;
        }
    }

    private static Utf8SliceNode RejectEmptySubject(Utf8SliceNode boundary, Utf8ConstructionArena arena)
    {
        // CPython 3.13's non-boundary rejects an empty subject.
        var empty = PythonReScalarNodeFactory.Concat(Pair(
            PythonReScalarNodeFactory.Anchor(Utf8SliceAnchorKind.Beginning),
            PythonReScalarNodeFactory.Anchor(Utf8SliceAnchorKind.VeryEnd), arena));
        return PythonReScalarNodeFactory.Concat(Pair(
            PythonReScalarNodeFactory.Lookahead(empty, false), boundary, arena));
    }

    private static Utf8SliceNode? ConvertBracket(PythonReCharacterClassNode characterClass,
        RegexOptions options, Utf8ConstructionArena arena)
    {
        var firstPredefined = 0;
        for (; firstPredefined < characterClass.Items.Count; firstPredefined++)
        {
            arena.Budget.ChargeWork(1);
            if (characterClass.Items[firstPredefined] is PythonReCharacterClassPredefinedItem item
                && PythonReParser.IsUnicodePredefinedFragment(item.RegexClassFragment)) break;
        }
        // Ordinary literal/ASCII classes retain their original leaf preparation;
        // do not acquire staging arrays merely to discover no profile predicate.
        if (firstPredefined == characterClass.Items.Count) return null;
        using var literals = new Utf8ConstructionBuffer<PythonReCharacterClassItem>(arena.Budget);
        using var branches = new Utf8ConstructionBuffer<Utf8SliceNode>(arena.Budget);
        for (var index = 0; index < characterClass.Items.Count; index++)
        {
            arena.Budget.ChargeWork(1);
            var item = characterClass.Items[index];
            if (item is PythonReCharacterClassPredefinedItem predefined
                && ConvertPredefined(predefined.RegexClassFragment, arena) is { } converted)
                branches.Add(converted);
            else literals.Add(item);
        }
        if (branches.Count == 0) return null;
        if (literals.Count != 0)
            branches.Add(ParseLeaf(PythonReAstFactory.CharacterClass(false, arena.Take(literals, true)), options, arena));
        Utf8SliceNode positive;
        if (branches.Count == 1) positive = branches[0];
        else
        {
            var alternatives = arena.Allocate<Utf8SliceNode[]>(branches.Count, true);
            for (var index = 0; index < branches.Count; index++)
            {
                arena.Budget.ChargeWork(1);
                var branch = arena.Allocate<Utf8SliceNode>(1, true);
                branch[0] = branches[index];
                alternatives[index] = branch;
            }
            positive = PythonReScalarNodeFactory.Alternation(alternatives);
        }
        return characterClass.IsNegated
            ? PythonReScalarNodeFactory.Concat(Pair(PythonReScalarNodeFactory.Lookahead(positive, false),
                PythonReScalarNodeFactory.Dot(), arena))
            : positive;
    }

    private static Utf8SliceNode[] Pair(Utf8SliceNode first, Utf8SliceNode second, Utf8ConstructionArena arena)
    {
        var result = arena.Allocate<Utf8SliceNode>(2, true);
        result[0] = first;
        result[1] = second;
        return result;
    }
}
