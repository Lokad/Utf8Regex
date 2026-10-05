using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceReplacementLifetimeTests
{
    public static IEnumerable<object[]> FiniteSubjects()
    {
        yield return [@"(a\D{2,}b)", "xa\U0001f600b", "<$1>"];
        yield return [@"(a\D{3,}b)", "xa\U0001f600b", "<$1>"];
        yield return [@"(\D{2,})", "\U0001d7ce\U0001d7ce1\U00010400a2", "<$1>"];
        yield return [@"([\p{Cs}]{2,})", "\U00010400a\U0001f600", "<$1>"];
        yield return [@"([^\p{Cs}]{2,})", "ab\U00010400cd", "<$1>"];
        yield return [@"(.{3,})", "\U0001f600\nx\U0001f600\n", "<$1>"];
        yield return [@"(?s)(.{3,})", "\U0001f600\nx", "<$1>"];
        yield return [@"(?s)(.*)", "a\U0001f600\nb", "<$1>"];
        yield return [@"(?<=a+)b", "xaaabxab", "<$&>"];
        yield return [@"(a|ab)+$", "xaab", "<$1>"];
        yield return [@"(a|ab)+$", "", "<$1>"];
    }

    [Theory]
    [MemberData(nameof(FiniteSubjects))]
    public void FiniteReplacementKeepsAllOwnedSurfaces(string pattern, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var timeout = TimeSpan.FromSeconds(30);
            var oracle = new Regex(pattern, options, timeout);
            var regex = new Utf8Regex(pattern, options, timeout);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            using var source = new CancellationTokenSource();
            foreach (var replacement in new[] { "Q", "", template, "$`|$&|$'|$_" })
            {
                var expected = oracle.Replace(text, replacement);
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                foreach (var operation in Operations)
                {
                    Assert.Equal(expected, Replace(regex, input, replacement, operation, source.Token));
                    Assert.Equal(expected, Replace(regex, input, replacement, operation, null));
                }
                if (expectedBytes.Length > 0)
                {
                    var destination = new byte[expectedBytes.Length - 1];
                    Array.Fill(destination, (byte)0xa5);
                    Assert.Equal(OperationStatus.DestinationTooSmall,
                        regex.TryReplace(input, replacement, destination, out var written, source.Token));
                    Assert.Equal(0, written);
                    Assert.All(destination, b => Assert.Equal((byte)0xa5, b));
                }
            }
            AssertOwned(regex);
        }
    }

    private static readonly string[] Operations = ["string", "bytes-string", "bytes-pattern", "destination-string", "destination-pattern"];

    public static IEnumerable<object[]> ReplacementSurfaces()
    {
        foreach (var operation in Operations)
        {
            yield return [operation, "Q"];
            yield return [operation, "<$1>"];
        }
    }

    [Theory]
    [MemberData(nameof(ReplacementSurfaces))]
    public void CancellationAfterVmEntryCarriesTheCallerToken(string operation, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 5000) + "!");
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex("(a|ab)+$", options, TimeSpan.FromSeconds(30));
            using var source = new CancellationTokenSource();
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                entered = true;
                source.Cancel();
                if (steps >= 128)
                {
                    throw new InvalidOperationException("The caller's cancellation did not reach the next VM step.");
                }
            });
            try
            {
                var exception = Assert.Throws<OperationCanceledException>(
                    () => Replace(regex, input, replacement, operation, source.Token));
                Assert.True(entered);
                Assert.Equal(source.Token, exception.CancellationToken);
            }
            finally
            {
                session.Complete();
            }
            AssertRecovered(regex, options, operation, replacement);
        }
    }

    [Theory]
    [MemberData(nameof(ReplacementSurfaces))]
    public void DeadlineAfterVmEntryUsesThePublicTimeoutIdentity(string operation, string replacement)
    {
        var text = new string('a', 5000) + "!";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var timeout = TimeSpan.FromMilliseconds(20);
            var regex = new Utf8Regex("(a|ab)+$", options, timeout);
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                if (!entered)
                {
                    entered = true;
                    Thread.Sleep(50);
                }
            });
            try
            {
                var exception = Assert.Throws<RegexMatchTimeoutException>(
                    () => Replace(regex, input, replacement, operation, null));
                Assert.True(entered);
                Assert.Equal(text, exception.Input);
                Assert.Equal("(a|ab)+$", exception.Pattern);
                Assert.Equal(timeout, exception.MatchTimeout);
            }
            finally
            {
                session.Complete();
            }
            AssertRecovered(regex, options, operation, replacement);
        }
    }

    private static string Replace(Utf8Regex regex, byte[] input, string replacement, string operation, CancellationToken? token)
    {
        switch (operation)
        {
            case "string":
                return token.HasValue ? regex.ReplaceToString(input, replacement, token.Value) : regex.ReplaceToString(input, replacement);
            case "bytes-string":
                return Encoding.UTF8.GetString(token.HasValue ? regex.Replace(input, replacement, token.Value) : regex.Replace(input, replacement));
            case "bytes-pattern":
                var patternBytes = Encoding.UTF8.GetBytes(replacement);
                return Encoding.UTF8.GetString(token.HasValue ? regex.Replace(input, patternBytes, token.Value) : regex.Replace(input, patternBytes));
            default:
                var destination = new byte[65536];
                int written;
                OperationStatus status;
                if (operation == "destination-string")
                {
                    status = token.HasValue
                        ? regex.TryReplace(input, replacement, destination, out written, token.Value)
                        : regex.TryReplace(input, replacement, destination, out written);
                }
                else
                {
                    var utf8 = Encoding.UTF8.GetBytes(replacement);
                    status = token.HasValue
                        ? regex.TryReplace(input, utf8, destination, out written, token.Value)
                        : regex.TryReplace(input, utf8, destination, out written);
                }
                Assert.Equal(OperationStatus.Done, status);
                return Encoding.UTF8.GetString(destination.AsSpan(0, written));
        }
    }

    private static void AssertRecovered(Utf8Regex regex, RegexOptions options, string operation, string replacement)
    {
        var oracle = new Regex("(a|ab)+$", options);
        Assert.Equal(oracle.Replace("ab", replacement), Replace(regex, "ab"u8.ToArray(), replacement, operation, CancellationToken.None));
        AssertOwned(regex);
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
