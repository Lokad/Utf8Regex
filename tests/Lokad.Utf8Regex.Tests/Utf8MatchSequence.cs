namespace Lokad.Utf8Regex.Tests;

// Aggregate the production enumerator without hiding its subject/start/token.
internal static class Utf8MatchSequence
{
    internal static int Count(Utf8ValueMatchEnumerator matches)
    {
        var count = 0;
        while (matches.MoveNext()) count = checked(count + 1);
        return count;
    }
}
