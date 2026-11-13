using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReOperationBoundaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FinalPublicPollPreservesTimeoutDiagnosticsAndOriginalToken( bool expire, bool funded)
    {
        const string pattern = "([a-z]+)-([0-9]+)";
        var input = "item-12 other-345"u8.ToArray();
        var timeout = TimeSpan.FromMilliseconds(20);
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(1 << 20);
        Assert.True(account.TryReserve(17));
        using var regex = funded
            ? Utf8PythonRegex.Compile(pattern, default, timeout, default, default, Timeout.InfiniteTimeSpan, default, account)
            : new Utf8PythonRegex(pattern, matchTimeout: timeout);
        for (var operation = 0; operation < 14; operation++)
        {
            // Observe the final poll of the real public route, including projection
            // and output. Warm cold preparation before counting those polls.
            using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => 0)) Invoke(regex, input, operation, default);
            var reads = 0;
            using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { reads++; return 0; }))
                Invoke(regex, input, operation, default);
            var held = account.Used;
            var terminal = reads;
            Assert.True(terminal > 1);
            reads = 0;
            using var token = new CancellationTokenSource();
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            {
                if (++reads != terminal) return 0;
                if (!expire) token.Cancel();
                return expire ? 50 : 0;
            });
            var published = false;
            var failure = Record.Exception(() => { Invoke(regex, input, operation, token.Token); published = true; });
            Assert.False(published);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(timeout, error.MatchTimeout);
                Assert.Equal(funded ? string.Empty : "item-12 other-345", error.Input);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(held, account.Used);
        }
        regex.Dispose();
        Assert.Equal(17UL, account.Used);
        account.Release(17);
    }

    private static void Invoke(Utf8PythonRegex regex, byte[] input, int operation, CancellationToken token)
    {
        switch (operation)
        {
            case 0: _ = regex.IsMatch(input, 0, token); break;
            case 1: _ = regex.Search(input, 0, token); break;
            case 2: _ = regex.SearchDetailed(input, 0, token); break;
            case 3: _ = regex.SearchDetailedData(input, 0, token); break;
            case 4: _ = regex.FindIterDetailed(input, 0, token); break;
            case 5: _ = regex.FindAll(input, 0, token); break;
            case 6: _ = regex.FindAllToStrings(input, 0, token); break;
            case 7: _ = regex.FindAllToUtf8(input, 0, token); break;
            case 8: _ = regex.SplitRanges(input, 0, 0, token); break;
            case 9: _ = regex.SplitToStrings(input, 0, 0, token); break;
            case 10: _ = regex.ReplaceToString(input, @"\2:\1", 0, 0, token); break;
            case 11: _ = regex.SearchToString(input, 0, token); break;
            case 12: _ = regex.FullMatch(input, 0, token); break;
            case 13: _ = regex.Count(input, 0, token); break;
        }
    }
}
