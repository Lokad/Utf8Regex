using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;
namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiCharClassRunPlan
{
    public AsciiCharClassRunPlan(AsciiCharClass charClass, int minLength, int maxLength)
    {
        CharClass = charClass;
        PredicateKind = charClass.TryGetKnownPredicateKind(out var predicateKind)
            ? predicateKind
            : AsciiCharClassPredicateKind.None;
        MinLength = minLength;
        MaxLength = maxLength;
        Search = Utf8PreparedByteSearchFactory.CreateFromPositiveClass(charClass);
    }

    public AsciiCharClass CharClass { get; }

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => !CharClass.IsEmpty && MinLength > 0 && MaxLength >= MinLength;
}
