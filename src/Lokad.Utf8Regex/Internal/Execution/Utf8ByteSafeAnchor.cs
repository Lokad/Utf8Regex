namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ByteSafeAnchor
{
    internal static bool MatchesBeginning(ReadOnlySpan<byte> input, int index, Utf8ExecutionNodeKind kind,
        RegexOptions options, int origin)
        => kind switch
        {
            Utf8ExecutionNodeKind.Beginning => index == 0,
            Utf8ExecutionNodeKind.Start => index == origin,
            _ => index == 0 || (options & RegexOptions.Multiline) != 0 && input[index - 1] == (byte)'\n',
        };

    internal static bool MatchesEnd(ReadOnlySpan<byte> input, int index, Utf8ExecutionNodeKind kind, RegexOptions options)
        => index == input.Length || kind != Utf8ExecutionNodeKind.End &&
            (index == input.Length - 1 && input[index] == (byte)'\n' ||
             kind == Utf8ExecutionNodeKind.Eol && (options & RegexOptions.Multiline) != 0 && input[index] == (byte)'\n');
}
