using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReOperationTests
{
    internal static IEnumerable<int> OperationIds()
    {
        for (var operation = 0; operation < 25; operation++)
            if (operation is not (5 or 6 or 7 or 11 or 12 or 13 or 15 or 14 or 16 or 17 or 19 or 20 or 22 or 23 or 24))
                yield return operation;
    }

    public static IEnumerable<object[]> Operations()
    {
        // Exact literal, two native-specialized forms, and general scalar discovery.
        foreach (var (pattern, input) in new[] { ("cat", "cat"), ("cat\\d+", "cat123"),
            ("(?s:cat.*dog)", "cat😀dog"), ("(?P<x>a|ab)+(?P=x)", "abab"), (".*?", "😀a"),
            ("foo(?=bar)", "foobar") })
            foreach (var operation in OperationIds())
                yield return [operation, pattern, input];
    }

    internal static string Run(Utf8PythonRegex regex, byte[] input, int operation, int start = 0,
        CancellationToken token = default)
    {
        if (operation is >= 8 and <= 10)
        {
            using var cursor = regex.CreateMatchCursor(input, start, token, mode: (Utf8PythonMatchMode)(operation - 8));
            return cursor.MoveNext(out _).ToString();
        }
        return operation switch
        {
            0 => regex.IsMatch(input, start, token).ToString(),
            1 => regex.Match(input, start, cancellationToken: token, mode: Utf8PythonMatchMode.Search).Success.ToString(),
            2 => regex.Match(input, start, cancellationToken: token).Success.ToString(),
            3 => regex.Match(input, start, cancellationToken: token, mode: Utf8PythonMatchMode.FullMatch).Success.ToString(),
            4 => regex.Count(input, start, token).ToString(),
            18 => string.Join("|", regex.SplitRanges(input, 0, start, token).Select(x => $"{x.Number}:{x.Success}:{x.StartOffsetInBytes}:{x.EndOffsetInBytes}")),
            21 => Encoding.UTF8.GetString(regex.Subn(input, "<\\g<0>>"u8, 0, start, token).ResultBytes),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    [Fact]
    public void CancellationAndExpiredDeadlineKeepTheirIdentity()
    {
        using var token = new CancellationTokenSource();
        token.Cancel();
        var regex = new Utf8PythonRegex("cat");
        var error = Assert.Throws<OperationCanceledException>(() => regex.Match("cat"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search));
        Assert.Equal(token.Token, error.CancellationToken);
        Assert.Throws<OperationCanceledException>(() => regex.CreateMatchCursor("cat"u8.ToArray(), 0, token.Token));

        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks += 100);
        regex = new("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("cat"u8, mode: Utf8PythonMatchMode.Search));
    }
}
