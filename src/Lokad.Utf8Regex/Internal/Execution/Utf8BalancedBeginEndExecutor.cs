namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8BalancedBeginEndExecutor
{
    private const int MaxNativeNestingDepth = 128;

    public static bool TryCount(ReadOnlySpan<byte> input, out int count)
    {
        Span<int> completedChildrenByDepth = stackalloc int[MaxNativeNestingDepth];
        var depth = 0;
        var rootCount = 0;
        var searchStart = 0;

        while (searchStart < input.Length)
        {
            var relativeIndex = input[searchStart..].IndexOfAny((byte)'B', (byte)'E');
            if (relativeIndex < 0)
            {
                break;
            }

            var tokenIndex = searchStart + relativeIndex;
            if (input[tokenIndex] == (byte)'B' && input[tokenIndex..].StartsWith("BEGIN"u8))
            {
                if (depth == completedChildrenByDepth.Length)
                {
                    count = 0;
                    return false;
                }

                completedChildrenByDepth[depth] = 0;
                depth++;
                searchStart = tokenIndex + "BEGIN"u8.Length;
                continue;
            }

            if (input[tokenIndex] == (byte)'E' && input[tokenIndex..].StartsWith("END"u8))
            {
                if (depth > 0)
                {
                    depth--;
                    if (depth == 0)
                    {
                        rootCount++;
                    }
                    else
                    {
                        completedChildrenByDepth[depth - 1]++;
                    }
                }

                searchStart = tokenIndex + "END"u8.Length;
                continue;
            }

            searchStart = tokenIndex + 1;
        }

        // A failed outer BEGIN does not hide the complete matches nested within it:
        // .NET retries at the next BEGIN and promotes those completed child blocks.
        while (depth > 0)
        {
            depth--;
            var completedChildren = completedChildrenByDepth[depth];
            if (depth == 0)
            {
                rootCount += completedChildren;
            }
            else
            {
                completedChildrenByDepth[depth - 1] += completedChildren;
            }
        }

        count = rootCount;
        return true;
    }
}
