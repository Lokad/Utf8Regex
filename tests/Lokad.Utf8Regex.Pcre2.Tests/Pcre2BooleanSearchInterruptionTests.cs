using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BooleanSearchInterruptionTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void SearchKeepsOriginalAuthorityAndRetainedCaptures(int kind, bool expire)
    {
        var pattern = kind switch
        {
            0 => @"^((.)(?1)\2|.?)$",
            1 => @".*(ss)",
            _ => @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?",
        };
        var shortText = kind switch { 0 => "abba", 1 => "glass", _ => "x://ab" };
        var input = Encoding.UTF8.GetBytes(kind switch
        {
            0 => new string('a', 256) + "b" + new string('a', 256),
            1 => new string('a', 65536) + "ss",
            _ => new string('a', 8192) + "://ab",
        });
        using var source = new CancellationTokenSource();
        var reads = 0;
        var entered = false;
        var armed = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            // One invocation start, its entry check, then a later work check.
            // PCRE2 backtracking does not use the scalar VM's diagnostic hook.
            if (!armed || ++reads < 3) return 0;
            entered = true;
            if (expire) return 50;
            source.Cancel();
            return 0;
        });
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 5_000_000, DepthLimit = 8192,
                HeapLimitInBytes = 16 << 20, ResultLimitInBytes = 16 << 20 },
            TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed(Encoding.UTF8.GetBytes(shortText));
        armed = true;
        Exception? failure = null;
        try { _ = regex.IsMatch(input, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        finally { armed = false; }
        Assert.True(entered);
        if (expire) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(shortText, retained.GetValueString());
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(shortText)));
    }
}
