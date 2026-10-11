using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

// This optimizer publishes complete UTF-8 fragments. Declines restore speculative
// capacity and roots without refunding work; the prepared compiler owns other grammar.
internal static class Pcre2SimpleReplacementParser
{
    internal static Pcre2SimpleReplacementPlan Literal(string replacement)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(scope.Budget);
        var segments = storage.Allocate<Pcre2SimpleReplacementSegment>(1);
        segments[0] = FromLiteral(storage, replacement.AsSpan());
        Pcre2ReplacementPreparationControl.Check();
        storage.Complete(); scope.Complete();
        return new(segments);
    }

    internal static bool TryParse(string replacement, Pcre2SubstitutionOptions options, out Pcre2SimpleReplacementPlan plan)
    {
        plan = default;
        if (options != Pcre2SubstitutionOptions.None) return false;
        using var scope = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(scope.Budget);
        var segments = storage.Buffer<Pcre2SimpleReplacementSegment>();
        var literalStart = 0;
        for (var index = 0; index < replacement.Length; index++)
        {
            Pcre2ReplacementPreparationControl.Check();
            storage.Budget.ChargeWork(1);
            if (replacement[index] != '$' || index + 1 == replacement.Length) continue;
            storage.Budget.ChargeWork(1);
            var next = replacement[index + 1];
            if (next == '0' && index + 2 < replacement.Length)
            {
                storage.Budget.ChargeWork(1);
                // $01 is group 1, not group 0 followed by a literal 1.
                if (char.IsAsciiDigit(replacement[index + 2])) return false;
            }
            var end = index + 1;
            var wholeMatch = next is '&' or '0';
            if (next == '{')
            {
                if (index + 3 >= replacement.Length) return false;
                storage.Budget.ChargeWork(2);
                if (replacement[index + 2] != '0' || replacement[index + 3] != '}') return false;
                wholeMatch = true;
                end = index + 3;
            }
            else if (!wholeMatch && next != '$') return false;

            if (index > literalStart) segments.Add(FromLiteral(storage, replacement.AsSpan(literalStart, index - literalStart)));
            segments.Add(wholeMatch ? new([], true) : FromLiteral(storage, "$".AsSpan()));
            index = end;
            literalStart = index + 1;
        }
        if (segments.Count == 0 || literalStart < replacement.Length)
            segments.Add(FromLiteral(storage, replacement.AsSpan(literalStart)));
        plan = new(storage.Copy(segments));
        Pcre2ReplacementPreparationControl.Check();
        storage.Complete(); scope.Complete();
        return true;
    }

    private static Pcre2SimpleReplacementSegment FromLiteral(Utf8ConstructionSession storage, ReadOnlySpan<char> literal)
        => new(storage.EncodeRepairedUtf8(literal, default(Pcre2ReplacementTextControl)), false);
}

// Value descriptors borrow the admitted segment array and its payloads.
internal readonly record struct Pcre2SimpleReplacementPlan(Pcre2SimpleReplacementSegment[] Segments);
internal readonly record struct Pcre2SimpleReplacementSegment(byte[] LiteralUtf8, bool IsWholeMatch);
